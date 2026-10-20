using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Valdrio.Application;
using Valdrio.Domain;

namespace Valdrio.Infrastructure.CompanyFiles;
public sealed partial class SqliteCompanyFileService
{
    private sealed record FormatUpgrade(int FromSchema, int ToSchema, string MigrationId);
    // Minimal read before instantiating the current EF model. Unknown formats never migrate.
    private static int DetectSchema(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Slot,Format,SchemaVersion,CatalogVersion FROM FileMetadata";
        using var reader = command.ExecuteReader();
        if (!reader.Read() || reader.GetInt32(0) != 1 || reader.GetString(1) != FormatId || string.IsNullOrWhiteSpace(reader.GetString(3)))
            throw Error("FILE_FOREIGN_FORMAT", "File.ForeignFormat");
        var schema = reader.GetInt32(2);
        if (reader.Read())
            throw Error("FILE_FOREIGN_FORMAT", "File.ForeignFormat");
        if (schema is not (1 or 2 or 3 or 4 or 5 or 6 or 7 or 8 or 9 or 10 or 11 or 12))
            throw Error("FILE_SCHEMA_UNSUPPORTED", "File.SchemaUnsupported");
        return schema;
    }

    // Detect known historical formats without opening an edit session, writing, or throwing
    // FILE_UPGRADE_REQUIRED as part of an expected user workflow.
    public bool RequiresUpgrade(string path, CancellationToken cancellationToken = default) => Guard(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        var source = Normalize(path);
        if (!File.Exists(source)) throw Error("FILE_NOT_FOUND", "File.NotFound");
        using var connection = Connect(source, SqliteOpenMode.ReadOnly);
        if (Scalar(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='FileMetadata';") != "1")
            throw Error("FILE_FOREIGN_FORMAT", "File.ForeignFormat");
        return DetectSchema(connection) < SchemaVersion;
    });

    public ICompanyFileSession Upgrade(string source, string destination, string actor, CancellationToken cancellationToken = default) => Guard(() =>
    {
        var original = Normalize(source);
        var target = Normalize(destination);
        if (string.IsNullOrWhiteSpace(actor))
            throw Error("FILE_INPUT_INVALID", "File.InputInvalid");
        EnsureNew(target);
        // Own both paths until promotion. No migration of the source, including EF lock recovery.
        using var sourceLock = Acquire(original, cancellationToken);
        var before = Validate(original, cancellationToken, allowLegacy: true);
        if (before.SchemaVersion == SchemaVersion)
            throw Error("FILE_UPGRADE_NOT_REQUIRED", "File.UpgradeNotRequired");
        using var reservation = Acquire(target, cancellationToken);
        EnsureNew(target);
        var temporary = Temporary(target);
        try
        {
            using (var input = Connect(original, SqliteOpenMode.ReadOnly))
            using (var output = Connect(temporary, SqliteOpenMode.ReadWriteCreate))
            {
                cancellationToken.ThrowIfCancellationRequested();
                input.BackupDatabase(output);
                Execute(output, "PRAGMA journal_mode=DELETE;");
                // Reject a foreign/stale EF lock rather than erase it in a copy.
                if (Scalar(output, "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='__EFMigrationsLock';") == "1" && Scalar(output, "SELECT COUNT(*) FROM __EFMigrationsLock;") != "0")
                    throw Error("FILE_MIGRATION_LOCKED", "File.MigrationLocked");
                if (before.SchemaVersion == 1)
                {
                    using var model = new ModelDbContext(output); model.Database.Migrate();
                }
                using (var studies = new StudyDbContext(output)) studies.Database.Migrate();
                using (var imports = new ImportDbContext(output)) imports.Database.Migrate();
                using (var groups = new GroupedImportDbContext(output)) groups.Database.Migrate();
                using (var ledger = new ImportLedgerDbContext(output)) ledger.Database.Migrate();
                using (var international = new InternationalImportDbContext(output)) international.Database.Migrate();
                using (var resources = new ResourceDbContext(output)) resources.Database.Migrate();
                using (var allocations = new AllocationDbContext(output)) allocations.Database.Migrate();
                using (var runs = new RunDbContext(output)) runs.Database.Migrate();
                using (var physical = new PhysicalDbContext(output)) physical.Database.Migrate();
                using var db = new CompositionDbContext(output);
                db.Database.Migrate();
                onCheckpoint?.Invoke(CompanyWriteCheckpoint.AfterFirstWrite);
                cancellationToken.ThrowIfCancellationRequested();
                using var tx = db.Database.BeginTransaction();
                db.Metadata.Single().SchemaVersion = SchemaVersion;
                var prior = db.Audit.Where(a => EF.Property<string>(a, "AggregateType") == "CompanyFile").Count();
                for (var from = before.SchemaVersion; from < SchemaVersion; from++)
                {
                    var oldMigration = from switch { 1 => MigrationId, 2 => UpgradeMigrationId, 3 => StudyMigrationId, 4 => ImportMigrationId, 5 => GroupedImportMigrationId, 6 => ImportLedgerMigrationId, 7 => InternationalImportMigrationId, 8 => ResourceMigrationId, 9 => AllocationMigrationId, 10 => RunMigrationId, _ => PhysicalMigrationId };
                    var newMigration = from switch { 1 => UpgradeMigrationId, 2 => StudyMigrationId, 3 => ImportMigrationId, 4 => GroupedImportMigrationId, 5 => ImportLedgerMigrationId, 6 => InternationalImportMigrationId, 7 => ResourceMigrationId, 8 => AllocationMigrationId, 9 => RunMigrationId, 10 => PhysicalMigrationId, _ => CompositionMigrationId };
                    AddAudit(db, new() { Id = NewId(), CompanyId = before.Id.ToString(), Revision = prior++, Operation = "UpgradeFormat", Actor = actor,
                        AtUtc = time.GetUtcNow().ToCanonicalString(), BeforeJson = Json(new FormatUpgrade(from, from, oldMigration)),
                        AfterJson = Json(new FormatUpgrade(from, from + 1, newMigration)) }, "CompanyFile", before.Id.ToString(), CanonicalSnapshotJson.SchemaVersion);
                }
                db.SaveChanges();
                onCheckpoint?.Invoke(CompanyWriteCheckpoint.BeforeCommit);
                cancellationToken.ThrowIfCancellationRequested();
                tx.Commit();
            }

            Validate(temporary, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, target, overwrite: false);
            return new Session(this, target, CompanyFileOpenMode.Edit, reservation.Detach());
        }
        finally
        {
            RemoveTemporary(temporary);
        }
    });
    private static string NewId() => new EntityId(Guid.NewGuid()).ToString();
    private static void AddAudit(ModelDbContext db, AuditRow row, string aggregateType, string aggregateId, string payloadSchema, string? transactionId = null)
    {
        var entry = db.Audit.Add(row);
        entry.Property("AggregateType").CurrentValue = aggregateType;
        entry.Property("AggregateId").CurrentValue = aggregateId;
        entry.Property("PayloadSchema").CurrentValue = payloadSchema;
        entry.Property("TransactionId").CurrentValue = transactionId ?? row.Id;
    }
}
