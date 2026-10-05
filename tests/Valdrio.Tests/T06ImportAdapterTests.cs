using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Valdrio.Application;
using Valdrio.Domain;
using Valdrio.Infrastructure;
using Valdrio.Infrastructure.CompanyFiles;

namespace Valdrio.Tests;

[TestClass]
public sealed class T06ImportAdapterTests
{
    private string directory = null!;

    private sealed class Clock : IClock
    {
        public UtcInstant GetUtcNow() => UtcInstant.ParseIso("2026-10-05T18:00:00Z");
    }

    [TestInitialize]
    public void Init()
    {
        directory = Path.Combine(Path.GetTempPath(), "valdrio-t06-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(directory, true);

    private static SourceParseProfileVersion Profile(
        string encoding = "utf-8",
        CsvBomPolicy bom = CsvBomPolicy.Optional,
        ImportDuplicatePolicy duplicates = ImportDuplicatePolicy.Reject,
        ImportNegativeSyntax negative = ImportNegativeSyntax.LeadingMinus,
        bool signConfirmed = true) =>
        new(
            SourceParseProfileVersion.CurrentSchema,
            new EntityId(Guid.Parse("924af8af-0a04-4b4a-9543-17299f850001")),
            1,
            "T06 synthetic es fixture",
            ImportSourceKind.Csv,
            null,
            1,
            2,
            new ImportColumnMap(account: 1, description: 2, amount: 3),
            new CsvSourceDialect(encoding, ';', '"', bom),
            new SourceNumberFormat(",", ".", [3], negative),
            "dd/MM/yyyy",
            ImportAmountMode.PeriodAmount,
            ImportSignConvention.SignedAmount,
            BusinessDate.ParseIso("2026-02-01"),
            BusinessDate.ParseIso("2026-02-28"),
            new CurrencyCode("EUR"),
            null,
            duplicates,
            ImportZeroPolicy.Keep,
            ImportAccountSelection.All,
            new ImportProfileApproval(ImportProfileApprovalState.SyntheticFixture, "T06-fixture-only-D03-pending"),
            signConfirmed,
            "es-ES");

    private static ImportSourceDocument Read(byte[] bytes, SourceParseProfileVersion? profile = null)
    {
        using var stream = new MemoryStream(bytes);
        return new CsvImportAdapter().ReadAsync(stream, "synthetic.csv", profile ?? Profile()).GetAwaiter().GetResult();
    }

    private static SourceParseProfileVersion XlsxProfile(
        XlsxNumericAccountPolicy numericAccountPolicy = XlsxNumericAccountPolicy.Reject,
        string worksheet = "Saldos",
        int amountColumn = 3) =>
        new(
            SourceParseProfileVersion.CurrentSchema,
            new EntityId(Guid.Parse("924af8af-0a04-4b4a-9543-17299f850002")),
            1,
            "T06 synthetic XLSX fixture",
            ImportSourceKind.Xlsx,
            worksheet,
            1,
            2,
            new ImportColumnMap(account: 1, description: 2, amount: amountColumn),
            null,
            new SourceNumberFormat(".", ",", [3], ImportNegativeSyntax.LeadingMinus),
            "yyyy-MM-dd",
            ImportAmountMode.PeriodAmount,
            ImportSignConvention.SignedAmount,
            BusinessDate.ParseIso("2026-02-01"),
            BusinessDate.ParseIso("2026-02-28"),
            new CurrencyCode("EUR"),
            null,
            ImportDuplicatePolicy.Reject,
            ImportZeroPolicy.Keep,
            ImportAccountSelection.All,
            new ImportProfileApproval(ImportProfileApprovalState.SyntheticFixture, "T06-xlsx-fixture-only-D03-pending"),
            true,
            "en-US",
            numericAccountPolicy);

    private static ImportSourceDocument ReadXlsx(
        string fixture,
        SourceParseProfileVersion? profile = null)
    {
        using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture));
        return new XlsxImportAdapter().ReadAsync(
            stream,
            fixture,
            profile ?? XlsxProfile()).GetAwaiter().GetResult();
    }

