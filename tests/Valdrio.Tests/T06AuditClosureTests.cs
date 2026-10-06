using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Validation;
using Valdrio.Application;
using Valdrio.Domain;
using Valdrio.Infrastructure;

namespace Valdrio.Tests;

[TestClass]
public sealed class T06AuditClosureTests
{
    private static SourceParseProfileVersion Profile(ImportSourceKind kind,
        XlsxNumericAccountPolicy policy = XlsxNumericAccountPolicy.Reject) => new(
        SourceParseProfileVersion.CurrentSchema, new EntityId(Guid.NewGuid()), 1, "Synthetic audit fixture",
        kind, kind == ImportSourceKind.Xlsx ? "Saldos" : null, 1, 2, new ImportColumnMap(1, 2, 3),
        kind == ImportSourceKind.Csv ? new CsvSourceDialect("utf-8", ';', '"', CsvBomPolicy.Optional) : null,
        new SourceNumberFormat(".", ",", [3], ImportNegativeSyntax.LeadingMinus), "yyyy-MM-dd",
        ImportAmountMode.PeriodAmount, ImportSignConvention.SignedAmount,
        BusinessDate.ParseIso("2026-02-01"), BusinessDate.ParseIso("2026-02-28"), new CurrencyCode("EUR"), null,
        ImportDuplicatePolicy.Reject, ImportZeroPolicy.Keep, ImportAccountSelection.All,
        new ImportProfileApproval(ImportProfileApprovalState.SyntheticFixture, "audit-D03-pending"), true,
        "en-US", policy);

    [TestMethod]
    [DataRow("0.00;yyyy-mm-dd", "1234.50", SourceValueKind.Number)]
    [DataRow("yyyy-mm-dd;0.00", "-1234.50", SourceValueKind.Number)]
    [DataRow("0.00;0.00;yyyy-mm-dd", "0", SourceValueKind.DateSerial)]
    [DataRow("yyyy-mm-dd;0.00;0.00", "0", SourceValueKind.Number)]
    [DataRow("[>=100000]yyyy-mm-dd;0.00", "1234.50", SourceValueKind.Number)]
    [DataRow("[>=100000]0.00;yyyy-mm-dd", "45323", SourceValueKind.DateSerial)]
    [DataRow("[<0]0.00;[>=100000]yyyy-mm-dd;0.00", "45323", SourceValueKind.Number)]
    [DataRow("[<0]0.00;[>=100000]0.00;yyyy-mm-dd", "45323", SourceValueKind.DateSerial)]
    [DataRow("[>100]yyyy-mm-dd;0.00", "100.000000000000000000000000000001", SourceValueKind.DateSerial)]
    [DataRow("h:mm AM/PM", "0.5", SourceValueKind.Number)]
    [DataRow("h:mm:ss A/P", "0.5", SourceValueKind.Number)]
    [DataRow("[h]:mm", "0.5", SourceValueKind.Number)]
    [DataRow("mm", "45323", SourceValueKind.DateSerial)]
    [DataRow("0.00\";yyyy-mm-dd\";yyyy-mm-dd", "1234.50", SourceValueKind.Number)]
    [DataRow("0.00\\;\\d;yyyy-mm-dd", "1234.50", SourceValueKind.Number)]
    public async Task E03_OnlyTheEffectiveFormatSectionDeterminesCellKind(
        string format, string raw, SourceValueKind expected)
    {
        var bytes = Workbook(format, raw);
        AssertSchemaValid(bytes);
        var source = await Read(bytes);
        Assert.AreEqual(expected, source.Rows.Single().GetCell(3).Kind);
        Assert.AreEqual(raw, source.Rows.Single().GetCell(3).RawLexical);
    }

