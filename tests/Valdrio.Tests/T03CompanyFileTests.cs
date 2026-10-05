using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Valdrio.Application;
using Valdrio.Domain;
using Valdrio.Infrastructure;
using Valdrio.Infrastructure.CompanyFiles;

namespace Valdrio.Tests;

[TestClass]
public sealed class T03CompanyFileTests
{
    private string directory = null!;
    private string PathOf(string name = "company.valdrio") => Path.Combine(directory, name);
    private sealed class FixedClock : IClock { public UtcInstant GetUtcNow() => UtcInstant.ParseIso("2026-10-05T12:00:00.0000000Z"); }
    private SqliteCompanyFileService Service(Action<CompanyWriteCheckpoint>? hook = null) => new(
        new JsonCurrencyCatalog(Path.Combine(AppContext.BaseDirectory, "reference", "currency-catalog-example.json")), new FixedClock(), hook);
    private CreateCompanyFile Command(string? path = null, string currency = "EUR") => new(path ?? PathOf(), "Fábrica 日本 00123", new(currency), PrecisionPolicy.Pdec1Proposed);
    private static UnitDefinition Unit(string code = "00123") => new(new(new(Guid.NewGuid())), code, "time", "h", 12, false);
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static string Scalar(string path, string sql)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        connection.Open(); using var command = connection.CreateCommand(); command.CommandText = sql;
        return Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture)!;
    }
    private static void Sql(string path, string sql)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false, ForeignKeys = true }.ToString());
        connection.Open(); using var command = connection.CreateCommand(); command.CommandText = sql; command.ExecuteNonQuery();
    }
    [TestInitialize] public void Init() { directory = Path.Combine(Path.GetTempPath(), "valdrio-t03-" + Guid.NewGuid()); Directory.CreateDirectory(directory); }
    [TestCleanup] public void Clean() => Directory.Delete(directory, true);

    [TestMethod] public void F01_CreateReopenRetainsIdentityPolicyUnitsAndAudit()
    {
        var service = Service(); CompanyFileSnapshot original;
        using (var session = service.Create(Command()))
        {
            original = session.AddUnits([Unit(), Unit("工場")], 0, "controller");
            Assert.AreEqual(1L, original.Revision);
        }
        using var reopened = service.Open(PathOf()); var read = reopened.Read();
        Assert.AreEqual(original.Id, read.Id); Assert.AreEqual(original.CreatedAt, read.CreatedAt);
        Assert.AreEqual(Command().PrecisionPolicy, read.PrecisionPolicy);
        Assert.AreEqual("Fábrica 日本 00123", read.Name); Assert.AreEqual("EUR", read.DefaultCurrency.ToString());
        CollectionAssert.AreEquivalent(new[] { "00123", "工場" }, read.Units.Select(x => x.Code).ToArray());
        Assert.AreEqual("2", Scalar(PathOf(), "SELECT count(*) FROM AuditEvents"));
        Assert.AreEqual("delete", Scalar(PathOf(), "PRAGMA journal_mode"));
        Assert.AreEqual(SqliteCompanyFileService.MigrationId, Scalar(PathOf(), "SELECT MigrationId FROM __EFMigrationsHistory"));
        using var connection = new SqliteConnection("Data Source=" + PathOf() + ";Pooling=False"); connection.Open();
        using var db = new CompanyDbContext(connection);
        Assert.IsFalse(db.Database.HasPendingModelChanges(), "Frozen EF snapshot must match schema v1");
        Console.WriteLine("Native SQLite " + Scalar(PathOf(), "SELECT sqlite_version()"));
    }
    [TestMethod] public void F02_CreateNeverOverwritesExistingFile()
    {
        File.WriteAllText(PathOf(), "original-not-sqlite"); var hash = Hash(PathOf());
        var error = Assert.ThrowsExactly<CompanyFileException>(() => Service().Create(Command()));
        Assert.AreEqual("FILE_ALREADY_EXISTS", error.Diagnostic.Code); Assert.AreEqual(hash, Hash(PathOf()));
    }
    [TestMethod] public void F_CustomPolicyAndSnapshotCollectionsArePreserved()
    {
        var policy = new PrecisionPolicy("custom-test", 6, 9, 18, 999999.0001m, 0.000001m);
        using (var session = Service().Create(Command() with { PrecisionPolicy = policy }))
        {
            var input = new[] { Unit("A") }; var snapshot = session.AddUnits(input, 0, "test"); input[0] = Unit("B");
            Assert.AreEqual("A", snapshot.Units[0].Code);
            Assert.ThrowsExactly<NotSupportedException>(() => ((IList<UnitDefinition>)snapshot.Units)[0] = Unit("C"));
        }
        using var reopened = Service().Open(PathOf()); Assert.AreEqual(policy, reopened.Read().PrecisionPolicy);
        Assert.AreEqual("999999.0001", Scalar(PathOf(), "SELECT MaximumInitialMagnitude FROM PrecisionPolicies"));
    }
    private sealed class DecimalRow { public int Id { get; set; } public decimal Value { get; set; } }
    private sealed class DecimalDb(string path) : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder b) => b.UseSqlite("Data Source=" + path + ";Pooling=False");
        protected override void OnModelCreating(ModelBuilder b) { b.Entity<DecimalRow>().Property(x => x.Value).HasConversion<CanonicalDecimalConverter>().HasColumnType("TEXT"); }
    }
    [TestMethod] public void F03_RealEfDecimalTextRoundTripsWithoutBinaryConversion()
    {
        // Test-only probe; T03 does not pretend to implement resource/rate persistence (T05/T06A).
        decimal[] values = [1_000_000_000_000m, -999999999999.12345678m, 0.123456789012m, 0.123456789012345678m, -0.00000001m];
        using (var db = new DecimalDb(PathOf())) { db.Database.EnsureCreated(); db.AddRange(values.Select((v,i) => new DecimalRow { Id = i + 1, Value = v })); db.SaveChanges(); }
        using (var db = new DecimalDb(PathOf())) CollectionAssert.AreEqual(values, db.Set<DecimalRow>().AsNoTracking().OrderBy(x => x.Id).Select(x => x.Value).ToArray());
        Assert.AreEqual("5", Scalar(PathOf(), "SELECT count(*) FROM DecimalRow WHERE typeof(Value)='text'"));
        Assert.AreEqual("-999999999999.12345678", Scalar(PathOf(), "SELECT Value FROM DecimalRow WHERE Id=2"));
    }
    [TestMethod] public void F05_CorruptTypedValuesNeverGetDefaults()
    {
        string[] changes = ["UPDATE Companies SET Currency='bad'", "UPDATE Companies SET Id='00000000-0000-0000-0000-000000000000'", "UPDATE PrecisionPolicies SET MaximumNumericCorrection='0,01'", "UPDATE Units SET Dimension=''", "UPDATE Companies SET CreatedAt='2026-10-05'", "UPDATE AuditEvents SET AfterJson='{}' WHERE Revision=1"];
        for (var i=0;i<changes.Length;i++)
        {
            var path = PathOf(i + ".valdrio"); using (var session = Service().Create(Command(path))) session.AddUnits([Unit()], 0, "test");
            // Tamper fixture with FK disabled for the malformed ID case.
            Sql(path, "PRAGMA foreign_keys=OFF; " + changes[i]); var hash = Hash(path);
            Assert.ThrowsExactly<CompanyFileException>(() => Service().Open(path)); Assert.AreEqual(hash, Hash(path));
        }
    }
    [TestMethod] public void F06_PhysicalConstraintsRejectSecondCompanyForeignKeyAndDuplicateCode()
    {
        using var session = Service().Create(Command()); session.AddUnits([Unit()], 0, "test");
        Assert.ThrowsExactly<SqliteException>(() => Sql(PathOf(), "INSERT INTO Companies SELECT 'second',Slot,Name,Currency,CreatedAt,Revision FROM Companies"));
        Assert.ThrowsExactly<SqliteException>(() => Sql(PathOf(), "INSERT INTO Units VALUES('different','missing','code','time','h',12,0)"));
        Assert.ThrowsExactly<SqliteException>(() => Sql(PathOf(), "INSERT INTO Units SELECT 'different',CompanyId,Code,Dimension,Symbol,DecimalScale,IsDiscrete FROM Units"));
        var error = Assert.ThrowsExactly<CompanyFileException>(() => session.AddUnits([Unit()], 1, "test"));
        Assert.AreEqual("FILE_DUPLICATE_UNIT", error.Diagnostic.Code);
        Assert.AreEqual(1, session.Read().Units.Count);
    }
    [TestMethod] public async Task F07_TwoProcessesAndKillReleaseActualOsLock()
    {
        string id; using (var first = Service().Create(Command())) id = first.Read().Id.ToString();
        using var child = Probe();
        try
        {
            Console.WriteLine("Probe PID " + child.Id);
            var ready = await child.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(20));
            Assert.AreEqual("READY " + id, ready);
            var blocked = Assert.ThrowsExactly<CompanyFileException>(() => Service().Open(PathOf()));
            Assert.AreEqual("FILE_IN_USE", blocked.Diagnostic.Code);
            child.Kill(true); await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            Console.WriteLine("Killed probe, HasExited=" + child.HasExited);
            using var afterKill = Service().Open(PathOf()); Assert.AreEqual(id, afterKill.Read().Id.ToString());
        }
        catch (Exception ex) { Console.WriteLine(ex.ToString()); throw; }
        finally { if (!child.HasExited) { child.Kill(true); await child.WaitForExitAsync(); } }
    }
    [TestMethod] public async Task F08_NormalCloseReleasesLockForAnotherProcess()
    {
        using (Service().Create(Command())) { }
        using var child = Probe();
        try { Assert.IsTrue((await child.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(20)))!.StartsWith("READY ", StringComparison.Ordinal));
            await child.StandardInput.WriteLineAsync("close"); await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); Assert.AreEqual(0, child.ExitCode); }
        finally { if (!child.HasExited) { child.Kill(true); await child.WaitForExitAsync(); } }
        using var reopened = Service().Open(PathOf()); Assert.AreEqual(0L, reopened.Read().Revision);
    }
    private Process Probe()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Valdrio.slnx"))) root = root.Parent;
        Assert.IsNotNull(root);
        var dll = Path.Combine(root.FullName, "src", "Valdrio.Runner", "bin", "Release", "net10.0", "Valdrio.Runner.dll");
        var info = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
            { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        info.ArgumentList.Add(dll); info.ArgumentList.Add("--company-lock-probe"); info.ArgumentList.Add(PathOf());
        return Process.Start(info)!;
    }
    [TestMethod] public void F09_FutureSchemaBlockedBeforeAnyMigrationOrWrite()
    {
        using (Service().Create(Command())) { } Sql(PathOf(), "UPDATE FileMetadata SET SchemaVersion=99"); var hash = Hash(PathOf());
        var ex = Assert.ThrowsExactly<CompanyFileException>(() => Service().Open(PathOf()));
        Assert.AreEqual("FILE_SCHEMA_UNSUPPORTED", ex.Diagnostic.Code); Assert.AreEqual(hash, Hash(PathOf()));
    }
    [TestMethod] public void F10_ForeignCorruptMissingAndIncompleteFilesAreRejected()
    {
        File.WriteAllText(PathOf("corrupt"), "not a database"); Sql(PathOf("foreign"), "CREATE TABLE Something(Id INTEGER)");
        using (Service().Create(Command())) { } Sql(PathOf(), "DELETE FROM AuditEvents");
        foreach (var name in new[] { "corrupt", "foreign", "missing", "company.valdrio" })
            Assert.ThrowsExactly<CompanyFileException>(() => Service().Open(PathOf(name)));
    }
    [TestMethod] public void F11_FailureBetweenWritesRollsBackUnitsRevisionAndAuditAfterReopen()
    {
        using (Service().Create(Command())) { }
        using (var session = Service(point => { if (point == CompanyWriteCheckpoint.AfterFirstWrite) throw new IOException("injected"); }).Open(PathOf()))
            Assert.ThrowsExactly<CompanyFileException>(() => session.AddUnits([Unit("A"), Unit("B")], 0, "test"));
        using var fresh = Service().Open(PathOf()); Assert.AreEqual(0L, fresh.Read().Revision); Assert.AreEqual(0, fresh.Read().Units.Count);
        Assert.AreEqual("1", Scalar(PathOf(), "SELECT count(*) FROM AuditEvents"));
    }
    [TestMethod] public void F12_CancellationImmediatelyBeforeCommitRevertsEverything()
    {
        using (Service().Create(Command())) { } using var token = new CancellationTokenSource();
        using (var session = Service(point => { if (point == CompanyWriteCheckpoint.BeforeCommit) token.Cancel(); }).Open(PathOf()))
            Assert.ThrowsExactly<OperationCanceledException>(() => session.AddUnits([Unit()], 0, "test", token.Token));
        using var fresh = Service().Open(PathOf()); Assert.AreEqual(0, fresh.Read().Units.Count); Assert.AreEqual(0L, fresh.Read().Revision);
        Assert.AreEqual("1", Scalar(PathOf(), "SELECT count(*) FROM AuditEvents"));
    }
    [TestMethod] public void F13_LiveBackupIncludesCommittedWalWithoutRawCopy()
    {
        using (Service().Create(Command())) { }
        // External WAL fixture, no concurrent checkpoint/write. Product edit mode is DELETE.
        using var wal = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = PathOf(), Pooling = false }.ToString()); wal.Open();
        using (var cmd = wal.CreateCommand()) { cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA wal_autocheckpoint=0; UPDATE FileMetadata SET CatalogVersion='Committed in WAL'"; cmd.ExecuteNonQuery(); }
        Assert.IsTrue(File.Exists(PathOf() + "-wal"));
        using (var readOnly = Service().Open(PathOf(), CompanyFileOpenMode.ReadOnly)) readOnly.Backup(PathOf("copy.valdrio-backup"));
        using var backup = Service().Open(PathOf("copy.valdrio-backup")); Assert.IsTrue(backup.Read().Id.IsValid);
        Assert.AreEqual("Committed in WAL", Scalar(backup.Path, "SELECT CatalogVersion FROM FileMetadata"));
    }
    [TestMethod] public void F14_RestoreValidatesCreatesNewFileAndNeverOverwrites()
    {
        using var original = Service().Create(Command()); original.AddUnits([Unit()], 0, "test");
        original.Backup(PathOf("copy.valdrio-backup")); var hash = Hash(PathOf());
        using var restored = Service().Restore(PathOf("copy.valdrio-backup"), PathOf("restored.valdrio"));
        Assert.AreEqual(original.Read().Id, restored.Read().Id); Assert.AreEqual(1, restored.Read().Units.Count);
        Assert.ThrowsExactly<CompanyFileException>(() => Service().Restore(PathOf("copy.valdrio-backup"), PathOf()));
        File.WriteAllText(PathOf("bad"), "bad"); Assert.ThrowsExactly<CompanyFileException>(() => Service().Restore(PathOf("bad"), PathOf("failed.valdrio")));
        Assert.IsFalse(File.Exists(PathOf("failed.valdrio"))); Assert.AreEqual(hash, Hash(PathOf()));
        Assert.AreEqual(0, Directory.GetFiles(directory, "*.creating-*").Length);
    }
    [TestMethod] public void F15_CultureDoesNotChangeStoredCurrencyDecimalsOrUtc()
    {
        var prior = CultureInfo.CurrentCulture;
        try
        {
            foreach (var pair in new[] { ("es-ES","USD"), ("en-US","EUR"), ("de-DE","JPY") })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(pair.Item1);
                var path = PathOf(pair.Item1 + ".valdrio"); using (Service().Create(Command(path, pair.Item2))) { }
                var hash = Hash(path); CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                using var reopened = Service().Open(path); Assert.AreEqual(pair.Item2, reopened.Read().DefaultCurrency.ToString());
                Assert.AreEqual(PrecisionPolicy.Pdec1Proposed, reopened.Read().PrecisionPolicy);
                Assert.AreEqual("2026-10-05T12:00:00.0000000Z", reopened.Read().CreatedAt.ToCanonicalString()); Assert.AreEqual(hash, Hash(path));
            }
        }
        finally { CultureInfo.CurrentCulture = prior; }
    }
    [TestMethod] public void F_ReadOnlyAndStaleRevisionRejectWrites()
    {
        using var editor = Service().Create(Command()); editor.AddUnits([Unit("A")], 0, "test");
        Assert.AreEqual("FILE_REVISION_CHANGED", Assert.ThrowsExactly<CompanyFileException>(() => editor.AddUnits([Unit("B")], 0, "test")).Diagnostic.Code);
        using var viewer = Service().Open(PathOf(), CompanyFileOpenMode.ReadOnly);
        Assert.AreEqual("FILE_READ_ONLY", Assert.ThrowsExactly<CompanyFileException>(() => viewer.AddUnits([Unit("B")], 1, "test")).Diagnostic.Code);
        Assert.AreEqual(1, viewer.Read().Units.Count);
    }
    [TestMethod] public void F_CreationFailureCleansOnlyOwnedTemporaryFile()
    {
        Assert.ThrowsExactly<CompanyFileException>(() => Service(_ => throw new IOException("injected")).Create(Command()));
        Assert.IsFalse(File.Exists(PathOf())); Assert.AreEqual(0, Directory.GetFiles(directory, "*.creating-*").Length);
        using var retry = Service().Create(Command()); Assert.IsTrue(retry.Read().Id.IsValid);
    }
    [TestMethod] public void F_PathAliasesShareLockAndSymlinksAreRejected()
    {
        using var editor = Service().Create(Command());
        var alias = Path.Combine(directory, "..", new DirectoryInfo(directory).Name, "company.valdrio");
        Assert.AreEqual("FILE_IN_USE", Assert.ThrowsExactly<CompanyFileException>(() => Service().Open(alias)).Diagnostic.Code);
        if (OperatingSystem.IsWindows())
        {
            Assert.AreEqual("FILE_IN_USE", Assert.ThrowsExactly<CompanyFileException>(() => Service().Open(PathOf().ToUpperInvariant())).Diagnostic.Code);
            Assert.IsTrue(CreateHardLink(PathOf("hardlink.valdrio"), PathOf(), IntPtr.Zero));
            Assert.AreEqual("FILE_PATH_UNSUPPORTED", Assert.ThrowsExactly<CompanyFileException>(() => Service().Open(PathOf("hardlink.valdrio"))).Diagnostic.Code);
        }
        else { File.CreateSymbolicLink(PathOf("alias.valdrio"), PathOf());
            Assert.AreEqual("FILE_PATH_UNSUPPORTED", Assert.ThrowsExactly<CompanyFileException>(() => Service().Open(PathOf("alias.valdrio"))).Diagnostic.Code); }
    }
    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string name, string existing, IntPtr security);
}
