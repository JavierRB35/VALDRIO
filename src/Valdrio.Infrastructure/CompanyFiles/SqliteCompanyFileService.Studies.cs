using Microsoft.EntityFrameworkCore;
using Valdrio.Application;
using Valdrio.Domain;

namespace Valdrio.Infrastructure.CompanyFiles;

public sealed partial class SqliteCompanyFileService
{
    private static StudyDefinition Study(StudyRow row)
    {
        var value = CanonicalSnapshotJson.Deserialize<StudyDefinition>(row.ContextJson);
        Invalid(value.Id.ToString() != row.Id || value.CompanyId.ToString() != row.CompanyId || value.Revision != row.Revision ||
            value.SelectedModelVersionId.ToString() != row.SelectedModelVersionId || value.PeriodStart.ToString() != row.PeriodStart ||
            value.PeriodEnd.ToString() != row.PeriodEnd || value.Currency.FunctionalCurrencyCode.ToString() != row.FunctionalCurrencyCode ||
            value.State.ToString() != row.State || row.CurrentRunId is not null || Json(value) != row.ContextJson, "Study columns/payload differ");
        return value;
    }
    private static void Apply(StudyRow row, StudyDefinition value)
    {
        row.Id = value.Id.ToString(); row.CompanyId = value.CompanyId.ToString();
        row.SelectedModelVersionId = value.SelectedModelVersionId.ToString();
        row.PeriodStart = value.PeriodStart.ToString(); row.PeriodEnd = value.PeriodEnd.ToString();
        row.FunctionalCurrencyCode = value.Currency.FunctionalCurrencyCode.ToString(); row.State = value.State.ToString();
        row.Revision = value.Revision; row.ContextJson = Json(value);
    }
    private static StudySnapshotReceipt Receipt(StudySnapshotRow r) => new(EntityId.Parse(r.Id), EntityId.Parse(r.CompanyId),
        EntityId.Parse(r.StudyId), r.InputRevision, UtcInstant.ParseIso(r.CapturedAtUtc), r.PayloadSchema, r.EconomicInputHash, r.FullSnapshotHash, r.CanonicalJson);
    private static StudyRow RequiredStudy(StudyDbContext db, EntityId companyId, EntityId id)
    {
        id.EnsureValid();
        return db.Studies.SingleOrDefault(s => s.Id == id.ToString() && s.CompanyId == companyId.ToString())
            ?? throw StudyRules.Error("STUDY_NOT_FOUND", "Study.NotFound", id);
    }
    private static PublishedModelStructure RequiredPublication(StudyDbContext db, EntityId companyId, EntityId id)
    {
        var row = db.Models.SingleOrDefault(m => m.CompanyId == companyId.ToString() && m.Id == id.ToString());
        if (row is null || row.Status != "Published") throw StudyRules.Error("STUDY_MODEL_NOT_PUBLISHED", "Study.ModelNotPublished", id);
        return Model(db, row).Publication ?? throw StudyRules.Error("STUDY_MODEL_NOT_PUBLISHED", "Study.ModelNotPublished", id);
    }
    private CurrencyValidity StudyCurrencyValidity(CurrencyCode code) => catalog.GetRequired(code).Validity;
    private StudyCurrencyContext FreezeStudyCurrency(CurrencyCode code, BusinessDate start, BusinessDate end)
    {
        var definition = catalog.GetRequired(code);
        if (!definition.IsMonetary || !definition.Validity.Contains(start.Value) || !definition.Validity.Contains(end.Value))
            throw StudyRules.Error("STUDY_CURRENCY_INVALID", "Study.CurrencyInvalid");
        return new(code, definition.Freeze());
    }
    private static StudyDefinition Changed(StudyDefinition before, string name, BusinessDate start, BusinessDate end,
        StudyPurpose purpose, StudyScenario scenario, EntityId modelId, StudyCurrencyContext currency, StudyState state, CurrencyValidity? validity = null) =>
        new(before.Id, before.CompanyId, name, start, end, purpose, scenario, null, modelId, currency, validity ?? before.CurrencyValidity,
            checked(before.Revision + 1), state, null, null, before.CreatedAtUtc);

