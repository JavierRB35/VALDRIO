using Microsoft.EntityFrameworkCore;
using Valdrio.Application;
using Valdrio.Domain;
namespace Valdrio.Infrastructure.CompanyFiles;

public sealed partial class SqliteCompanyFileService
{
    private CurrencyMetadataSnapshot FreezeResourceCurrency(CurrencyCode code) => catalog.GetRequired(code).Freeze();
    private static ResourceMutationReceipt ResourceChange(ResourceChangeRow row)
    {
        var value = CanonicalSnapshotJson.Deserialize<ResourceMutationReceipt>(row.CanonicalJson);
        Invalid(value.Request.RequestId.ToString() != row.Id || value.Request.CompanyId.ToString() != row.CompanyId ||
            value.Request.StudyId.ToString() != row.StudyId || value.Request.ExpectedRevision != row.ExpectedRevision ||
            value.ConfirmedStudy.Revision != row.ConfirmedRevision || Json(value) != row.CanonicalJson, "Resource change columns/payload differ");
        return value;
    }
    private static IReadOnlyList<ResourceMutationReceipt> ResourceChanges(ResourceDbContext db, EntityId companyId, EntityId studyId) =>
        FrozenList.Copy(db.ResourceChanges.AsNoTracking().Where(r => r.CompanyId == companyId.ToString() && r.StudyId == studyId.ToString())
            .OrderBy(r => r.ConfirmedRevision).ToArray().Select(ResourceChange));
    private static ResourceLedger LedgerAt(ResourceDbContext db, EntityId companyId, EntityId studyId, long revision) =>
        ResourceChanges(db, companyId, studyId).LastOrDefault(r => r.ConfirmedStudy.Revision <= revision)?.Ledger ?? ResourceLedger.Empty;
    private static ResourceWorkspaceSnapshot ResourceWorkspace(ResourceDbContext db, CompanyFileSnapshot file, StudyDefinition study) =>
        new(study, file.PrecisionPolicy, RequiredPublication(db, file.Id, study.SelectedModelVersionId),
            ResourceOrigins.FromImports(study, Batches(db, file.Id, study.Id)), LedgerAt(db, file.Id, study.Id, study.Revision));

    private sealed partial class Session
    {
        public ResourceWorkspaceSnapshot ReadResources(EntityId companyId, EntityId studyId, CancellationToken token = default) =>
            Workspace(false, token, (context, file) =>
            {
                Scope(companyId, file); var db = (ResourceDbContext)context;
                return ResourceWorkspace(db, file, Study(RequiredStudy(db, file.Id, studyId)));
            });
        public IReadOnlyList<ResourceMutationReceipt> ReadResourceHistory(EntityId companyId, EntityId studyId, CancellationToken token = default) =>
            Workspace(false, token, (context, file) =>
            {
                Scope(companyId, file); var db = (ResourceDbContext)context; _ = RequiredStudy(db, companyId, studyId);
                return ResourceChanges(db, companyId, studyId);
            });
        public ResourceMutationReceipt ChangeResources(ResourceMutation command, CancellationToken token = default) =>
            Workspace(true, token, (context, file) =>
            {
                Scope(command.CompanyId, file); var db = (ResourceDbContext)context;
                var existing = db.ResourceChanges.AsNoTracking().SingleOrDefault(r => r.Id == command.RequestId.ToString());
                if (existing is not null)
                {
                    var receipt = ResourceChange(existing);
                    if (Json(receipt.Request) != Json(command)) throw ResourceWorkspaceRules.Error("RES_REQUEST_REUSED", "Resource.RequestReused");
                    return receipt;
                }
                var row = RequiredStudy(db, file.Id, command.StudyId); var before = Study(row);
                foreach (var delta in command.Adjustments.Where(a => a.ReversalOfId is null))
                {
                    if (delta.OriginalDelta is not { } money || delta.CurrencyMetadata != owner.FreezeResourceCurrency(money.Currency))
                        throw ResourceWorkspaceRules.Error("RES_ADJUSTMENT_CURRENCY_INVALID", "Resource.AdjustmentCurrencyInvalid");
                }
                // IDs may not be reused after an explicit reset either.
                var used = ResourceChanges(db, file.Id, before.Id).SelectMany(r => r.Request.Adjustments).Select(a => a.Id).ToHashSet();
                if (command.Adjustments.Any(a => used.Contains(a.Id))) throw ResourceWorkspaceRules.Error("RES_DUPLICATE_ID", "Resource.DuplicateId");
                var ledger = ResourceWorkspaceRules.Apply(ResourceWorkspace(db, file, before), command);
                var after = Changed(before, before.Name, before.PeriodStart, before.PeriodEnd, before.Purpose, before.ScenarioKind,
                    before.SelectedModelVersionId, before.Currency, before.State);
                var result = new ResourceMutationReceipt(command, before, after, ledger, owner.time.GetUtcNow());
                db.ResourceChanges.Add(new() { Id = command.RequestId.ToString(), CompanyId = file.Id.ToString(), StudyId = before.Id.ToString(),
                    ExpectedRevision = before.Revision, ConfirmedRevision = after.Revision, CanonicalJson = Json(result) });
                FirstWrite(db);
                Apply(row, after);
                Audit(db, file, "Study", before.Id, after.Revision, "ChangeResources", command.Actor, before, after);
                Audit(db, file, "ResourceChange", command.RequestId, 0, "ChangeResources", command.Actor,
                    (ResourceMutationReceipt?)null, result, command.RequestId.ToString());
                foreach (var delta in command.Adjustments)
                {
                    token.ThrowIfCancellationRequested();
                    var saved = ledger.Entries.SelectMany(e => e.Adjustments).Single(a => a.Id == delta.Id);
                    AddAudit(db, new() { Id = saved.AuditEventId.ToString(), CompanyId = file.Id.ToString(), Revision = 0,
                        Operation = "CreateAdjustment", Actor = command.Actor, AtUtc = result.AtUtc.ToString(), BeforeJson = "null", AfterJson = Json(saved) },
                        "ResourceAdjustment", saved.Id.ToString(), CanonicalSnapshotJson.SchemaVersion);
                }
                return result;
            });
    }

