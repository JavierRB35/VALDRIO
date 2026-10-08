using Microsoft.EntityFrameworkCore;
using Valdrio.Application;
using Valdrio.Domain;

namespace Valdrio.Infrastructure.CompanyFiles;

public sealed partial class SqliteCompanyFileService
{
    private static ImportedBatch Batch(ImportBatchRow row)
    {
        var batch = CanonicalSnapshotJson.Deserialize<ImportedBatch>(row.CanonicalJson);
        Invalid(batch.Id.ToString() != row.Id || batch.Prepared.StudyContext.CompanyId.ToString() != row.CompanyId ||
            batch.Prepared.StudyContext.Id.ToString() != row.StudyId || batch.Prepared.StudyContext.Revision != row.ExpectedRevision ||
            batch.ConfirmedStudy.Revision != row.ConfirmedRevision || batch.Prepared.Source.FileHash != row.FileHash ||
            batch.Prepared.Fingerprint != row.PreparedFingerprint || batch.Prepared.DuplicateFingerprint != row.DuplicateFingerprint ||
            Json(batch) != row.CanonicalJson, "Import batch columns/payload differ");
        return batch;
    }
    private static IReadOnlyList<ImportedBatch> Batches(ImportDbContext db, EntityId companyId, EntityId studyId) =>
        FrozenList.Copy(db.ImportBatches.AsNoTracking().Where(r => r.CompanyId == companyId.ToString() && r.StudyId == studyId.ToString())
            .ToArray().Select(Batch).OrderBy(b => b.ConfirmedStudy.Revision));

    private sealed partial class Session
    {
        public PreparedImport PrepareImport(EntityId companyId, EntityId studyId, long expectedRevision, EntityId preparedId,
            ImportSourceDocument source, SourceParseProfileVersion profile, CancellationToken token = default) =>
            Workspace(false, token, (context, file) =>
            {
                Scope(companyId, file); var study = Study(RequiredStudy((StudyDbContext)context, file.Id, studyId));
                StudyRules.CheckRevision(expectedRevision, study.Revision, study.Id);
                return PreparedImport.Create(preparedId, study, file.PrecisionPolicy, source, profile, token);
            });

