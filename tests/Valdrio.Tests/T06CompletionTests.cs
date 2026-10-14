using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Valdrio.Application;
using Valdrio.Domain;
using Valdrio.Desktop;
using Valdrio.Infrastructure;
using Valdrio.Infrastructure.CompanyFiles;

namespace Valdrio.Tests;

public sealed partial class T06ImportConfirmationTests
{
    [TestMethod]
    [DataRow(false)][DataRow(true)]
    public async Task ComparisonUiRechecksBothFilesBeforeConfirming(bool changeEarlier)
    {
        using var file = Baseline(); var study = Study(file);
        var earlier = FileOf("earlier.csv"); var current = FileOf("current.csv");
        File.WriteAllText(earlier, "Amount;Description;Code\n100;Prior;00123");
        File.WriteAllText(current, "Amount;Description;Code\n160;Current;00123");
        var dialogs = new ComparisonDialogs(earlier, current);
        using var vm = new Valdrio.Desktop.ImportColumnMappingViewModel(new ImportSourceSessionFactory(), dialogs,
            new Valdrio.Localization.UiTextCatalog(), new Valdrio.Localization.PresentationPreferencesService(
                new Valdrio.Localization.LanguageCatalog(), Valdrio.Localization.UserPresentationPreferences.Default));
        vm.BindTarget(file.Imports, study, true, null);
        await vm.OpenCommand.ExecuteAsync(null); await vm.ReadCommand.ExecuteAsync(null);
        vm.AccountColumn = 3; vm.AmountColumn = 1; await vm.ValidateCommand.ExecuteAsync(null);
        vm.FiscalStart = new(2026, 1, 1); vm.FiscalEnd = new(2026, 12, 31); vm.PreviousEnd = new(2026, 9, 30);
        vm.ComparisonScope = "Synthetic same scope"; vm.SourceCurrency = "EUR"; vm.ApprovalReference = "100 prior"; vm.InterpretationApproved = true;
        Assert.IsTrue(vm.FreezeComparisonCommand.CanExecute(null)); vm.FreezeComparisonCommand.Execute(null);
        await vm.OpenCommand.ExecuteAsync(null); await vm.ReadCommand.ExecuteAsync(null);
        vm.AccountColumn = 3; vm.AmountColumn = 1; await vm.ValidateCommand.ExecuteAsync(null);
        vm.AmountMode = "cumulative"; vm.SourceCurrency = "EUR"; vm.ApprovalReference = "160-100=60"; vm.InterpretationApproved = true;
        await vm.PrepareCommand.ExecuteAsync(null);
        Assert.AreEqual(60m, vm.Preparation!.Reconciliation.NetAmount);
        Assert.AreEqual(100m, vm.Preparation.ComparisonRows!.Single().Earlier.Amount);
        var before = Hash(file.Path); File.AppendAllText(changeEarlier ? earlier : current, "\n0;Added;OTHER");
        await vm.ConfirmCommand.ExecuteAsync(null);
        Assert.IsTrue(vm.Status.StartsWith("IMP_SOURCE_CHANGED", StringComparison.Ordinal));
        Assert.IsNull(vm.ConfirmedImport); Assert.AreEqual(before, Hash(file.Path));
    }
    private sealed class ComparisonDialogs(params string[] paths) : IImportSourceDialogs
    {
        private readonly Queue<string> paths = new(paths);
        public string? SelectSource() => paths.Dequeue();
    }

