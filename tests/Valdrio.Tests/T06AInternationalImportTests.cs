using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using Valdrio.Application;
using Valdrio.Domain;
using Valdrio.Desktop;
using Valdrio.Infrastructure;
using Valdrio.Infrastructure.CompanyFiles;
using Valdrio.Localization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace Valdrio.Tests;

public sealed partial class T06ImportConfirmationTests
{
    private static ImportConversionPolicy FxPolicy(decimal quote = .9m, IReadOnlyList<ImportRateSelection>? selections = null,
        string currency = "USD", string date = "2026-10-01", int? dateColumn = 5) =>
        new(new("synthetic-v1", BusinessDate.ParseIso("2026-10-31"), "synthetic", [new(new("usd-eur"), new(currency), new("EUR"),
            quote, BusinessDate.ParseIso(date), "synthetic", "synthetic-v1")]),
            selections ?? [new(2, new("usd-eur")), new(3, new("usd-eur"))], Catalog.GetAll(),
            BusinessDate.ParseIso("2026-10-01"), BusinessDate.ParseIso("2026-10-31"), "synthetic explicit choice", dateColumn);
    private static SourceParseProfileVersion InternationalProfile(StudyDefinition study, ImportConversionPolicy? conversion = null,
        SourceNumberFormat? number = null, string pattern = "yyyy-MM-dd", ImportComparison? comparison = null,
        IReadOnlyList<ImportRecordTarget>? records = null, ImportAmountMode mode = ImportAmountMode.PeriodAmount) =>
        new(SourceParseProfileVersion.InternationalSchema, Id(), 1, "International synthetic", ImportSourceKind.Csv, null, 1, 2,
            new(3, amount: 1), new("utf-8", ';', '"', CsvBomPolicy.Optional), number ?? new(".", null, [], ImportNegativeSyntax.LeadingMinus),
            pattern, mode, ImportSignConvention.SignedAmount, study.PeriodStart, study.PeriodEnd, null, 4,
            ImportDuplicatePolicy.AggregateWithProvenance, ImportZeroPolicy.Keep, ImportAccountSelection.All,
            new(ImportProfileApprovalState.ApprovedBusiness, "synthetic approved"), true,
            grouping: new(records ?? [new(2, "00123"), new(3, "00123"), new(4, "00123")]), comparison: comparison,
            conversion: conversion ?? FxPolicy());
    private const string InternationalCsv = "Amount;Description;Account;Currency;Date\n100;x;00123;USD;2026-10-01\n-10;different;00123;USD;2026-10-02\n20;ignored description;00123;EUR;2026-10-03";