    private sealed partial class Session
    {
        public IReadOnlyList<StudyDefinition> ReadStudies(CancellationToken token = default) =>
            Workspace(false, token, (context, _) => FrozenList.Copy(((StudyDbContext)context).Studies.AsNoTracking().ToArray().Select(Study).OrderBy(s => s.Id)));

        public StudyDefinition Create(CreateStudy command, CancellationToken token = default) => Workspace(true, token, (context, file) =>
        {
            Scope(command.CompanyId, file); command.Id.EnsureValid();
            var db = (StudyDbContext)context;
            var existing = db.Studies.SingleOrDefault(s => s.Id == command.Id.ToString());
            if (existing is not null)
            {
                var first = db.Audit.Single(a => EF.Property<string>(a, "AggregateType") == "Study" &&
                    EF.Property<string>(a, "AggregateId") == command.Id.ToString() && a.Revision == 0);
                var original = CanonicalSnapshotJson.Deserialize<StudyDefinition>(first.AfterJson);
                if (existing.CompanyId != file.Id.ToString() || first.Actor != command.Actor || original.Name != command.Name ||
                    original.PeriodStart != command.PeriodStart || original.PeriodEnd != command.PeriodEnd || original.Purpose != command.Purpose ||
                    original.ScenarioKind != command.ScenarioKind || original.SelectedModelVersionId != command.SelectedModelVersionId ||
                    original.Currency.FunctionalCurrencyCode != command.FunctionalCurrency)
                    throw StudyRules.Error("STUDY_REQUEST_REUSED", "Study.RequestReused", command.Id);
                return Study(existing);
            }
            _ = RequiredPublication(db, file.Id, command.SelectedModelVersionId);
            var currency = owner.FreezeStudyCurrency(command.FunctionalCurrency, command.PeriodStart, command.PeriodEnd);
            var value = new StudyDefinition(command.Id, file.Id, command.Name, command.PeriodStart, command.PeriodEnd, command.Purpose,
                command.ScenarioKind, null, command.SelectedModelVersionId, currency, owner.StudyCurrencyValidity(command.FunctionalCurrency),
                0, StudyState.Open, null, null, owner.time.GetUtcNow());
            var row = new StudyRow(); Apply(row, value); db.Studies.Add(row); FirstWrite(db);
            Audit(db, file, "Study", value.Id, 0, "CreateStudy", command.Actor, (StudyDefinition?)null, value);
            return value;
        });

        public StudyDefinition UpdateContext(UpdateStudyContext command, CancellationToken token = default) => Workspace(true, token, (context, file) =>
        {
            Scope(command.CompanyId, file); var db = (StudyDbContext)context;
            var row = RequiredStudy(db, file.Id, command.Id); var before = Study(row);
            StudyRules.CheckRevision(command.ExpectedRevision, before.Revision, before.Id);
            if (before.State != StudyState.Open) throw StudyRules.Error("STUDY_CLOSED", "Study.ClosedDiagnostic", before.Id);
            if (((ImportDbContext)db).ImportBatches.Any(b => b.CompanyId == file.Id.ToString() && b.StudyId == before.Id.ToString()) &&
                (command.FunctionalCurrency != before.Currency.FunctionalCurrencyCode || command.PeriodStart != before.PeriodStart ||
                 command.PeriodEnd != before.PeriodEnd || command.SelectedModelVersionId != before.SelectedModelVersionId))
                throw ImportRules.Error("IMP_CONTEXT_LOCKED", "Import.ContextLocked");
            _ = RequiredPublication(db, file.Id, command.SelectedModelVersionId);
            var currency = before.Currency.FunctionalCurrencyCode == command.FunctionalCurrency ? before.Currency
                : owner.FreezeStudyCurrency(command.FunctionalCurrency, command.PeriodStart, command.PeriodEnd);
            var after = Changed(before, command.Name, command.PeriodStart, command.PeriodEnd, command.Purpose, command.ScenarioKind,
                command.SelectedModelVersionId, currency, before.State,
                before.Currency.FunctionalCurrencyCode == command.FunctionalCurrency ? before.CurrencyValidity : owner.StudyCurrencyValidity(command.FunctionalCurrency));
            Apply(row, after); FirstWrite(db);
            Audit(db, file, "Study", before.Id, after.Revision, "UpdateStudyContext", command.Actor, before, after);
            return after;
        });

