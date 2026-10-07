using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.Data.Sqlite;
using Valdrio.Application;
using Valdrio.Desktop;
using Valdrio.Domain;
using Valdrio.Infrastructure;
using Valdrio.Infrastructure.CompanyFiles;
using Valdrio.Localization;

namespace Valdrio.Tests;

[TestClass]
public sealed class T06DebitCreditImportTests
{
    private string directory = null!;
    private const string Csv = "Credit;Ignored;Account;Debit\n20;x;00123;180\n7,50;x;CREDIT;0\n0;x;ZERO;0";
    private static EntityId Id() => new(Guid.NewGuid());
    private string PathOf(string name) => Path.Combine(directory, name);
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    private sealed class Clock : IClock { public UtcInstant GetUtcNow() => UtcInstant.ParseIso("2026-10-06T12:00:00Z"); }
    private static SqliteCompanyFileService Service(Action<CompanyWriteCheckpoint>? hook = null) =>
        new(new JsonCurrencyCatalog(Path.Combine(AppContext.BaseDirectory, "reference/currency-catalog-example.json")), new Clock(), hook);
    [TestInitialize] public void Initialize() { directory = Path.Combine(Path.GetTempPath(), "valdrio-debit-" + Guid.NewGuid()); Directory.CreateDirectory(directory); }
    [TestCleanup] public void Cleanup() => Directory.Delete(directory, true);
    private string Fixture(string name = "t05-v3.json")
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name)));
        var path = PathOf(name + ".valdrio");
        using (var input = new GZipStream(new MemoryStream(Convert.FromBase64String(json.RootElement.GetProperty("GzipBase64").GetString()!)), CompressionMode.Decompress))
        using (var output = File.Create(path)) input.CopyTo(output);
        Assert.AreEqual(json.RootElement.GetProperty("Sha256").GetString(), Hash(path));
        return path;
    }
    private ICompanyFileSession Baseline() => Service().Upgrade(Fixture(), PathOf("current.valdrio"), "test");
    private static StudyDefinition Study(ICompanyFileSession file) => file.Studies.Create(new(file.Read().Id, Id(), "Debit credit period",
        BusinessDate.ParseIso("2026-10-01"), BusinessDate.ParseIso("2026-10-31"), StudyPurpose.Management, StudyScenario.Actual,
        file.Models.ReadModels().First(m => m.Status == ModelStatus.Published).Structure.Id, new("EUR"), "test"));
    private static SourceParseProfileVersion Profile(StudyDefinition study, ImportNegativeSyntax signs = ImportNegativeSyntax.LeadingMinus,
        bool confirmed = true, ImportProfileApprovalState approval = ImportProfileApprovalState.ApprovedBusiness,
        CurrencyCode? currency = null, ImportSourceKind kind = ImportSourceKind.Csv) => new(SourceParseProfileVersion.CurrentSchema,
        Id(), 1, "Synthetic checked debit/credit example", kind, kind == ImportSourceKind.Xlsx ? "Selected" : null, 1, 2,
        new(3, debit: 4, credit: 1), kind == ImportSourceKind.Csv ? new("utf-8", ';', '"', CsvBomPolicy.Optional) : null,
        new(",", null, [], signs), "yyyy-MM-dd", ImportAmountMode.PeriodAmount, ImportSignConvention.DebitMinusCredit,
        study.PeriodStart, study.PeriodEnd, currency ?? new("EUR"), null, ImportDuplicatePolicy.Reject, ImportZeroPolicy.Keep,
        ImportAccountSelection.All, new(approval, "Synthetic example 00123: debit180-credit20=160; CREDIT:0-7.5=-7.5"), confirmed);
    private string WriteCsv(string text = Csv) { var path = PathOf("source.csv"); File.WriteAllText(path, text); return path; }
    private static async Task<ImportSourceDocument> Read(string path, SourceParseProfileVersion profile)
    {
        using var file = await new ImportSourceSessionFactory().OpenAsync(path);
        return await file.ReadAsync(new(profile.SourceKind, profile.WorksheetName, profile.HeaderRow, profile.FirstDataRow, profile.Csv));
    }
    private static ImportColumnSelectionResult Validate(ImportSourceDocument source, SourceParseProfileVersion profile) =>
        ImportColumnSelection.Validate(source, new(profile.SourceKind, profile.WorksheetName, profile.HeaderRow, profile.FirstDataRow, profile.Csv),
            profile.Columns, profile.NumberFormat, profile.XlsxNumericAccountPolicy);
    private static PreparedImport Prepare(ICompanyFileSession file, StudyDefinition study, ImportSourceDocument source, SourceParseProfileVersion profile) =>
        file.Imports.PrepareImport(study.CompanyId, study.Id, study.Revision, Id(), source, profile);
    private static ConfirmImport Command(PreparedImport prepared) => new(prepared.StudyContext.CompanyId, prepared.StudyContext.Id,
        prepared.StudyContext.Revision, Id(), prepared.Fingerprint, prepared, ImportConfirmMode.AppendBatch, "test");
    private static string Query(string path, string sql)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        connection.Open(); using var command = connection.CreateCommand(); command.CommandText = sql;
        return Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture) ?? "";
    }

    [TestMethod]
    public async Task BothOriginalsProduceExactPositiveNegativeAndZeroPeriodAmounts()
    {
        using var file = Baseline(); var study = Study(file); var profile = Profile(study);
        var source = await Read(WriteCsv(), profile); var selected = Validate(source, profile); var sha = Hash(file.Path);
        Assert.IsTrue(selected.IsValid); Assert.AreEqual("00123", selected.Rows[0].Account);
        Assert.AreEqual(180m, selected.Rows[0].Debit); Assert.AreEqual(20m, selected.Rows[0].Credit);
        Assert.AreEqual(160m, selected.Rows[0].Amount); Assert.AreEqual(-7.5m, selected.Rows[1].Amount); Assert.AreEqual(0m, selected.Rows[2].Amount);
        var prepared = Prepare(file, study, source, profile);
        Assert.IsTrue(prepared.CanConfirm); Assert.AreEqual(160m, prepared.Reconciliation.PositiveAmount);
        Assert.AreEqual(-7.5m, prepared.Reconciliation.NegativeAmount); Assert.AreEqual(152.5m, prepared.Reconciliation.NetAmount);
        Assert.AreEqual(1, prepared.Reconciliation.ZeroRows); Assert.AreEqual(3, prepared.Reconciliation.SelectedRows);
        Assert.AreEqual(sha, Hash(file.Path)); Assert.AreEqual(0L, file.Studies.ReadStudies().Single(s => s.Id == study.Id).Revision);
    }

    [TestMethod] [DataRow("", "0", 4)] [DataRow("0", "", 1)]
    [DataRow("no_disponible", "0", 4)] [DataRow("0", "no_disponible", 1)]
    public async Task EmptyAndInvalidOperandInMiddleRowNeverBecomesZero(string debit, string credit, int column)
    {
        using var file = Baseline(); var study = Study(file); var profile = Profile(study);
        var source = await Read(WriteCsv("Credit;x;Account;Debit\n0;x;GOOD;10\n" + credit + ";x;BROKEN;" + debit + "\n0;x;ZERO;0"), profile);
        var selected = Validate(source, profile); Assert.IsFalse(selected.IsValid); Assert.IsNull(selected.Rows[1].Amount);
        Assert.IsTrue(selected.Diagnostics.Any(d => d.Diagnostic.Code == "IMP_INVALID_AMOUNT" && d.Location.RecordNumber == 3 && d.Location.Column == column));
        var prepared = Prepare(file, study, source, profile); Assert.IsNull(prepared.Rows[1].Original); Assert.IsFalse(prepared.CanConfirm);
        var sha = Hash(file.Path); Assert.ThrowsExactly<DomainRuleException>(() => file.Imports.ConfirmImport(Command(prepared)));
        Assert.AreEqual(sha, Hash(file.Path)); Assert.AreEqual("0", Query(file.Path, "SELECT count(*) FROM ImportBatches"));
    }

    [TestMethod]
    public async Task MissingDebitCellRemainsMissingEvenWhenCreditIsExplicitZero()
    {
        using var file = Baseline(); var study = Study(file); var profile = Profile(study);
        var source = await Read(WriteCsv("Credit;x;Account;Debit\n0;x;MISSING"), profile);
        Assert.AreEqual(SourceValueKind.Missing, source.Rows[0].GetCell(4).Kind);
        var prepared = Prepare(file, study, source, profile); Assert.IsNull(prepared.Rows[0].Original);
        Assert.IsTrue(prepared.Diagnostics.Any(d => d.Diagnostic.Code == "IMP_INVALID_AMOUNT" && d.Location.Column == 4));
        Assert.IsFalse(prepared.CanConfirm);
    }

    [TestMethod]
    public async Task DifferenceOverflowIsDiagnosedWithoutProducingAMoneyValue()
    {
        using var file = Baseline(); var study = Study(file); var profile = Profile(study);
        var limit = decimal.MaxValue.ToString(CultureInfo.InvariantCulture);
        var source = await Read(WriteCsv("Credit;x;Account;Debit\n-" + limit + ";x;OVERFLOW;" + limit), profile);
        var selected = Validate(source, profile); Assert.IsNull(selected.Rows[0].Amount); Assert.IsFalse(selected.IsValid);
        Assert.IsTrue(selected.Diagnostics.Any(d => d.Diagnostic.Code == "IMP_AMOUNT_OVERFLOW" && d.Location.RecordNumber == 2));
        var prepared = PreparedImport.Create(Id(), study, PrecisionPolicy.Pdec1Proposed with { MaximumInitialMagnitude = decimal.MaxValue }, source, profile);
        Assert.IsNull(prepared.Rows[0].Original); Assert.IsFalse(prepared.CanConfirm);
    }

    [TestMethod] [DataRow("0,123456789", "NUM_SCALE_EXCEEDED")] [DataRow("1000000000001", "NUM_MAGNITUDE_EXCEEDED")]
    public async Task EqualOperandsCannotHideExcessivePrecisionOrMagnitude(string operand, string code)
    {
        using var file = Baseline(); var study = Study(file); var profile = Profile(study);
        var source = await Read(WriteCsv("Credit;x;Account;Debit\n" + operand + ";x;A;" + operand), profile);
        Assert.AreEqual(0m, Validate(source, profile).Rows[0].Amount);
        var prepared = Prepare(file, study, source, profile); Assert.IsFalse(prepared.CanConfirm); Assert.IsNull(prepared.Rows[0].Original);
        Assert.IsTrue(prepared.Diagnostics.Any(d => d.Diagnostic.Code == code && d.Location.Column == 1));
        Assert.IsTrue(prepared.Diagnostics.Any(d => d.Diagnostic.Code == code && d.Location.Column == 4));
        var sha = Hash(file.Path); Assert.ThrowsExactly<DomainRuleException>(() => file.Imports.ConfirmImport(Command(prepared))); Assert.AreEqual(sha, Hash(file.Path));
    }

    [TestMethod]
    public async Task DifferenceAlsoRespectsMagnitudePolicyWhenBothOperandsFit()
    {
        using var file = Baseline(); var study = Study(file); var profile = Profile(study);
        var source = await Read(WriteCsv("Credit;x;Account;Debit\n-1000000000000;x;A;1000000000000"), profile);
        var prepared = Prepare(file, study, source, profile); Assert.IsNull(prepared.Rows[0].Original); Assert.IsFalse(prepared.CanConfirm);
        Assert.IsTrue(prepared.Diagnostics.Any(d => d.Diagnostic.Code == "NUM_MAGNITUDE_EXCEEDED" && d.Location.Column is null && d.Location.RecordNumber == 2));
    }

    [TestMethod] [DataRow("-180", "-20", ImportNegativeSyntax.LeadingMinus)]
    [DataRow("(180)", "(20)", ImportNegativeSyntax.Parentheses)] [DataRow("180-", "20-", ImportNegativeSyntax.TrailingMinus)]
    public async Task ApprovedSignSyntaxAppliesToEachOperandWithoutTakingAbsoluteValues(string debit, string credit, ImportNegativeSyntax syntax)
    {
        using var file = Baseline(); var study = Study(file); var profile = Profile(study, syntax);
        var prepared = Prepare(file, study, await Read(WriteCsv("Credit;x;Account;Debit\n" + credit + ";x;A;" + debit), profile), profile);
        Assert.IsTrue(prepared.CanConfirm); Assert.AreEqual(-160m, prepared.Rows[0].Original!.Value.Amount);
    }

    [TestMethod] [DataRow(ImportProfileApprovalState.SyntheticFixture)] [DataRow(ImportProfileApprovalState.PendingBusinessApproval)]
    public async Task BusinessApprovalIsRequiredForDebitCreditToo(ImportProfileApprovalState approval)
    {
        using var file = Baseline(); var study = Study(file); var profile = Profile(study, approval: approval);
        var prepared = Prepare(file, study, await Read(WriteCsv(), profile), profile); Assert.IsFalse(prepared.CanConfirm);
        Assert.IsTrue(prepared.Diagnostics.Any(d => d.Diagnostic.Code == "IMP_APPROVAL_REQUIRED"));
    }

    [TestMethod]
    public async Task UnconfirmedDebitMinusCreditConventionCannotSave()
    {
        using var file = Baseline(); var study = Study(file); var profile = Profile(study, confirmed: false);
        var prepared = Prepare(file, study, await Read(WriteCsv(), profile), profile); Assert.IsFalse(prepared.CanConfirm);
        var sha = Hash(file.Path); Assert.ThrowsExactly<DomainRuleException>(() => file.Imports.ConfirmImport(Command(prepared))); Assert.AreEqual(sha, Hash(file.Path));
    }

    [TestMethod]
    public async Task ForeignCurrencyIsRetainedAndStillNeedsFx()
    {
        using var file = Baseline(); var study = Study(file); var profile = Profile(study, currency: new("USD"));
        var prepared = Prepare(file, study, await Read(WriteCsv(), profile), profile);
        Assert.AreEqual(new CurrencyCode("USD"), prepared.Rows[0].Original!.Value.Currency); Assert.AreEqual(160m, prepared.Rows[0].Original!.Value.Amount);
        Assert.IsFalse(prepared.CanConfirm); Assert.IsTrue(prepared.Diagnostics.Any(d => d.Diagnostic.Code == "IMP_FX_REQUIRED"));
    }

    [TestMethod]
    public async Task OpposingDuplicateAccountsCannotConfirm()
    {
        using var file = Baseline(); var study = Study(file); var profile = Profile(study);
        var prepared = Prepare(file, study, await Read(WriteCsv("Credit;x;Account;Debit\n0;x;A;10\n10;x;A;0"), profile), profile);
        Assert.AreEqual(10m, prepared.Rows[0].Original!.Value.Amount); Assert.AreEqual(-10m, prepared.Rows[1].Original!.Value.Amount);
        Assert.IsFalse(prepared.CanConfirm); Assert.IsTrue(prepared.Diagnostics.Any(d => d.Diagnostic.Code == "IMP_DUPLICATE_ACCOUNT"));
        Assert.ThrowsExactly<DomainRuleException>(() => file.Imports.ConfirmImport(Command(prepared)));
    }

    [TestMethod]
    public async Task AtomicSaveReopenSnapshotsBackupAndRunnerRetainBothOriginals()
    {
        EntityId studyId; ImportedBatch batch; StudySnapshotReceipt before, photo;
        using (var file = Baseline())
        {
            var study = Study(file); studyId = study.Id; before = file.Studies.Capture(new(study.CompanyId, study.Id, 0, Id(), "test"));
            var profile = Profile(study); batch = file.Imports.ConfirmImport(Command(Prepare(file, study, await Read(WriteCsv(), profile), profile)));
            Assert.AreEqual(3, batch.Resources.Count); Assert.AreEqual(1L, batch.ConfirmedStudy.Revision);
            Assert.AreEqual("3", Query(file.Path, "SELECT count(*) FROM Resources WHERE typeof(OriginalAmount)='text' AND typeof(FunctionalAmount)='text'"));
            Assert.AreEqual("160", Query(file.Path, "SELECT Amount FROM ImportedRows WHERE Account='00123'"));
            photo = file.Studies.Capture(new(study.CompanyId, study.Id, 1, Id(), "test"));
            Assert.AreEqual(ImportedStudySnapshotInput.Schema, photo.PayloadSchema); Assert.AreEqual(3, photo.Input.Resources.Count);
            file.Backup(PathOf("backup"));
        }
        using var reopened = Service().Open(PathOf("current.valdrio")); var companyId = reopened.Read().Id;
        var saved = reopened.Imports.ReadImports(companyId, studyId).Single();
        Assert.AreEqual(CanonicalSnapshotJson.Serialize(batch), CanonicalSnapshotJson.Serialize(saved));
        Assert.AreEqual("180", saved.Prepared.Source.Rows[0].GetCell(4).RawLexical); Assert.AreEqual("20", saved.Prepared.Source.Rows[0].GetCell(1).RawLexical);
        Assert.AreEqual(before.CanonicalJson, reopened.Studies.ReadSnapshots(companyId, studyId).Single(s => s.Id == before.Id).CanonicalJson);
        Assert.AreEqual(photo.CanonicalJson, reopened.Studies.ReadSnapshots(companyId, studyId).Single(s => s.Id == photo.Id).CanonicalJson);
        using var restored = Service().Restore(PathOf("backup"), PathOf("restored"));
        Assert.AreEqual(CanonicalSnapshotJson.Serialize(batch), CanonicalSnapshotJson.Serialize(restored.Imports.ReadImports(companyId, studyId).Single()));
        await VerifyRunner(photo, "152.5");
    }

    [TestMethod]
    public async Task IdempotencyAndSourceRepetitionStillApplyToDebitCredit()
    {
        using var file = Baseline(); var study = Study(file); var profile = Profile(study); var source = await Read(WriteCsv(), profile);
        var command = Command(Prepare(file, study, source, profile)); var saved = file.Imports.ConfirmImport(command);
        Assert.AreEqual(CanonicalSnapshotJson.Serialize(saved), CanonicalSnapshotJson.Serialize(file.Imports.ConfirmImport(command)));
        var duplicate = Prepare(file, saved.ConfirmedStudy, source, Profile(saved.ConfirmedStudy));
        Assert.AreEqual("IMP_DUPLICATE_BATCH", Assert.ThrowsExactly<DomainRuleException>(() => file.Imports.ConfirmImport(Command(duplicate))).Diagnostic.Code);
        Assert.AreEqual(1, file.Imports.ReadImports(study.CompanyId, study.Id).Count); Assert.AreEqual(1L, file.Studies.ReadStudies().Single(s => s.Id == study.Id).Revision);
    }

    [TestMethod] [DataRow(CompanyWriteCheckpoint.AfterFirstWrite, false)] [DataRow(CompanyWriteCheckpoint.BeforeCommit, false)]
    [DataRow(CompanyWriteCheckpoint.AfterFirstWrite, true)] [DataRow(CompanyWriteCheckpoint.BeforeCommit, true)]
    public async Task ActualWriteFailureAndCancellationRollBackDebitCredit(CompanyWriteCheckpoint stage, bool cancel)
    {
        PreparedImport prepared; string sha;
        using (var file = Baseline())
        { var study = Study(file); var profile = Profile(study); prepared = Prepare(file, study, await Read(WriteCsv(), profile), profile); sha = Hash(file.Path); }
        using var token = new CancellationTokenSource();
        using (var failing = Service(s => { if (s == stage) { if (cancel) token.Cancel(); else throw new IOException("Injected debit/credit write fault"); } }).Open(PathOf("current.valdrio")))
        {
            if (cancel) Assert.ThrowsExactly<OperationCanceledException>(() => failing.Imports.ConfirmImport(Command(prepared), token.Token));
            else Assert.ThrowsExactly<CompanyFileException>(() => failing.Imports.ConfirmImport(Command(prepared)));
        }
        Assert.AreEqual(sha, Hash(PathOf("current.valdrio")));
        using var other = Service().Open(PathOf("current.valdrio"), CompanyFileOpenMode.ReadOnly);
        Assert.AreEqual(0, other.Imports.ReadImports(prepared.StudyContext.CompanyId, prepared.StudyContext.Id).Count);
        Assert.AreEqual(0L, other.Studies.ReadStudies().Single(s => s.Id == prepared.StudyContext.Id).Revision);
        foreach (var table in new[] { "ImportBatches", "ImportedRows", "Resources", "ResourceSourceLinks" })
            Assert.AreEqual("0", Query(other.Path, "SELECT count(*) FROM " + table));
    }

    private sealed class Dialogs : IImportSourceDialogs
    { public string? Path { get; set; } public string? SelectSource() => Path; }
    private async Task<ImportColumnMappingViewModel> ReadyVm(ICompanyFileSession file, StudyDefinition study,
        PresentationPreferencesService? preferences = null, Func<ImportedBatch, Task>? refreshed = null)
    {
        var vm = new ImportColumnMappingViewModel(new ImportSourceSessionFactory(), new Dialogs { Path = WriteCsv() }, new UiTextCatalog(),
            preferences ?? new PresentationPreferencesService(new LanguageCatalog(), UserPresentationPreferences.Default));
        vm.BindTarget(file.Imports, study, true, refreshed);
        await vm.OpenCommand.ExecuteAsync(null); await vm.ReadCommand.ExecuteAsync(null);
        vm.AmountLayout = "debit-credit"; vm.AccountColumn = 3; vm.DebitColumn = 4; vm.CreditColumn = 1;
        await vm.ValidateCommand.ExecuteAsync(null); vm.SourceCurrency = "EUR";
        vm.ApprovalReference = "Checked synthetic example:180-20=160,0-7.5=-7.5"; vm.InterpretationApproved = true;
        await vm.PrepareCommand.ExecuteAsync(null); return vm;
    }

    [TestMethod]
    public async Task ChangingLayoutOrOperandColumnInvalidatesMappingPreparationAndApproval()
    {
        using var file = Baseline(); var study = Study(file); using var vm = await ReadyVm(file, study);
        Assert.IsTrue(vm.Preparation!.CanConfirm); Assert.AreEqual(ImportSignConvention.DebitMinusCredit, vm.Preparation.Profile.SignConvention);
        vm.CreditColumn = 4; Assert.IsFalse(vm.InterpretationApproved); Assert.IsNull(vm.Preparation); Assert.IsFalse(vm.IsReady);
        await vm.ValidateCommand.ExecuteAsync(null); Assert.IsTrue(vm.Status.StartsWith("IMP_COLUMN_SELECTION", StringComparison.Ordinal));
        vm.AmountLayout = "amount"; Assert.IsNull(vm.AmountColumn); Assert.IsNull(vm.DebitColumn); Assert.IsNull(vm.CreditColumn);
        Assert.IsFalse(vm.ValidateCommand.CanExecute(null)); Assert.IsFalse(vm.ConfirmCommand.CanExecute(null));
        vm.AmountColumn = 4; await vm.ValidateCommand.ExecuteAsync(null);
        Assert.IsTrue(vm.IsReady); Assert.AreEqual(180m, vm.ValidatedSelection!.Rows[0].Amount); Assert.IsFalse(vm.InterpretationApproved);
    }

    [TestMethod] [DataRow("en", "es-ES")] [DataRow("es", "en-US")] [DataRow("en", "de-DE")] [DataRow("en", "en-GB")]
    public async Task RegionLanguageAndPseudoPreserveBothColumnsAndPreparedFingerprint(string language, string format)
    {
        using var file = Baseline(); var study = Study(file);
        var preferences = new PresentationPreferencesService(new LanguageCatalog(), UserPresentationPreferences.Default);
        using var vm = await ReadyVm(file, study, preferences); var fingerprint = vm.Preparation!.Fingerprint;
        preferences.Update(preferences.Current with { UiLanguageTag = language, FormatCultureTag = format }); vm.RefreshPresentation(true);
        Assert.AreEqual(fingerprint, vm.Preparation!.Fingerprint); Assert.IsTrue(vm.IsDebitCredit); Assert.IsTrue(vm.InterpretationApproved);
        Assert.AreEqual(3, vm.AccountColumn); Assert.AreEqual(4, vm.DebitColumn); Assert.AreEqual(1, vm.CreditColumn);
        Assert.AreEqual("180", vm.Preview[0].OriginalDebit); Assert.AreEqual("20", vm.Preview[0].OriginalCredit);
        Assert.AreEqual(152.5m, vm.Preparation.Reconciliation.NetAmount); Assert.IsTrue(vm.ConfirmCommand.CanExecute(null));
    }

    [TestMethod]
    public async Task PostCommitRefreshFailureRetainsSavedDebitCreditAndDisablesReplayClick()
    {
        using var file = Baseline(); var study = Study(file);
        using var vm = await ReadyVm(file, study, refreshed: _ => throw new IOException("Refresh failed after actual commit"));
        await vm.ConfirmCommand.ExecuteAsync(null); Assert.IsNotNull(vm.ConfirmedImport);
        Assert.IsTrue(vm.Status.Contains("saved", StringComparison.OrdinalIgnoreCase)); Assert.IsFalse(vm.ConfirmCommand.CanExecute(null));
        Assert.AreEqual(3, vm.ConfirmedImport.Resources.Count); Assert.AreEqual(1L, vm.ConfirmedImport.ConfirmedStudy.Revision);
        Assert.AreEqual(1, file.Imports.ReadImports(study.CompanyId, study.Id).Count);
    }

    [TestMethod]
    public async Task GenuinePreviouslyAcceptedV4PeriodLedgerAndSnapshotsRemainIdentical()
    {
        var path = Fixture("t06-period-v4.json"); var sha = Hash(path);
        var oldBatch = Query(path, "SELECT CanonicalJson FROM ImportBatches"); var oldPhotos = Query(path, "SELECT group_concat(CanonicalJson) FROM StudySnapshots ORDER BY Id");
        using (var file = Service().Upgrade(path, Path.Combine(directory, "upgraded-period.valdrio"), "test"))
        {
            Assert.AreEqual(6, file.Read().SchemaVersion); var batches = file.Studies.ReadStudies().SelectMany(s => file.Imports.ReadImports(s.CompanyId, s.Id)).ToArray();
            Assert.AreEqual(1, batches.Length); Assert.AreEqual(ImportSignConvention.SignedAmount, batches[0].Prepared.Profile.SignConvention);
            Assert.AreEqual(oldBatch, CanonicalSnapshotJson.Serialize(batches[0])); Assert.AreEqual(2, batches[0].Resources.Count);
            var snapshots = file.Studies.ReadStudies().SelectMany(s => file.Studies.ReadSnapshots(s.CompanyId, s.Id)).ToArray();
            Assert.IsTrue(snapshots.Any(s => s.PayloadSchema == StudySnapshotInput.Schema));
            var imported = snapshots.Single(s => s.PayloadSchema == ImportedStudySnapshotInput.Schema); imported.EnsureValid();
            await VerifyRunner(imported, "1250.5");
        }
        Assert.AreEqual(sha, Hash(path)); Assert.AreEqual(oldPhotos, Query(path, "SELECT group_concat(CanonicalJson) FROM StudySnapshots ORDER BY Id"));
    }

    [TestMethod]
    public async Task SelectedXlsxWorksheetStoredNumbersAndCachedFormulaPreserveBothOriginals()
    {
        using var file = Baseline(); var study = Study(file); var profile = Profile(study, kind: ImportSourceKind.Xlsx);
        var source = await Read(Workbook("cached"), profile); var prepared = Prepare(file, study, source, profile);
        Assert.IsTrue(prepared.CanConfirm); Assert.AreEqual(160m, prepared.Rows[0].Original!.Value.Amount);
        Assert.AreEqual("180.00", source.Rows[0].GetCell(4).RawLexical); Assert.AreEqual("10+10", source.Rows[0].GetCell(1).Formula);
        Assert.AreEqual("Selected", source.SheetName);
        var saved = file.Imports.ConfirmImport(Command(prepared)); Assert.AreEqual(160m, saved.Resources.Single().Functional.Amount);
    }

    [TestMethod] [DataRow("missing")] [DataRow("empty")] [DataRow("no-cache")] [DataRow("error")]
    [DataRow("date")] [DataRow("boolean")] [DataRow("bad-text")]
    public async Task InvalidXlsxCreditTypeNeverBecomesAZeroOperand(string type)
    {
        using var file = Baseline(); var study = Study(file); var profile = Profile(study, kind: ImportSourceKind.Xlsx);
        var source = await Read(Workbook(type), profile); var prepared = Prepare(file, study, source, profile);
        Assert.IsFalse(prepared.CanConfirm); Assert.IsNull(prepared.Rows[0].Original);
        Assert.IsTrue(prepared.Diagnostics.Any(d => d.Location.Column == 1 && d.Severity == ImportDiagnosticSeverity.Blocking));
        var sha = Hash(file.Path); Assert.ThrowsExactly<DomainRuleException>(() => file.Imports.ConfirmImport(Command(prepared))); Assert.AreEqual(sha, Hash(file.Path));
    }

    [TestMethod] [DataRow("en")] [DataRow("es")]
    public void DebitCreditLabelsAndOverflowDiagnosticsAreTranslated(string language)
    {
        var resource = new UiTextCatalog().ReadAll(CultureInfo.GetCultureInfo(language));
        foreach (var key in new[] { "AmountLayout", "SignedLayout", "DebitCreditLayout", "DebitColumn", "CreditColumn", "OriginalDebit", "OriginalCredit", "DebitCreditHelp", "ApproveDebitCredit" })
            Assert.IsTrue(resource.ContainsKey("ImportMapping." + key));
        Assert.IsTrue(resource.ContainsKey("Import.AmountOverflow"));
    }

    private string Workbook(string creditType)
    {
        var path = PathOf("source.xlsx"); using var document = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook);
        var book = document.AddWorkbookPart(); book.Workbook = new Workbook(new Sheets());
        static Cell TextCell(string reference, string text) => new() { CellReference = reference, DataType = CellValues.InlineString, InlineString = new InlineString(new Text(text)) };
        foreach (var name in new[] { "Other", "Selected" })
        {
            var part = book.AddNewPart<WorksheetPart>(); var cells = new List<Cell>();
            if (creditType != "missing") cells.Add(creditType switch
            {
                "cached" => new() { CellReference = "A2", DataType = CellValues.Number, CellFormula = new CellFormula("10+10"), CellValue = new CellValue("20") },
                "empty" => new() { CellReference = "A2" },
                "no-cache" => new() { CellReference = "A2", CellFormula = new CellFormula("10+10") },
                "error" => new() { CellReference = "A2", DataType = CellValues.Error, CellValue = new CellValue("#VALUE!") },
                "date" => new() { CellReference = "A2", DataType = CellValues.Date, CellValue = new CellValue(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)) },
                "boolean" => new() { CellReference = "A2", DataType = CellValues.Boolean, CellValue = new CellValue("1") },
                _ => TextCell("A2", "no_disponible")
            });
            cells.Add(TextCell("B2", "ignored")); cells.Add(TextCell("C2", "00123"));
            cells.Add(new() { CellReference = "D2", DataType = CellValues.Number, CellValue = new CellValue(name == "Selected" ? "180.00" : "999999") });
            part.Worksheet = new Worksheet(new SheetData(new Row(TextCell("A1", "Credit"), TextCell("B1", "Ignored"), TextCell("C1", "Account"), TextCell("D1", "Debit")) { RowIndex = 1U }, new Row(cells) { RowIndex = 2U }));
            book.Workbook.GetFirstChild<Sheets>()!.Append(new Sheet { Name = name, SheetId = name == "Other" ? 1U : 2U, Id = book.GetIdOfPart(part) });
        }
        return path;
    }

    private async Task VerifyRunner(StudySnapshotReceipt photo, string net)
    {
        var path = PathOf("snapshot.json"); File.WriteAllText(path, CanonicalSnapshotJson.Serialize(photo));
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet") { RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "runner", "Valdrio.Runner.dll"));
        start.ArgumentList.Add("--study-snapshot"); start.ArgumentList.Add(path);
        using var process = Process.Start(start)!; var text = await process.StandardOutput.ReadToEndAsync(); var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(); Assert.AreEqual(0, process.ExitCode, error);
        using var json = JsonDocument.Parse(text);
        Assert.AreEqual(net, json.RootElement.GetProperty("imports")[0].GetProperty("Reconciliation").GetProperty("NetAmount").GetString());
        Assert.AreEqual(photo.EconomicInputHash, json.RootElement.GetProperty("EconomicInputHash").GetString());
    }
}