    [TestMethod]
    public async Task InternationalImportFreezesBridgeTotalsAndReopensSnapshot()
    {
        StudySnapshotReceipt photo; EntityId studyId; string batchJson;
        using (var file = Baseline())
        {
            var study = Study(file); studyId = study.Id; var profile = InternationalProfile(study);
            var source = await Source(profile, InternationalCsv); var before = Hash(file.Path);
            var prepared = Prepare(file, study, source, profile);
            Assert.AreEqual(before, Hash(file.Path));
            Assert.IsTrue(prepared.CanConfirm, string.Join(",", prepared.Diagnostics.Select(d => d.Diagnostic.Code)));
            Assert.AreEqual(101m, prepared.Reconciliation.NetAmount);
            Assert.AreEqual(101m, prepared.Groups!.Single().Amount.Amount);
            Assert.AreEqual(90m, prepared.ConversionBridge!.OriginalTotals.Single(t => t.Currency.Value == "USD").Net);
            Assert.AreEqual(100m, prepared.ConversionBridge.OriginalTotals.Single(t => t.Currency.Value == "USD").Positive);
            Assert.AreEqual(-10m, prepared.ConversionBridge.OriginalTotals.Single(t => t.Currency.Value == "USD").Negative);
            Assert.AreEqual(110m, prepared.Reconciliation.PositiveAmount);
            Assert.AreEqual(-9m, prepared.Reconciliation.NegativeAmount);
            Assert.AreEqual(20m, prepared.ConversionBridge.OriginalTotals.Single(t => t.Currency.Value == "EUR").Net);
            var command = Command(prepared); var batch = file.Imports.ConfirmImport(command);
            Assert.AreEqual(batch.Id, file.Imports.ConfirmImport(command).Id);
            Assert.AreEqual(2, batch.Resources.Count); // Same account, distinct original currencies.
            Assert.AreEqual(81m, batch.Resources.Single(r => r.Original.Currency.Value == "USD").Functional.Amount);
            batchJson = CanonicalSnapshotJson.Serialize(batch);
            photo = file.Studies.Capture(new(study.CompanyId, study.Id, 1, Id(), "test"));
            Assert.AreEqual(ImportedStudySnapshotInput.InternationalSchema, photo.PayloadSchema);
            Assert.AreEqual(3, photo.Input.AppliedFx.Count); photo.EnsureValid();
        }
        using var reopened = Service().Open(FileOf("current.valdrio"));
        Assert.AreEqual(batchJson, CanonicalSnapshotJson.Serialize(reopened.Imports.ReadImports(reopened.Read().Id, studyId).Single()));
        Assert.AreEqual(photo.CanonicalJson, reopened.Studies.ReadSnapshots(reopened.Read().Id, studyId).Single().CanonicalJson);
        reopened.Backup(FileOf("fx.backup"));
        using var restored = Service().Restore(FileOf("fx.backup"), FileOf("restored.valdrio"));
        Assert.AreEqual(photo.FullSnapshotHash, restored.Studies.ReadSnapshots(restored.Read().Id, studyId).Single().FullSnapshotHash);
    }

