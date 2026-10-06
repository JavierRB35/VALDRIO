using System.Globalization;
using System.Security.Cryptography;
using System.Xml;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Valdrio.Application;
using Valdrio.Domain;

namespace Valdrio.Infrastructure;

public sealed class XlsxImportAdapter : IImportAdapter
{
    private static readonly HashSet<uint> BuiltInDateFormats =
    [
        14, 15, 16, 17, 22, 27, 28, 29, 30, 31, 36, 50, 51, 54, 57, 58
    ];
    private readonly ImportReadLimits limits;
    public XlsxImportAdapter(ImportReadLimits? limits = null) => this.limits = limits ?? ImportReadLimits.Default;

    public ImportSourceKind SourceKind => ImportSourceKind.Xlsx;

    public async Task<ImportSourceDocument> ReadAsync(
        Stream source,
        string fileName,
        SourceParseProfileVersion profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(profile);

        if (profile.SourceKind != ImportSourceKind.Xlsx ||
            string.IsNullOrWhiteSpace(profile.WorksheetName) ||
            !fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw ImportRules.Error("IMP_PROFILE_INVALID", "Import.InvalidProfile");

        cancellationToken.ThrowIfCancellationRequested();
        using var buffer = new MemoryStream();
        await BoundedImportRead.CopyAsync(source, buffer, limits, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var hash = Convert.ToHexString(SHA256.HashData(buffer.GetBuffer().AsSpan(0, (int)buffer.Length))).ToLowerInvariant();
        buffer.Position = 0;

        try
        {
            BoundedImportRead.ValidatePackage(buffer, limits, cancellationToken);
            using var document = SpreadsheetDocument.Open(buffer, false,
                new OpenSettings { MaxCharactersInPart = limits.MaxPartBytes });
            if (document.DocumentType != SpreadsheetDocumentType.Workbook)
                throw ImportRules.Error("IMP_XLSX_UNSUPPORTED_TYPE", "Import.XlsxUnsupportedType");

            var workbookPart = document.WorkbookPart ??
                throw ImportRules.Error("IMP_XLSX_INVALID", "Import.XlsxInvalid");
            var workbook = workbookPart.Workbook ??
                throw ImportRules.Error("IMP_XLSX_INVALID", "Import.XlsxInvalid");

            var sheets = workbook.GetFirstChild<Sheets>()?.Elements<Sheet>().ToArray() ??
                throw ImportRules.Error("IMP_XLSX_INVALID", "Import.XlsxInvalid");
            var selected = sheets.FirstOrDefault(sheet =>
                string.Equals(sheet.Name?.Value, profile.WorksheetName, StringComparison.Ordinal));
            if (selected is null)
                throw ImportRules.Error(
                    "IMP_XLSX_SHEET_NOT_FOUND",
                    "Import.XlsxSheetNotFound",
                    new DiagnosticArgument("sheet", DiagnosticArgumentType.Text, profile.WorksheetName));

            var relationshipId = selected.Id?.Value;
            if (string.IsNullOrWhiteSpace(relationshipId) ||
                !workbookPart.TryGetPartById(relationshipId, out var selectedPart) ||
                selectedPart is not WorksheetPart worksheetPart)
                throw ImportRules.Error("IMP_XLSX_INVALID", "Import.XlsxInvalid");

            var worksheet = worksheetPart.Worksheet ??
                throw ImportRules.Error("IMP_XLSX_INVALID", "Import.XlsxInvalid");
            var sheetData = worksheet.GetFirstChild<SheetData>() ??
                throw ImportRules.Error("IMP_XLSX_INVALID", "Import.XlsxInvalid");
            var dateSystem = workbook.GetFirstChild<WorkbookProperties>()?.Date1904?.Value == true
                ? XlsxDateSystem.Date1904
                : XlsxDateSystem.Date1900;
            var sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable?
                .Elements<SharedStringItem>()
                .ToArray() ?? [];
            var styleContext = XlsxStyleContext.Create(workbookPart.WorkbookStylesPart?.Stylesheet);
            ImportReadLimits.Require(sharedStrings.Length <= limits.MaxSharedStrings, "sharedStrings", limits.MaxSharedStrings);
            foreach (var shared in sharedStrings)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ImportReadLimits.Require(shared.InnerText.Length <= limits.MaxCellCharacters,
                    "cellCharacters", limits.MaxCellCharacters);
            }

            var rows = new List<SourceRow>();
            var rowNumbers = new HashSet<int>();
            foreach (var row in sheetData.Elements<Row>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (row.RowIndex?.Value is not uint rawRow ||
                    rawRow == 0 ||
                    rawRow > 1_048_576)
                    throw ImportRules.Error("IMP_XLSX_INVALID", "Import.XlsxInvalid");

                var rowNumber = checked((int)rawRow);
                if (!rowNumbers.Add(rowNumber))
                    throw ImportRules.Error("IMP_XLSX_INVALID", "Import.XlsxInvalid");
                ImportReadLimits.Require(rows.Count < limits.MaxRows, "rows", limits.MaxRows);
                var cells = new List<SourceCell>();
                var columns = new HashSet<int>();
                foreach (var cell in row.Elements<Cell>())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var (column, referenceRow) = ParseReference(cell.CellReference?.Value);
                    if (referenceRow != rowNumber || !columns.Add(column))
                        throw ImportRules.Error("IMP_XLSX_INVALID", "Import.XlsxInvalid");
                    ImportReadLimits.Require(column <= limits.MaxColumns, "columns", limits.MaxColumns);
                    ImportReadLimits.Require(cell.InnerText.Length <= limits.MaxCellCharacters,
                        "cellCharacters", limits.MaxCellCharacters);

                    cells.Add(ReadCell(
                        cell,
                        column,
                        sharedStrings,
                        styleContext.IsDate(cell),
                        dateSystem));
                }

                rows.Add(new SourceRow(rowNumber, rowNumber, cells));
            }

            var headerRow = rows.FirstOrDefault(row => row.RecordNumber == profile.HeaderRow);
            if (headerRow is null)
                throw ImportRules.Error("IMP_XLSX_INVALID", "Import.XlsxInvalid");

            var maximumHeaderColumn = headerRow.Cells.Count == 0
                ? 0
                : headerRow.Cells.Max(cell => cell.Column);
            if (maximumHeaderColumn == 0)
                throw ImportRules.Error("IMP_XLSX_INVALID", "Import.XlsxInvalid");

            var headers = Enumerable.Range(1, maximumHeaderColumn)
                .Select(column => HeaderText(headerRow.GetCell(column)))
                .ToArray();
            var dataRows = rows
                .Where(row => row.RecordNumber >= profile.FirstDataRow)
                .ToArray();

            return new ImportSourceDocument(
                ImportSourceKind.Xlsx,
                fileName,
                hash,
                profile.WorksheetName,
                headers,
                dataRows,
                dateSystem);
        }
        catch (OpenXmlPackageException)
        {
            throw ImportRules.Error("IMP_XLSX_INVALID", "Import.XlsxInvalid");
        }
        catch (InvalidDataException)
        {
            throw ImportRules.Error("IMP_XLSX_INVALID", "Import.XlsxInvalid");
        }
        catch (XmlException)
        {
            throw ImportRules.Error("IMP_XLSX_INVALID", "Import.XlsxInvalid");
        }
        catch (FormatException)
        {
            throw ImportRules.Error("IMP_XLSX_INVALID", "Import.XlsxInvalid");
        }
        catch (IOException)
        {
            throw ImportRules.Error("IMP_XLSX_INVALID", "Import.XlsxInvalid");
        }
    }