        public StudyDefinition ChangeState(ChangeStudyState command, CancellationToken token = default) => Workspace(true, token, (context, file) =>
        {
            Scope(command.CompanyId, file); var db = (StudyDbContext)context;
            var row = RequiredStudy(db, file.Id, command.Id); var before = Study(row);
            StudyRules.CheckRevision(command.ExpectedRevision, before.Revision, before.Id);
            if (!Enum.IsDefined(command.State) || command.State == before.State)
                throw StudyRules.Error("STUDY_CONTEXT_INVALID", "Study.ContextInvalid", before.Id);
            var after = Changed(before, before.Name, before.PeriodStart, before.PeriodEnd, before.Purpose, before.ScenarioKind,
                before.SelectedModelVersionId, before.Currency, command.State);
            Apply(row, after); FirstWrite(db);
            Audit(db, file, "Study", before.Id, after.Revision, "ChangeStudyState", command.Actor, before, after);
            return after;
        });

        public StudySnapshotReceipt Capture(CaptureStudySnapshot command, CancellationToken token = default) => Workspace(true, token, (context, file) =>
        {
            Scope(command.CompanyId, file); command.RequestId.EnsureValid(); var db = (StudyDbContext)context;
            var existing = db.StudySnapshots.AsNoTracking().SingleOrDefault(s => s.Id == command.RequestId.ToString());
            if (existing is not null)
            {
                var receipt = Receipt(existing); receipt.EnsureValid();
                if (receipt.CompanyId != file.Id || receipt.StudyId != command.StudyId || receipt.InputRevision != command.ExpectedRevision ||
                    receipt.Input.Provenance.Actor != command.Actor)
                    throw StudyRules.Error("STUDY_REQUEST_REUSED", "Study.RequestReused", command.RequestId);
                return receipt;
            }
            var study = Study(RequiredStudy(db, file.Id, command.StudyId));
            StudyRules.CheckRevision(command.ExpectedRevision, study.Revision, study.Id);
            var model = RequiredPublication(db, file.Id, study.SelectedModelVersionId);
            var instant = owner.time.GetUtcNow();
            var imports = Batches((ImportDbContext)db, file.Id, study.Id);
            IStudySnapshotInput input = imports.Count == 0
                ? new StudySnapshotInput(StudySnapshotInput.Schema, StudySnapshotInput.PreparationVersion, study, file.PrecisionPolicy,
                    model, new(command.RequestId, command.RequestId, instant, command.Actor), [], [], [], [])
                : new ImportedStudySnapshotInput(study, file.PrecisionPolicy, model,
                    new(command.RequestId, command.RequestId, instant, command.Actor), imports);
            var json = StudySnapshotJson.Serialize(input);
            var result = new StudySnapshotReceipt(command.RequestId, file.Id, study.Id, study.Revision, instant, input.SnapshotSchemaVersion,
                StudySnapshotJson.EconomicHash(input), PublishedModelStructure.Hash(json), json);
            result.EnsureValid();
            db.StudySnapshots.Add(new() { Id = result.Id.ToString(), CompanyId = file.Id.ToString(), StudyId = study.Id.ToString(),
                InputRevision = study.Revision, CapturedAtUtc = instant.ToString(), PayloadSchema = result.PayloadSchema,
                EconomicInputHash = result.EconomicInputHash, FullSnapshotHash = result.FullSnapshotHash, CanonicalJson = json });
            FirstWrite(db);
            Audit(db, file, "StudySnapshot", result.Id, 0, "CaptureStudySnapshot", command.Actor, (StudySnapshotReceipt?)null, result);
            return result;
        });

        public IReadOnlyList<StudySnapshotReceipt> ReadSnapshots(EntityId companyId, EntityId studyId, CancellationToken token = default) =>
            Workspace(false, token, (context, file) =>
            {
                Scope(companyId, file); var db = (StudyDbContext)context; _ = RequiredStudy(db, companyId, studyId);
                return FrozenList.Copy(db.StudySnapshots.AsNoTracking().Where(s => s.CompanyId == companyId.ToString() && s.StudyId == studyId.ToString())
                    .ToArray().Select(Receipt).OrderBy(s => s.InputRevision).ThenBy(s => s.CapturedAtUtc.Value).ThenBy(s => s.Id));
            });
    }
}