    [TestMethod]
    [DataRow(false, XlsxNumericAccountPolicy.Reject)]
    [DataRow(true, XlsxNumericAccountPolicy.Reject)]
    [DataRow(false, XlsxNumericAccountPolicy.PreserveStoredLexical)]
    [DataRow(true, XlsxNumericAccountPolicy.PreserveStoredLexical)]
    public async Task E04_IsoDateAccountAndCachedDateRemainTypedAndBlocked(bool formula, XlsxNumericAccountPolicy policy)
    {
        var raw = new CellValue(new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc)).InnerText;
        var bytes = Mutate(Workbook("0.00", "1234.50"), doc =>
        {
            var cell = doc.WorkbookPart!.WorksheetParts.Single().Worksheet!.Descendants<Cell>()
                .Single(c => c.CellReference?.Value == "A2");
            cell.InlineString = null; cell.DataType = CellValues.Date; cell.CellValue = new CellValue(raw);
            if (formula) cell.CellFormula = new CellFormula("DATE(2024,2,1)");
        });
        AssertSchemaValid(bytes);
        var source = await Read(bytes);
        var account = source.Rows.Single().GetCell(1);
        Assert.AreEqual(formula ? SourceValueKind.FormulaWithCachedValue : SourceValueKind.DateIso, account.Kind);
        if (formula) Assert.AreEqual(SourceValueKind.DateIso, account.CachedValueKind);
        Assert.AreEqual(raw, account.RawLexical);
        var diagnostic = ImportSourceInspector.ValidateBasic(source, Profile(ImportSourceKind.Xlsx, policy))
            .Single(d => d.Diagnostic.Code == "IMP_XLSX_DATE_ACCOUNT");
        Assert.AreEqual(2, diagnostic.Location.RecordNumber);
        Assert.AreEqual(1, diagnostic.Location.Column);
        Assert.AreEqual(ImportDiagnosticSeverity.Blocking, diagnostic.Severity);
    }

    [TestMethod]
    [DataRow(false, XlsxNumericAccountPolicy.Reject)]
    [DataRow(true, XlsxNumericAccountPolicy.Reject)]
    [DataRow(false, XlsxNumericAccountPolicy.PreserveStoredLexical)]
    [DataRow(true, XlsxNumericAccountPolicy.PreserveStoredLexical)]
    public async Task E04_DateSerialAccountIsBlockedEvenWhenNumericAccountsAreAuthorized(bool formula, XlsxNumericAccountPolicy policy)
    {
        var bytes = Mutate(Workbook("yyyy-mm-dd", "45323"), doc =>
        {
            var cell = doc.WorkbookPart!.WorksheetParts.Single().Worksheet!.Descendants<Cell>()
                .Single(c => c.CellReference?.Value == "A2");
            cell.InlineString = null; cell.DataType = CellValues.Number;
            cell.StyleIndex = 1U; cell.CellValue = new CellValue("45323");
            if (formula) cell.CellFormula = new CellFormula("DATE(2024,2,1)");
        });
        AssertSchemaValid(bytes);
        var source = await Read(bytes);
        Assert.IsTrue(ImportSourceInspector.ValidateBasic(source, Profile(ImportSourceKind.Xlsx, policy))
            .Any(d => d.Diagnostic.Code == "IMP_XLSX_DATE_ACCOUNT"));
    }

    [TestMethod]
    [DataRow("reference")]
    [DataRow("relationship")]
    [DataRow("duplicate-cell")]
    [DataRow("duplicate-row")]
    [DataRow("outside-sheet")]
    [DataRow("style")]
    public async Task E05_CorruptedWorkbookProducesControlledDiagnostic(string fault)
    {
        var bytes = Mutate(Workbook("0.00", "1234.50"), doc =>
        {
            var workbook = doc.WorkbookPart!;
            var sheet = workbook.WorksheetParts.Single().Worksheet!;
            var row = sheet.Descendants<Row>().Single(r => r.RowIndex?.Value == 2);
            var cell = row.Elements<Cell>().First();
            switch (fault)
            {
                case "reference": cell.CellReference = "ZZZZZZZZZZ2"; break;
                case "relationship": workbook.Workbook!.GetFirstChild<Sheets>()!.Elements<Sheet>().Single().Id = "rIdMissing"; break;
                case "duplicate-cell": row.Append(cell.CloneNode(true)); break;
                case "duplicate-row": sheet.GetFirstChild<SheetData>()!.Append(row.CloneNode(true)); break;
                case "outside-sheet": cell.CellReference = "XFE2"; break;
                case "style": row.Elements<Cell>().Last().StyleIndex = 9999U; break;
            }
        });
        var error = await Assert.ThrowsExactlyAsync<DomainRuleException>(() => Read(bytes));
        Assert.AreEqual("IMP_XLSX_INVALID", error.Diagnostic.Code);
    }

    [TestMethod]
    [DataRow("45323.000000000000000000000000000001")]
    [DataRow("0.000000000000000000000000000001")]
    [DataRow("45323.5")]
    [DataRow("-0.000000000000000000000000000001")]
    [DataRow("45323.")]
    [DataRow("4.5323e4")]
    public void E06_FractionalDateLexemesCannotRoundOrUnderflowToWholeDays(string raw)
    {
        foreach (var system in Enum.GetValues<XlsxDateSystem>())
        {
            var result = XlsxDateSerialParser.Parse(raw, system);
            Assert.IsNull(result.Value);
            Assert.AreEqual("IMP_XLSX_DATE_INVALID", result.Diagnostic?.Code);
        }
    }

    [TestMethod]
    public async Task E06_ExactWholeDaysAndInvalidMappedDateHaveExplicitResults()
    {
        Assert.AreEqual(BusinessDate.ParseIso("2024-02-01"),
            XlsxDateSerialParser.Parse("+45323.000000000000000000000000000000", XlsxDateSystem.Date1900).Value);
        Assert.AreEqual(BusinessDate.ParseIso("1904-01-01"),
            XlsxDateSerialParser.Parse("0.000000000000000000000000000000", XlsxDateSystem.Date1904).Value);
        var source = await Read(Workbook("yyyy-mm-dd", "45323.000000000000000000000000000001"));
        Assert.AreEqual("IMP_XLSX_DATE_INVALID", ImportSourceInspector.ValidateBasic(source,
            Profile(ImportSourceKind.Xlsx)).Single().Diagnostic.Code);
    }

    [TestMethod]
    [DataRow("file")]
    [DataRow("rows")]
    [DataRow("columns")]
    [DataRow("cell")]
    public async Task M01_CsvLimitsAlsoApplyToNonSeekableStreams(string resource)
    {
        var bytes = Encoding.UTF8.GetBytes("Cuenta;Descripcion;Importe\n00123;Texto;12.34\n");
        var limits = resource switch
        {
            "file" => new ImportReadLimits(maxFileBytes: bytes.Length - 1),
            "rows" => new ImportReadLimits(maxRows: 1),
            "columns" => new ImportReadLimits(maxColumns: 2),
            _ => new ImportReadLimits(maxCellCharacters: 4)
        };
        using var stream = new NonSeekableStream(bytes);
        var error = await Assert.ThrowsExactlyAsync<DomainRuleException>(() => new CsvImportAdapter(limits)
            .ReadAsync(stream, "limits.csv", Profile(ImportSourceKind.Csv)));
        Assert.AreEqual("IMP_SOURCE_LIMIT", error.Diagnostic.Code);
    }

    [TestMethod]
    [DataRow("file")]
    [DataRow("expanded")]
    [DataRow("part")]
    [DataRow("entries")]
    [DataRow("rows")]
    [DataRow("columns")]
    [DataRow("cell")]
    public async Task M01_XlsxResourceLimitsRejectBeforeReturningAPartialSource(string resource)
    {
        var bytes = Workbook("0.00", "1234.50");
        var limits = resource switch
        {
            "file" => new ImportReadLimits(maxFileBytes: bytes.Length - 1),
            "expanded" => new ImportReadLimits(maxExpandedBytes: 1),
            "part" => new ImportReadLimits(maxPartBytes: 1),
            "entries" => new ImportReadLimits(maxPackageEntries: 1),
            "rows" => new ImportReadLimits(maxRows: 1),
            "columns" => new ImportReadLimits(maxColumns: 2),
            _ => new ImportReadLimits(maxCellCharacters: 4)
        };
        using var stream = new MemoryStream(bytes);
        var error = await Assert.ThrowsExactlyAsync<DomainRuleException>(() => new XlsxImportAdapter(limits)
            .ReadAsync(stream, "limits.xlsx", Profile(ImportSourceKind.Xlsx)));
        Assert.AreEqual("IMP_SOURCE_LIMIT", error.Diagnostic.Code);
    }

    [TestMethod]
    public async Task M01_ExactFileBoundaryAndCancellationRemainSupported()
    {
        var bytes = Encoding.UTF8.GetBytes("Cuenta;Descripcion;Importe\n00123;Texto;12.34\n");
        using var stream = new NonSeekableStream(bytes);
        var profile = Profile(ImportSourceKind.Csv);
        var source = await new CsvImportAdapter(new ImportReadLimits(maxFileBytes: bytes.Length))
            .ReadAsync(stream, "boundary.csv", profile);
        Assert.AreEqual("00123", source.Rows.Single().GetCell(1).TextValue);
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        Assert.ThrowsExactly<OperationCanceledException>(() =>
            ImportSourceInspector.ValidateBasic(source, profile, cancel.Token));
        using var xlsx = new MemoryStream(Workbook("0.00", "1234.50"));
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => new XlsxImportAdapter()
            .ReadAsync(xlsx, "cancel.xlsx", Profile(ImportSourceKind.Xlsx), cancel.Token));
    }

    [TestMethod]
    public async Task E03_AmbiguousRegionalFormatIsBlockedRatherThanGuessed()
    {
        var bytes = Mutate(Workbook("0.00", "0.5"), doc =>
        {
            var styles = doc.WorkbookPart!.WorkbookStylesPart!.Stylesheet!;
            styles.CellFormats!.Elements<CellFormat>().Last().NumberFormatId = 34U;
        });
        var error = await Assert.ThrowsExactlyAsync<DomainRuleException>(() => Read(bytes));
        Assert.AreEqual("IMP_XLSX_FORMAT_UNSUPPORTED", error.Diagnostic.Code);
    }

    private static async Task<ImportSourceDocument> Read(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        return await new XlsxImportAdapter().ReadAsync(stream, "synthetic.xlsx", Profile(ImportSourceKind.Xlsx));
    }

    private static byte[] Mutate(byte[] bytes, Action<SpreadsheetDocument> change)
    {
        using var stream = new MemoryStream(); stream.Write(bytes); stream.Position = 0;
        using (var doc = SpreadsheetDocument.Open(stream, true)) change(doc);
        return stream.ToArray();
    }

    private static void AssertSchemaValid(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var document = SpreadsheetDocument.Open(stream, false);
        var errors = new OpenXmlValidator(FileFormatVersions.Office2010).Validate(document).ToArray();
        Assert.HasCount(0, errors, string.Join("; ", errors.Select(e => e.Description)));
    }

    private static Cell TextCell(string reference, string text) => new()
    {
        CellReference = reference, DataType = CellValues.InlineString,
        InlineString = new InlineString(new Text(text))
    };

    private static byte[] Workbook(string format, string rawAmount)
    {
        using var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook, true))
        {
            var workbook = document.AddWorkbookPart(); workbook.Workbook = new Workbook();
            var sheet = workbook.AddNewPart<WorksheetPart>();
            sheet.Worksheet = new Worksheet(new SheetData(
                new Row(TextCell("A1", "Cuenta"), TextCell("B1", "Descripcion"), TextCell("C1", "Importe")) { RowIndex = 1U },
                new Row(TextCell("A2", "00123"), TextCell("B2", "Texto"),
                    new Cell { CellReference = "C2", DataType = CellValues.Number, StyleIndex = 1U,
                        CellValue = new CellValue(rawAmount) }) { RowIndex = 2U }));
            workbook.Workbook.Append(new Sheets(new Sheet { Id = workbook.GetIdOfPart(sheet), SheetId = 1U, Name = "Saldos" }));
            var styles = workbook.AddNewPart<WorkbookStylesPart>();
            styles.Stylesheet = new Stylesheet(
                new NumberingFormats(new NumberingFormat { NumberFormatId = 164U, FormatCode = format }) { Count = 1U },
                new Fonts(new Font()) { Count = 1U },
                new Fills(new Fill(new PatternFill { PatternType = PatternValues.None }),
                    new Fill(new PatternFill { PatternType = PatternValues.Gray125 })) { Count = 2U },
                new Borders(new Border()) { Count = 1U },
                new CellStyleFormats(new CellFormat { NumberFormatId = 0U, FontId = 0U, FillId = 0U, BorderId = 0U }) { Count = 1U },
                new CellFormats(new CellFormat { NumberFormatId = 0U, FontId = 0U, FillId = 0U, BorderId = 0U, FormatId = 0U },
                    new CellFormat { NumberFormatId = 164U, FontId = 0U, FillId = 0U, BorderId = 0U, FormatId = 0U,
                        ApplyNumberFormat = true }) { Count = 2U });
        }
        return stream.ToArray();
    }

    private sealed class NonSeekableStream(byte[] bytes) : Stream
    {
        private readonly MemoryStream inner = new(bytes);
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default) => inner.ReadAsync(buffer, token);
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
    }
}
