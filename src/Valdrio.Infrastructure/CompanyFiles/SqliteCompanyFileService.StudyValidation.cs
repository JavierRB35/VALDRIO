using Microsoft.EntityFrameworkCore;
using Valdrio.Application;
using Valdrio.Domain;

namespace Valdrio.Infrastructure.CompanyFiles;

public sealed partial class SqliteCompanyFileService
{
    private static void ValidateStudies(StudyDbContext db, CompanyFileSnapshot file)
    {
        var studies = db.Studies.AsNoTracking().ToArray().Select(Study).ToArray();
        var snapshots = db.StudySnapshots.AsNoTracking().ToArray().Select(Receipt).ToArray();
        var imports = db is ImportDbContext current ? current.ImportBatches.AsNoTracking().ToArray().Select(Batch).ToArray() : [];
        var audit = db.Audit.AsNoTracking().Where(a => EF.Property<string>(a, "AggregateType") == "Study" || EF.Property<string>(a, "AggregateType") == "StudySnapshot")
            .Select(a => new { Row = a, Type = EF.Property<string>(a, "AggregateType"), Id = EF.Property<string>(a, "AggregateId") }).ToArray();
        foreach (var study in studies)
        {
            Invalid(study.CompanyId != file.Id, "Study scope invalid");
            _ = RequiredPublication(db, file.Id, study.SelectedModelVersionId);
            var rows = audit.Where(a => a.Type == "Study" && a.Id == study.Id.ToString()).OrderBy(a => a.Row.Revision).ToArray();
            Invalid(rows.LongLength != study.Revision + 1, "Study audit count invalid");
            var previous = "null"; StudyDefinition? prior = null;
            for (var i = 0; i < rows.Length; i++)
            {
                var row = rows[i].Row; var after = CanonicalSnapshotJson.Deserialize<StudyDefinition>(row.AfterJson);
                Invalid(after.Id != study.Id || after.CompanyId != file.Id || after.Revision != i || row.Revision != i || row.BeforeJson != previous ||
                    after.CreatedAtUtc != study.CreatedAtUtc || (prior is not null && prior.State == StudyState.Closed && row.Operation != "ChangeStudyState"), "Study audit chain invalid");
                Invalid(i == 0 ? row.Operation != "CreateStudy" : row.Operation is not ("UpdateStudyContext" or "ChangeStudyState" or "ConfirmImport" or "ChangeResources"), "Study operation invalid");
                Invalid(i == 0 && after.State != StudyState.Open, "Study must be created open");
                if (prior is not null && row.Operation == "UpdateStudyContext")
                {
                    Invalid(prior.State != StudyState.Open || after.State != prior.State,
                        "Context update changes study state");
                    if (imports.Any(b => b.ConfirmedStudy.Id == study.Id && b.ConfirmedStudy.Revision <= prior.Revision))
                        Invalid(after.Currency != prior.Currency || after.PeriodStart != prior.PeriodStart ||
                            after.PeriodEnd != prior.PeriodEnd || after.SelectedModelVersionId != prior.SelectedModelVersionId,
                            "Context update changes imported economic context");
                }
                if (prior is not null && row.Operation == "ConfirmImport")
                {
                    var matching = imports.Where(b => b.ConfirmedStudy.Id == study.Id && b.ConfirmedStudy.Revision == after.Revision).ToArray();
                    Invalid(matching.Length != 1 || !ImportConfirmationRules.IsImportRevision(prior, after), "Import revision has no matching batch");
                    Invalid(Json(matching[0].Prepared.StudyContext) != Json(prior) || Json(matching[0].ConfirmedStudy) != Json(after) ||
                        matching[0].Actor != row.Actor, "Import revision differs from batch");
                }
                if (prior is not null && row.Operation == "ChangeResources")
                {
                    Invalid(db is not ResourceDbContext, "Resource transition in old schema");
                    var matching = ResourceChanges((ResourceDbContext)db, file.Id, study.Id).Where(c => c.ConfirmedStudy.Revision == after.Revision).ToArray();
                    Invalid(matching.Length != 1 || !ImportConfirmationRules.IsImportRevision(prior, after), "Resource revision has no matching change");
                    Invalid(Json(matching[0].PreviousStudy) != Json(prior) || Json(matching[0].ConfirmedStudy) != Json(after) || matching[0].Request.Actor != row.Actor,
                        "Resource revision differs from change");
                }
                _ = RequiredPublication(db, file.Id, after.SelectedModelVersionId);
                if (prior is not null && row.Operation == "ChangeStudyState")
                    Invalid(Json(Changed(prior, prior.Name, prior.PeriodStart, prior.PeriodEnd, prior.Purpose, prior.ScenarioKind,
                        prior.SelectedModelVersionId, prior.Currency, after.State)) != Json(after) || prior.State == after.State, "State transition changes inputs");
                previous = Json(after); prior = after;
            }
            Invalid(previous != Json(study), "Study/current audit mismatch");
        }
        foreach (var snapshot in snapshots)
        {
            snapshot.EnsureValid(); var input = snapshot.Input;
            var study = studies.SingleOrDefault(s => s.Id == snapshot.StudyId);
            Invalid(study is null || snapshot.CompanyId != file.Id || snapshot.InputRevision > study!.Revision, "Snapshot revision/scope invalid");
            var atRevision = audit.SingleOrDefault(a => a.Type == "Study" && a.Id == snapshot.StudyId.ToString() && a.Row.Revision == snapshot.InputRevision);
            Invalid(atRevision is null || Json(input.StudyContext) != atRevision.Row.AfterJson, "Snapshot not captured from study revision");
            Invalid(Json(input.Model) != Json(RequiredPublication(db, file.Id, input.StudyContext.SelectedModelVersionId)) || input.PrecisionPolicy != file.PrecisionPolicy,
                "Snapshot applied definition differs");
            var atCapture = imports.Where(b => b.ConfirmedStudy.Id == snapshot.StudyId && b.ConfirmedStudy.Revision <= snapshot.InputRevision)
                .OrderBy(b => b.ConfirmedStudy.Revision).ToArray();
            if (input is ImportedStudySnapshotInput imported)
                Invalid(Json(imported.Imports) != Json(atCapture), "Snapshot import ledger differs");
            else if (input is ResourceStudySnapshotInput prepared)
            {
                Invalid(db is not ResourceDbContext, "Resource snapshot in old schema");
                var history = ResourceChanges((ResourceDbContext)db, file.Id, snapshot.StudyId)
                    .Where(c => c.ConfirmedStudy.Revision <= snapshot.InputRevision).ToArray();
                Invalid(Json(prepared.Imports) != Json(atCapture) || Json(prepared.History) != Json(history), "Resource snapshot ledger differs");
            }
            else Invalid(atCapture.Length != 0, "Context-only snapshot omits confirmed imports");
            if (db is ResourceDbContext resourceDb && input is not ResourceStudySnapshotInput)
                Invalid(ResourceChanges(resourceDb, file.Id, snapshot.StudyId).Any(c => c.ConfirmedStudy.Revision <= snapshot.InputRevision),
                    "Snapshot omits saved resource preparation");
            var rows = audit.Where(a => a.Type == "StudySnapshot" && a.Id == snapshot.Id.ToString()).ToArray();
            Invalid(rows.Length != 1, "Snapshot audit count invalid");
            var row = rows[0].Row;
            Invalid(row.Revision != 0 || row.Operation != "CaptureStudySnapshot" || row.BeforeJson != "null" || row.AfterJson != Json(snapshot) ||
                row.Actor != input.Provenance.Actor, "Snapshot audit invalid");
        }
        Invalid(audit.Any(a => a.Type == "Study" ? !studies.Any(s => s.Id.ToString() == a.Id) : !snapshots.Any(s => s.Id.ToString() == a.Id)), "Orphan study audit");
    }
}