    private static void ValidateResources(ResourceDbContext db, CompanyFileSnapshot file)
    {
        var changes = db.ResourceChanges.AsNoTracking().ToArray().Select(ResourceChange).ToArray();
        var audits = db.Audit.AsNoTracking().Select(a => new { Row = a, Type = EF.Property<string>(a, "AggregateType"),
            Id = EF.Property<string>(a, "AggregateId"), Request = EF.Property<string>(a, "TransactionId") }).ToArray();
        var expectedAdjustments = new HashSet<string>();
        foreach (var study in db.Studies.AsNoTracking().ToArray().Select(Study))
        {
            var history = changes.Where(c => c.Request.StudyId == study.Id).OrderBy(c => c.ConfirmedStudy.Revision).ToArray();
            _ = ResourceHistory.Replay(study, file.PrecisionPolicy, RequiredPublication(db, file.Id, study.SelectedModelVersionId),
                Batches(db, file.Id, study.Id), history);
            foreach (var change in history)
            {
                var c = change.Request;
                var audit = audits.Where(a => a.Type == "ResourceChange" && a.Id == c.RequestId.ToString()).ToArray();
                Invalid(audit.Length != 1, "Resource change audit missing");
                var a = audit[0];
                Invalid(a.Request != c.RequestId.ToString() || a.Row.Revision != 0 || a.Row.Operation != "ChangeResources" ||
                    a.Row.Actor != c.Actor || a.Row.BeforeJson != "null" || a.Row.AfterJson != Json(change), "Resource change audit differs");
                var studyEvent = audits.SingleOrDefault(a => a.Type == "Study" && a.Id == study.Id.ToString() && a.Row.Revision == change.ConfirmedStudy.Revision);
                Invalid(studyEvent is null || studyEvent.Row.Operation != "ChangeResources" || studyEvent.Row.Actor != c.Actor ||
                    studyEvent.Row.BeforeJson != Json(change.PreviousStudy) ||
                    studyEvent.Row.AfterJson != Json(change.ConfirmedStudy), "Resource change has no study transition");
                foreach (var request in c.Adjustments)
                {
                    var delta = change.Ledger.Entries.SelectMany(e => e.Adjustments).Single(d => d.Id == request.Id);
                    expectedAdjustments.Add(delta.Id.ToString());
                    var aa = audits.Where(a => a.Type == "ResourceAdjustment" && a.Id == delta.Id.ToString()).ToArray();
                    Invalid(aa.Length != 1, "Adjustment audit missing");
                    var d = aa[0];
                    Invalid(d.Row.Id != delta.AuditEventId.ToString() || d.Request != delta.AuditEventId.ToString() || d.Row.Actor != c.Actor ||
                        d.Row.Operation != "CreateAdjustment" || d.Row.Revision != 0 || d.Row.BeforeJson != "null" ||
                        d.Row.AtUtc != change.AtUtc.ToString() || d.Row.AfterJson != Json(delta), "Adjustment audit differs");
                }
            }
        }
        Invalid(audits.Any(a => a.Type == "ResourceChange" && !changes.Any(c => c.Request.RequestId.ToString() == a.Id) ||
            a.Type == "ResourceAdjustment" && !expectedAdjustments.Contains(a.Id)), "Orphan resource audit");
    }
}