    [TestMethod]
    public void GenuineV5UpgradesToV6WithoutChangingHistoricalPayloads()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "t06-v5.json")));
        var source = FileOf("v5.valdrio");
        using (var gzip = new GZipStream(new MemoryStream(Convert.FromBase64String(doc.RootElement.GetProperty("GzipBase64").GetString()!)), CompressionMode.Decompress))
        using (var output = File.Create(source)) gzip.CopyTo(output);
        var hash = Hash(source); Assert.AreEqual(doc.RootElement.GetProperty("Sha256").GetString(), hash);
        var batches = Query(source, "SELECT group_concat(CanonicalJson) FROM ImportBatches ORDER BY Id");
        var photos = Query(source, "SELECT group_concat(CanonicalJson) FROM StudySnapshots ORDER BY Id");
        Assert.AreEqual("FILE_UPGRADE_REQUIRED", Assert.ThrowsExactly<CompanyFileException>(() => Service().Open(source)).Diagnostic.Code);
        using var file = Service().Upgrade(source, FileOf("v6.valdrio"), "test");
        Assert.AreEqual(hash, Hash(source)); Assert.AreEqual(SqliteCompanyFileService.SchemaVersion, file.Read().SchemaVersion);
        Assert.AreEqual(batches, Query(file.Path, "SELECT group_concat(CanonicalJson) FROM ImportBatches ORDER BY Id"));
        Assert.AreEqual(photos, Query(file.Path, "SELECT group_concat(CanonicalJson) FROM StudySnapshots ORDER BY Id"));
        using var connection = new SqliteConnection("Data Source=" + file.Path + ";Pooling=False"); connection.Open();
        using var db = new ImportLedgerDbContext(connection); Assert.IsFalse(db.Database.HasPendingModelChanges());
    }

    [TestMethod]
    public async Task CumulativeDuplicateDoesNotDependOnProfileIdsOrApprovalWording()
    {
        using var file = Baseline(); var study = Study(file);
        var profile = await ComparisonProfile(study); var source = await Source(profile, "Amount;Description;Code\n160;x;00123");
        var first = Prepare(file, study, source, profile); var batch = file.Imports.ConfirmImport(Command(first));
        var same = await ComparisonProfile(batch.ConfirmedStudy);
        var prepared = Prepare(file, batch.ConfirmedStudy, source, same);
        Assert.AreEqual(first.DuplicateFingerprint, prepared.DuplicateFingerprint);
        Assert.AreEqual("IMP_DUPLICATE_BATCH", Assert.ThrowsExactly<DomainRuleException>(() => file.Imports.ConfirmImport(Command(prepared))).Diagnostic.Code);
    }

    [TestMethod]
    public async Task ReplacementRejectsForeignRetiredAndUnspecifiedTargetsWithoutWrites()
    {
        using var file = Baseline(); var study = Study(file); var other = Study(file); var profile = Profile(study);
        var source = await Source(profile); var foreign = file.Imports.ConfirmImport(Command(Prepare(file, other, source, Profile(other))));
        var first = file.Imports.ConfirmImport(Command(Prepare(file, study, source, profile)));
        var prepared = Prepare(file, first.ConfirmedStudy, await Source(profile, "Amount;Description;Code\n200;x;00123"), profile);
        var command = Command(prepared) with { Mode = ImportConfirmMode.ReplaceIdentifiedBatch, ReplacesBatchId = foreign.Id };
        var before = Hash(file.Path);
        Assert.ThrowsExactly<DomainRuleException>(() => file.Imports.ConfirmImport(command));
        Assert.ThrowsExactly<DomainRuleException>(() => file.Imports.ConfirmImport(command with { ReplacesBatchId = null }));
        Assert.AreEqual(before, Hash(file.Path));
        var second = file.Imports.ConfirmImport(command with { ReplacesBatchId = first.Id });
        var next = Prepare(file, second.ConfirmedStudy, await Source(profile, "Amount;Description;Code\n300;x;00123"), profile);
        before = Hash(file.Path);
        Assert.ThrowsExactly<DomainRuleException>(() => file.Imports.ConfirmImport(Command(next) with { Mode = ImportConfirmMode.ReplaceIdentifiedBatch, ReplacesBatchId = first.Id }));
        Assert.AreEqual(before, Hash(file.Path));
    }

    private async Task<SourceParseProfileVersion> ComparisonProfile(StudyDefinition study, string previous = "100;x;00123",
        string start = "2026-01-01", string end = "2026-09-30", string currency = "EUR")
    {
        var prior = new SourceParseProfileVersion(SourceParseProfileVersion.GroupingSchema, Id(), 1, "Prior cumulative",
            ImportSourceKind.Csv, null, 1, 2, new(3, amount: 1), new("utf-8", ';', '"', CsvBomPolicy.Optional),
            new(",", null, [], ImportNegativeSyntax.LeadingMinus), "yyyy-MM-dd", ImportAmountMode.ClosingBalanceAsStudyAmount,
            ImportSignConvention.SignedAmount, BusinessDate.ParseIso(start), BusinessDate.ParseIso(end), new(currency), null,
            ImportDuplicatePolicy.AggregateWithProvenance, ImportZeroPolicy.Keep, ImportAccountSelection.All,
            new(ImportProfileApprovalState.ApprovedBusiness, "Synthetic earlier balance 100"), true,
            grouping: new(previous.Split('\n').Select((r, i) => new ImportRecordTarget(i + 2, r.Split(';')[2])).ToArray()));
        var source = await Source(prior, "Amount;Description;Code\n" + previous);
        return new(SourceParseProfileVersion.ComparisonSchema, Id(), 1, "Cumulative difference", ImportSourceKind.Csv, null, 1, 2,
            prior.Columns, prior.Csv, prior.NumberFormat, prior.DatePattern, ImportAmountMode.CumulativeExpenseDifference,
            prior.SignConvention, study.PeriodStart, study.PeriodEnd, new("EUR"), null, prior.DuplicatePolicy,
            prior.ZeroPolicy, prior.AccountSelection, new(ImportProfileApprovalState.ApprovedBusiness, "Synthetic 160-100=60"), true,
            grouping: new([new(2, "00123")]), comparison: new(source, prior, BusinessDate.ParseIso("2026-01-01"),
                BusinessDate.ParseIso("2026-12-31"), "Same synthetic entity, accounts and accounting basis"));
    }

    [TestMethod]
    [DataRow("es-ES")][DataRow("en-US")][DataRow("de-DE")][DataRow("en-GB")]
    public async Task Cumulative160Minus100Persists60AndBothOriginals(string culture)
    {
        var original = CultureInfo.CurrentCulture; StudySnapshotReceipt photo; EntityId companyId, studyId;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            using (var file = Baseline())
            {
                var study = Study(file); companyId = study.CompanyId; studyId = study.Id;
                var profile = await ComparisonProfile(study); var source = await Source(profile, "Amount;Description;Code\n160;different;00123");
                var before = Hash(file.Path); var prepared = Prepare(file, study, source, profile);
                Assert.IsTrue(prepared.CanConfirm); Assert.AreEqual(before, Hash(file.Path));
                Assert.AreEqual(160m, prepared.Rows.Single().Original!.Value.Amount);
                Assert.AreEqual(60m, prepared.Groups!.Single().Amount.Amount);
                Assert.AreEqual(60m, prepared.Reconciliation.NetAmount);
                Assert.AreEqual(prepared.Fingerprint, CanonicalSnapshotJson.Deserialize<PreparedImport>(CanonicalSnapshotJson.Serialize(prepared)).Fingerprint);
                var batch = file.Imports.ConfirmImport(Command(prepared));
                Assert.AreEqual(60m, batch.Resources.Single().Functional.Amount);
                photo = file.Studies.Capture(new(companyId, studyId, 1, Id(), "test"));
                Assert.AreEqual(ImportedStudySnapshotInput.LedgerSchema, photo.PayloadSchema);
            }
            using var read = Service().Open(FileOf("current.valdrio"));
            Assert.AreEqual(photo.CanonicalJson, read.Studies.ReadSnapshots(companyId, studyId).Single().CanonicalJson);
            var saved = read.Imports.ReadImports(companyId, studyId).Single();
            Assert.AreEqual("100", saved.Prepared.Profile.Comparison!.Source.Rows.Single().Cells.Single(c => c.Column == 1).RawLexical);
            photo.EnsureValid();
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    [TestMethod]
    [DataRow("100;x;OTHER", "2026-01-01", "2026-09-30", "EUR", "IMP_COMPARISON_MISSING")]
    [DataRow("100;x;00123", "2025-01-01", "2025-09-30", "EUR", "IMP_COMPARISON_INVALID")]
    [DataRow("100;x;00123", "2026-01-01", "2026-09-29", "EUR", "IMP_COMPARISON_INVALID")]
    [DataRow("100;x;00123", "2026-01-01", "2026-09-30", "USD", "IMP_COMPARISON_INVALID")]
    [DataRow("bad;x;00123", "2026-01-01", "2026-09-30", "EUR", "")]
    public async Task NonComparableCumulativesNeverWrite(string earlier, string start, string end, string currency, string code)
    {
        using var file = Baseline(); var study = Study(file); var profile = await ComparisonProfile(study, earlier, start, end, currency);
        var source = await Source(profile, "Amount;Description;Code\n160;x;00123"); var before = Hash(file.Path);
        var prepared = Prepare(file, study, source, profile);
        Assert.IsFalse(prepared.CanConfirm);
        if (code.Length > 0) Assert.IsTrue(prepared.Diagnostics.Any(d => d.Diagnostic.Code == code));
        Assert.ThrowsExactly<DomainRuleException>(() => file.Imports.ConfirmImport(Command(prepared)));
        Assert.AreEqual(before, Hash(file.Path));
    }

    [TestMethod]
    [DataRow("160", "160", "0")][DataRow("100", "160", "-60")][DataRow("-160", "-100", "-60")]
    public async Task CumulativeSignsAndExplicitZeroArePreserved(string current, string earlier, string expected)
    {
        using var file = Baseline(); var study = Study(file); var profile = await ComparisonProfile(study, earlier + ";x;00123");
        var prepared = Prepare(file, study, await Source(profile, "Amount;Description;Code\n" + current + ";x;00123"), profile);
        Assert.IsTrue(prepared.CanConfirm);
        Assert.AreEqual(decimal.Parse(expected, CultureInfo.InvariantCulture), prepared.Groups!.Single().Amount.Amount);
    }

    [TestMethod]
    public async Task ReplacementKeepsHistorySnapshotsAndCanRestoreAnEarlierSourceWithoutDoubleCounting()
    {
        EntityId companyId, studyId; StudySnapshotReceipt firstPhoto, finalPhoto;
        using (var file = Baseline())
        {
            var study = Study(file); companyId = study.CompanyId; studyId = study.Id; var profile = Profile(study);
            var source = await Source(profile, "Amount;Description;Code\n100;x;00123");
            var first = file.Imports.ConfirmImport(Command(Prepare(file, study, source, profile)));
            firstPhoto = file.Studies.Capture(new(companyId, studyId, 1, Id(), "test"));
            var corrected = await Source(profile, "Amount;Description;Code\n160;x;00123");
            var prepared = Prepare(file, first.ConfirmedStudy, corrected, profile);
            var command = Command(prepared) with { Mode = ImportConfirmMode.ReplaceIdentifiedBatch, ReplacesBatchId = first.Id };
            var second = file.Imports.ConfirmImport(command);
            Assert.AreEqual(second.Id, file.Imports.ConfirmImport(command).Id);
            Assert.ThrowsExactly<DomainRuleException>(() => file.Imports.ConfirmImport(command with { Mode = ImportConfirmMode.AppendBatch, ReplacesBatchId = null }));
            var secondPhoto = file.Studies.Capture(new(companyId, studyId, 2, Id(), "test"));
            Assert.AreEqual(160m, secondPhoto.Input.Resources.Single().Functional.Amount);
            var restore = Command(Prepare(file, second.ConfirmedStudy, source, profile)) with { Mode = ImportConfirmMode.ReplaceIdentifiedBatch, ReplacesBatchId = second.Id };
            var third = file.Imports.ConfirmImport(restore);
            finalPhoto = file.Studies.Capture(new(companyId, studyId, 3, Id(), "test"));
            Assert.AreEqual(100m, finalPhoto.Input.Resources.Single().Functional.Amount);
            Assert.AreEqual(3, file.Imports.ReadImports(companyId, studyId).Count);
            Assert.AreEqual(third.Id, ImportConfirmationRules.ActiveBatches(file.Imports.ReadImports(companyId, studyId)).Single().Id);
            Assert.AreEqual(100m, firstPhoto.Input.Resources.Single().Functional.Amount);
            file.Backup(FileOf("ledger-backup"));
        }
        using var restored = Service().Restore(FileOf("ledger-backup"), FileOf("ledger-restored"));
        var photos = restored.Studies.ReadSnapshots(companyId, studyId);
        Assert.AreEqual(firstPhoto.CanonicalJson, photos.Single(p => p.InputRevision == 1).CanonicalJson);
        Assert.AreEqual(finalPhoto.CanonicalJson, photos.Single(p => p.InputRevision == 3).CanonicalJson);
    }

    [TestMethod]
    [DataRow(CompanyWriteCheckpoint.AfterFirstWrite, false)][DataRow(CompanyWriteCheckpoint.BeforeCommit, false)]
    [DataRow(CompanyWriteCheckpoint.AfterFirstWrite, true)][DataRow(CompanyWriteCheckpoint.BeforeCommit, true)]
    public async Task ReplacementFailureOrCancellationPreservesOriginalBatch(CompanyWriteCheckpoint stage, bool cancel)
    {
        ConfirmImport command; string before;
        using (var file = Baseline())
        {
            var study = Study(file); var profile = Profile(study);
            var first = file.Imports.ConfirmImport(Command(Prepare(file, study, await Source(profile), profile)));
            command = Command(Prepare(file, first.ConfirmedStudy, await Source(profile, "Amount;Description;Code\n200;x;00123"), profile))
                with { Mode = ImportConfirmMode.ReplaceIdentifiedBatch, ReplacesBatchId = first.Id };
            before = Hash(file.Path);
        }
        using var cts = new CancellationTokenSource();
        using (var file = Service(point => { if (point == stage) { if (cancel) cts.Cancel(); else throw new IOException("Injected replacement failure"); } }).Open(FileOf("current.valdrio")))
        {
            if (cancel) Assert.ThrowsExactly<OperationCanceledException>(() => file.Imports.ConfirmImport(command, cts.Token));
            else Assert.ThrowsExactly<CompanyFileException>(() => file.Imports.ConfirmImport(command));
        }
        using var read = Service().Open(FileOf("current.valdrio")); Assert.AreEqual(before, Hash(read.Path));
        Assert.AreEqual(1, read.Imports.ReadImports(command.CompanyId, command.StudyId).Count);
    }
}