        public ImportedBatch ConfirmImport(ConfirmImport command, CancellationToken token = default) => Workspace(true, token, (context, file) =>
        {
            Scope(command.CompanyId, file); command.RequestId.EnsureValid(); ArgumentNullException.ThrowIfNull(command.Prepared);
            var db = (ImportDbContext)context; var prepared = command.Prepared;
            if (!Enum.IsDefined(command.Mode) || (command.Mode == ImportConfirmMode.ReplaceIdentifiedBatch) != command.ReplacesBatchId.HasValue || command.PreparedFingerprint != prepared.Fingerprint ||
                prepared.StudyContext.CompanyId != file.Id || prepared.StudyContext.Id != command.StudyId ||
                prepared.StudyContext.Revision != command.ExpectedRevision || prepared.PrecisionPolicy != file.PrecisionPolicy ||
                string.IsNullOrWhiteSpace(command.Actor))
                throw ImportRules.Error("IMP_PREPARED_INVALID", "Import.PreparedInvalid");
            var existing = db.ImportBatches.AsNoTracking().SingleOrDefault(r => r.Id == command.RequestId.ToString());
            if (existing is not null)
            {
                var previous = Batch(existing);
                if (previous.PreparedFingerprint != command.PreparedFingerprint || previous.Actor != command.Actor || previous.ReplacesBatchId != command.ReplacesBatchId)
                    throw ImportRules.Error("IMP_REQUEST_REUSED", "Import.RequestReused");
                return previous;
            }
            var row = RequiredStudy(db, file.Id, command.StudyId); var before = Study(row);
            StudyRules.CheckRevision(command.ExpectedRevision, before.Revision, before.Id);
            if (before.State != StudyState.Open) throw StudyRules.Error("STUDY_CLOSED", "Study.ClosedDiagnostic", before.Id);
            if (Json(before) != Json(prepared.StudyContext)) throw ImportRules.Error("IMP_PREPARED_INVALID", "Import.PreparedInvalid");
            // Recompute the interpretation in the transaction; no UI row is authoritative.
            var checkedPreparation = PreparedImport.Create(prepared.Id, before, file.PrecisionPolicy, prepared.Source, prepared.Profile, token);
            if (!checkedPreparation.CanConfirm || checkedPreparation.Fingerprint != command.PreparedFingerprint)
                throw ImportRules.Error("IMP_PREPARED_INVALID", "Import.PreparedInvalid");
            var active = ImportConfirmationRules.ActiveBatches(Batches(db, file.Id, before.Id));
            if (command.ReplacesBatchId is { } replaced && !active.Any(b => b.Id == replaced))
                throw ImportRules.Error("IMP_REPLACEMENT_INVALID", "Import.ReplacementInvalid");
            if (active.Any(b => b.Prepared.DuplicateFingerprint == checkedPreparation.DuplicateFingerprint))
                throw ImportRules.Error("IMP_DUPLICATE_BATCH", "Import.DuplicateBatch");
            var after = Changed(before, before.Name, before.PeriodStart, before.PeriodEnd, before.Purpose, before.ScenarioKind,
                before.SelectedModelVersionId, before.Currency, before.State);
            var result = new ImportedBatch(command.RequestId, checkedPreparation, command.PreparedFingerprint, after, owner.time.GetUtcNow(), command.Actor, command.ReplacesBatchId);
            if (db is ResourceDbContext resourceDb)
            {
                // Imported amounts are immutable; edited routes must be reset explicitly before their sources change.
                _ = new ResourceWorkspaceSnapshot(after, file.PrecisionPolicy, RequiredPublication(db, file.Id, after.SelectedModelVersionId),
                    ResourceOrigins.FromImports(after, Batches(db, file.Id, before.Id).Append(result).ToArray()),
                    LedgerAt(resourceDb, file.Id, before.Id, before.Revision));
            }
            db.ImportBatches.Add(new() { Id = result.Id.ToString(), CompanyId = file.Id.ToString(), StudyId = before.Id.ToString(),
                ExpectedRevision = before.Revision, ConfirmedRevision = after.Revision, FileHash = prepared.Source.FileHash,
                PreparedFingerprint = prepared.Fingerprint, DuplicateFingerprint = prepared.DuplicateFingerprint, CanonicalJson = Json(result) });
            FirstWrite(db); // Failure here rolls back the already written batch too.
            var interpretedRows = checkedPreparation.Rows.ToDictionary(r => r.RecordNumber);
            foreach (var sourceRow in checkedPreparation.Source.Rows)
            {
                token.ThrowIfCancellationRequested(); var interpreted = interpretedRows[sourceRow.RecordNumber];
                db.ImportedRows.Add(new() { CompanyId = file.Id.ToString(), StudyId = before.Id.ToString(), BatchId = result.Id.ToString(),
                    RecordNumber = sourceRow.RecordNumber, RawJson = Json(sourceRow), Account = interpreted.Account,
                    Amount = interpreted.Original is { } money ? CanonicalDecimal.Format(money.Amount) : null,
                    Currency = interpreted.Original?.Currency.ToString(), Selected = interpreted.Selected });
            }
            foreach (var resource in result.Resources)
            {
                token.ThrowIfCancellationRequested();
                db.Resources.Add(new() { Id = resource.Id.ToString(), CompanyId = file.Id.ToString(), StudyId = before.Id.ToString(),
                    BatchId = result.Id.ToString(), Account = resource.Account, OriginalAmount = CanonicalDecimal.Format(resource.Original.Amount),
                    FunctionalAmount = CanonicalDecimal.Format(resource.Functional.Amount), Currency = resource.Functional.Currency.ToString(),
                    PeriodStart = resource.PeriodStart.ToString(), PeriodEnd = resource.PeriodEnd.ToString() });
                foreach (var record in resource.SourceRecords ?? [resource.RecordNumber])
                    db.ResourceSourceLinks.Add(new() { ResourceId = resource.Id.ToString(), CompanyId = file.Id.ToString(), StudyId = before.Id.ToString(),
                        BatchId = result.Id.ToString(), RecordNumber = record });
            }
            Apply(row, after);
            Audit(db, file, "Study", before.Id, after.Revision, "ConfirmImport", command.Actor, before, after);
            Audit(db, file, "ImportBatch", result.Id, 0, "ConfirmImport", command.Actor, (ImportedBatch?)null, result, command.RequestId.ToString());
            return result;
        });

        public IReadOnlyList<ImportedBatch> ReadImports(EntityId companyId, EntityId studyId, CancellationToken token = default) =>
            Workspace(false, token, (context, file) =>
            { Scope(companyId, file); var db = (ImportDbContext)context; _ = RequiredStudy(db, companyId, studyId); return Batches(db, companyId, studyId); });
    }
}
