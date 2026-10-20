using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Valdrio.Application;
using Valdrio.Calculation;
using Valdrio.Domain;
namespace Valdrio.Infrastructure.CompanyFiles;

public sealed partial class SqliteCompanyFileService
{
    private static CompositionPool[] CompositionPools(ModelDbContext db) => Models(db)
        .Where(m => m.Status == ModelStatus.Published)
        .SelectMany(m => m.Structure.Pools.Select(p =>
        {
            var node = m.Structure.Nodes.Single(n => n.Id == p.NodeId);
            return new CompositionPool(p.Id, m.Structure.Id, node.Code + " · " + p.Description, p.DriverUnitId, node.IsActive);
        })).ToArray();

    private static CompositionCatalog RestoreCatalog(CompositionCatalogRow? row, EntityId company)
    {
        if (row is null) return new(company, 0, [], []);
        var catalog = CanonicalSnapshotJson.Deserialize<CompositionCatalog>(row.CanonicalJson);
        Invalid(row.CompanyId != company.ToString() || catalog.CompanyId != company ||
            row.Revision != catalog.Revision || Json(catalog) != row.CanonicalJson ||
            PublishedModelStructure.Hash(row.CanonicalJson) != row.ContentHash, "Composition payload differs");
        return catalog;
    }
    private static void ValidateCatalogTransition(CompositionCatalog before, CompositionCatalog after)
    {
        Invalid(after.Revision != before.Revision + 1 || after.CompanyId != before.CompanyId, "Composition revision differs");
        foreach (var item in before.Items)
        {
            var next = after.Items.SingleOrDefault(i => i.Id == item.Id);
            Invalid(next is null || next.Code != item.Code || next.Name != item.Name ||
                next.UnitId != item.UnitId || next.Kind != item.Kind, "Composition item identity changed");
        }
        foreach (var version in before.Versions)
        {
            var next = after.Versions.SingleOrDefault(v => v.Id == version.Id);
            Invalid(next is null || next.ProductId != version.ProductId || next.Number != version.Number ||
                next.ModelVersionId != version.ModelVersionId ||
                version.State == CompositionState.Published && Json(next) != Json(version), "Published composition changed");
        }
    }
    private static void ValidateCompositions(CompositionDbContext db, CompanyFileSnapshot file)
    {
        var catalog = RestoreCatalog(db.CompositionCatalogs.AsNoTracking().SingleOrDefault(), file.Id);
        var pools = CompositionPools(db);
        CompositionCalculator.Validate(catalog, file.Units, pools);
        Invalid(catalog.Versions.Any(v => !db.Models.Any(m => m.Id == v.ModelVersionId.ToString() && m.Status == "Published")),
            "Composition model missing");
        var audit = db.Audit.AsNoTracking().Where(a => EF.Property<string>(a, "AggregateType") == "CompositionCatalog")
            .OrderBy(a => a.Revision).ToArray();
        Invalid(audit.LongLength != catalog.Revision, "Composition audit count differs");
        var previous = new CompositionCatalog(file.Id, 0, [], []);
        foreach (var entry in audit)
        {
            var after = CanonicalSnapshotJson.Deserialize<CompositionCatalog>(entry.AfterJson);
            Invalid(entry.Operation != "SaveCompositions" || entry.BeforeJson != Json(previous) ||
                entry.Revision != after.Revision, "Composition audit chain differs");
            ValidateCatalogTransition(previous, after);
            CompositionCalculator.Validate(after, file.Units, pools);
            previous = after;
        }
        Invalid(Json(previous) != Json(catalog), "Composition state differs from audit");
    }
    private sealed partial class Session : ICompositionWorkspace
    {
        public ICompositionWorkspace Compositions => this;
        public CompositionCatalog ReadCompositions(CancellationToken token = default)
        {
            lock (gate)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                return Guard(() =>
                {
                    var file = ReadValidated(token);
                    using var connection = Connect(Path, SqliteOpenMode.ReadOnly);
                    using var db = new CompositionDbContext(connection);
                    return RestoreCatalog(db.CompositionCatalogs.AsNoTracking().SingleOrDefault(), file.Id);
                });
            }
        }
        public CompositionCatalog SaveCompositions(CompositionCatalog proposal, long expectedRevision, string actor,
            CancellationToken token = default)
        {
            lock (gate)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                if (Mode != CompanyFileOpenMode.Edit) throw Error("FILE_READ_ONLY", "File.ReadOnly");
                return Guard(() =>
                {
                    var file = Validate(Path, token);
                    using var connection = Connect(Path, SqliteOpenMode.ReadWrite);
                    using var db = new CompositionDbContext(connection);
                    using var tx = db.Database.BeginTransaction();
                    var row = db.CompositionCatalogs.SingleOrDefault();
                    var before = RestoreCatalog(row, file.Id);
                    if (expectedRevision != before.Revision) throw CompositionCalculator.Error("Conflict");
                    if (proposal.CompanyId != file.Id || string.IsNullOrWhiteSpace(actor)) throw CompositionCalculator.Error("InvalidCatalog");
                    // Canonical round-trip detaches caller-owned lists before validation and persistence.
                    var after = CanonicalSnapshotJson.Deserialize<CompositionCatalog>(Json(proposal with { Revision = checked(before.Revision + 1) }));
                    try { ValidateCatalogTransition(before, after); }
                    catch (InvalidDataException) { throw CompositionCalculator.Error("Immutable"); }
                    var pools = CompositionPools(db);
                    CompositionCalculator.Validate(after, file.Units, pools);
                    foreach (var version in after.Versions.Where(v => v.State == CompositionState.Published &&
                        !before.Versions.Any(p => p.Id == v.Id && p.State == CompositionState.Published)))
                        CompositionCalculator.Validate(after, file.Units, pools, true, version.Id);
                    if (after.Versions.Any(v => !db.Models.Any(m => m.Id == v.ModelVersionId.ToString() && m.Status == "Published")))
                        throw CompositionCalculator.Error("MissingTarget");
                    row ??= new() { CompanyId = file.Id.ToString() };
                    if (before.Revision == 0) db.CompositionCatalogs.Add(row);
                    row.Revision = after.Revision;
                    row.CanonicalJson = Json(after); row.ContentHash = PublishedModelStructure.Hash(row.CanonicalJson);
                    AddAudit(db, new() { Id = NewId(), CompanyId = file.Id.ToString(), Revision = after.Revision,
                        Operation = "SaveCompositions", Actor = actor, AtUtc = owner.time.GetUtcNow().ToCanonicalString(),
                        BeforeJson = Json(before), AfterJson = Json(after) }, "CompositionCatalog", file.Id.ToString(), CanonicalSnapshotJson.SchemaVersion);
                    FirstWrite(db);
                    owner.onCheckpoint?.Invoke(CompanyWriteCheckpoint.BeforeCommit);
                    token.ThrowIfCancellationRequested(); tx.Commit();
                    return after;
                });
            }
        }
    }
}