    [TestMethod]
    public void T06_01_CsvPreservesLeadingZerosUnicodeQuotesDelimiterAndMultiline()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "t06-csv-es-v1.csv");
        var document = Read(File.ReadAllBytes(path));

        Assert.AreEqual(2, document.Rows.Count);
        var first = document.Rows[0];
        Assert.AreEqual("00123", first.GetCell(1).TextValue);
        Assert.AreEqual("Energía; línea 1\r\nlínea 2", first.GetCell(2).TextValue);
        Assert.IsTrue(first.GetCell(2).WasQuoted);
        Assert.AreEqual("\"Energía; línea 1\r\nlínea 2\"", first.GetCell(2).RawLexical);
        Assert.AreEqual("\"1.234,50\"", first.GetCell(3).RawLexical);
        Assert.AreEqual("Servicio \"especial\"", document.Rows[1].GetCell(2).TextValue);
        Assert.AreEqual(2, first.RecordNumber);
        Assert.AreEqual(2, first.PhysicalStartLine);
        Assert.AreEqual(4, document.Rows[1].PhysicalStartLine);
    }

    [TestMethod]
    public void T06_02_CsvAcceptsDeclaredBomAndEncodingWithoutUsingCurrentCulture()
    {
        var csvText = "Cuenta;Descripción;Importe\r\n00123;Café;123,45\r\n";
        var utf8 = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes(csvText)).ToArray();
        var utf8Document = Read(utf8);
        Assert.AreEqual("Café", utf8Document.Rows.Single().GetCell(2).TextValue);

        var latinBytes = Encoding.Latin1.GetBytes(csvText);
        var latinDocument = Read(latinBytes, Profile("iso-8859-1", CsvBomPolicy.Forbidden));
        Assert.AreEqual("Café", latinDocument.Rows.Single().GetCell(2).TextValue);

        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var amount = SourceAmountParser.Parse(latinDocument.Rows.Single().GetCell(3).TextValue, Profile().NumberFormat);
            Assert.AreEqual(123.45m, amount.Value.GetValueOrDefault());
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [TestMethod]
    public void T06_03_CsvRejectsBomEncodingMismatchAndMalformedQuoteState()
    {
        var utf8 = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes("A;B;C\r\n1;2;3")).ToArray();
        var mismatch = Assert.ThrowsExactly<DomainRuleException>(() => Read(utf8, Profile("iso-8859-1", CsvBomPolicy.Forbidden)));
        Assert.AreEqual("IMP_ENCODING_MISMATCH", mismatch.Diagnostic.Code);

        var malformed = Encoding.UTF8.GetBytes("Cuenta;Descripción;Importe\r\n1;\"sin cerrar;1,00");
        var invalid = Assert.ThrowsExactly<DomainRuleException>(() => Read(malformed));
        Assert.AreEqual("IMP_CSV_INVALID", invalid.Diagnostic.Code);
    }

    [TestMethod]
    public void T06_04_NumberParsingIsExplicitForGroupingAmbiguityAndZero()
    {
        var format = Profile().NumberFormat;
        Assert.AreEqual(1234.50m, SourceAmountParser.Parse("1.234,50", format).Value.GetValueOrDefault());
        Assert.AreEqual(0m, SourceAmountParser.Parse("0", format).Value.GetValueOrDefault());

        Assert.AreEqual("IMP_AMBIGUOUS_FORMAT", SourceAmountParser.Parse("1,234", null).Diagnostic?.Code);
        Assert.AreEqual("IMP_INVALID_AMOUNT", SourceAmountParser.Parse("12.34,50", format).Diagnostic?.Code);
        Assert.AreEqual("IMP_INVALID_AMOUNT", SourceAmountParser.Parse("", format).Diagnostic?.Code);
    }

    [TestMethod]
    public void T06_05_NumberParsingOnlyAcceptsDeclaredNegativeSyntax()
    {
        var leading = Profile(negative: ImportNegativeSyntax.LeadingMinus).NumberFormat;
        Assert.AreEqual(-123m, SourceAmountParser.Parse("-123", leading).Value.GetValueOrDefault());
        Assert.AreEqual("IMP_SIGN_UNCONFIRMED", SourceAmountParser.Parse("(123)", leading).Diagnostic?.Code);
        Assert.AreEqual("IMP_SIGN_UNCONFIRMED", SourceAmountParser.Parse("123-", leading).Diagnostic?.Code);

        var all = Profile(negative: ImportNegativeSyntax.LeadingMinus | ImportNegativeSyntax.Parentheses | ImportNegativeSyntax.TrailingMinus).NumberFormat;
        Assert.AreEqual(-123m, SourceAmountParser.Parse("(123)", all).Value.GetValueOrDefault());
        Assert.AreEqual(-123m, SourceAmountParser.Parse("123-", all).Value.GetValueOrDefault());
    }

    [TestMethod]
    public void T06_06_NumberParsingRejectsExponentInvalidGroupingOverflowAndUnderflow()
    {
        var format = Profile().NumberFormat;
        foreach (var value in new[]
        {
            "1e3",
            "1.23.4,50",
            "999999999999999999999999999999",
            "0,00000000000000000000000000001"
        })
        {
            Assert.AreEqual("IMP_INVALID_AMOUNT", SourceAmountParser.Parse(value, format).Diagnostic?.Code, value);
        }
    }

    [TestMethod]
    public void T06_07_DuplicateIdentityIsOrdinalAndLeadingZerosAreNotNormalized()
    {
        var csv = Encoding.UTF8.GetBytes(
            "Cuenta;Descripción;Importe\r\n" +
            "00123;Uno;1,00\r\n" +
            "123;Otro;2,00\r\n" +
            "abc;Minúscula;3,00\r\n" +
            "ABC;Mayúscula;4,00\r\n" +
            "00123;Duplicada;5,00\r\n");

        var document = Read(csv);
        var diagnostics = ImportSourceInspector.ValidateBasic(document, Profile());

        Assert.AreEqual(1, diagnostics.Count);
        Assert.AreEqual("IMP_DUPLICATE_ACCOUNT", diagnostics[0].Diagnostic.Code);
        Assert.AreEqual(6, diagnostics[0].Location.RecordNumber);
        Assert.AreEqual("00123", document.Rows[0].GetCell(1).TextValue);
        Assert.AreEqual("123", document.Rows[1].GetCell(1).TextValue);
    }

    [TestMethod]
    public void T06_08_AggregatePolicyDoesNotDiscardSourceRows()
    {
        var csv = Encoding.UTF8.GetBytes(
            "Cuenta;Descripción;Importe\r\n00123;A;1,00\r\n00123;B;2,00\r\n");
        var profile = Profile(duplicates: ImportDuplicatePolicy.AggregateWithProvenance);
        var document = Read(csv, profile);

        Assert.AreEqual(2, document.Rows.Count);
        Assert.AreEqual(0, ImportSourceInspector.ValidateBasic(document, profile).Count);
        Assert.AreEqual("A", document.Rows[0].GetCell(2).TextValue);
        Assert.AreEqual("B", document.Rows[1].GetCell(2).TextValue);
    }

    [TestMethod]
    public void T06_09_CancelledReadDoesNotProduceAStagingDocument()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Cuenta;Descripción;Importe\r\n00123;A;1,00\r\n"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.ThrowsExactly<OperationCanceledException>(() =>
            new CsvImportAdapter().ReadAsync(stream, "cancelled.csv", Profile(), cancellation.Token).GetAwaiter().GetResult());
    }

    [TestMethod]
    public void T06_10_SourceExtractionAndBasicInspectionDoNotMutateStudyOrAudit()
    {
        var source = HistoricalV2Fixture();
        var current = Path.Combine(directory, "current.valdrio");
        using var file = Service().Upgrade(source, current, "t06-test");
        var company = file.Read();
        var model = file.Models.ReadModels().First(item => item.Status == ModelStatus.Published);
        var study = file.Studies.Create(new CreateStudy(
            company.Id,
            new EntityId(Guid.Parse("f03402f2-0175-41c0-8296-8c9ee17c3861")),
            "T06 staging",
            BusinessDate.ParseIso("2026-02-01"),
            BusinessDate.ParseIso("2026-02-28"),
            StudyPurpose.Management,
            StudyScenario.Actual,
            model.Structure.Id,
            new CurrencyCode("EUR"),
            "t06-test"));

        var revision = study.Revision;
        var audit = Query(current, "SELECT count(*) FROM AuditEvents WHERE AggregateType='Study' AND AggregateId='" + study.Id + "'");

        var document = Read(Encoding.UTF8.GetBytes(
            "Cuenta;Descripción;Importe\r\n00123;Energía;1.234,50\r\n"));
        Assert.AreEqual(0, ImportSourceInspector.ValidateBasic(document, Profile()).Count);

        var after = file.Studies.ReadStudies().Single(item => item.Id == study.Id);
        Assert.AreEqual(revision, after.Revision);
        Assert.AreEqual(audit, Query(current, "SELECT count(*) FROM AuditEvents WHERE AggregateType='Study' AND AggregateId='" + study.Id + "'"));
        Assert.AreEqual("0", Query(current, "SELECT count(*) FROM StudySnapshots WHERE StudyId='" + study.Id + "'"));
    }

    private SqliteCompanyFileService Service() =>
        new(
            new JsonCurrencyCatalog(Path.Combine(AppContext.BaseDirectory, "reference", "currency-catalog-example.json")),
            new Clock());

    private string HistoricalV2Fixture()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "t04-v2.json")));
        using var compressed = new MemoryStream(Convert.FromBase64String(document.RootElement.GetProperty("GzipBase64").GetString()!));
        using var gzip = new GZipStream(compressed, CompressionMode.Decompress);
        var path = Path.Combine(directory, "historical-v2.valdrio");
        using (var output = File.Create(path))
            gzip.CopyTo(output);
        return path;
    }

    private static string Query(string path, string sql)
    {
        using var db = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Pooling = false,
            ForeignKeys = true
        }.ToString());
        db.Open();
        using var command = db.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture) ?? "";
    }

    [TestMethod]
    public void T06_11_XlsxPreservesTypedCellsCachedFormulaEmptyAndMissing()
    {
        var document = ReadXlsx("t06-xlsx-1900-v1.xlsx");
        Assert.AreEqual(XlsxDateSystem.Date1900, document.XlsxDateSystem);
        Assert.AreEqual("Saldos", document.SheetName);
        Assert.AreEqual(2, document.Rows.Count);

        var row = document.Rows[0];
        Assert.AreEqual(SourceValueKind.Text, row.GetCell(1).Kind);
        Assert.AreEqual("00123", row.GetCell(1).TextValue);
        Assert.AreEqual("8", row.GetCell(1).RawLexical);
        Assert.AreEqual("Energía Δ", row.GetCell(2).TextValue);
        Assert.AreEqual(SourceValueKind.Number, row.GetCell(3).Kind);
        Assert.AreEqual("1234.50", row.GetCell(3).RawLexical);

        var date = row.GetCell(4);
        Assert.AreEqual(SourceValueKind.DateSerial, date.Kind);
        Assert.AreEqual("45323", date.RawLexical);
        Assert.AreEqual(BusinessDate.ParseIso("2024-02-01"), date.DateValue);

        var cached = row.GetCell(5);
        Assert.AreEqual(SourceValueKind.FormulaWithCachedValue, cached.Kind);
        Assert.AreEqual("C2*2", cached.Formula);
        Assert.AreEqual(SourceValueKind.Number, cached.CachedValueKind);
        Assert.AreEqual("2469", cached.RawLexical);

        var noCache = row.GetCell(6);
        Assert.AreEqual(SourceValueKind.FormulaWithoutCachedValue, noCache.Kind);
        Assert.AreEqual("C2*3", noCache.Formula);
        Assert.IsNull(noCache.RawLexical);

        Assert.AreEqual(SourceValueKind.Error, row.GetCell(7).Kind);
        Assert.AreEqual("#DIV/0!", row.GetCell(7).RawLexical);
        Assert.AreEqual(SourceValueKind.Empty, row.GetCell(8).Kind);
        Assert.AreEqual(SourceValueKind.Missing, row.GetCell(9).Kind);
    }

    [TestMethod]
    public void T06_12_XlsxSupports1900And1904DatesAndRejectsSerial60()
    {
        var workbook1900 = ReadXlsx("t06-xlsx-1900-v1.xlsx");
        var serial60 = workbook1900.Rows[1].GetCell(4);
        Assert.AreEqual(SourceValueKind.DateSerial, serial60.Kind);
        Assert.IsNull(serial60.DateValue);

        var invalid = XlsxDateSerialParser.Parse("60", XlsxDateSystem.Date1900);
        Assert.AreEqual("IMP_XLSX_DATE_SERIAL_60", invalid.Diagnostic?.Code);

        var workbook1904 = ReadXlsx("t06-xlsx-1904-v1.xlsx");
        Assert.AreEqual(XlsxDateSystem.Date1904, workbook1904.XlsxDateSystem);
        Assert.AreEqual(
            BusinessDate.ParseIso("1904-01-02"),
            workbook1904.Rows[0].GetCell(4).DateValue);
    }

    [TestMethod]
    public void T06_13_XlsxNumericAccountRequiresExplicitPolicyAndNeverRestoresZeros()
    {
        var document = ReadXlsx("t06-xlsx-1900-v1.xlsx");
        var diagnostics = ImportSourceInspector.ValidateBasic(document, XlsxProfile());

        Assert.AreEqual(
            1,
            diagnostics.Count(item => item.Diagnostic.Code == "IMP_XLSX_NUMERIC_ACCOUNT"));
        Assert.AreEqual("123", document.Rows[1].GetCell(1).RawLexical);
        Assert.AreEqual("123", document.Rows[1].GetCell(1).TextValue);

        var allowed = XlsxProfile(XlsxNumericAccountPolicy.PreserveStoredLexical);
        Assert.AreEqual(
            0,
            ImportSourceInspector.ValidateBasic(document, allowed)
                .Count(item => item.Diagnostic.Code == "IMP_XLSX_NUMERIC_ACCOUNT"));
    }

    [TestMethod]
    public void T06_14_XlsxFormulaWithoutCacheAndCellErrorAreBlockingSourceDiagnostics()
    {
        var document = ReadXlsx("t06-xlsx-1900-v1.xlsx");

        var formulaDiagnostics = ImportSourceInspector.ValidateBasic(
            document,
            XlsxProfile(XlsxNumericAccountPolicy.PreserveStoredLexical, amountColumn: 6));
        Assert.AreEqual(
            1,
            formulaDiagnostics.Count(item => item.Diagnostic.Code == "IMP_XLSX_FORMULA_NO_CACHE"));

        var errorDiagnostics = ImportSourceInspector.ValidateBasic(
            document,
            XlsxProfile(XlsxNumericAccountPolicy.PreserveStoredLexical, amountColumn: 7));
        Assert.AreEqual(
            1,
            errorDiagnostics.Count(item => item.Diagnostic.Code == "IMP_XLSX_CELL_ERROR"));
    }

    [TestMethod]
    public void T06_15_XlsxRequiresExactDeclaredSheet()
    {
        using var stream = File.OpenRead(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "t06-xlsx-1900-v1.xlsx"));
        var error = Assert.ThrowsExactly<DomainRuleException>(() =>
            new XlsxImportAdapter().ReadAsync(
                stream,
                "t06-xlsx-1900-v1.xlsx",
                XlsxProfile(worksheet: "Otra"),
                CancellationToken.None).GetAwaiter().GetResult());
        Assert.AreEqual("IMP_XLSX_SHEET_NOT_FOUND", error.Diagnostic.Code);
    }

}
