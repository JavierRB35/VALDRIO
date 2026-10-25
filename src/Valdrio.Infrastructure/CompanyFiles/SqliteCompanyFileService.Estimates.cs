using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Valdrio.Application;
using Valdrio.Calculation;
using Valdrio.Domain;
namespace Valdrio.Infrastructure.CompanyFiles;
public sealed partial class SqliteCompanyFileService
{
    private static EntityId EstimateProduct(EstimatePreview value) => value.Input.Catalog.Versions.Single(v => v.Id == value.Input.Request.RecipeVersionId).ProductId;
    private static AcceptedEstimate RestoreEstimate(EstimateRow row)
    {
        var value = CanonicalSnapshotJson.Deserialize<AcceptedEstimate>(row.CanonicalJson);
        var request = value.Preview.Input.Request;
        Invalid(value.Id.ToString() != row.Id || request.CompanyId.ToString() != row.CompanyId || request.StudyId.ToString() != row.StudyId ||
            request.OrderId.ToString() != row.OrderId || value.Version != row.Version || Json(value) != row.CanonicalJson ||
            PublishedModelStructure.Hash(row.CanonicalJson) != row.ContentHash, "Estimate envelope differs");
        EstimateService.Validate(value);
        return value;
    }
    private static void ValidateEstimates(EstimateDbContext db, CompanyFileSnapshot file)
    {
        var rows = db.Estimates.AsNoTracking().ToArray();
        var audit = db.Audit.AsNoTracking().Where(a => EF.Property<string>(a, "AggregateType") == "Estimate")
            .Select(a => new { Row = a, Order = EF.Property<string>(a, "AggregateId") }).ToArray();
        Invalid(audit.Length != rows.Length, "Estimate audit count differs");
        foreach (var order in rows.GroupBy(x => x.OrderId))
        {
            string previous = "null";
            AcceptedEstimate? prior = null;
            var version = 0;
            foreach (var row in order.OrderBy(x => x.Version))
            {
                var value = RestoreEstimate(row);
                var request = value.Preview.Input.Request;
                var entry = audit.SingleOrDefault(a => a.Order == row.OrderId && a.Row.Revision == row.Version)?.Row;
                Invalid(row.CompanyId != file.Id.ToString() || row.Version != ++version || !db.Studies.Any(s => s.Id == row.StudyId) ||
                    request.PreviousEstimateId != prior?.Id || entry is null || entry.Operation != "AcceptEstimate" ||
                    entry.BeforeJson != previous || entry.AfterJson != row.CanonicalJson || entry.Actor != value.Actor ||
                    entry.AtUtc != value.AcceptedAtUtc.ToCanonicalString() ||
                    prior is not null && (prior.Preview.Input.Request.StudyId != request.StudyId ||
                        prior.Preview.Input.Request.OrderCode != request.OrderCode || EstimateProduct(prior.Preview) != EstimateProduct(value.Preview)), "Estimate audit chain differs");
                previous = row.CanonicalJson; prior = value;
            }
        }
    }
    private sealed partial class Session : IEstimateWorkspace
    {
        public IEstimateWorkspace Estimates => this;
        public IReadOnlyList<AcceptedEstimate> ReadEstimates(EntityId studyId, CancellationToken token = default)
        {
            lock (gate)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                return Guard(() =>
                {
                    ReadValidated(token);
                    using var connection = Connect(Path, SqliteOpenMode.ReadOnly);
                    using var db = new EstimateDbContext(connection);
                    return (IReadOnlyList<AcceptedEstimate>)db.Estimates.AsNoTracking().Where(x => x.StudyId == studyId.ToString())
                        .ToArray().Select(RestoreEstimate).OrderBy(x => x.Preview.Input.Request.OrderCode, StringComparer.Ordinal)
                        .ThenBy(x => x.Version).ToArray();
                });
            }
        }
        public EstimatePreview PreviewEstimate(EstimateRequest request, CancellationToken token = default)
        {
            lock (gate)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                var file = ReadValidated(token);
                if (request.CompanyId != file.Id) throw EstimateCalculator.Error("InvalidInput");
                var run = Runs.ReadRuns(file.Id, request.StudyId, token).Runs.SingleOrDefault(x => x.Id == request.RunId);
                if (run?.Status != CalculationRunStatus.Completed || run.Snapshot.Input is not AllocationStudySnapshotInput input)
                    throw EstimateCalculator.Error("MissingRun");
                var study = input.Allocations.Resources.Study;
                var rates = PrincipalRateReportService.Read(study, input.Model, new(study.Revision, [run]),
                    PhysicalActivity.ReadCurrent(file.Id, study.Id, token));
                var catalog = ReadCompositions(token);
                CompositionCalculator.Validate(catalog, file.Units, AllCompositionPools(), true, request.RecipeVersionId);
                // Retain all model pools needed to validate unrelated catalogue versions as well.
                return EstimateService.Calculate(new(request, catalog, file.Units, AllCompositionPools(), rates,
                    file.DefaultCurrency.ToString(), input.PrecisionPolicy));
            }
        }
        private CompositionPool[] AllCompositionPools()
        {
            using var connection = Connect(Path, SqliteOpenMode.ReadOnly);
            using var db = new CompositionDbContext(connection);
            return CompositionPools(db);
        }
        public AcceptedEstimate AcceptEstimate(EstimateRequest request, string expectedInputHash, string actor, CancellationToken token = default)
        {
            lock (gate)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                if (Mode != CompanyFileOpenMode.Edit) throw Error("FILE_READ_ONLY", "File.ReadOnly");
                if (string.IsNullOrWhiteSpace(actor)) throw EstimateCalculator.Error("InvalidInput");
                return Guard(() =>
                {
                    if (Studies.ReadStudies(token).Single(s => s.Id == request.StudyId).State != StudyState.Open)
                        throw EstimateCalculator.Error("ReadOnly");
                    var preview = PreviewEstimate(request, token);
                    if (preview.InputHash != expectedInputHash) throw EstimateCalculator.Error("Changed");
                    using var connection = Connect(Path, SqliteOpenMode.ReadWrite);
                    using var db = new EstimateDbContext(connection);
                    using var tx = db.Database.BeginTransaction();
                    var order = db.Estimates.Where(x => x.OrderId == request.OrderId.ToString()).OrderBy(x => x.Version).ToArray();
                    var previous = order.LastOrDefault();
                    var prior = previous is null ? null : RestoreEstimate(previous);
                    if (request.PreviousEstimateId != prior?.Id || prior is not null &&
                        (prior.Preview.Input.Request.StudyId != request.StudyId || prior.Preview.Input.Request.OrderCode != request.OrderCode || EstimateProduct(prior.Preview) != EstimateProduct(preview)))
                        throw EstimateCalculator.Error("Changed");
                    if (prior is null && db.Estimates.Where(x => x.StudyId == request.StudyId.ToString()).AsEnumerable()
                        .Any(x => RestoreEstimate(x).Preview.Input.Request.OrderCode == request.OrderCode))
                        throw EstimateCalculator.Error("DuplicateOrder");
                    var value = new AcceptedEstimate(new(Guid.NewGuid()), (prior?.Version ?? 0) + 1, "Estimate",
                        EstimateCalculator.Version, owner.time.GetUtcNow(), actor, preview);
                    var json = Json(value);
                    db.Estimates.Add(new() { Id = value.Id.ToString(), CompanyId = request.CompanyId.ToString(), StudyId = request.StudyId.ToString(),
                        OrderId = request.OrderId.ToString(), Version = value.Version, CanonicalJson = json, ContentHash = PublishedModelStructure.Hash(json) });
                    AddAudit(db, new() { Id = NewId(), CompanyId = request.CompanyId.ToString(), Revision = value.Version,
                        Operation = "AcceptEstimate", Actor = actor, AtUtc = value.AcceptedAtUtc.ToCanonicalString(),
                        BeforeJson = previous?.CanonicalJson ?? "null", AfterJson = json }, "Estimate", request.OrderId.ToString(), CanonicalSnapshotJson.SchemaVersion);
                    FirstWrite(db); owner.onCheckpoint?.Invoke(CompanyWriteCheckpoint.BeforeCommit);
                    token.ThrowIfCancellationRequested(); tx.Commit();
                    return CanonicalSnapshotJson.Deserialize<AcceptedEstimate>(json);
                });
            }
        }
    }
}
