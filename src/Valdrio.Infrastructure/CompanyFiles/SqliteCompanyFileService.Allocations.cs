using Microsoft.EntityFrameworkCore;
using Valdrio.Application;
using Valdrio.Domain;
namespace Valdrio.Infrastructure.CompanyFiles;

public sealed partial class SqliteCompanyFileService
{
    private static AllocationKeyReceipt KeyEvent(AllocationKeyEventRow row)
    {
        var value = CanonicalSnapshotJson.Deserialize<AllocationKeyReceipt>(row.CanonicalJson);
        Invalid(value.Request.RequestId.ToString() != row.Id || value.Request.CompanyId.ToString() != row.CompanyId ||
            value.Request.ModelVersionId.ToString() != row.ModelVersionId || value.Request.KeyId.ToString() != row.KeyId ||
            value.After.Revision != row.Revision || Json(value) != row.CanonicalJson, "Allocation key columns/payload differ");
        return value;
    }
    private static AllocationChangeReceipt AllocationChange(AllocationChangeRow row)
    {
        var value = CanonicalSnapshotJson.Deserialize<AllocationChangeReceipt>(row.CanonicalJson);
        Invalid(value.Request.RequestId.ToString() != row.Id || value.Request.CompanyId.ToString() != row.CompanyId ||
            value.Request.StudyId.ToString() != row.StudyId || value.Request.ExpectedRevision != row.ExpectedRevision ||
            value.ConfirmedStudy.Revision != row.ConfirmedRevision || Json(value) != row.CanonicalJson, "Allocation change columns/payload differ");
        return value;
    }
    private static IReadOnlyList<AllocationKeyVersion> AllocationKeys(AllocationDbContext db, EntityId companyId, EntityId modelId, bool includeDeleted = false) =>
        FrozenList.Copy(db.AllocationKeyEvents.AsNoTracking().Where(r => r.CompanyId == companyId.ToString() && r.ModelVersionId == modelId.ToString())
            .ToArray().Select(KeyEvent).GroupBy(r => r.After.Id).Select(g => g.MaxBy(r => r.After.Revision)!.After).Where(k => includeDeleted || !k.IsDeleted).OrderBy(k => k.Id));
    private static IReadOnlyList<AllocationChangeReceipt> AllocationChanges(AllocationDbContext db, EntityId companyId, EntityId studyId) =>
        FrozenList.Copy(db.AllocationChanges.AsNoTracking().Where(r => r.CompanyId == companyId.ToString() && r.StudyId == studyId.ToString())
            .OrderBy(r => r.ConfirmedRevision).ToArray().Select(AllocationChange));
    private static IEnumerable<AllocationKeyVersion> UsedKeys(AllocationChangeReceipt change)
    {
        var c = change.Request.Configuration;
        return change.ReferencedKeys.Where(k => c.PrimaryRules.Any(r => r.IsActive && r.KeyVersionId == k.Id)
            || c.SecondaryRules.Any(r => r.IsActive && r.KeyVersionId == k.Id)
            || c.ExplicitAssignments.Any(a => a.KeyVersionId == k.Id));
    }
    private static AllocationWorkspaceSnapshot AllocationWorkspace(AllocationDbContext db, CompanyFileSnapshot file, StudyDefinition study)
    {
        var last = AllocationChanges(db, file.Id, study.Id).LastOrDefault(c => c.ConfirmedStudy.Revision <= study.Revision);
        return new(ResourceWorkspace(db, file, study), AllocationKeys(db, file.Id, study.SelectedModelVersionId),
            last?.Request.Configuration ?? AllocationConfiguration.Empty, last?.ConfirmedStudy.Revision ?? -1,
            usedKeyIds: db.AllocationChanges.AsNoTracking().ToArray().Select(AllocationChange)
                .SelectMany(UsedKeys).Select(k => k.Id).Distinct().ToArray());
    }
    private static void EnsurePrimaryOpen(AllocationDbContext db, EntityId companyId, EntityId studyId)
    {
        if (AllocationChanges(db, companyId, studyId).LastOrDefault()?.Request.Configuration.PrimaryClosed == true)
            throw AllocationDiagnostics.Error("PRIMARY_CLOSED", "Allocation.PrimaryClosed");
    }
    private sealed partial class Session
    {
        public IReadOnlyList<AllocationKeyVersion> ReadAllocationKeys(EntityId companyId, EntityId modelId, CancellationToken token = default) =>
            Workspace(false, token, (context, file) =>
            {
                Scope(companyId, file); _ = RequiredPublication((StudyDbContext)context, file.Id, modelId);
                return AllocationKeys((AllocationDbContext)context, companyId, modelId);
            });
        public AllocationWorkspaceSnapshot ReadAllocations(EntityId companyId, EntityId studyId, CancellationToken token = default) =>
            Workspace(false, token, (context, file) =>
            {
                Scope(companyId, file);
                return AllocationWorkspace((AllocationDbContext)context, file, Study(RequiredStudy((StudyDbContext)context, file.Id, studyId)));
            });
        public IReadOnlyList<AllocationChangeReceipt> ReadAllocationHistory(EntityId companyId, EntityId studyId, CancellationToken token = default) =>
            Workspace(false, token, (context, file) =>
            {
                Scope(companyId, file); _ = RequiredStudy((StudyDbContext)context, file.Id, studyId);
                return AllocationChanges((AllocationDbContext)context, companyId, studyId);
            });
        public AllocationKeyReceipt SaveAllocationKey(SaveAllocationKey request, CancellationToken token = default) =>
            Workspace(true, token, (context, file) =>
            {
                Scope(request.CompanyId, file); var db = (AllocationDbContext)context;
                // Freeze caller-owned collections before validation and persistence.
                request = request with { Lines = FrozenList.Copy(request.Lines) };
                var saved = db.AllocationKeyEvents.AsNoTracking().SingleOrDefault(r => r.Id == request.RequestId.ToString());
                if (saved is not null)
                {
                    var receipt = KeyEvent(saved);
                    if (Json(receipt.Request) != Json(request)) throw AllocationDiagnostics.Error("KEY_REQUEST_REUSED", "Allocation.RequestReused");
                    return receipt;
                }
                var model = RequiredPublication(db, file.Id, request.ModelVersionId);
                var keys = AllocationKeys(db, file.Id, request.ModelVersionId, true);
                var before = keys.SingleOrDefault(k => k.Id == request.KeyId);
                if (request.Delete && (db.AllocationChanges.AsNoTracking().ToArray().Select(AllocationChange).Any(c => c.ReferencedKeys.Any(k => k.Id == request.KeyId))
                    || keys.Any(k => k.ParentVersionId == request.KeyId)))
                    throw AllocationDiagnostics.Error("KEY_DELETE_USED", "Allocation.KeyDeleteUsed");
                var now = owner.time.GetUtcNow();
                var used = db.AllocationChanges.AsNoTracking().ToArray().Select(AllocationChange)
                    .Any(c => UsedKeys(c).Any(k => k.Id == request.KeyId));
                var after = AllocationWorkspaceRules.SaveKey(request, model, file.PrecisionPolicy, keys, now, !used);
                var result = new AllocationKeyReceipt(request, before, after, now);
                db.AllocationKeyEvents.Add(new() { Id = request.RequestId.ToString(), CompanyId = file.Id.ToString(),
                    ModelVersionId = request.ModelVersionId.ToString(), KeyId = request.KeyId.ToString(), Revision = after.Revision, CanonicalJson = Json(result) });
                FirstWrite(db);
                Audit(db, file, "AllocationKey", after.Id, after.Revision, "SaveAllocationKey", request.Actor,
                    before, after, request.RequestId.ToString());
                return result;
            });
        public AllocationChangeReceipt ChangeAllocations(ChangeAllocations request, CancellationToken token = default) =>
            Workspace(true, token, (context, file) =>
            {
                Scope(request.CompanyId, file); var db = (AllocationDbContext)context;
                var saved = db.AllocationChanges.AsNoTracking().SingleOrDefault(r => r.Id == request.RequestId.ToString());
                if (saved is not null)
                {
                    var receipt = AllocationChange(saved);
                    if (Json(receipt.Request) != Json(request)) throw AllocationDiagnostics.Error("KEY_REQUEST_REUSED", "Allocation.RequestReused");
                    return receipt;
                }
                var row = RequiredStudy(db, file.Id, request.StudyId); var before = Study(row);
                var current = AllocationWorkspace(db, file, before);
                var config = request.Configuration;
                if (current.Configuration.PrimaryClosed && (config.PrimaryClosed || Json(config) != Json(current.Configuration.WithPrimaryClosed(false))))
                    throw AllocationDiagnostics.Error("PRIMARY_CLOSED", "Allocation.PrimaryClosed");
                var referenced = AllocationWorkspaceRules.ValidateConfiguration(current.Resources, current.Keys, request);
                if (config.PrimaryClosed && (current.Resources.Origins.Count == 0 ||
                    !Valdrio.Application.PrimaryAllocationService.Calculate(new AllocationWorkspaceSnapshot(current.Resources, current.Keys, config, current.ConfigurationRevision)).IsComplete))
                    throw AllocationDiagnostics.Error("PRIMARY_INCOMPLETE", "AllocationUI.PrimaryIncomplete");
                var after = Changed(before, before.Name, before.PeriodStart, before.PeriodEnd, before.Purpose, before.ScenarioKind,
                    before.SelectedModelVersionId, before.Currency, before.State);
                var result = new AllocationChangeReceipt(request, before, after, referenced, owner.time.GetUtcNow());
                db.AllocationChanges.Add(new() { Id = request.RequestId.ToString(), CompanyId = file.Id.ToString(), StudyId = before.Id.ToString(),
                    ExpectedRevision = before.Revision, ConfirmedRevision = after.Revision, CanonicalJson = Json(result) });
                FirstWrite(db); Apply(row, after);
                Audit(db, file, "Study", before.Id, after.Revision, "ChangeAllocations", request.Actor, before, after);
                Audit(db, file, "AllocationChange", request.RequestId, 0, "ChangeAllocations", request.Actor,
                    (AllocationChangeReceipt?)null, result, request.RequestId.ToString());
                return result;
            });
    }
    private static void ValidateAllocations(AllocationDbContext db, CompanyFileSnapshot file)
    {
        var events = db.AllocationKeyEvents.AsNoTracking().ToArray().Select(KeyEvent).ToArray();
        var changes = db.AllocationChanges.AsNoTracking().ToArray().Select(AllocationChange).ToArray();
        var audits = db.Audit.AsNoTracking().Select(a => new { Row = a, Type = EF.Property<string>(a, "AggregateType"),
            Id = EF.Property<string>(a, "AggregateId"), Request = EF.Property<string>(a, "TransactionId") }).ToArray();
        var keys = new Dictionary<EntityId, AllocationKeyVersion>();
        foreach (var e in events.OrderBy(e => e.After.FamilyId).ThenBy(e => e.After.VersionNumber).ThenBy(e => e.After.Revision))
        {
            keys.TryGetValue(e.After.Id, out var before);
            Invalid((e.Before is null ? "null" : Json(e.Before)) != (before is null ? "null" : Json(before)), "Key history chain differs");
            var after = AllocationWorkspaceRules.SaveKey(e.Request, RequiredPublication(db, file.Id, e.Request.ModelVersionId),
                file.PrecisionPolicy, keys.Values.ToArray(), e.AtUtc,
                !changes.Any(c => UsedKeys(c).Any(k => k.Id == e.After.Id && k.Revision <= (before?.Revision ?? -1))));
            Invalid(Json(after) != Json(e.After), "Key history replay differs");
            keys[after.Id] = after;
            var matches = audits.Where(a => a.Type == "AllocationKey" && a.Id == after.Id.ToString() && a.Row.Revision == after.Revision).ToArray();
            Invalid(matches.Length != 1, "Key audit missing"); var a = matches[0];
            Invalid(a.Request != e.Request.RequestId.ToString() || a.Row.Operation != "SaveAllocationKey" || a.Row.Actor != e.Request.Actor ||
                a.Row.BeforeJson != (before is null ? "null" : Json(before)) || a.Row.AfterJson != Json(after), "Key audit differs");
        }
        foreach (var change in changes)
        {
            var c = change.Request;
            var historicalImports = Batches(db, file.Id, c.StudyId).Where(b => b.ConfirmedStudy.Revision <= change.PreviousStudy.Revision).ToArray();
            var resources = new ResourceWorkspaceSnapshot(change.PreviousStudy, file.PrecisionPolicy,
                RequiredPublication(db, file.Id, change.PreviousStudy.SelectedModelVersionId),
                ResourceOrigins.FromImports(change.PreviousStudy, historicalImports), LedgerAt(db, file.Id, c.StudyId, change.PreviousStudy.Revision),
                ResourceHistory.EconomicRevision(change.PreviousStudy, historicalImports, ResourceChanges(db, file.Id, c.StudyId)));
            foreach (var key in change.ReferencedKeys)
                Invalid(!events.Any(e => e.After.Id == key.Id && e.After.Revision == key.Revision && Json(e.After) == Json(key)), "Referenced key revision missing");
            var referenced = AllocationWorkspaceRules.ValidateConfiguration(resources, change.ReferencedKeys, c);
            Invalid(Json(referenced) != Json(change.ReferencedKeys), "Referenced immutable keys differ");
            var matches = audits.Where(a => a.Type == "AllocationChange" && a.Id == c.RequestId.ToString()).ToArray();
            Invalid(matches.Length != 1, "Allocation audit missing"); var a = matches[0];
            Invalid(a.Row.Revision != 0 || a.Request != c.RequestId.ToString() || a.Row.Operation != "ChangeAllocations" ||
                a.Row.Actor != c.Actor || a.Row.BeforeJson != "null" || a.Row.AfterJson != Json(change), "Allocation audit differs");
        }
        Invalid(audits.Any(a => a.Type == "AllocationKey" && !events.Any(e => e.After.Id.ToString() == a.Id && e.After.Revision == a.Row.Revision) ||
            a.Type == "AllocationChange" && !changes.Any(c => c.Request.RequestId.ToString() == a.Id)), "Orphan allocation audit");
    }
}

