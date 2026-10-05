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
        if (reader.Read()) throw Error("FILE_FOREIGN_FORMAT", "File.ForeignFormat");
        if (schema is not (1 or 2)) throw Error("FILE_SCHEMA_UNSUPPORTED", "File.SchemaUnsupported");
        return schema;
    }

    public ICompanyFileSession Upgrade(string source, string destination, string actor, CancellationToken cancellationToken = default) => Guard(() =>
    {
        var original = Normalize(source); var target = Normalize(destination);
        if (string.IsNullOrWhiteSpace(actor)) throw Error("FILE_INPUT_INVALID", "File.InputInvalid");
        EnsureNew(target);
        // Own both paths until promotion. No migration of the source, including EF lock recovery.
        using var sourceLock = Acquire(original, cancellationToken);
        var before = Validate(original, cancellationToken, allowLegacy: true);
        if (before.SchemaVersion != 1) throw Error("FILE_UPGRADE_NOT_REQUIRED", "File.UpgradeNotRequired");
        using var reservation = Acquire(target, cancellationToken); EnsureNew(target);
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
                if (Scalar(output, "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='__EFMigrationsLock';") == "1"
                    && Scalar(output, "SELECT COUNT(*) FROM __EFMigrationsLock;") != "0")
                    throw Error("FILE_MIGRATION_LOCKED", "File.MigrationLocked");
                using var db = new ModelDbContext(output);
                db.Database.Migrate();
                onCheckpoint?.Invoke(CompanyWriteCheckpoint.AfterFirstWrite);
                cancellationToken.ThrowIfCancellationRequested();
                using var tx = db.Database.BeginTransaction();
                db.Metadata.Single().SchemaVersion = SchemaVersion;
                AddAudit(db, new() { Id = NewId(), CompanyId = before.Id.ToString(), Revision = 0,
                    Operation = "UpgradeFormat", Actor = actor, AtUtc = time.GetUtcNow().ToCanonicalString(),
                    BeforeJson = CanonicalSnapshotJson.Serialize(new FormatUpgrade(1, 1, MigrationId)),
                    AfterJson = CanonicalSnapshotJson.Serialize(new FormatUpgrade(1, 2, UpgradeMigrationId)) },
                    "CompanyFile", before.Id.ToString(), CanonicalSnapshotJson.SchemaVersion);
                db.SaveChanges(); onCheckpoint?.Invoke(CompanyWriteCheckpoint.BeforeCommit);
                cancellationToken.ThrowIfCancellationRequested(); tx.Commit();
            }
            Validate(temporary, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested(); File.Move(temporary, target, overwrite: false);
            return new Session(this, target, CompanyFileOpenMode.Edit, reservation.Detach());
        }
        finally { RemoveTemporary(temporary); }
    });

    private static string NewId() => new EntityId(Guid.NewGuid()).ToString();
    private static void AddAudit(ModelDbContext db, AuditRow row, string aggregateType, string aggregateId,
        string payloadSchema, string? transactionId = null)
    {
        var entry = db.Audit.Add(row);
        entry.Property("AggregateType").CurrentValue = aggregateType;
        entry.Property("AggregateId").CurrentValue = aggregateId;
        entry.Property("PayloadSchema").CurrentValue = payloadSchema;
        entry.Property("TransactionId").CurrentValue = transactionId ?? row.Id;
    }
}
