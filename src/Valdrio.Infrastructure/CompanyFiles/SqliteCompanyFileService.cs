using System.Diagnostics;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Valdrio.Application;
using Valdrio.Domain;

namespace Valdrio.Infrastructure.CompanyFiles;

public enum CompanyWriteCheckpoint { AfterFirstWrite, BeforeCommit }

// Checkpoints allow real rollback/cancellation tests; they never substitute the database.
public sealed partial class SqliteCompanyFileService(ICurrencyCatalog catalog, IClock clock,
    Action<CompanyWriteCheckpoint>? checkpoint = null) : ICompanyFileService
{
    private readonly Action<CompanyWriteCheckpoint>? onCheckpoint = checkpoint;
    private readonly IClock time = clock;
    public const string FormatId = "VALDRIO-COMPANY";
    public const int SchemaVersion = 3;
    public const string StudyMigrationId = "202610050003_StudiesAndSnapshots";
    public const string UpgradeMigrationId = "202610050002_VersionedModels";
    public const string MigrationId = "202610050001_InitialCompanyFile";
    private sealed record CreatedAudit(EntityId Id, string Name, CurrencyCode DefaultCurrency, PrecisionPolicy PrecisionPolicy);

    public ICompanyFileSession Create(CreateCompanyFile command, CancellationToken cancellationToken = default) =>
        Guard(() =>
        {
            ArgumentNullException.ThrowIfNull(command);
            if (string.IsNullOrWhiteSpace(command.Name)) throw Error("FILE_NAME_REQUIRED", "File.NameRequired");
            command.PrecisionPolicy.EnsureValid();
            if (!catalog.GetRequired(command.DefaultCurrency).IsMonetary)
                throw Error("FILE_CURRENCY_INVALID", "File.CurrencyInvalid");
            var target = Normalize(command.Path);
            EnsureNew(target);
            using var reservation = Acquire(target, cancellationToken);
            EnsureNew(target);
            var temporary = Temporary(target);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                using (var connection = Connect(temporary, SqliteOpenMode.ReadWriteCreate))
                using (var db = new StudyDbContext(connection))
                {
                    // Only our unpublished temporary file can receive migrations.
                    using (var legacy = new CompanyDbContext(connection)) legacy.Database.Migrate();
                    using (var v2 = new ModelDbContext(connection)) v2.Database.Migrate();
                    db.Database.Migrate();
                    Execute(connection, "PRAGMA journal_mode=DELETE;");
                    using var transaction = db.Database.BeginTransaction();
                    var id = new EntityId(Guid.NewGuid()).ToString();
                    var created = time.GetUtcNow().ToCanonicalString();
                    db.Metadata.Add(new() { Slot = 1, Format = FormatId, SchemaVersion = SchemaVersion, CatalogVersion = catalog.CatalogVersion });
                    db.Companies.Add(new() { Id = id, Slot = 1, Name = command.Name, Currency = command.DefaultCurrency.ToString(), CreatedAt = created, Revision = 0 });
                    var policy = command.PrecisionPolicy;
                    db.Policies.Add(new() { CompanyId = id, Version = policy.Version, AmountScale = policy.AmountScale,
                        QuantityScale = policy.QuantityScale, PercentageScale = policy.PercentageScale,
                        MaximumInitialMagnitude = policy.MaximumInitialMagnitude, MaximumNumericCorrection = policy.MaximumNumericCorrection });
                    db.SaveChanges();
                    onCheckpoint?.Invoke(CompanyWriteCheckpoint.AfterFirstWrite);
                    AddAudit(db, new() { Id = new EntityId(Guid.NewGuid()).ToString(), CompanyId = id, Revision = 0,
                        Operation = "CreateCompany", Actor = "local-user", AtUtc = created, BeforeJson = "null",
                        AfterJson = CanonicalSnapshotJson.Serialize(new CreatedAudit(EntityId.Parse(id), command.Name, command.DefaultCurrency, command.PrecisionPolicy)) }, "Company", id, CanonicalSnapshotJson.SchemaVersion);
                    db.SaveChanges();
                    onCheckpoint?.Invoke(CompanyWriteCheckpoint.BeforeCommit);
                    cancellationToken.ThrowIfCancellationRequested();
                    transaction.Commit();
                }
                Validate(temporary, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                File.Move(temporary, target, overwrite: false);
            }
            finally { RemoveTemporary(temporary); }
            // Transfer the OS lock without a release/reacquire window.
            return new Session(this, target, CompanyFileOpenMode.Edit, reservation.Detach());
        });

    public ICompanyFileSession Open(string path, CompanyFileOpenMode mode = CompanyFileOpenMode.Edit,
        CancellationToken cancellationToken = default) => Guard(() =>
        {
            if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            var target = Normalize(path);
            if (!File.Exists(target)) throw Error("FILE_NOT_FOUND", "File.NotFound");
            // Never acquire a write connection or migrate an incompatible file.
            Validate(target, cancellationToken);
            if (mode == CompanyFileOpenMode.ReadOnly) return new Session(this, target, mode, null);
            using var reservation = Acquire(target, cancellationToken);
            Validate(target, cancellationToken);
            using (var connection = Connect(target, SqliteOpenMode.ReadOnly))
                if (!string.Equals(Scalar(connection, "PRAGMA journal_mode;"), "delete", StringComparison.OrdinalIgnoreCase))
                    throw Error("FILE_JOURNAL_UNSUPPORTED", "File.JournalUnsupported");
            return new Session(this, target, mode, reservation.Detach());
        });

    public ICompanyFileSession Restore(string source, string destination, CancellationToken cancellationToken = default) => Guard(() =>
        {
            var target = Normalize(destination);
            EnsureNew(target);
            using var reservation = Acquire(target, cancellationToken);
            EnsureNew(target);
            using var original = Open(source, CompanyFileOpenMode.ReadOnly, cancellationToken);
            CopyDatabase(original.Path, target, cancellationToken);
            return new Session(this, target, CompanyFileOpenMode.Edit, reservation.Detach());
        });

    private void CopyDatabase(string source, string target, CancellationToken token)
    {
        EnsureNew(target);
        var temporary = Temporary(target);
        try
        {
            Validate(source, token);
            using (var connection = Connect(source, SqliteOpenMode.ReadOnly))
            using (var backup = Connect(temporary, SqliteOpenMode.ReadWriteCreate))
            {
                token.ThrowIfCancellationRequested();
                connection.BackupDatabase(backup);
                Execute(backup, "PRAGMA journal_mode=DELETE;");
            }
            Validate(temporary, token);
            token.ThrowIfCancellationRequested();
            File.Move(temporary, target, overwrite: false);
        }
        finally { RemoveTemporary(temporary); }
    }

    private static CompanyFileSnapshot Validate(string path, CancellationToken token, bool allowLegacy = false)
    {
        token.ThrowIfCancellationRequested();
        try
        {
            using var connection = Connect(path, SqliteOpenMode.ReadOnly);
            if (Scalar(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='FileMetadata';") != "1")
                throw Error("FILE_FOREIGN_FORMAT", "File.ForeignFormat");
            var detected = DetectSchema(connection);
            if (detected < SchemaVersion && !allowLegacy) throw Error("FILE_UPGRADE_REQUIRED", "File.UpgradeRequired");
            using var db = new CompanyDbContext(connection);
            // One read transaction makes metadata, values and audit a consistent snapshot.
            using var transaction = db.Database.BeginTransaction();
            var metadata = db.Metadata.AsNoTracking().Single();
            if (metadata.Format != FormatId || metadata.Slot != 1 || string.IsNullOrWhiteSpace(metadata.CatalogVersion))
                throw Error("FILE_FOREIGN_FORMAT", "File.ForeignFormat");
            if (metadata.SchemaVersion != detected)
                throw Error("FILE_SCHEMA_UNSUPPORTED", "File.SchemaUnsupported");
            if (Scalar(connection, "PRAGMA quick_check;") != "ok") throw new InvalidDataException("Integrity check failed");
            using (var check = connection.CreateCommand())
            {
                check.CommandText = "PRAGMA foreign_key_check;";
                using var reader = check.ExecuteReader();
                if (reader.Read()) throw new InvalidDataException("Foreign key check failed");
            }
            if (!db.Database.GetAppliedMigrations().SequenceEqual(detected switch { 1 => [MigrationId], 2 => [MigrationId, UpgradeMigrationId], _ => new[] { MigrationId, UpgradeMigrationId, StudyMigrationId } })) throw new InvalidDataException("Migration history differs");
            var company = db.Companies.AsNoTracking().Single();
            var policyRow = db.Policies.AsNoTracking().Single();
            var id = EntityId.Parse(company.Id);
            if (id.ToString() != company.Id || company.Slot != 1 || company.Revision < 0 || string.IsNullOrWhiteSpace(company.Name)
                || policyRow.CompanyId != company.Id) throw new InvalidDataException("Company metadata invalid");
            var currency = new CurrencyCode(company.Currency);
            if (currency.ToString() != company.Currency) throw new InvalidDataException("Currency is not canonical");
            var created = UtcInstant.ParseIso(company.CreatedAt);
            if (created.ToCanonicalString() != company.CreatedAt) throw new InvalidDataException("Instant is not canonical");
            var policy = new PrecisionPolicy(policyRow.Version, policyRow.AmountScale, policyRow.QuantityScale,
                policyRow.PercentageScale, policyRow.MaximumInitialMagnitude, policyRow.MaximumNumericCorrection);
            policy.EnsureValid();
            var units = db.Units.AsNoTracking().ToArray().Select(row =>
            {
                var unitId = UnitId.Parse(row.Id);
                if (unitId.ToString() != row.Id || row.CompanyId != company.Id || row.Symbol is null)
                    throw new InvalidDataException("Unit metadata invalid");
                return new UnitDefinition(unitId, row.Code, row.Dimension, row.Symbol, row.DecimalScale, row.IsDiscrete);
            }).OrderBy(unit => unit.Id).ToArray();
            if (units.Select(x => x.Code).Distinct(StringComparer.Ordinal).Count() != units.Length)
                throw new InvalidDataException("Duplicate unit code");
            using ModelDbContext? current = detected switch { 2 => new ModelDbContext(connection), 3 => new StudyDbContext(connection), _ => null };
            if (current is not null) current.Database.UseTransaction(transaction.GetDbTransaction());
            var audit = current is null ? db.Audit.AsNoTracking().OrderBy(x => x.Revision).ToArray()
                : current.Audit.AsNoTracking().Where(x => EF.Property<string>(x, "AggregateType") == "Company").OrderBy(x => x.Revision).ToArray();
            if (audit.LongLength != company.Revision + 1) throw new InvalidDataException("Audit/revision mismatch");
            for (var i = 0; i < audit.Length; i++)
            {
                var row = audit[i];
                if (row.CompanyId != company.Id || row.Revision != i || EntityId.Parse(row.Id).ToString() != row.Id
                    || string.IsNullOrWhiteSpace(row.Actor) || row.Operation != (i == 0 ? "CreateCompany" : "AddUnits")
                    || UtcInstant.ParseIso(row.AtUtc).ToCanonicalString() != row.AtUtc)
                    throw new InvalidDataException("Audit metadata invalid");
                if (i == 0)
                {
                    var original = CanonicalSnapshotJson.Deserialize<CreatedAudit>(row.AfterJson);
                    if (row.BeforeJson != "null" || original.Id != id || original.Name != company.Name
                        || original.DefaultCurrency != currency || original.PrecisionPolicy != policy)
                        throw new InvalidDataException("Creation audit does not describe this company");
                }
                else
                {
                    var before = CanonicalSnapshotJson.Deserialize<UnitDefinition[]>(row.BeforeJson);
                    var after = CanonicalSnapshotJson.Deserialize<UnitDefinition[]>(row.AfterJson);
                    var previous = i == 1 ? [] : CanonicalSnapshotJson.Deserialize<UnitDefinition[]>(audit[i - 1].AfterJson);
                    if (!before.SequenceEqual(previous) || !after.SequenceEqual(after.OrderBy(x => x.Id))
                        || after.Length <= before.Length || after.Select(x => x.Id).Distinct().Count() != after.Length
                        || after.Select(x => x.Code).Distinct(StringComparer.Ordinal).Count() != after.Length
                        || before.Any(x => !after.Contains(x)) || after.Any(x => x.Symbol is null)
                        || (i == audit.Length - 1 && !after.SequenceEqual(units)))
                        throw new InvalidDataException("Unit audit does not describe this revision");
                }
            }
            if (company.Revision == 0 && units.Length != 0) throw new InvalidDataException("Units lack audit");
            if (current is not null)
            {
                var file = new CompanyFileSnapshot(id, company.Name, currency, created, company.Revision, policy, units, metadata.SchemaVersion);
                ValidateWorkspace(current, file);
                if (current is StudyDbContext studies) ValidateStudies(studies, file);
            }
            token.ThrowIfCancellationRequested();
            return new(id, company.Name, currency, created, company.Revision, policy, units, metadata.SchemaVersion);
        }
        catch (CompanyFileException) { throw; }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is SqliteException or InvalidOperationException or ArgumentException or FormatException
            or InvalidDataException or JsonException or DomainRuleException)
        {
            throw Error("FILE_INVALID_DATA", "File.InvalidData", ex);
        }
    }

    private static SqliteConnection Connect(string path, SqliteOpenMode mode)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = path, Mode = mode, Pooling = false, ForeignKeys = true, DefaultTimeout = 2 }.ToString());
        try
        {
            connection.Open();
            Execute(connection, "PRAGMA foreign_keys=ON; PRAGMA busy_timeout=2000; PRAGMA synchronous=FULL;");
            return connection;
        }
        catch { connection.Dispose(); throw; }
    }
    private static string Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand(); command.CommandText = sql;
        return Convert.ToString(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) ?? "";
    }
    private static void Execute(SqliteConnection connection, string sql)
    { using var command = connection.CreateCommand(); command.CommandText = sql; command.ExecuteNonQuery(); }

    private static string Normalize(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var full = System.IO.Path.GetFullPath(path);
        if (full.StartsWith(@"\\", StringComparison.Ordinal)) throw Error("FILE_PATH_UNSUPPORTED", "File.PathUnsupported");
        // Reject symlink/reparse aliases; relative and case aliases use the same sidecar on Windows.
        for (FileSystemInfo? part = File.Exists(full) ? new FileInfo(full) : new DirectoryInfo(System.IO.Path.GetDirectoryName(full)!);
             part is not null; part = part is FileInfo file ? file.Directory : ((DirectoryInfo)part).Parent)
            if (part.Exists && (part.Attributes & FileAttributes.ReparsePoint) != 0)
                throw Error("FILE_PATH_UNSUPPORTED", "File.PathUnsupported");
        return WindowsFileIdentity.Normalize(full);
    }
    private static void EnsureNew(string target)
    {
        if (File.Exists(target) || Directory.Exists(target)) throw Error("FILE_ALREADY_EXISTS", "File.AlreadyExists");
        if (!Directory.Exists(System.IO.Path.GetDirectoryName(target))) throw Error("FILE_DIRECTORY_REQUIRED", "File.DirectoryRequired");
    }
    private static string Temporary(string target) => target + ".creating-" + Guid.NewGuid().ToString("N");
    private static void RemoveTemporary(string temporary)
    {
        foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" })
            if (File.Exists(temporary + suffix)) File.Delete(temporary + suffix);
    }
    private static Reservation Acquire(string target, CancellationToken token = default)
    {
        var watch = Stopwatch.StartNew();
        while (true)
        {
            token.ThrowIfCancellationRequested();
            try { return new(new FileStream(target + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)); }
            catch (IOException ex) when (!OperatingSystem.IsWindows() || (ex.HResult & 0xffff) is 32 or 33)
            {
                // Kernel/filter drivers can release handles slightly after process exit is signalled.
                // A bounded retry never removes the sidecar or admits two live owners.
                if (watch.ElapsedMilliseconds >= 2000) throw Error("FILE_IN_USE", "File.InUse", ex);
                if (token.WaitHandle.WaitOne(50)) token.ThrowIfCancellationRequested();
            }
        }
    }
    private sealed class Reservation(FileStream stream) : IDisposable
    {
        private FileStream? current = stream;
        public FileStream Detach() { var result = current!; current = null; return result; }
        public void Dispose() => current?.Dispose();
    }
    private static CompanyFileException Error(string code, string key, Exception? inner = null) => new(code, key, inner);
    private static T Guard<T>(Func<T> action)
    {
        try { return action(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SqliteException or DbUpdateException)
        {
            Trace.TraceError("VALDRIO company file I/O: {0}", ex);
            throw Error("FILE_IO_FAILED", "File.IoFailed", ex);
        }
    }

    private sealed partial class Session(SqliteCompanyFileService owner, string path, CompanyFileOpenMode mode, FileStream? editLock)
        : ICompanyFileSession, IModelWorkspace, IStudyWorkspace
    {
        private readonly object gate = new();
        private bool disposed;
        public IModelWorkspace Models => this;
        public IStudyWorkspace Studies => this;
        public string Path { get; } = path;
        public CompanyFileOpenMode Mode { get; } = mode;
        public CompanyFileSnapshot Read(CancellationToken cancellationToken = default)
        { lock (gate) { ObjectDisposedException.ThrowIf(disposed, this); return Guard(() => Validate(Path, cancellationToken)); } }

        public CompanyFileSnapshot AddUnits(IEnumerable<UnitDefinition> units, long expectedRevision, string actor,
            CancellationToken cancellationToken = default)
        {
            lock (gate)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                if (Mode != CompanyFileOpenMode.Edit) throw Error("FILE_READ_ONLY", "File.ReadOnly");
                var added = units.ToArray();
                if (added.Length == 0 || string.IsNullOrWhiteSpace(actor)) throw Error("FILE_UNIT_INVALID", "File.UnitInvalid");
                foreach (var unit in added)
                {
                    ArgumentNullException.ThrowIfNull(unit);
                    _ = new UnitDefinition(unit.Id, unit.Code, unit.Dimension, unit.Symbol, unit.DecimalScale, unit.IsDiscrete);
                    if (unit.Symbol is null) throw Error("FILE_UNIT_INVALID", "File.UnitInvalid");
                }
                return Guard(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var before = Validate(Path, cancellationToken);
                    if (before.Revision != expectedRevision) throw Error("FILE_REVISION_CHANGED", "File.RevisionChanged");
                    if (added.Select(x => x.Code).Concat(before.Units.Select(x => x.Code)).Distinct(StringComparer.Ordinal).Count() != added.Length + before.Units.Count
                        || added.Select(x => x.Id).Concat(before.Units.Select(x => x.Id)).Distinct().Count() != added.Length + before.Units.Count)
                        throw Error("FILE_DUPLICATE_UNIT", "File.DuplicateUnit");
                    using var connection = Connect(Path, SqliteOpenMode.ReadWrite);
                    using var db = new StudyDbContext(connection);
                    using var transaction = db.Database.BeginTransaction();
                    var company = db.Companies.Single();
                    if (company.Revision != expectedRevision) throw Error("FILE_REVISION_CHANGED", "File.RevisionChanged");
                    db.Units.AddRange(added.Select(unit => new UnitRow { Id = unit.Id.ToString(), CompanyId = company.Id,
                        Code = unit.Code, Dimension = unit.Dimension, Symbol = unit.Symbol, DecimalScale = unit.DecimalScale, IsDiscrete = unit.IsDiscrete }));
                    db.UnitStates.AddRange(added.Select(unit => new UnitStateRow { CompanyId = company.Id, UnitId = unit.Id.ToString(), IsActive = true, Revision = 0 }));
                    db.SaveChanges();
                    owner.onCheckpoint?.Invoke(CompanyWriteCheckpoint.AfterFirstWrite);
                    company.Revision = checked(company.Revision + 1);
                    AddAudit(db, new() { Id = new EntityId(Guid.NewGuid()).ToString(), CompanyId = company.Id, Revision = company.Revision,
                        Operation = "AddUnits", Actor = actor, AtUtc = owner.time.GetUtcNow().ToCanonicalString(),
                        BeforeJson = CanonicalSnapshotJson.Serialize(before.Units),
                        AfterJson = CanonicalSnapshotJson.Serialize(before.Units.Concat(added).OrderBy(x => x.Id).ToArray()) }, "Company", company.Id, CanonicalSnapshotJson.SchemaVersion);
                    db.SaveChanges();
                    owner.onCheckpoint?.Invoke(CompanyWriteCheckpoint.BeforeCommit);
                    cancellationToken.ThrowIfCancellationRequested();
                    transaction.Commit();
                    return new CompanyFileSnapshot(before.Id, before.Name, before.DefaultCurrency, before.CreatedAt,
                        company.Revision, before.PrecisionPolicy, before.Units.Concat(added).OrderBy(x => x.Id), before.SchemaVersion);
                });
            }
        }
        public void Backup(string destination, CancellationToken cancellationToken = default)
        {
            lock (gate)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                Guard(() => { var target = Normalize(destination); using var reservation = Acquire(target, cancellationToken);
                    owner.CopyDatabase(Path, target, cancellationToken); return true; });
            }
        }
        public void Dispose() { lock (gate) { if (disposed) return; editLock?.Dispose(); disposed = true; } }
    }
}
