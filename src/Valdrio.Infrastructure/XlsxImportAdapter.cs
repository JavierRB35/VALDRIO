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
        14, 15, 16, 17, 22, 27, 28, 30, 36, 50, 57
    ];

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
        await source.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var bytes = buffer.ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        buffer.Position = 0;

        try
        {
            using var document = SpreadsheetDocument.Open(buffer, false);
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
                workbookPart.GetPartById(relationshipId) is not WorksheetPart worksheetPart)
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

            var rows = new List<SourceRow>();
            foreach (var row in sheetData.Elements<Row>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (row.RowIndex?.Value is not uint rawRow ||
                    rawRow == 0 ||
                    rawRow > int.MaxValue)
                    throw ImportRules.Error("IMP_XLSX_INVALID", "Import.XlsxInvalid");

                var rowNumber = checked((int)rawRow);
                var cells = new List<SourceCell>();
                foreach (var cell in row.Elements<Cell>())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var (column, referenceRow) = ParseReference(cell.CellReference?.Value);
                    if (referenceRow != rowNumber)
                        throw ImportRules.Error("IMP_XLSX_INVALID", "Import.XlsxInvalid");

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

        if (type == CellValues.String || type == CellValues.Date)
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
            checked
            {
                column = column * 26 + reference[index] - 'A' + 1;
            }
            index++;
        }

        if (column < 1 ||
            index == 0 ||
            index == reference.Length ||
            !int.TryParse(reference[index..], NumberStyles.None, CultureInfo.InvariantCulture, out var row) ||
            row < 1)
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
            var styleIndex = cell.StyleIndex?.Value ?? 0U;
            if (styleIndex >= (uint)cellFormats.Length)
                return false;

            var numberFormatId = cellFormats[(int)styleIndex].NumberFormatId?.Value ?? 0U;
            if (BuiltInDateFormats.Contains(numberFormatId))
                return true;

            return customFormats.TryGetValue(numberFormatId, out var code) &&
                   LooksLikeDateFormat(code);
        }

        private static bool LooksLikeDateFormat(string formatCode)
        {
            var effective = new System.Text.StringBuilder();
            var quoted = false;

            for (var index = 0; index < formatCode.Length; index++)
            {
                var character = formatCode[index];
                if (character == '"')
                {
                    quoted = !quoted;
                    continue;
                }

                if (quoted)
                    continue;

                if (character == '[')
                {
                    var closing = formatCode.IndexOf(']', index + 1);
                    if (closing < 0)
                        throw ImportRules.Error("IMP_XLSX_INVALID", "Import.XlsxInvalid");

                    var bracket = formatCode[(index + 1)..closing];
                    if (bracket.Length != 0 &&
                        bracket.All(value => char.ToLowerInvariant(value) is 'h' or 'm' or 's'))
                    {
                        // Elapsed-time brackets are time context, never a month token.
                        effective.Append('h');
                    }

                    index = closing;
                    continue;
                }

                if (character is '\\' or '_' or '*')
                {
                    if (index + 1 >= formatCode.Length)
                        throw ImportRules.Error("IMP_XLSX_INVALID", "Import.XlsxInvalid");
                    index++;
                    continue;
                }

                effective.Append(char.ToLowerInvariant(character));
            }

            if (quoted)
                throw ImportRules.Error("IMP_XLSX_INVALID", "Import.XlsxInvalid");

            var value = effective.ToString();
            return value.Contains('y') ||
                   value.Contains('d') ||
                   ContainsMonthToken(value);
        }

        private static bool ContainsMonthToken(string value)
        {
            for (var index = 0; index < value.Length; index++)
            {
                if (value[index] != 'm')
                    continue;

                var start = index;
                while (index + 1 < value.Length && value[index + 1] == 'm')
                    index++;

                var previous = FindDateTimeToken(value, start - 1, -1);
                var next = FindDateTimeToken(value, index + 1, 1);
                if (previous != 'h' && next != 's')
                    return true;
            }

            return false;
        }

        private static char? FindDateTimeToken(string value, int index, int step)
        {
            while (index >= 0 && index < value.Length)
            {
                var character = value[index];
                if (character is 'y' or 'm' or 'd' or 'h' or 's')
                    return character;
                index += step;
            }

            return null;
        }
    }
}
