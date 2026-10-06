using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Valdrio.Application;
using Valdrio.Desktop;
using Valdrio.Domain;
using Valdrio.Infrastructure;
using Valdrio.Localization;

namespace Valdrio.Tests;

[TestClass]
public sealed class ImportColumnMappingTests
{
    private string directory = null!;
    [TestInitialize] public void Init() { directory = Path.Combine(Path.GetTempPath(), "valdrio-columns-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory); }
    [TestCleanup] public void Cleanup() => Directory.Delete(directory, true);
    private static ImportSourceReadSettings Csv(int header = 1, int first = 2) =>
        new(ImportSourceKind.Csv, null, header, first, new CsvSourceDialect("utf-8", ';', '"', CsvBomPolicy.Optional));
    private static ImportSourceReadSettings Xlsx(int header = 1, int first = 2) => new(ImportSourceKind.Xlsx, "Selected", header, first, null);
    private static SourceNumberFormat TextNumber => new(",", ".", [3], ImportNegativeSyntax.LeadingMinus);
    private string WriteCsv(string content) { var p = Path.Combine(directory, Guid.NewGuid() + ".csv"); File.WriteAllText(p, content); return p; }
    private static ImportColumnSelectionResult Validate(ImportSourceDocument source, ImportSourceReadSettings settings, int account = 3, int amount = 1) =>
        ImportColumnSelection.Validate(source, settings, new ImportColumnMap(account, amount: amount), TextNumber, XlsxNumericAccountPolicy.Reject);

    [TestMethod]
    public async Task Csv_ManualNonAdjacentColumnsPreserveLeadingZerosAndIgnoreOtherAmounts()
    {
        using var session = await new ImportSourceSessionFactory().OpenAsync(WriteCsv("value;ignored;code\n1.234,50;99999;00123\n-2,75;77777;A-42"));
        var source = await session.ReadAsync(Csv());
        var result = Validate(source, Csv());
        Assert.IsTrue(result.IsValid); Assert.AreEqual("00123", result.Rows[0].Account);
        Assert.AreEqual(1234.50m, result.Rows[0].Amount); Assert.AreEqual(-2.75m, result.Rows[1].Amount);
        Assert.AreEqual(1, result.Columns.Amount); Assert.AreEqual(3, result.Columns.Account);
    }

    [TestMethod]
    public async Task Xlsx_ExplicitSecondSheetAndReorderedColumnsUseStoredNumberGrammar()
    {
        using var session = await new ImportSourceSessionFactory().OpenAsync(WriteWorkbook());
        CollectionAssert.AreEqual(new[] { "Other", "Selected" }, session.WorksheetNames.ToArray());
        var source = await session.ReadAsync(Xlsx()); var result = Validate(source, Xlsx());
        Assert.IsTrue(result.IsValid); Assert.AreEqual("00123", result.Rows.Single().Account);
        Assert.AreEqual(1250.5m, result.Rows.Single().Amount);
        Assert.AreEqual("Selected", result.Source.SheetName);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task HeaderlessSourcesKeepTheFirstDataRow(bool excel)
    {
        var path = excel ? WriteWorkbook() : WriteCsv("1,50;ignored;00123");
        using var session = await new ImportSourceSessionFactory().OpenAsync(path);
        var settings = excel ? Xlsx(0, 2) : Csv(0, 1);
        var source = await session.ReadAsync(settings);
        Assert.AreEqual(0, source.Headers.Count);
        Assert.AreEqual(excel ? 2 : 1, source.Rows[0].RecordNumber);
        Assert.IsTrue(Validate(source, settings).IsValid);
    }

    [TestMethod]
    public async Task DuplicateAndEmptyHeadersRemainDistinctByColumnPosition()
    {
        using var session = await new ImportSourceSessionFactory().OpenAsync(WriteCsv("value;;value\n1,50;ignored;00123"));
        var source = await session.ReadAsync(Csv()); var columns = ImportColumnSelection.GetColumns(source);
        Assert.AreEqual(3, columns.Count); Assert.AreEqual("value", columns[0].Header);
        Assert.AreEqual("", columns[1].Header); Assert.AreEqual("value", columns[2].Header);
        Assert.IsTrue(Validate(source, Csv()).IsValid);
    }

    [TestMethod]
    public async Task DataColumnBeyondShortHeaderCanStillBeSelected()
    {
        using var session = await new ImportSourceSessionFactory().OpenAsync(WriteCsv("value\n1,50;ignored;00123"));
        var source = await session.ReadAsync(Csv());
        Assert.AreEqual(3, ImportColumnSelection.GetColumns(source).Count);
        Assert.IsTrue(Validate(source, Csv()).IsValid);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("invalid")]
    public async Task InvalidAmountsStayNullWithTheirOriginalLocation(string amount)
    {
        using var session = await new ImportSourceSessionFactory().OpenAsync(WriteCsv("value;ignored;code\n" + amount + ";x;00123"));
        var result = Validate(await session.ReadAsync(Csv()), Csv());
        Assert.IsFalse(result.IsValid); Assert.IsNull(result.Rows.Single().Amount);
        var issue = result.Diagnostics.Single(d => d.Diagnostic.Code == "IMP_INVALID_AMOUNT");
        Assert.AreEqual(2, issue.Location.RecordNumber); Assert.AreEqual(1, issue.Location.Column);
    }

    [TestMethod]
    public async Task ZeroIsValidAndMissingAmountIsBlocked()
    {
        using var session = await new ImportSourceSessionFactory().OpenAsync(WriteCsv("code;value\n00123;0\n00456"));
        var result = Validate(await session.ReadAsync(Csv()), Csv(), 1, 2);
        Assert.AreEqual(0m, result.Rows[0].Amount); Assert.IsNull(result.Rows[1].Amount);
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    [DataRow("date")]
    [DataRow("boolean")]
    [DataRow("formula")]
    public async Task TypedExcelAccountErrorsStayBlocked(string type)
    {
        using var session = await new ImportSourceSessionFactory().OpenAsync(WriteWorkbook(type));
        var result = Validate(await session.ReadAsync(Xlsx()), Xlsx());
        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Diagnostics.Any(d => d.Location.Column == 3 && d.Severity == ImportDiagnosticSeverity.Blocking));
    }

    [TestMethod]
    public async Task NumericAccountsRequireExplicitConsentAndPreserveStoredIdentity()
    {
        using var session = await new ImportSourceSessionFactory().OpenAsync(WriteWorkbook("number"));
        var source = await session.ReadAsync(Xlsx());
        Assert.IsFalse(Validate(source, Xlsx()).IsValid);
        var result = ImportColumnSelection.Validate(source, Xlsx(), new ImportColumnMap(3, amount: 1),
            TextNumber, XlsxNumericAccountPolicy.PreserveStoredLexical);
        Assert.IsTrue(result.IsValid); Assert.AreEqual("123", result.Rows.Single().Account);
    }

    [TestMethod]
    public async Task SourceSessionCapturesTheFileOnceAndDoesNotFollowExternalEdits()
    {
        var path = WriteCsv("value;ignored;code\n1,50;x;00123");
        using var session = await new ImportSourceSessionFactory().OpenAsync(path);
        var first = await session.ReadAsync(Csv()); File.WriteAllText(path, "value;ignored;code\n999,50;x;other");
        var second = await session.ReadAsync(Csv());
        Assert.AreEqual(first.FileHash, second.FileHash); Assert.AreEqual("00123", second.Rows.Single().GetCell(3).TextValue);
    }

    [TestMethod]
    public async Task ViewModelRejectsSameColumnAndInvalidatesReadyMappingWhenSelectionChanges()
    {
        var dialogs = new Dialogs(); dialogs.Paths.Enqueue(WriteCsv("value;ignored;code\n1,50;x;00123"));
        using var vm = Vm(dialogs); await vm.OpenCommand.ExecuteAsync(null); await vm.ReadCommand.ExecuteAsync(null);
        Assert.IsNull(vm.AccountColumn); Assert.IsNull(vm.AmountColumn);
        vm.AccountColumn = 3; vm.AmountColumn = 3; await vm.ValidateCommand.ExecuteAsync(null);
        Assert.IsFalse(vm.IsReady); StringAssert.Contains(vm.Status, "IMP_COLUMN_SELECTION");
        vm.AmountColumn = 1; await vm.ValidateCommand.ExecuteAsync(null); Assert.IsTrue(vm.IsReady);
        vm.AmountColumn = 2; Assert.IsFalse(vm.IsReady); Assert.IsNull(vm.ValidatedSelection);
    }

    [TestMethod]
    [DataRow("delimiter")]
    [DataRow("row")]
    [DataRow("encoding")]
    public async Task SourceLayoutChangeClearsColumnsAndValidatedMapping(string setting)
    {
        var dialogs = new Dialogs(); dialogs.Paths.Enqueue(WriteCsv("value;ignored;code\n1,50;x;00123"));
        using var vm = Vm(dialogs); await Ready(vm);
        if (setting == "delimiter") vm.Delimiter = ","; else if (setting == "row") vm.FirstDataRow = "3"; else vm.Encoding = "utf-16le";
        Assert.IsFalse(vm.IsReady); Assert.IsFalse(vm.HasColumns); Assert.AreEqual(0, vm.Preview.Count);
        Assert.IsNull(vm.AccountColumn); Assert.IsNull(vm.AmountColumn);
    }

    [TestMethod]
    public async Task ValidationChecksRowsBeyondTheFiftyRowPreview()
    {
        var csv = "value;ignored;code\n" + string.Join("\n", Enumerable.Range(1, 60).Select(i => $"1,50;x;{i}")) + "\n1,50;x;1";
        var dialogs = new Dialogs(); dialogs.Paths.Enqueue(WriteCsv(csv)); using var vm = Vm(dialogs); await Ready(vm);
        Assert.AreEqual(50, vm.Preview.Count); Assert.IsFalse(vm.IsReady);
        StringAssert.Contains(vm.GlobalIssues, "IMP_DUPLICATE_ACCOUNT");
    }

    [TestMethod]
    public async Task PresentationChangesPreserveValidatedSelectionAndStableColumnOptions()
    {
        var dialogs = new Dialogs(); dialogs.Paths.Enqueue(WriteCsv("value;;code\n1,50;x;00123"));
        var preferences = new PresentationPreferencesService(new LanguageCatalog(), UserPresentationPreferences.Default);
        using var vm = Vm(dialogs, preferences); await Ready(vm);
        var result = vm.ValidatedSelection; var option = vm.Columns[1];
        preferences.Update(preferences.Current with { UiLanguageTag = "es", FormatCultureTag = "en-US" });
        vm.RefreshPresentation(true);
        Assert.AreSame(result, vm.ValidatedSelection); Assert.AreSame(option, vm.Columns[1]);
        Assert.AreEqual(3, vm.AccountColumn); Assert.AreEqual(1, vm.AmountColumn); Assert.IsTrue(vm.IsReady);
        Assert.AreEqual("00123", vm.Preview.Single().Account);
    }

    [TestMethod]
    public async Task FailedNewFileCannotLeaveThePreviousMappingReady()
    {
        var dialogs = new Dialogs(); dialogs.Paths.Enqueue(WriteCsv("value;x;code\n1,50;x;00123"));
        using var vm = Vm(dialogs); await Ready(vm);
        dialogs.Paths.Enqueue(Path.Combine(directory, "missing.csv")); await vm.OpenCommand.ExecuteAsync(null);
        Assert.IsFalse(vm.IsReady); Assert.IsFalse(vm.HasColumns); Assert.IsFalse(vm.HasFile);
    }

    [TestMethod]
    public async Task CancelledReadKeepsNoPartialMapping()
    {
        var dialogs = new Dialogs(); dialogs.Paths.Enqueue(WriteCsv("value;x;code\n1,50;x;00123"));
        using var vm = Vm(dialogs, factory: new CancellableFactory());
        await vm.OpenCommand.ExecuteAsync(null); var read = vm.ReadCommand.ExecuteAsync(null);
        Assert.IsTrue(vm.IsBusy); vm.CancelCommand.Execute(null); await read;
        Assert.IsFalse(vm.IsBusy); Assert.IsFalse(vm.HasColumns); Assert.IsNull(vm.ValidatedSelection);
    }

    [TestMethod]
    [DataRow("en")]
    [DataRow("es")]
    public void AllMappingResourcesExistWithoutFallback(string language)
    {
        var texts = new UiTextCatalog(); var resource = texts.ReadAll(CultureInfo.GetCultureInfo(language));
        var keys = new[] { "OpenMapping", "Title", "Intro", "Open", "Worksheet", "HeaderRow", "FirstDataRow",
            "Delimiter", "Encoding", "Read", "AccountColumn", "AmountColumn", "Decimal", "Grouping", "Negative",
            "NumberHelp", "NumericAccounts", "NumericWarning", "Validate", "PreviewHelp", "Row", "Original",
            "Parsed", "Issues", "StagingOnly", "Cancel", "Close", "ChooseFile", "ReadSource", "SelectColumns",
            "SelectWorksheet", "Working", "Cancelled", "FileError", "Ready", "HasErrors", "Summary", "Valid",
            "Blocked", "None", "Space", "Tab", "NoHeader" };
        Assert.IsTrue(keys.All(k => resource.ContainsKey("ImportMapping." + k)));
    }

    private static ImportColumnMappingViewModel Vm(Dialogs dialogs, PresentationPreferencesService? preferences = null,
        IImportSourceSessionFactory? factory = null) => new(factory ?? new ImportSourceSessionFactory(), dialogs, new UiTextCatalog(),
            preferences ?? new PresentationPreferencesService(new LanguageCatalog(), UserPresentationPreferences.Default));
    private static async Task Ready(ImportColumnMappingViewModel vm)
    { await vm.OpenCommand.ExecuteAsync(null); await vm.ReadCommand.ExecuteAsync(null); vm.AccountColumn = 3; vm.AmountColumn = 1; await vm.ValidateCommand.ExecuteAsync(null); }
    private sealed class Dialogs : IImportSourceDialogs
    { public Queue<string?> Paths { get; } = new(); public string? SelectSource() => Paths.Dequeue(); }
    private sealed class CancellableFactory : IImportSourceSessionFactory
    {
        public Task<IImportSourceSession> OpenAsync(string path, CancellationToken cancellationToken = default) => Task.FromResult<IImportSourceSession>(new Session());
        private sealed class Session : IImportSourceSession
        {
            public string FileName => "cancel.csv"; public ImportSourceKind SourceKind => ImportSourceKind.Csv;
            public IReadOnlyList<string> WorksheetNames => [];
            public async Task<ImportSourceDocument> ReadAsync(ImportSourceReadSettings settings, CancellationToken cancellationToken = default)
            { await Task.Delay(Timeout.Infinite, cancellationToken); throw new InvalidOperationException(); }
            public void Dispose() { }
        }
    }
    private string WriteWorkbook(string accountType = "text")
    {
        var path = Path.Combine(directory, Guid.NewGuid() + ".xlsx");
        using var doc = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook);
        var book = doc.AddWorkbookPart(); book.Workbook = new Workbook(new Sheets());
        foreach (var name in new[] { "Other", "Selected" })
        {
            var part = book.AddNewPart<WorksheetPart>();
            var account = TextCell("C2", "00123");
            if (accountType == "number") account = new Cell { CellReference = "C2", DataType = CellValues.Number, CellValue = new CellValue("123") };
            if (accountType == "date") account = new Cell { CellReference = "C2", DataType = CellValues.Date,
                CellValue = new CellValue(new DateTime(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc)) };
            if (accountType == "boolean") account = new Cell { CellReference = "C2", DataType = CellValues.Boolean, CellValue = new CellValue("1") };
            if (accountType == "formula") account = new Cell { CellReference = "C2", CellFormula = new CellFormula("1+1") };
            part.Worksheet = new Worksheet(new SheetData(new Row(TextCell("A1", "Amount"), TextCell("B1", "Ignored"), TextCell("C1", "Code")) { RowIndex = 1U },
                new Row(new Cell { CellReference = "A2", DataType = CellValues.Number, CellValue = new CellValue(name == "Selected" ? "1250.5" : "99999") }, TextCell("B2", "unused"), account) { RowIndex = 2U }));
            book.Workbook.GetFirstChild<Sheets>()!.Append(new Sheet { Name = name, SheetId = (uint)(name == "Other" ? 1 : 2), Id = book.GetIdOfPart(part) });
        }
        return path;
    }
    private static Cell TextCell(string reference, string value) => new() { CellReference = reference,
        DataType = CellValues.InlineString, InlineString = new InlineString(new Text(value)) };
}
