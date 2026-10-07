using Microsoft.EntityFrameworkCore;
using Valdrio.Application;
using Valdrio.Domain;

namespace Valdrio.Infrastructure.CompanyFiles;

public sealed partial class SqliteCompanyFileService
{
    private static void ValidateImports(ImportDbContext db, CompanyFileSnapshot file)
    {
        var batches = db.ImportBatches.AsNoTracking().ToArray().Select(Batch).ToArray();
        var rows = db.ImportedRows.AsNoTracking().ToArray().ToLookup(r => r.BatchId);
        var resources = db.Resources.AsNoTracking().ToArray().ToLookup(r => r.BatchId);
        var links = db.ResourceSourceLinks.AsNoTracking().ToArray().ToLookup(r => r.BatchId);
        var audit = db.Audit.AsNoTracking().Where(a => EF.Property<string>(a, "AggregateType") == "ImportBatch")
            .Select(a => new { Row = a, Id = EF.Property<string>(a, "AggregateId"), Request = EF.Property<string>(a, "TransactionId") }).ToArray();
        foreach (var batch in batches)
        {
            var study = batch.Prepared.StudyContext;
            Invalid(file.SchemaVersion < 5 && batch.Prepared.Profile.Grouping is not null, "Grouped import in legacy schema");
            Invalid(study.CompanyId != file.Id || batch.Prepared.PrecisionPolicy != file.PrecisionPolicy, "Import scope/precision invalid");
            var expectedRows = batch.Prepared.Rows.ToDictionary(r => r.RecordNumber);
            var originalRows = batch.Prepared.Source.Rows.ToDictionary(r => r.RecordNumber);
            var savedRows = rows[batch.Id.ToString()].ToArray();
            Invalid(savedRows.Length != expectedRows.Count, "Import row count differs");
            foreach (var row in savedRows)
            {
                Invalid(row.CompanyId != file.Id.ToString() || row.StudyId != study.Id.ToString() ||
                    !expectedRows.ContainsKey(row.RecordNumber), "Import row scope invalid");
                var expected = expectedRows[row.RecordNumber];
                Invalid(row.RawJson != Json(originalRows[row.RecordNumber]) || row.Account != expected.Account ||
                    row.Amount != (expected.Original is { } money ? CanonicalDecimal.Format(money.Amount) : null) ||
                    row.Currency != expected.Original?.Currency.ToString() || row.Selected != expected.Selected, "Import row values differ");
            }
            var expectedResources = batch.Resources.ToDictionary(r => r.Id.ToString());
            var savedResources = resources[batch.Id.ToString()].ToArray();
            var savedLinks = links[batch.Id.ToString()].ToLookup(l => l.ResourceId);
            Invalid(savedResources.Length != expectedResources.Count || savedLinks.Sum(g => g.Count()) !=
                expectedResources.Values.Sum(r => r.SourceRecords?.Count ?? 1), "Import resource/link count differs");
            foreach (var row in savedResources)
            {
                Invalid(row.CompanyId != file.Id.ToString() || row.StudyId != study.Id.ToString() ||
                    !expectedResources.ContainsKey(row.Id) || !savedLinks.Contains(row.Id), "Resource scope invalid");
                var expected = expectedResources[row.Id]; var resourceLinks = savedLinks[row.Id].ToArray();
                Invalid(row.Account != expected.Account || row.OriginalAmount != CanonicalDecimal.Format(expected.Original.Amount) ||
                    row.FunctionalAmount != CanonicalDecimal.Format(expected.Functional.Amount) || row.Currency != expected.Functional.Currency.ToString() ||
                    row.PeriodStart != expected.PeriodStart.ToString() || row.PeriodEnd != expected.PeriodEnd.ToString() ||
                    !resourceLinks.Select(l => l.RecordNumber).Order().SequenceEqual((expected.SourceRecords ?? [expected.RecordNumber]).Order()) ||
                    resourceLinks.Any(link => link.CompanyId != row.CompanyId || link.StudyId != row.StudyId),
                    "Resource/source link differs");
            }
            var events = audit.Where(a => a.Id == batch.Id.ToString()).ToArray();
            Invalid(events.Length != 1, "Import audit count invalid"); var entry = events[0];
            Invalid(entry.Request != batch.Id.ToString() || entry.Row.Revision != 0 || entry.Row.Operation != "ConfirmImport" ||
                entry.Row.BeforeJson != "null" || entry.Row.AfterJson != Json(batch) || entry.Row.Actor != batch.Actor,
                "Import audit differs");
            var studyEvent = db.Audit.AsNoTracking().SingleOrDefault(a => EF.Property<string>(a, "AggregateType") == "Study" &&
                EF.Property<string>(a, "AggregateId") == study.Id.ToString() && a.Revision == batch.ConfirmedStudy.Revision);
            Invalid(studyEvent is null || studyEvent.Operation != "ConfirmImport" || studyEvent.Actor != batch.Actor ||
                studyEvent.BeforeJson != Json(study) || studyEvent.AfterJson != Json(batch.ConfirmedStudy), "Import has no matching study event");
        }
        var ids = batches.Select(b => b.Id.ToString()).ToHashSet(StringComparer.Ordinal);
        Invalid(audit.Any(a => !ids.Contains(a.Id)) || rows.Any(r => !ids.Contains(r.Key)) ||
            resources.Any(r => !ids.Contains(r.Key)) || links.Any(r => !ids.Contains(r.Key)), "Orphan import data");
    }
}