    private static SourceCell ReadCell(
        Cell cell,
        int column,
        IReadOnlyList<SharedStringItem> sharedStrings,
        bool dateStyle,
        XlsxDateSystem dateSystem)
    {
        var raw = cell.CellValue?.InnerText;
        var formula = cell.CellFormula?.Text;

        if (formula is not null)
        {
            if (raw is null)
                return new SourceCell(
                    column,
                    SourceValueKind.FormulaWithoutCachedValue,
                    null,
                    null,
                    false,
                    formula);

            var cached = ReadStoredValue(cell, column, raw, sharedStrings, dateStyle, dateSystem);
            return new SourceCell(
                column,
                SourceValueKind.FormulaWithCachedValue,
                raw,
                cached.TextValue,
                false,
                formula,
                cached.Kind,
                cached.DateValue);
        }

        if (raw is null && cell.InlineString is null)
            return new SourceCell(column, SourceValueKind.Empty, null, "", false);

        return ReadStoredValue(cell, column, raw, sharedStrings, dateStyle, dateSystem);
    }

    private static SourceCell ReadStoredValue(
        Cell cell,
        int column,
        string? raw,
        IReadOnlyList<SharedStringItem> sharedStrings,
        bool dateStyle,
        XlsxDateSystem dateSystem)
    {
        var type = cell.DataType?.Value;
        if (type == CellValues.SharedString)
        {
            if (raw is null ||
                !int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var index) ||
                index < 0 ||
                index >= sharedStrings.Count)
                throw ImportRules.Error("IMP_XLSX_INVALID", "Import.XlsxInvalid");

            return new SourceCell(
                column,
                SourceValueKind.Text,
                raw,
                sharedStrings[index].InnerText,
                false);
        }

