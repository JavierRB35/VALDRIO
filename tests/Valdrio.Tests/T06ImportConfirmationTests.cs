using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Valdrio.Application;
using Valdrio.Desktop;
using Valdrio.Domain;
using Valdrio.Infrastructure;
using Valdrio.Infrastructure.CompanyFiles;
using Valdrio.Localization;

namespace Valdrio.Tests;

[TestClass]
public sealed partial class T06ImportConfirmationTests
{
    private string directory = null!;
    private static EntityId Id() => new(Guid.NewGuid());
    private string FileOf(string name) => Path.Combine(directory, name);
    private sealed class Clock : IClock { public UtcInstant GetUtcNow() => UtcInstant.ParseIso("2026-10-06T10:00:00Z"); }
    private static JsonCurrencyCatalog Catalog => new(Path.Combine(AppContext.BaseDirectory, "reference", "currency-catalog-example.json"));
    private static SqliteCompanyFileService Service(Action<CompanyWriteCheckpoint>? hook = null) => new(Catalog, new Clock(), hook);
    [TestInitialize] public void Init() { directory = Path.Combine(Path.GetTempPath(), "valdrio-confirm-" + Guid.NewGuid()); Directory.CreateDirectory(directory); }
    [TestCleanup] public void Cleanup() => Directory.Delete(directory, true);
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    private string Fixture(int version = 3)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", version == 3 ? "t05-v3.json" : "t04-v2.json")));
        using var input = new GZipStream(new MemoryStream(Convert.FromBase64String(doc.RootElement.GetProperty("GzipBase64").GetString()!)), CompressionMode.Decompress);
        var path = FileOf("original-" + version + ".valdrio"); using (var output = File.Create(path)) input.CopyTo(output);
        Assert.AreEqual(doc.RootElement.GetProperty("Sha256").GetString(), Hash(path)); return path;
    }
    private ICompanyFileSession Baseline() => Service().Upgrade(Fixture(), FileOf("current.valdrio"), "test");
    private static StudyDefinition Study(ICompanyFileSession file) => file.Studies.Create(new(file.Read().Id, Id(), "Period import",
        BusinessDate.ParseIso("2026-10-01"), BusinessDate.ParseIso("2026-10-31"), StudyPurpose.Management, StudyScenario.Actual,
        file.Models.ReadModels().First(m => m.Status == ModelStatus.Published).Structure.Id, new("EUR"), "test"));
    private static SourceParseProfileVersion Profile(StudyDefinition study, CurrencyCode? currency = null,
        ImportAmountMode mode = ImportAmountMode.PeriodAmount, ImportProfileApprovalState approval = ImportProfileApprovalState.ApprovedBusiness,
        ImportZeroPolicy zero = ImportZeroPolicy.Keep, int header = 1, int first = 2) => new(SourceParseProfileVersion.CurrentSchema,
        Id(), 1, "Synthetic checked example", ImportSourceKind.Csv, null, header, first,
        new(3, amount: 1), new("utf-8", ';', '"', CsvBomPolicy.Optional), new(",", ".", [3], ImportNegativeSyntax.LeadingMinus),
        "yyyy-MM-dd", mode, ImportSignConvention.SignedAmount, study.PeriodStart, study.PeriodEnd, currency ?? new("EUR"), null,
        ImportDuplicatePolicy.Reject, zero, ImportAccountSelection.All,
        new(approval, "Synthetic example 00123=160, credit=-2.5, zero=0"), true);
    private async Task<ImportSourceDocument> Source(SourceParseProfileVersion profile, string csv = "Amount;Ignored;Account\n160;x;00123\n-2,50;y;A-42\n0;z;ZERO")
    {
        var path = FileOf("source.csv"); File.WriteAllText(path, csv); using var captured = await new ImportSourceSessionFactory().OpenAsync(path);
        return await captured.ReadAsync(new(profile.SourceKind, profile.WorksheetName, profile.HeaderRow, profile.FirstDataRow, profile.Csv));
    }
    private static PreparedImport Prepare(ICompanyFileSession file, StudyDefinition study, ImportSourceDocument source, SourceParseProfileVersion profile) =>
        file.Imports.PrepareImport(study.CompanyId, study.Id, study.Revision, Id(), source, profile);
    private static ConfirmImport Command(PreparedImport prepared) => new(prepared.StudyContext.CompanyId, prepared.StudyContext.Id,
        prepared.StudyContext.Revision, Id(), prepared.Fingerprint, prepared, ImportConfirmMode.AppendBatch, "test");
    private static string Query(string path, string sql)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false, ForeignKeys = true }.ToString());
        connection.Open(); using var command = connection.CreateCommand(); command.CommandText = sql;
        return Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture) ?? "";
    }

    [TestMethod]
    public async Task ExplicitFullClosingBalanceKeeps160AndPersistsItsDistinctMeaning()
    {
        EntityId studyId; ImportedBatch batch; StudySnapshotReceipt photo;
        using (var file = Baseline())
        {
            var study = Study(file); studyId = study.Id;
            var profile = Profile(study, mode: ImportAmountMode.ClosingBalanceAsStudyAmount);
            var prepared = Prepare(file, study, await Source(profile, "Amount;Ignored;Account\n160;x;00123"), profile);
            Assert.IsTrue(prepared.CanConfirm);
            Assert.AreEqual(160m, prepared.Reconciliation.NetAmount);
            var command = Command(prepared); batch = file.Imports.ConfirmImport(command);
            Assert.AreEqual(batch.Id, file.Imports.ConfirmImport(command).Id);
            Assert.AreEqual(160m, batch.Resources.Single().Functional.Amount);
            photo = file.Studies.Capture(new(study.CompanyId, study.Id, 1, Id(), "test"));
        }
        using var reopened = Service().Open(FileOf("current.valdrio"));
        var company = reopened.Read(); var saved = reopened.Imports.ReadImports(company.Id, studyId).Single();
        Assert.AreEqual(ImportAmountMode.ClosingBalanceAsStudyAmount, saved.Prepared.Profile.AmountMode);
        Assert.AreEqual(BusinessDate.ParseIso("2026-10-31"), saved.Prepared.Profile.PeriodEnd);
        Assert.AreEqual(160m, saved.Resources.Single().Functional.Amount);
        Assert.AreEqual(CanonicalSnapshotJson.Serialize(batch), CanonicalSnapshotJson.Serialize(saved));
        Assert.AreEqual(photo.CanonicalJson, reopened.Studies.ReadSnapshots(company.Id, studyId).Single().CanonicalJson);
    }
    [TestMethod]
    public async Task FullClosingBalanceStillRequiresApprovalAndNeverWritesWhenUnapproved()
    {
        using var file = Baseline(); var study = Study(file);
        var profile = Profile(study, mode: ImportAmountMode.ClosingBalanceAsStudyAmount,
            approval: ImportProfileApprovalState.PendingBusinessApproval);
        var before = Hash(file.Path);
        var prepared = Prepare(file, study, await Source(profile), profile);
        Assert.IsFalse(prepared.CanConfirm);
        Assert.IsTrue(prepared.Diagnostics.Any(d => d.Diagnostic.Code == "IMP_APPROVAL_REQUIRED"));
        Assert.ThrowsExactly<DomainRuleException>(() => file.Imports.ConfirmImport(Command(prepared)));
        Assert.AreEqual(before, Hash(file.Path));
        Assert.AreEqual(0, file.Imports.ReadImports(study.CompanyId, study.Id).Count);
    }
    [TestMethod]
    public void V4MappingAndMigrationSnapshotHaveNoPendingChanges()
    {
        using var connection = new SqliteConnection("Data Source=:memory:"); connection.Open(); using var context = new ImportDbContext(connection);
        var snapshot = context.GetService<IMigrationsAssembly>().ModelSnapshot!.Model;
        var initialized = context.GetService<IModelRuntimeInitializer>().Initialize(snapshot, designTime: true);
        var changes = context.GetService<IMigrationsModelDiffer>().GetDifferences(initialized.GetRelationalModel(),
            context.GetService<Microsoft.EntityFrameworkCore.Metadata.IDesignTimeModel>().Model.GetRelationalModel());
        Assert.AreEqual(0, changes.Count, string.Join("\n", changes.Select(c => c switch
        {
            AlterColumnOperation a => a.Table + "." + a.Name + " nullable " + a.OldColumn.IsNullable + "→" + a.IsNullable,
            AddColumnOperation a => "add " + a.Table + "." + a.Name,
            CreateIndexOperation a => "index " + a.Table + "." + a.Name,
            _ => c.GetType().Name
        })));
        Assert.IsFalse(context.Database.HasPendingModelChanges());
    }
    [TestMethod]
    public void GenuineV3MigrationPreservesAllFiveSnapshotsAuditAndSourceBytes()
    {
        var source = Fixture(); var sha = Hash(source);
        var photos = Query(source, "SELECT group_concat(CanonicalJson) FROM StudySnapshots ORDER BY Id");
        var audit = Query(source, "SELECT group_concat(Id||AfterJson) FROM AuditEvents ORDER BY Id");
        Assert.AreEqual("FILE_UPGRADE_REQUIRED", Assert.ThrowsExactly<CompanyFileException>(() => Service().Open(source)).Diagnostic.Code);
        using var file = Service().Upgrade(source, FileOf("current.valdrio"), "test"); Assert.AreEqual(SqliteCompanyFileService.SchemaVersion, file.Read().SchemaVersion);
        Assert.AreEqual(sha, Hash(source)); Assert.AreEqual(photos, Query(file.Path, "SELECT group_concat(CanonicalJson) FROM StudySnapshots ORDER BY Id"));
        Assert.AreEqual(audit, Query(file.Path, "SELECT group_concat(Id||AfterJson) FROM AuditEvents WHERE AggregateType<>'CompanyFile' ORDER BY Id"));
        var study = file.Studies.ReadStudies().Single(); var snapshots = file.Studies.ReadSnapshots(study.CompanyId, study.Id);
        Assert.AreEqual(5, snapshots.Count); foreach (var snapshot in snapshots) { snapshot.EnsureValid(); Assert.IsInstanceOfType<StudySnapshotInput>(snapshot.Input); }
        Assert.AreEqual(0, file.Imports.ReadImports(study.CompanyId, study.Id).Count);
    }
    [TestMethod]
    public async Task PreparingAndCancellationLeaveCompanyBytesRevisionAndAuditUnchanged()
    {
        using var file = Baseline(); var study = Study(file); var profile = Profile(study); var source = await Source(profile); var sha = Hash(file.Path);
        var prepared = Prepare(file, study, source, profile);
        Assert.IsTrue(prepared.CanConfirm); Assert.AreEqual(160m, prepared.Rows[0].Original!.Value.Amount);
        Assert.AreEqual(157.50m, prepared.Reconciliation.NetAmount); Assert.AreEqual(-2.5m, prepared.Reconciliation.NegativeAmount);
        Assert.AreEqual(1, prepared.Reconciliation.ZeroRows); Assert.AreEqual(3, prepared.Reconciliation.SelectedRows);
        Assert.AreEqual(sha, Hash(file.Path)); Assert.AreEqual(study, file.Studies.ReadStudies().Single(s => s.Id == study.Id));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.ThrowsExactly<OperationCanceledException>(() => file.Imports.ConfirmImport(Command(prepared), cancellation.Token));
        Assert.AreEqual(sha, Hash(file.Path)); Assert.AreEqual("0", Query(file.Path, "SELECT count(*) FROM ImportBatches"));
    }
    [TestMethod]
    public async Task AtomicConfirmationReopensWithCanonicalAmountsRawRowsLinksAndHistory()
    {
        EntityId studyId; ImportedBatch batch; StudySnapshotReceipt before;
        using (var file = Baseline())
        {
            var study = Study(file); studyId = study.Id; before = file.Studies.Capture(new(study.CompanyId, study.Id, study.Revision, Id(), "test"));
            var profile = Profile(study); var source = await Source(profile, "Amount;Ignored;Account\n\"160,00\";x;00123\n-2,50;y;A-42\n0;z;ZERO");
            batch = file.Imports.ConfirmImport(Command(Prepare(file, study, source, profile)));
            Assert.AreEqual(1L, batch.ConfirmedStudy.Revision); Assert.AreEqual(3, batch.Resources.Count);
            Assert.AreEqual("3", Query(file.Path, "SELECT count(*) FROM Resources WHERE typeof(OriginalAmount)='text' AND typeof(FunctionalAmount)='text'"));
            Assert.AreEqual("3", Query(file.Path, "SELECT count(*) FROM ResourceSourceLinks"));
            Assert.AreEqual("160", Query(file.Path, "SELECT OriginalAmount FROM Resources WHERE Account='00123'"));
            Assert.AreEqual("\"160,00\"", batch.Prepared.Source.Rows[0].GetCell(1).RawLexical);
        }
        using var reopened = Service().Open(FileOf("current.valdrio")); var current = reopened.Studies.ReadStudies().Single(s => s.Id == studyId);
        var saved = reopened.Imports.ReadImports(current.CompanyId, studyId).Single(); Assert.AreEqual(CanonicalSnapshotJson.Serialize(batch), CanonicalSnapshotJson.Serialize(saved));
        Assert.AreEqual(before.CanonicalJson, reopened.Studies.ReadSnapshots(current.CompanyId, studyId).Single().CanonicalJson);
    }
    [TestMethod] [DataRow(ImportAmountMode.ClosingBalance)] [DataRow(ImportAmountMode.CumulativeExpenseDifference)]
    public async Task ClosingAndCumulativeSourcesRemainBlockedInsteadOfBecomingPeriodCosts(ImportAmountMode mode)
    {
        using var file = Baseline(); var study = Study(file); var profile = Profile(study, mode: mode); var prepared = Prepare(file, study, await Source(profile), profile);
        Assert.IsFalse(prepared.CanConfirm); Assert.IsTrue(prepared.Diagnostics.Any(d => d.Diagnostic.Code == "IMP_MODE_UNAVAILABLE"));
        var sha = Hash(file.Path); Assert.ThrowsExactly<DomainRuleException>(() => file.Imports.ConfirmImport(Command(prepared))); Assert.AreEqual(sha, Hash(file.Path));
    }
    [TestMethod] [DataRow(ImportProfileApprovalState.SyntheticFixture)] [DataRow(ImportProfileApprovalState.PendingBusinessApproval)]
    public async Task PendingAndSyntheticBusinessProfilesCannotConfirm(ImportProfileApprovalState approval)
    {
        using var file = Baseline(); var study = Study(file); var profile = Profile(study, approval: approval);
        var prepared = Prepare(file, study, await Source(profile), profile); Assert.IsFalse(prepared.CanConfirm);
        Assert.IsTrue(prepared.Diagnostics.Any(d => d.Diagnostic.Code == "IMP_APPROVAL_REQUIRED"));
    }
    [TestMethod]
    public async Task ForeignCurrencyIsPreservedAndCannotConfirmWithoutFxBridge()
    {
        using var file = Baseline(); var study = Study(file); var profile = Profile(study, new("USD"));
        var prepared = Prepare(file, study, await Source(profile), profile); Assert.AreEqual(new CurrencyCode("USD"), prepared.Rows[0].Original!.Value.Currency);
        Assert.IsFalse(prepared.CanConfirm); Assert.IsTrue(prepared.Diagnostics.Any(d => d.Diagnostic.Code == "IMP_FX_REQUIRED"));
        var sha = Hash(file.Path); Assert.ThrowsExactly<DomainRuleException>(() => file.Imports.ConfirmImport(Command(prepared))); Assert.AreEqual(sha, Hash(file.Path));
    }
    [TestMethod] [DataRow("not-an-amount")] [DataRow("")] [DataRow("1,234567891")]
    public async Task InvalidMiddleRowNeverBecomesZeroOrConfirmsPartialBatch(string value)
    {
        using var file = Baseline(); var study = Study(file); var profile = Profile(study);
        var prepared = Prepare(file, study, await Source(profile, "a;b;c\n160;x;00123\n" + value + ";y;BROKEN\n0;z;ZERO"), profile);
        Assert.IsFalse(prepared.CanConfirm); Assert.IsNull(prepared.Rows[1].Original); Assert.AreEqual(1, prepared.Reconciliation.InvalidRows);
        Assert.AreEqual(0m, prepared.Rows[2].Original!.Value.Amount); Assert.IsTrue(prepared.Diagnostics.Any(d => d.Location.RecordNumber == 3 && d.Location.Column == 1));
        Assert.ThrowsExactly<DomainRuleException>(() => file.Imports.ConfirmImport(Command(prepared))); Assert.AreEqual("0", Query(file.Path, "SELECT count(*) FROM Resources"));
    }
    [TestMethod]
    public async Task DuplicateAccountsBlockWholeBatchAndSignTotalsDoNotHideErrors()
    {
        using var file = Baseline(); var study = Study(file); var profile = Profile(study);
        var prepared = Prepare(file, study, await Source(profile, "a;b;c\n160;x;00123\n-160;y;00123"), profile);
        Assert.IsFalse(prepared.CanConfirm); Assert.IsTrue(prepared.Diagnostics.Any(d => d.Diagnostic.Code == "IMP_DUPLICATE_ACCOUNT"));
        Assert.ThrowsExactly<DomainRuleException>(() => file.Imports.ConfirmImport(Command(prepared))); Assert.AreEqual("0", Query(file.Path, "SELECT count(*) FROM ImportBatches"));
    }
    [TestMethod]
    public async Task ExactRequestReplayReturnsSameBatchAndChangedActorOrHashIsRejected()
    {
        using var file = Baseline(); var study = Study(file); var profile = Profile(study); var prepared = Prepare(file, study, await Source(profile), profile);
        var command = Command(prepared); var first = file.Imports.ConfirmImport(command); var repeated = file.Imports.ConfirmImport(command);
        Assert.AreEqual(CanonicalSnapshotJson.Serialize(first), CanonicalSnapshotJson.Serialize(repeated));
        Assert.AreEqual("1", Query(file.Path, "SELECT count(*) FROM ImportBatches")); Assert.AreEqual(1L, file.Studies.ReadStudies().Single(s => s.Id == study.Id).Revision);
        Assert.AreEqual("IMP_REQUEST_REUSED", Assert.ThrowsExactly<DomainRuleException>(() => file.Imports.ConfirmImport(command with { Actor = "other" })).Diagnostic.Code);
        Assert.AreEqual("IMP_PREPARED_INVALID", Assert.ThrowsExactly<DomainRuleException>(() => file.Imports.ConfirmImport(command with { PreparedFingerprint = "wrong" })).Diagnostic.Code);
    }
    [TestMethod]
    public async Task NewRequestForRepeatedSourceIsBlockedEvenWithNewProfileAndPreparedIds()
    {
        using var file = Baseline(); var study = Study(file); var profile = Profile(study); var source = await Source(profile);
        var first = file.Imports.ConfirmImport(Command(Prepare(file, study, source, profile)));
        var newProfile = Profile(first.ConfirmedStudy); var next = Prepare(file, first.ConfirmedStudy, source, newProfile);
        Assert.AreEqual(profile.Columns.Account, newProfile.Columns.Account); Assert.AreEqual(first.Prepared.DuplicateFingerprint, next.DuplicateFingerprint);
        Assert.AreEqual("IMP_DUPLICATE_BATCH", Assert.ThrowsExactly<DomainRuleException>(() => file.Imports.ConfirmImport(Command(next))).Diagnostic.Code);
        Assert.AreEqual("3", Query(file.Path, "SELECT count(*) FROM Resources"));
    }
    [TestMethod]
    public async Task StaleRevisionWrongScopeAndClosedStudyCannotWrite()
    {
        using var file = Baseline(); var study = Study(file); var profile = Profile(study); var prepared = Prepare(file, study, await Source(profile), profile);
        var closed = file.Studies.ChangeState(new(study.CompanyId, study.Id, study.Revision, StudyState.Closed, "test"));
        Assert.AreEqual("STUDY_REVISION_CHANGED", Assert.ThrowsExactly<DomainRuleException>(() => file.Imports.ConfirmImport(Command(prepared))).Diagnostic.Code);
        Assert.ThrowsExactly<DomainRuleException>(() => file.Imports.ConfirmImport(Command(prepared) with { CompanyId = Id() }));
        var blocked = Prepare(file, closed, prepared.Source, profile); Assert.IsFalse(blocked.CanConfirm);
        Assert.AreEqual("0", Query(file.Path, "SELECT count(*) FROM ImportBatches"));
    }
    [TestMethod] [DataRow(CompanyWriteCheckpoint.AfterFirstWrite, false)] [DataRow(CompanyWriteCheckpoint.BeforeCommit, false)]
    [DataRow(CompanyWriteCheckpoint.AfterFirstWrite, true)] [DataRow(CompanyWriteCheckpoint.BeforeCommit, true)]
    public async Task RealWriteFailureOrCancellationRollsBackAllTablesAuditAndRevision(CompanyWriteCheckpoint stage, bool cancel)
    {
        PreparedImport prepared; StudyDefinition study; string sha; string audit;
        using (var file = Baseline())
        { study = Study(file); var profile = Profile(study); prepared = Prepare(file, study, await Source(profile), profile); sha = Hash(file.Path); audit = Query(file.Path, "SELECT count(*) FROM AuditEvents"); }
        using var token = new CancellationTokenSource();
        using (var failing = Service(s => { if (s == stage) { if (cancel) token.Cancel(); else throw new IOException("Injected write fault"); } }).Open(FileOf("current.valdrio")))
        {
            if (cancel) Assert.ThrowsExactly<OperationCanceledException>(() => failing.Imports.ConfirmImport(Command(prepared), token.Token));
            else Assert.ThrowsExactly<CompanyFileException>(() => failing.Imports.ConfirmImport(Command(prepared)));
        }
        using var other = Service().Open(FileOf("current.valdrio"), CompanyFileOpenMode.ReadOnly);
        Assert.AreEqual(study, other.Studies.ReadStudies().Single(s => s.Id == study.Id)); Assert.AreEqual(audit, Query(other.Path, "SELECT count(*) FROM AuditEvents"));
        foreach (var table in new[] { "ImportBatches", "ImportedRows", "Resources", "ResourceSourceLinks" }) Assert.AreEqual("0", Query(other.Path, "SELECT count(*) FROM " + table));
        Assert.AreEqual(sha, Hash(other.Path));
    }
    [TestMethod]
    public async Task ImportedSnapshotV2RetainsV1HistoryAndRunnerReconciliation()
    {
        using var file = Baseline(); var study = Study(file); var before = file.Studies.Capture(new(study.CompanyId, study.Id, 0, Id(), "test"));
        var profile = Profile(study); var batch = file.Imports.ConfirmImport(Command(Prepare(file, study, await Source(profile), profile)));
        var photo = file.Studies.Capture(new(study.CompanyId, study.Id, 1, Id(), "test")); photo.EnsureValid();
        Assert.AreEqual(ImportedStudySnapshotInput.Schema, photo.PayloadSchema); Assert.AreEqual(3, photo.Input.Resources.Count);
        Assert.AreEqual(batch.Id, ((ImportedStudySnapshotInput)photo.Input).Imports.Single().Id);
        var readiness = StudyPreparationService.Evaluate(photo.Input); Assert.IsFalse(readiness.ReadyForCalculation);
        Assert.IsFalse(readiness.Diagnostics.Any(d => d.Code == "STUDY_RESOURCES_PENDING")); Assert.IsTrue(readiness.Diagnostics.Any(d => d.Code == "STUDY_PARTITIONS_PENDING"));
        Assert.AreNotEqual(before.EconomicInputHash, photo.EconomicInputHash); Assert.AreEqual(before.CanonicalJson, file.Studies.ReadSnapshots(study.CompanyId, study.Id).Single(p => p.Id == before.Id).CanonicalJson);
        var receiptPath = FileOf("imported-snapshot.json"); File.WriteAllText(receiptPath, CanonicalSnapshotJson.Serialize(photo));
        var process = new System.Diagnostics.ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet") { RedirectStandardOutput = true, RedirectStandardError = true };
        process.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "runner", "Valdrio.Runner.dll"));
        process.ArgumentList.Add("--study-snapshot"); process.ArgumentList.Add(receiptPath);
        using var runner = System.Diagnostics.Process.Start(process)!; var text = await runner.StandardOutput.ReadToEndAsync(); var error = await runner.StandardError.ReadToEndAsync();
        await runner.WaitForExitAsync(); Assert.AreEqual(0, runner.ExitCode, error); using var output = JsonDocument.Parse(text);
        Assert.AreEqual(3, output.RootElement.GetProperty("resourceCount").GetInt32());
        Assert.AreEqual("157.5", output.RootElement.GetProperty("imports")[0].GetProperty("Reconciliation").GetProperty("NetAmount").GetString());
        Assert.AreEqual(photo.EconomicInputHash, output.RootElement.GetProperty("EconomicInputHash").GetString());
    }
    [TestMethod]
    public async Task StrictV2RoundTripRejectsDeletedDuplicateAndAlteredAmounts()
    {
        using var file = Baseline(); var study = Study(file); var profile = Profile(study); _ = file.Imports.ConfirmImport(Command(Prepare(file, study, await Source(profile), profile)));
        var photo = file.Studies.Capture(new(study.CompanyId, study.Id, 1, Id(), "test"));
        var root = JsonNode.Parse(photo.CanonicalJson)!.AsObject(); root["Value"]!.AsObject().Remove("Imports");
        Assert.ThrowsExactly<JsonException>(() => StudySnapshotJson.Read(root.ToJsonString()));
        root = JsonNode.Parse(photo.CanonicalJson)!.AsObject();
        var resource = root["Value"]!["Resources"]!.AsArray().First(r => r!["Original"]!["Amount"]!.GetValue<string>() != "0")!;
        resource["Functional"]!["Amount"] = "0";
        Assert.ThrowsExactly<JsonException>(() => StudySnapshotJson.Read(root.ToJsonString()));
        root = JsonNode.Parse(photo.CanonicalJson)!.AsObject(); root["Value"]!["Unknown"] = 42;
        Assert.ThrowsExactly<JsonException>(() => StudySnapshotJson.Read(root.ToJsonString()));
    }
    [TestMethod]
    public async Task ImportedContextChangesAreBlockedButRenameCloseReopenAndBackupRemainValid()
    {
        using var file = Baseline(); var study = Study(file); var profile = Profile(study);
        var batch = file.Imports.ConfirmImport(Command(Prepare(file, study, await Source(profile), profile))); var current = batch.ConfirmedStudy;
        UpdateStudyContext update = new(current.CompanyId, current.Id, current.Revision, current.Name, current.PeriodStart, current.PeriodEnd,
            current.Purpose, current.ScenarioKind, current.SelectedModelVersionId, current.Currency.FunctionalCurrencyCode, "test");
        Assert.AreEqual("IMP_CONTEXT_LOCKED", Assert.ThrowsExactly<DomainRuleException>(() => file.Studies.UpdateContext(update with { FunctionalCurrency = new("USD") })).Diagnostic.Code);
        Assert.ThrowsExactly<DomainRuleException>(() => file.Studies.UpdateContext(update with { PeriodStart = BusinessDate.ParseIso("2026-10-02") }));
        var renamed = file.Studies.UpdateContext(update with { Name = "Renamed" });
        var closed = file.Studies.ChangeState(new(current.CompanyId, current.Id, renamed.Revision, StudyState.Closed, "test"));
        var reopened = file.Studies.ChangeState(new(current.CompanyId, current.Id, closed.Revision, StudyState.Open, "test"));
        var photo = file.Studies.Capture(new(current.CompanyId, current.Id, reopened.Revision, Id(), "test"));
        file.Backup(FileOf("backup")); using var restored = Service().Restore(FileOf("backup"), FileOf("restored"));
        Assert.AreEqual(photo, restored.Studies.ReadSnapshots(current.CompanyId, current.Id).Single());
        Assert.AreEqual(CanonicalSnapshotJson.Serialize(batch), CanonicalSnapshotJson.Serialize(restored.Imports.ReadImports(current.CompanyId, current.Id).Single()));
    }
    [TestMethod]
    public async Task ImmutableTablesCompositeScopeAndTamperedImportAuditAreEnforced()
    {
        using (var file = Baseline())
        {
            var study = Study(file); var profile = Profile(study); _ = file.Imports.ConfirmImport(Command(Prepare(file, study, await Source(profile), profile)));
            foreach (var table in new[] { "ImportBatches", "ImportedRows", "Resources", "ResourceSourceLinks" })
                Assert.ThrowsExactly<SqliteException>(() => Query(file.Path, "DELETE FROM " + table));
            Assert.ThrowsExactly<SqliteException>(() => Query(file.Path, "UPDATE Resources SET OriginalAmount='999'"));
            Assert.ThrowsExactly<SqliteException>(() => Query(file.Path, "INSERT INTO ImportedRows SELECT CompanyId,'" + Id() + "',BatchId,RecordNumber+100,RawJson,Account,Amount,Currency,Selected FROM ImportedRows LIMIT 1"));
        }
        Query(FileOf("current.valdrio"), "UPDATE AuditEvents SET Operation='UpdateStudyContext' WHERE AggregateType='Study' AND Operation='ConfirmImport'");
        var sha = Hash(FileOf("current.valdrio")); Assert.AreEqual("FILE_INVALID_DATA", Assert.ThrowsExactly<CompanyFileException>(() => Service().Open(FileOf("current.valdrio"))).Diagnostic.Code);
        Assert.AreEqual(sha, Hash(FileOf("current.valdrio")));
    }
    [TestMethod]
    public async Task HeaderlessSourceCanConfirmAndExplicitZeroExclusionRemainsTraceable()
    {
        using var file = Baseline(); var study = Study(file); var profile = Profile(study, zero: ImportZeroPolicy.Exclude, header: 0, first: 1);
        var prepared = Prepare(file, study, await Source(profile, "160;x;00123\n0;y;ZERO"), profile); Assert.IsTrue(prepared.CanConfirm);
        Assert.AreEqual(1, prepared.Reconciliation.IgnoredRows); var batch = file.Imports.ConfirmImport(Command(prepared));
        Assert.AreEqual(1, batch.Resources.Count); Assert.AreEqual("2", Query(file.Path, "SELECT count(*) FROM ImportedRows"));
    }
    [TestMethod] [DataRow("es-ES")] [DataRow("en-US")] [DataRow("de-DE")] [DataRow("tr-TR")]
    public async Task CultureDoesNotChangePreparedOrImportedSnapshotHashes(string culture)
    {
        using var file = Baseline(); var study = Study(file); var profile = Profile(study); var prepared = Prepare(file, study, await Source(profile), profile);
        var hash = prepared.Fingerprint; var prior = CultureInfo.CurrentCulture;
        try { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture); Assert.AreEqual(hash, prepared.Fingerprint);
            var batch = file.Imports.ConfirmImport(Command(prepared)); var photo = file.Studies.Capture(new(study.CompanyId, study.Id, batch.ConfirmedStudy.Revision, Id(), "test"));
            var economic = photo.EconomicInputHash; CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            Assert.AreEqual(economic, StudySnapshotJson.EconomicHash(photo.Input)); photo.EnsureValid(); }
        finally { CultureInfo.CurrentCulture = prior; }
    }
    private sealed class Dialogs(string path) : IImportSourceDialogs { public string? SelectSource() => path; }
    private async Task<ImportColumnMappingViewModel> Vm(ICompanyFileSession file, StudyDefinition study, Func<ImportedBatch, Task>? refreshed = null,
        IImportWorkspace? port = null, string? csv = null)
    {
        var path = FileOf("ui.csv"); File.WriteAllText(path, csv ?? "Amount;Ignored;Account\n1.250,50;x;00123\n0;y;ZERO");
        var vm = new ImportColumnMappingViewModel(new ImportSourceSessionFactory(), new Dialogs(path), new UiTextCatalog(),
            new PresentationPreferencesService(new LanguageCatalog(), UserPresentationPreferences.Default));
        vm.BindTarget(port ?? file.Imports, study, true, refreshed); await vm.OpenCommand.ExecuteAsync(null); await vm.ReadCommand.ExecuteAsync(null);
        vm.AccountColumn = 3; vm.AmountColumn = 1; vm.GroupSeparator = "."; await vm.ValidateCommand.ExecuteAsync(null);
        Assert.AreEqual(study.Currency.FunctionalCurrencyCode.ToString(), vm.SourceCurrency, "The study currency is the sole default for the import.");
        vm.SourceCurrency = "EUR"; vm.ApprovalReference = "Synthetic checked 00123=1250.5 and ZERO=0"; vm.InterpretationApproved = true;
        await vm.PrepareCommand.ExecuteAsync(null); return vm;
    }
    [TestMethod]
    public async Task ExplicitAccountSelectionPersistsOnlyChosenResourcesAndKeepsExcludedEvidence()
    {
        EntityId companyId, studyId; string canonical;
        using (var file = Baseline())
        {
            var study = Study(file); companyId = study.CompanyId; studyId = study.Id;
            using var vm = await Vm(file, study, csv: "Amount;Ignored;Account\n160;x;00123\n-2,50;y;返金\n0;z;ZERO\n9000;b;BALANCE");
            var original = Hash(file.Path);
            vm.ImportAllAccounts = false;
            Assert.IsFalse(vm.PrepareCommand.CanExecute(null));
            foreach (var choice in vm.AccountChoices.Where(c => c.Account != "BALANCE")) choice.Included = true;
            Assert.IsFalse(vm.InterpretationApproved);
            vm.InterpretationApproved = true; await vm.PrepareCommand.ExecuteAsync(null);
            var prepared = vm.Preparation!;
            Assert.IsTrue(prepared.CanConfirm);
            Assert.AreEqual(3, prepared.Reconciliation.SelectedRows);
            Assert.AreEqual(1, prepared.Reconciliation.IgnoredRows);
            Assert.AreEqual(157.5m, prepared.Reconciliation.NetAmount);
            Assert.AreEqual(1, prepared.Reconciliation.ZeroRows);
            Assert.AreEqual(original, Hash(file.Path));
            await vm.ConfirmCommand.ExecuteAsync(null);
            var batch = vm.ConfirmedImport!;
            Assert.AreEqual(3, batch.Resources.Count);
            Assert.IsFalse(batch.Resources.Any(r => r.Account == "BALANCE"));
            Assert.AreEqual(9000m, batch.Prepared.Rows.Single(r => r.Account == "BALANCE").Original!.Value.Amount);
            Assert.IsFalse(batch.Prepared.Rows.Single(r => r.Account == "BALANCE").Selected);
            Assert.AreEqual("4", Query(file.Path, "SELECT count(*) FROM ImportedRows"));
            Assert.AreEqual("3", Query(file.Path, "SELECT count(*) FROM ResourceSourceLinks"));
            canonical = CanonicalSnapshotJson.Serialize(batch);
            var photo = file.Studies.Capture(new(companyId, studyId, 1, Id(), "test"));
            Assert.AreEqual(3, photo.Input.Resources.Count);
        }
        using var reopened = Service().Open(FileOf("current.valdrio"));
        var saved = reopened.Imports.ReadImports(companyId, studyId).Single();
        Assert.AreEqual(canonical, CanonicalSnapshotJson.Serialize(saved));
        Assert.AreEqual(3, saved.Prepared.Profile.Grouping!.Records.Count);
        Assert.AreEqual(3, reopened.Studies.ReadSnapshots(companyId, studyId).Single().Input.Resources.Count);
    }
    [TestMethod]
    public async Task ChangingAccountsRevokesApprovalAndRereadingNeverReusesOldChoices()
    {
        using var file = Baseline(); var study = Study(file); using var vm = await Vm(file, study);
        var original = Hash(file.Path);
        vm.ImportAllAccounts = false;
        var oldChoice = vm.AccountChoices.Single(c => c.Account == "00123"); oldChoice.Included = true;
        vm.InterpretationApproved = true; await vm.PrepareCommand.ExecuteAsync(null);
        var fingerprint = vm.Preparation!.Fingerprint;
        vm.AccountChoices.Single(c => c.Account == "ZERO").Included = true;
        Assert.IsNull(vm.Preparation); Assert.IsFalse(vm.InterpretationApproved); Assert.IsFalse(vm.ConfirmCommand.CanExecute(null));
        vm.InterpretationApproved = true; await vm.PrepareCommand.ExecuteAsync(null);
        Assert.AreNotEqual(fingerprint, vm.Preparation!.Fingerprint);
        vm.ImportAllAccounts = true;
        Assert.IsNull(vm.Preparation); Assert.IsFalse(vm.InterpretationApproved);
        vm.ImportAllAccounts = false;
        await vm.ReadCommand.ExecuteAsync(null);
        Assert.AreEqual(0, vm.AccountChoices.Count);
        vm.AccountColumn = 3; vm.AmountColumn = 1; await vm.ValidateCommand.ExecuteAsync(null);
        Assert.IsTrue(vm.IsReady); Assert.IsTrue(vm.IsExplicitAccountSelection);
        Assert.IsTrue(vm.AccountChoices.All(c => !c.Included)); Assert.IsFalse(vm.PrepareCommand.CanExecute(null));
        vm.AccountChoices[0].Included = true; vm.InterpretationApproved = true;
        oldChoice.Included = false;
        Assert.IsTrue(vm.InterpretationApproved, "Detached choices must not change the active selection.");
        Assert.AreEqual(original, Hash(file.Path));
    }
    [TestMethod]
    public async Task AccountSelectionIncludesRowsBeyondPreviewAndComparesCodesOrdinally()
    {
        using var file = Baseline(); var study = Study(file);
        var csv = "Amount;Ignored;Account\n" + string.Join("\n", Enumerable.Range(1, 55).Select(i => $"1;x;{i:D5}")) +
            "\n2;x;a\n3;x;A\n4;x;é";
        using var vm = await Vm(file, study, csv: csv);
        Assert.AreEqual(50, vm.Preview.Count); Assert.AreEqual(58, vm.AccountChoices.Count);
        vm.ImportAllAccounts = false;
        foreach (var choice in vm.AccountChoices.Where(c => c.Account is "00055" or "a" or "é")) choice.Included = true;
        vm.InterpretationApproved = true; await vm.PrepareCommand.ExecuteAsync(null);
        Assert.IsTrue(vm.Preparation!.CanConfirm);
        Assert.AreEqual(7m, vm.Preparation.Reconciliation.NetAmount);
        Assert.IsFalse(vm.Preparation.Rows.Single(r => r.Account == "A").Selected);
        CollectionAssert.AreEqual(new[] { "00055", "a", "é" }, vm.Preparation.Profile.Grouping!.Records.Select(r => r.Code).ToArray());
    }
    [TestMethod]
    public async Task InvalidSourceCannotBeMadeReadyByChoosingOnlyValidAccounts()
    {
        using var file = Baseline(); var study = Study(file);
        using var vm = await Vm(file, study);
        File.WriteAllText(FileOf("ui.csv"), "Amount;Ignored;Account\n160;x;00123\ninvalid;y;BAD");
        await vm.OpenCommand.ExecuteAsync(null); await vm.ReadCommand.ExecuteAsync(null);
        vm.AccountColumn = 3; vm.AmountColumn = 1; await vm.ValidateCommand.ExecuteAsync(null);
        vm.ImportAllAccounts = false;
        Assert.IsFalse(vm.IsReady); Assert.AreEqual(0, vm.AccountChoices.Count);
        Assert.IsFalse(vm.PrepareCommand.CanExecute(null)); Assert.IsFalse(vm.ConfirmCommand.CanExecute(null));
        Assert.AreEqual(0, file.Imports.ReadImports(study.CompanyId, study.Id).Count);
    }
    [TestMethod]
    public async Task ProductionViewModelConfirmedStateSurvivesFailedRefreshAndDisablesDoubleClick()
    {
        using var file = Baseline(); var study = Study(file); using var vm = await Vm(file, study, _ => Task.FromException(new IOException("refresh fault")));
        Assert.IsTrue(vm.ConfirmCommand.CanExecute(null)); var sha = Hash(file.Path); Assert.AreEqual(0, file.Imports.ReadImports(study.CompanyId, study.Id).Count);
        await vm.ConfirmCommand.ExecuteAsync(null); Assert.IsNotNull(vm.ConfirmedImport); Assert.IsFalse(vm.ConfirmCommand.CanExecute(null));
        Assert.AreEqual(1L, vm.ConfirmedImport.ConfirmedStudy.Revision); Assert.IsTrue(vm.Status.Contains("saved", StringComparison.OrdinalIgnoreCase));
        Assert.AreNotEqual(sha, Hash(file.Path)); Assert.AreEqual(1, file.Imports.ReadImports(study.CompanyId, study.Id).Count);
    }
    [TestMethod]
    public async Task SourceChangedAfterPreviewCannotBeConfirmedAndNewApprovalInvalidatesPrepared()
    {
        using var file = Baseline(); var study = Study(file); using var vm = await Vm(file, study);
        File.AppendAllText(FileOf("ui.csv"), "\n999;x;NEW"); var sha = Hash(file.Path); await vm.ConfirmCommand.ExecuteAsync(null);
        Assert.IsNull(vm.ConfirmedImport); Assert.IsFalse(vm.ConfirmCommand.CanExecute(null)); Assert.IsTrue(vm.Status.StartsWith("IMP_SOURCE_CHANGED", StringComparison.Ordinal));
        Assert.AreEqual(sha, Hash(file.Path)); vm.InterpretationApproved = false; Assert.IsNull(vm.Preparation);
    }
    private sealed class CancelAfterCommit(IImportWorkspace inner, Action cancel) : IImportWorkspace
    {
        public PreparedImport PrepareImport(EntityId companyId, EntityId studyId, long expectedRevision, EntityId preparedId,
            ImportSourceDocument source, SourceParseProfileVersion profile, CancellationToken token = default) =>
            inner.PrepareImport(companyId, studyId, expectedRevision, preparedId, source, profile, token);
        public ImportedBatch ConfirmImport(ConfirmImport command, CancellationToken token = default)
        { var committed = inner.ConfirmImport(command, token); cancel(); return committed; }
        public IReadOnlyList<ImportedBatch> ReadImports(EntityId companyId, EntityId studyId, CancellationToken token = default) =>
            inner.ReadImports(companyId, studyId, token);
    }
    [TestMethod]
    public async Task CancellationImmediatelyAfterActualCommitStillReturnsSavedReceipt()
    {
        using var file = Baseline(); var study = Study(file); ImportColumnMappingViewModel? active = null;
        var port = new CancelAfterCommit(file.Imports, () => active!.CancelCommand.Execute(null));
        using var vm = await Vm(file, study, port: port); active = vm;
        await vm.ConfirmCommand.ExecuteAsync(null); Assert.IsNotNull(vm.ConfirmedImport);
        Assert.AreEqual(1, file.Imports.ReadImports(study.CompanyId, study.Id).Count); Assert.IsFalse(vm.ConfirmCommand.CanExecute(null));
    }
    [TestMethod]
    public async Task XlsxSecondWorksheetNativeNumbersConfirmExactlyInFunctionalCurrency()
    {
        using var file = Baseline(); var study = Study(file); var path = FileOf("source.xlsx");
        using (var workbook = DocumentFormat.OpenXml.Packaging.SpreadsheetDocument.Create(path, DocumentFormat.OpenXml.SpreadsheetDocumentType.Workbook))
        {
            var book = workbook.AddWorkbookPart(); book.Workbook = new(new DocumentFormat.OpenXml.Spreadsheet.Sheets());
            foreach (var name in new[] { "Other", "Selected" })
            {
                static DocumentFormat.OpenXml.Spreadsheet.Cell Text(string reference, string value) => new() { CellReference = reference,
                    DataType = DocumentFormat.OpenXml.Spreadsheet.CellValues.InlineString,
                    InlineString = new(new DocumentFormat.OpenXml.Spreadsheet.Text(value)) };
                var part = book.AddNewPart<DocumentFormat.OpenXml.Packaging.WorksheetPart>();
                part.Worksheet = new(new DocumentFormat.OpenXml.Spreadsheet.SheetData(
                    new DocumentFormat.OpenXml.Spreadsheet.Row(Text("A1", "Amount"), Text("B1", "Unused"), Text("C1", "Account")) { RowIndex = 1U },
                    new DocumentFormat.OpenXml.Spreadsheet.Row(new DocumentFormat.OpenXml.Spreadsheet.Cell { CellReference = "A2",
                        DataType = DocumentFormat.OpenXml.Spreadsheet.CellValues.Number,
                        CellValue = new(name == "Selected" ? "1250.50" : "99999") }, Text("B2", "unused"), Text("C2", "00123")) { RowIndex = 2U }));
                book.Workbook.GetFirstChild<DocumentFormat.OpenXml.Spreadsheet.Sheets>()!.Append(
                    new DocumentFormat.OpenXml.Spreadsheet.Sheet { Name = name, SheetId = (uint)(name == "Other" ? 1 : 2), Id = book.GetIdOfPart(part) });
            }
        }
        var profile = new SourceParseProfileVersion(SourceParseProfileVersion.CurrentSchema, Id(), 1, "Checked synthetic Excel",
            ImportSourceKind.Xlsx, "Selected", 1, 2, new(3, amount: 1), null, new(",", ".", [3], ImportNegativeSyntax.LeadingMinus), "yyyy-MM-dd",
            ImportAmountMode.PeriodAmount, ImportSignConvention.SignedAmount, study.PeriodStart, study.PeriodEnd, new CurrencyCode("EUR"), null,
            ImportDuplicatePolicy.Reject, ImportZeroPolicy.Keep, ImportAccountSelection.All, new(ImportProfileApprovalState.ApprovedBusiness, "00123=1250.50"), true);
        using var capture = await new ImportSourceSessionFactory().OpenAsync(path); var source = await capture.ReadAsync(new(ImportSourceKind.Xlsx, "Selected", 1, 2, null));
        var batch = file.Imports.ConfirmImport(Command(Prepare(file, study, source, profile)));
        Assert.AreEqual(1250.50m, batch.Resources.Single().Functional.Amount); Assert.AreEqual("00123", batch.Resources.Single().Account);
        Assert.AreEqual("1250.50", batch.Prepared.Source.Rows.Single().GetCell(1).RawLexical);
        Assert.AreEqual("Selected", batch.Prepared.Source.SheetName); Assert.AreEqual("1250.5", Query(file.Path, "SELECT OriginalAmount FROM Resources"));
    }
    [TestMethod] [DataRow("en")] [DataRow("es")]
    public void ConfirmationResourcesAreAvailableInBothLanguages(string language)
    {
        var values = new UiTextCatalog().ReadAll(CultureInfo.GetCultureInfo(language));
        foreach (var key in new[] { "Import.ModeUnavailable", "Import.ApprovalRequired", "Import.PeriodMismatch", "Import.FxRequired",
            "Import.PreparedInvalid", "Import.DuplicateBatch", "Import.ContextLocked", "Import.SourceChanged", "Study.PartitionsPending",
            "ImportMapping.Scope", "ImportMapping.Target", "ImportMapping.ConfirmImport", "ImportMapping.Totals", "ImportMapping.ConfirmedSummary" })
            Assert.IsTrue(values.ContainsKey(key), key);
    }
}