    [TestMethod]
    [DataRow("es-ES", "1.234,50", ",", ".", "01/02/2026", "dd/MM/yyyy")]
    [DataRow("en-US", "1,234.50", ".", ",", "02/01/2026", "MM/dd/yyyy")]
    [DataRow("en-GB", "1,234.50", ".", ",", "01/02/2026", "dd/MM/yyyy")]
    [DataRow("de-DE", "1.234,50", ",", ".", "01.02.2026", "dd.MM.yyyy")]
    public async Task InternationalEquivalentSourcesRetainTypedDatesAndAccount(string culture, string amount, string dec,
        string group, string date, string pattern)
    {
        var prior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            using var file = Baseline(); var study = Study(file);
            var profile = InternationalProfile(study, FxPolicy(selections: []), new(dec, group, [3], ImportNegativeSyntax.LeadingMinus),
                pattern, records: [new(2, "00123")]);
            var prepared = Prepare(file, study, await Source(profile, "Amount;Description;Account;Currency;Date\n" + amount + ";x;00123;EUR;" + date), profile);
            Assert.IsTrue(prepared.CanConfirm); Assert.AreEqual(1234.5m, prepared.Reconciliation.NetAmount);
            Assert.AreEqual(BusinessDate.ParseIso("2026-02-01"), prepared.ConversionBridge!.Rows.Single().DocumentDate);
            Assert.AreEqual("00123", prepared.Rows.Single().Account);
        }
        finally { CultureInfo.CurrentCulture = prior; }
    }

    [TestMethod]
    [DataRow("missing", "FX_RATE_MISSING")][DataRow("orientation", "FX_RATE_ORIENTATION_INVALID")]
    [DataRow("date", "IMP_RATE_DATE_INVALID")][DataRow("unknown", "IMP_SOURCE_CURRENCY_INVALID")]
    [DataRow("document", "IMP_DOCUMENT_DATE_INVALID")]
    public async Task InternationalInvalidBridgeCannotWrite(string scenario, string code)
    {
        using var file = Baseline(); var study = Study(file);
        var policy = scenario switch
        {
            "missing" => FxPolicy(selections: []), "orientation" => FxPolicy(currency: "JPY"),
            "date" => FxPolicy(date: "2026-09-30"), _ => FxPolicy()
        };
        var profile = InternationalProfile(study, policy);
        var csv = scenario == "unknown" ? InternationalCsv.Replace("USD", "ZZZ", StringComparison.Ordinal) :
            scenario == "document" ? InternationalCsv.Replace("2026-10-01", "01/10/2026", StringComparison.Ordinal) : InternationalCsv;
        var before = Hash(file.Path); var prepared = Prepare(file, study, await Source(profile, csv), profile);
        Assert.IsFalse(prepared.CanConfirm); Assert.IsTrue(prepared.Diagnostics.Any(d => d.Diagnostic.Code == code));
        Assert.ThrowsExactly<DomainRuleException>(() => file.Imports.ConfirmImport(Command(prepared)));
        Assert.AreEqual(before, Hash(file.Path));
    }

    [TestMethod]
    public async Task InternationalReplacementKeepsOldRateAndOnlyActiveAmount()
    {
        using var file = Baseline(); var study = Study(file); var p = InternationalProfile(study);
        var source = await Source(p, InternationalCsv); var first = file.Imports.ConfirmImport(Command(Prepare(file, study, source, p)));
        var old = file.Studies.Capture(new(study.CompanyId, study.Id, 1, Id(), "test"));
        var nextProfile = InternationalProfile(first.ConfirmedStudy, FxPolicy(.95m));
        var next = Prepare(file, first.ConfirmedStudy, source, nextProfile);
        var second = file.Imports.ConfirmImport(Command(next) with { Mode = ImportConfirmMode.ReplaceIdentifiedBatch, ReplacesBatchId = first.Id });
        var photo = file.Studies.Capture(new(study.CompanyId, study.Id, 2, Id(), "test"));
        Assert.AreEqual(101m, old.Input.Resources.Sum(r => r.Functional.Amount));
        Assert.AreEqual(105.5m, photo.Input.Resources.Sum(r => r.Functional.Amount));
        Assert.AreNotEqual(old.EconomicInputHash, photo.EconomicInputHash);
        Assert.AreEqual(.9m, old.Input.AppliedFx.First(a => !a.IsIdentity).Quote);
        Assert.AreEqual(.95m, photo.Input.AppliedFx.First(a => !a.IsIdentity).Quote);
        Assert.AreEqual(2, file.Imports.ReadImports(study.CompanyId, study.Id).Count);
    }

    [TestMethod]
    [DataRow(CompanyWriteCheckpoint.AfterFirstWrite, false)][DataRow(CompanyWriteCheckpoint.BeforeCommit, false)]
    [DataRow(CompanyWriteCheckpoint.AfterFirstWrite, true)][DataRow(CompanyWriteCheckpoint.BeforeCommit, true)]
    public async Task InternationalConfirmationRollsBackFully(CompanyWriteCheckpoint point, bool cancel)
    {
        StudyDefinition study; PreparedImport prepared;
        using (var file = Baseline()) { study = Study(file); var p = InternationalProfile(study); prepared = Prepare(file, study, await Source(p, InternationalCsv), p); }
        var before = Hash(FileOf("current.valdrio")); using var token = new CancellationTokenSource();
        using (var file = Service(at => { if (at == point) { if (cancel) token.Cancel(); else throw new IOException("injected"); } }).Open(FileOf("current.valdrio")))
        {
            try { file.Imports.ConfirmImport(Command(prepared), token.Token); Assert.Fail("Expected rollback"); }
            catch (CompanyFileException) when (!cancel) { }
            catch (OperationCanceledException) when (cancel) { }
        }
        Assert.AreEqual(before, Hash(FileOf("current.valdrio")));
        using var reopened = Service().Open(FileOf("current.valdrio")); Assert.AreEqual(0, reopened.Imports.ReadImports(study.CompanyId, study.Id).Count);
    }

    [TestMethod]
    public void InternationalAmbiguityAndDeclaredNbspAreExplicit()
    {
        Assert.AreEqual("IMP_AMBIGUOUS_FORMAT", SourceAmountParser.Parse("1,234", null).Diagnostic!.Code);
        Assert.AreEqual("IMP_AMBIGUOUS_FORMAT", Assert.ThrowsExactly<DomainRuleException>(() =>
            SourceBusinessDateParser.Parse(new(1, SourceValueKind.Text, "01/02/2026", "01/02/2026", false), null)).Diagnostic.Code);
        Assert.AreEqual(1234.5m, SourceAmountParser.Parse("1\u00a0234,50", new(",", "\u00a0", [3], ImportNegativeSyntax.LeadingMinus)).Value);
        Assert.IsFalse(SourceAmountParser.Parse("1\u00a0234,50", new(",", " ", [3], ImportNegativeSyntax.LeadingMinus)).IsValid);
    }

    [TestMethod]
    public void InternationalUpgradePreservesGenuineV6ComparisonsAndSnapshots()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "t06a-v6.json")));
        var path = FileOf("v6.valdrio");
        using (var gzip = new GZipStream(new MemoryStream(Convert.FromBase64String(doc.RootElement.GetProperty("GzipBase64").GetString()!)), CompressionMode.Decompress))
        using (var output = File.Create(path)) gzip.CopyTo(output);
        var before = Hash(path); Assert.AreEqual(doc.RootElement.GetProperty("Sha256").GetString(), before);
        var batches = Query(path, "SELECT group_concat(CanonicalJson) FROM ImportBatches ORDER BY Id");
        var photos = Query(path, "SELECT group_concat(CanonicalJson) FROM StudySnapshots ORDER BY Id");
        using var file = Service().Upgrade(path, FileOf("v7.valdrio"), "test");
        Assert.AreEqual(7, file.Read().SchemaVersion); Assert.AreEqual(before, Hash(path));
        Assert.AreEqual(batches, Query(file.Path, "SELECT group_concat(CanonicalJson) FROM ImportBatches ORDER BY Id"));
        Assert.AreEqual(photos, Query(file.Path, "SELECT group_concat(CanonicalJson) FROM StudySnapshots ORDER BY Id"));
    }

    [TestMethod]
    public async Task InternationalCumulativeKeepsBothApprovedConversionBridges()
    {
        using var file = Baseline(); var study = Study(file);
        var previousContext = new StudyDefinition(study.Id, study.CompanyId, study.Name, BusinessDate.ParseIso("2026-01-01"),
            BusinessDate.ParseIso("2026-09-30"), study.Purpose, study.ScenarioKind, null, study.SelectedModelVersionId,
            study.Currency, study.CurrencyValidity, study.Revision, study.State, null, null, study.CreatedAtUtc);
        var records = new[] { new ImportRecordTarget(2, "00123") };
        var prior = InternationalProfile(previousContext, FxPolicy(selections: [new(2, new("usd-eur"))]), records: records,
            mode: ImportAmountMode.ClosingBalanceAsStudyAmount);
        var earlierSource = await Source(prior, "Amount;Description;Account;Currency;Date\n100;prior;00123;USD;2026-09-30");
        var comparison = new ImportComparison(earlierSource, prior, previousContext.PeriodStart, BusinessDate.ParseIso("2026-12-31"), "synthetic matched scope");
        var current = InternationalProfile(study, FxPolicy(selections: [new(2, new("usd-eur"))]), comparison: comparison,
            records: records, mode: ImportAmountMode.CumulativeExpenseDifference);
        var prepared = Prepare(file, study, await Source(current, "Amount;Description;Account;Currency;Date\n160;current;00123;USD;2026-10-31"), current);
        Assert.IsTrue(prepared.CanConfirm); Assert.AreEqual(54m, prepared.Reconciliation.NetAmount);
        Assert.AreEqual(60m, prepared.ConversionBridge!.Groups.Single().Original.Amount);
        Assert.AreEqual(90m, prepared.ConversionBridge.EarlierRows.Single().Application.ConvertedOriginalMoney.Amount);
        Assert.AreEqual(144m, prepared.ConversionBridge.Rows.Single().Application.ConvertedOriginalMoney.Amount);
        var batch = file.Imports.ConfirmImport(Command(prepared));
        Assert.AreEqual(54m, batch.Resources.Single().Functional.Amount);
        var photo = file.Studies.Capture(new(study.CompanyId, study.Id, 1, Id(), "test")); photo.EnsureValid();
    }

    [TestMethod]
    public async Task InternationalUiFreezesRatesAndInvalidatesAfterAnEdit()
    {
        using var file = Baseline(); var study = Study(file);
        var path = FileOf("fx-ui.csv"); File.WriteAllText(path, InternationalCsv);
        var dialogs = new ComparisonDialogs(path); var preferences = new PresentationPreferencesService(new LanguageCatalog(), UserPresentationPreferences.Default);
        using var vm = new ImportColumnMappingViewModel(new ImportSourceSessionFactory(), dialogs, new UiTextCatalog(), preferences, Catalog);
        vm.BindTarget(file.Imports, study, true, null); await vm.OpenCommand.ExecuteAsync(null); await vm.ReadCommand.ExecuteAsync(null);
        vm.AccountColumn = 3; vm.AmountColumn = 1; vm.DecimalSeparator = "."; await vm.ValidateCommand.ExecuteAsync(null);
        vm.International = true; vm.CurrencyFromColumn = true; vm.CurrencyColumn = 4; vm.DateColumn = 5;
        vm.RateSetName = "ui-v1"; vm.AllowedRateStart = new(2026, 10, 1); vm.AllowedRateEnd = new(2026, 10, 31);
        var rate = new ImportRateDraft { Id = "usd", Currency = "USD", Quote = "0.9", Date = "2026-10-01", Source = "synthetic" };
        vm.Rates.Add(rate); vm.AccountChoices[0].RateId = vm.AccountChoices[1].RateId = "usd";
        vm.ApprovalReference = "90 USD + 20 EUR => 101 EUR"; vm.InterpretationApproved = true;
        await vm.PrepareCommand.ExecuteAsync(null); Assert.IsTrue(vm.Preparation?.CanConfirm == true, vm.Status);
        var fingerprint = vm.Preparation!.Fingerprint;
        vm.RefreshPresentation(true); Assert.AreEqual(fingerprint, vm.Preparation.Fingerprint);
        Assert.IsNull(vm.SelectionTotals); Assert.AreEqual(3, vm.ConversionRows.Count);
        rate.Quote = "0.95"; Assert.IsNull(vm.Preparation); Assert.IsFalse(vm.InterpretationApproved);
        vm.InterpretationApproved = true; await vm.PrepareCommand.ExecuteAsync(null); await vm.ConfirmCommand.ExecuteAsync(null);
        Assert.AreEqual(105.5m, vm.ConfirmedImport!.Resources.Sum(r => r.Functional.Amount));
    }

    [TestMethod]
    public async Task InternationalConvertsEachRecordBeforeGroupingAndPreservesTinyNegativeAndZero()
    {
        using var file = Baseline(); var study = Study(file);
        var policy = FxPolicy(.000000005m, [new(2, new("usd-eur")), new(3, new("usd-eur")), new(4, new("usd-eur"))]);
        var p = InternationalProfile(study, policy);
        var csv = "Amount;Description;Account;Currency;Date\n1;x;00123;USD;2026-10-01\n-3;y;00123;USD;2026-10-02\n0;z;00123;USD;2026-10-03";
        var prepared = Prepare(file, study, await Source(p, csv), p);
        Assert.IsTrue(prepared.CanConfirm); Assert.AreEqual(-.00000002m, prepared.Reconciliation.NetAmount);
        Assert.AreEqual(0m, prepared.ConversionBridge!.Rows[0].Application.ConvertedOriginalMoney.Amount);
        Assert.AreEqual(-.000000005m, prepared.ConversionBridge.Rows[0].Application.ConversionRoundingAdjustment.Amount);
        Assert.IsTrue(prepared.Diagnostics.Any(d => d.Severity == ImportDiagnosticSeverity.Warning && d.Diagnostic.Code == "FX_AMOUNT_BELOW_QUANTUM"));
        Assert.AreEqual(3, prepared.Rows.Count(r => r.Selected));
        Assert.AreEqual(1, prepared.Reconciliation.ZeroRows);
    }

    [TestMethod]
    [DataRow(false)][DataRow(true)]
    public async Task InternationalXlsxTypedAndTextCellsUseSourceRules(bool debitCredit)
    {
        using var file = Baseline(); var study = Study(file); var path = FileOf("international.xlsx");
        static Cell TextCell(string reference, string text) => new() { CellReference = reference,
            DataType = CellValues.InlineString, InlineString = new InlineString(new Text(text)) };
        static Cell NumberCell(string reference, string value) => new() { CellReference = reference, DataType = CellValues.Number, CellValue = new CellValue(value) };
        using (var bookFile = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook))
        {
            var book = bookFile.AddWorkbookPart(); book.Workbook = new Workbook(new Sheets());
            var sheet = book.AddNewPart<WorksheetPart>();
            sheet.Worksheet = new Worksheet(new SheetData(
                new Row(TextCell("A1", "Account"), TextCell("B1", "Amount/debit"), TextCell("C1", "Credit"), TextCell("D1", "Currency"), TextCell("E1", "Date")) { RowIndex = 1U },
                new Row(TextCell("A2", "00123"), NumberCell("B2", debitCredit ? "180.00" : "100.00"), NumberCell("C2", "20.00"), TextCell("D2", "USD"),
                    new Cell { CellReference = "E2", DataType = CellValues.Date, CellValue = new CellValue("2026-10-01T00:00:00Z") }) { RowIndex = 2U },
                new Row(TextCell("A3", "00123"), TextCell("B3", debitCredit ? "0,00" : "-10,00"), TextCell("C3", "10,00"), TextCell("D3", "USD"), TextCell("E3", "02/10/2026")) { RowIndex = 3U }));
            book.Workbook.GetFirstChild<Sheets>()!.Append(new Sheet { Name = "Data", SheetId = 1U, Id = book.GetIdOfPart(sheet) });
        }
        using var session = await new ImportSourceSessionFactory().OpenAsync(path);
        var source = await session.ReadAsync(new(ImportSourceKind.Xlsx, "Data", 1, 2, null));
        var profile = new SourceParseProfileVersion(SourceParseProfileVersion.InternationalSchema, Id(), 1, "typed source",
            ImportSourceKind.Xlsx, "Data", 1, 2, debitCredit ? new(1, debit: 2, credit: 3) : new(1, amount: 2), null,
            new(",", ".", [3], ImportNegativeSyntax.LeadingMinus), "dd/MM/yyyy", ImportAmountMode.PeriodAmount,
            debitCredit ? ImportSignConvention.DebitMinusCredit : ImportSignConvention.SignedAmount, study.PeriodStart, study.PeriodEnd,
            null, 4, ImportDuplicatePolicy.AggregateWithProvenance, ImportZeroPolicy.Keep, ImportAccountSelection.All,
            new(ImportProfileApprovalState.ApprovedBusiness, "synthetic XLSX"), true,
            grouping: new([new(2, "00123"), new(3, "00123")]), conversion: FxPolicy());
        var prepared = Prepare(file, study, source, profile);
        Assert.IsTrue(prepared.CanConfirm, string.Join(",", prepared.Diagnostics.Select(d => d.Diagnostic.Code)));
        Assert.AreEqual(debitCredit ? 135m : 81m, prepared.Reconciliation.NetAmount);
        Assert.AreEqual(BusinessDate.ParseIso("2026-10-01"), prepared.Rows[0].ParsedDate);
        Assert.AreEqual(BusinessDate.ParseIso("2026-10-02"), prepared.Rows[1].ParsedDate);
        var json = CanonicalSnapshotJson.Serialize(prepared);
        Assert.AreEqual(json, CanonicalSnapshotJson.Serialize(CanonicalSnapshotJson.Deserialize<PreparedImport>(json)));
        var batch = file.Imports.ConfirmImport(Command(prepared)); Assert.AreEqual("00123", batch.Resources.Single().Account);
    }
}