        if (type == CellValues.InlineString)
        {
            var text = cell.InlineString?.InnerText ?? "";
            return new SourceCell(column, SourceValueKind.Text, text, text, false);
        }

        if (type == CellValues.Error)
            return new SourceCell(column, SourceValueKind.Error, raw, raw, false);

        if (type == CellValues.Boolean)
            return new SourceCell(column, SourceValueKind.Boolean, raw, raw, false);

        if (type == CellValues.Date)
        {
            if (cell.CellValue is null ||
                !(cell.CellValue.TryGetDateTime(out _) || cell.CellValue.TryGetDateTimeOffset(out _)))
                throw ImportRules.Error("IMP_XLSX_INVALID", "Import.XlsxInvalid");
            return new SourceCell(column, SourceValueKind.DateIso, raw, raw, false);
        }

        if (type == CellValues.String)
            return new SourceCell(column, SourceValueKind.Text, raw, raw, false);

        if (raw is null)
            return new SourceCell(column, SourceValueKind.Empty, null, "", false);

        if (dateStyle)
        {
            var parsed = XlsxDateSerialParser.Parse(raw, dateSystem);
            return new SourceCell(
                column,
                SourceValueKind.DateSerial,
                raw,
                raw,
                false,
                DateValue: parsed.Value);
        }

        return new SourceCell(column, SourceValueKind.Number, raw, raw, false);
    }

    private static string HeaderText(SourceCell cell) =>
        cell.Kind switch
        {
            SourceValueKind.Missing or SourceValueKind.Empty => "",
            SourceValueKind.FormulaWithoutCachedValue => "",
            _ => cell.TextValue ?? cell.RawLexical ?? ""
        };

    private static (int Column, int Row) ParseReference(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
            throw ImportRules.Error("IMP_XLSX_INVALID", "Import.XlsxInvalid");

        var column = 0;
        var index = 0;
        while (index < reference.Length && char.IsAsciiLetterUpper(reference[index]))
        {
            var digit = reference[index] - 'A' + 1;
            if (column > (16_384 - digit) / 26)
                throw ImportRules.Error("IMP_XLSX_INVALID", "Import.XlsxInvalid");
            column = column * 26 + digit;
            index++;
        }

        if (column < 1 ||
            index == 0 ||
            index == reference.Length ||
            !int.TryParse(reference[index..], NumberStyles.None, CultureInfo.InvariantCulture, out var row) ||
            row is < 1 or > 1_048_576)
            throw ImportRules.Error("IMP_XLSX_INVALID", "Import.XlsxInvalid");

        return (column, row);
    }

    private sealed class XlsxStyleContext
    {
        private readonly CellFormat[] cellFormats;
        private readonly IReadOnlyDictionary<uint, string> customFormats;

        private XlsxStyleContext(CellFormat[] cellFormats, IReadOnlyDictionary<uint, string> customFormats)
        {
            this.cellFormats = cellFormats;
            this.customFormats = customFormats;
        }

        public static XlsxStyleContext Create(Stylesheet? stylesheet)
        {
            if (stylesheet is null)
                return new([], new Dictionary<uint, string>());

            var formats = stylesheet.GetFirstChild<CellFormats>()?
                .Elements<CellFormat>()
                .ToArray() ?? [];
            var custom = new Dictionary<uint, string>();
            foreach (var format in stylesheet.GetFirstChild<NumberingFormats>()?
                         .Elements<NumberingFormat>() ?? [])
            {
                if (format.NumberFormatId?.Value is uint id &&
                    format.FormatCode?.Value is string code)
                    custom[id] = code;
            }
            return new(formats, custom);
        }

        public bool IsDate(Cell cell)
        {
            if (cell.CellValue is null) return false;
            if (cell.DataType?.Value is { } type && type != CellValues.Number)
                return false;
            var styleIndex = cell.StyleIndex?.Value ?? 0U;
            if (cellFormats.Length == 0 && styleIndex == 0 && cell.StyleIndex is null)
                return false;
            if (styleIndex >= (uint)cellFormats.Length)
                throw ImportRules.Error("IMP_XLSX_INVALID", "Import.XlsxInvalid");

            var numberFormatId = cellFormats[(int)styleIndex].NumberFormatId?.Value ?? 0U;
            if (customFormats.TryGetValue(numberFormatId, out var code))
                return XlsxNumberFormatClassifier.IsDate(code, cell.CellValue?.InnerText);
            if (BuiltInDateFormats.Contains(numberFormatId))
                return true;

            if (numberFormatId is 34 or 35 or 52 or 53 or 55 or 56 || numberFormatId > 58)
                throw ImportRules.Error("IMP_XLSX_FORMAT_UNSUPPORTED", "Import.XlsxFormatUnsupported");
            return false;
        }

    }
}
