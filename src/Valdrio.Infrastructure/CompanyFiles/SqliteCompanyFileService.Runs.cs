using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Valdrio.Application;
using Valdrio.Domain;
namespace Valdrio.Infrastructure.CompanyFiles;
public sealed partial class SqliteCompanyFileService
{
    private static CalculationRun Run(CalculationRunRow row)
    {
        var value = CanonicalSnapshotJson.Deserialize<CalculationRun>(row.CanonicalJson);
        Invalid(value.Id.ToString() != row.Id || value.Snapshot.CompanyId.ToString() != row.CompanyId ||
            value.Snapshot.StudyId.ToString() != row.StudyId || value.Status.ToString() != row.Status ||
            Json(value) != row.CanonicalJson || PublishedModelStructure.Hash(row.CanonicalJson) != row.ContentHash,
            "Run columns/payload differ");
        value.Snapshot.EnsureValid();
        Invalid(value.Id != value.Snapshot.Id || value.StartedAtUtc != value.Snapshot.CapturedAtUtc ||
            value.Status == CalculationRunStatus.Started && (value.Output is not null || value.CompletedAtUtc is not null) ||
            value.Status != CalculationRunStatus.Started && value.CompletedAtUtc is null ||
            value.Status == CalculationRunStatus.Completed && value.Output?.Secondary is null ||
            value.Status != CalculationRunStatus.Completed && value.Output?.Secondary is not null,
            "Run lifecycle invalid");
        ValidateRunOutput(value);
        return value;
    }
    private static void ValidateRunOutput(CalculationRun run)
    {
        if (run.Output?.Primary is { } primary)
            Invalid(primary.StudyId != run.Snapshot.StudyId || primary.Revision != run.Snapshot.InputRevision ||
                primary.Currency != run.Snapshot.Input.StudyContext.Currency.FunctionalCurrencyCode, "Run primary scope invalid");
        if (run.Output?.Secondary is not { } result) return;
        Invalid(result.StudyId != run.Snapshot.StudyId || result.SourceRevision != run.Snapshot.InputRevision ||
            run.Output.Primary?.IsComplete != true || result.Reconciliations.Any(r => r.Difference.Amount != 0) ||
            result.ResourceBridges.Any(r => r.Difference.Amount != 0 || r.Pending.Amount != 0), "Run is incomplete");
        foreach (var bridge in result.ResourceBridges)
            Invalid(result.Facts.Where(f => f.ResourceId == bridge.ResourceId).Sum(f => f.PostedAmount.Amount) != bridge.Terminal.Amount ||
                bridge.Effective.Amount != bridge.Direct.Amount + bridge.Excluded.Amount + bridge.Pending.Amount + bridge.Terminal.Amount,
                "Run bridge does not reconcile");
        foreach (var total in result.Totals)
            Invalid(total.PostedAmount.Amount != result.Facts.Where(f => f.TerminalNodeId == total.TerminalNodeId &&
                f.Behavior == total.Behavior && f.CostLayer == total.CostLayer && f.CapacityTreatment == total.CapacityTreatment).Sum(f => f.PostedAmount.Amount),
                "Run terminal total differs");
    }
    private static void SetRun(CalculationRunRow row, CalculationRun value)
    {
        row.Id = value.Id.ToString(); row.CompanyId = value.Snapshot.CompanyId.ToString(); row.StudyId = value.Snapshot.StudyId.ToString();
        row.Status = value.Status.ToString(); row.CanonicalJson = Json(value); row.ContentHash = PublishedModelStructure.Hash(row.CanonicalJson);
    }
    private static void ValidateRuns(RunDbContext db, CompanyFileSnapshot file)
    {
        foreach (var row in db.Runs.AsNoTracking())
        {
            var run = Run(row);
            Invalid(run.Snapshot.CompanyId != file.Id || run.Snapshot.InputRevision > RequiredStudy(db, file.Id, run.Snapshot.StudyId).Revision,
                "Run scope invalid");
        }
    }
    private sealed partial class Session
    {
        public CalculationRun PrepareRun(CaptureStudySnapshot request, CancellationToken token = default)
        {
            // Capture already guarantees coherent frozen v7 inputs and hashes. A captured input is not a completed run.
            var snapshot = Capture(request, token);
            if (snapshot.Input is not AllocationStudySnapshotInput)
                throw new DomainRuleException(new("RUN_INPUT_REQUIRED", "Run.InputRequired", []));
            return RunWorkspace(true, token, db =>
            {
                var existing = db.Runs.SingleOrDefault(r => r.Id == snapshot.Id.ToString());
                if (existing is not null) return Run(existing);
                var run = new CalculationRun(snapshot.Id, snapshot, CalculationRun.Algorithm,
                    CalculationRunStatus.Started, snapshot.CapturedAtUtc, null, null);
                var row = new CalculationRunRow(); SetRun(row, run); db.Runs.Add(row); FirstWrite(db); return run;
            });
        }
        public CalculationRun PublishRun(EntityId runId, RunOutput output, CancellationToken token = default) => RunWorkspace(true, token, db =>
        {
            var row = db.Runs.Single(r => r.Id == runId.ToString()); var before = Run(row);
            if (before.Status != CalculationRunStatus.Started) throw new InvalidOperationException("Run already finished.");
            if (output.Secondary is null && output.Diagnostics.Count == 0) throw new InvalidOperationException("A failed run requires a diagnostic.");
            var after = before with { Output = output, Status = output.Secondary is null ? CalculationRunStatus.Failed : CalculationRunStatus.Completed,
                CompletedAtUtc = owner.time.GetUtcNow() };
            ValidateRunOutput(after); SetRun(row, after); FirstWrite(db); return Run(row);
        });
        public CalculationRun CancelRun(EntityId runId) => RunWorkspace(true, default, db =>
        {
            var row = db.Runs.Single(r => r.Id == runId.ToString()); var before = Run(row);
            if (before.Status != CalculationRunStatus.Started) return before;
            var after = before with { Status = CalculationRunStatus.Cancelled, CompletedAtUtc = owner.time.GetUtcNow() };
            SetRun(row, after); FirstWrite(db); return after;
        });
        public RunHistory ReadRuns(EntityId companyId, EntityId studyId, CancellationToken token = default) => RunWorkspace(false, token, db =>
        {
            var study = RequiredStudy(db, companyId, studyId);
            var history = new RunHistory(study.Revision, db.Runs.FromSqlInterpolated($"SELECT * FROM CalculationRuns WHERE CompanyId={companyId.ToString()} AND StudyId={studyId.ToString()} ORDER BY rowid").AsNoTracking()
                .ToArray().Select(Run).ToArray());
            var physical = ((PhysicalDbContext)db).PhysicalObservations.AsNoTracking().Where(o => o.StudyId == studyId.ToString() && o.ModelVersionId == study.SelectedModelVersionId && o.PeriodStart == study.PeriodStart && o.PeriodEnd == study.PeriodEnd)
                .ToArray().Select(RestorePhysical).GroupBy(o => o.PoolId).Select(g => g.OrderByDescending(o => o.Version).First()).ToArray();
            return history with { StalePhysicalRuns = history.Runs.Where(r => r.Snapshot.Input is AllocationStudySnapshotInput i &&
                !AuxiliaryCapacityRules.IsCurrent(i.Allocations.Configuration.AuxiliaryCapacity, physical)).Select(r => r.Id).ToArray() };
        });
        // No ValidateAllocations/replay here: querying a saved execution must not call any cost solver.
        private T RunWorkspace<T>(bool write, CancellationToken token, Func<RunDbContext, T> action)
        {
            lock (gate)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                if (write && Mode != CompanyFileOpenMode.Edit) throw Error("FILE_READ_ONLY", "File.ReadOnly");
                return Guard(() =>
                {
                token.ThrowIfCancellationRequested();
                using var connection = Connect(Path, write ? SqliteOpenMode.ReadWrite : SqliteOpenMode.ReadOnly);
                using var db = new PhysicalDbContext(connection); using var tx = db.Database.BeginTransaction();
                var result = action(db);
                if (write) { db.SaveChanges(); owner.onCheckpoint?.Invoke(CompanyWriteCheckpoint.BeforeCommit); }
                token.ThrowIfCancellationRequested();
                if (write) tx.Commit();
                return result;
                });
            }
        }
    }
}
