using System.Security.Cryptography;
using System.Text;
using Valdrio.Application;

namespace Valdrio.Infrastructure;

public sealed class CsvImportAdapter : IImportAdapter
{
    public ImportSourceKind SourceKind => ImportSourceKind.Csv;

    public async Task<ImportSourceDocument> ReadAsync(
        Stream source,
        string fileName,
        SourceParseProfileVersion profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(profile);

        if (profile.SourceKind != ImportSourceKind.Csv)
            throw ImportRules.Error("IMP_PROFILE_INVALID", "Import.InvalidProfile");
        var dialect = profile.Csv ?? throw ImportRules.Error("IMP_PROFILE_INVALID", "Import.InvalidProfile");

        cancellationToken.ThrowIfCancellationRequested();
        using var buffer = new MemoryStream();
        await source.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var bytes = buffer.ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var text = Decode(bytes, dialect);
        var records = Parse(text, dialect, cancellationToken);

        if (records.Count < profile.HeaderRow)
            throw ImportRules.Error("IMP_CSV_INVALID", "Import.InvalidCsv");

        var header = records[profile.HeaderRow - 1].Cells
            .Select(cell => cell.TextValue ?? "")
            .ToArray();

        var rows = records
            .Where(record => record.RecordNumber >= profile.FirstDataRow)
            .ToArray();

        return new ImportSourceDocument(ImportSourceKind.Csv, fileName, hash, null, header, rows);
    }

    private static string Decode(byte[] bytes, CsvSourceDialect dialect)
    {
        var (encoding, expectedBom) = ResolveEncoding(dialect.EncodingName);
        var actualBomLength = KnownBomLength(bytes, out var actualBom);

        if (actualBomLength > 0 && !actualBom.AsSpan().SequenceEqual(expectedBom) ||
            dialect.BomPolicy == CsvBomPolicy.Forbidden && actualBomLength > 0 ||
            dialect.BomPolicy == CsvBomPolicy.Required && (expectedBom.Length == 0 || actualBomLength == 0))
            throw ImportRules.Error("IMP_ENCODING_MISMATCH", "Import.EncodingMismatch");

        var offset = actualBomLength > 0 ? actualBomLength : 0;
        try
        {
            return encoding.GetString(bytes, offset, bytes.Length - offset);
        }
        catch (DecoderFallbackException)
        {
            throw ImportRules.Error("IMP_ENCODING_MISMATCH", "Import.EncodingMismatch");
        }
    }

    private static (Encoding Encoding, byte[] Bom) ResolveEncoding(string name) =>
        name.Trim().ToLowerInvariant() switch
        {
            "utf-8" or "utf8" =>
                (new UTF8Encoding(false, true), [0xEF, 0xBB, 0xBF]),
            "utf-16" or "utf-16le" or "unicode" =>
                (new UnicodeEncoding(false, false, true), [0xFF, 0xFE]),
            "utf-16be" =>
                (new UnicodeEncoding(true, false, true), [0xFE, 0xFF]),
            "iso-8859-1" or "latin1" =>
                (Encoding.Latin1, []),
            _ => throw ImportRules.Error("IMP_PROFILE_INVALID", "Import.InvalidProfile")
        };

    private static int KnownBomLength(byte[] bytes, out byte[] bom)
    {
        if (bytes.AsSpan().StartsWith(new byte[] { 0xFF, 0xFE, 0x00, 0x00 }))
        {
            bom = [0xFF, 0xFE, 0x00, 0x00];
            return 4;
        }

        if (bytes.AsSpan().StartsWith(new byte[] { 0x00, 0x00, 0xFE, 0xFF }))
        {
            bom = [0x00, 0x00, 0xFE, 0xFF];
            return 4;
        }

        if (bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }))
        {
            bom = [0xEF, 0xBB, 0xBF];
            return 3;
        }

        if (bytes.AsSpan().StartsWith(new byte[] { 0xFF, 0xFE }))
        {
            bom = [0xFF, 0xFE];
            return 2;
        }

        if (bytes.AsSpan().StartsWith(new byte[] { 0xFE, 0xFF }))
        {
            bom = [0xFE, 0xFF];
            return 2;
        }

        bom = [];
        return 0;
    }

    private static IReadOnlyList<SourceRow> Parse(
        string text,
        CsvSourceDialect dialect,
        CancellationToken cancellationToken)
    {
        var rows = new List<SourceRow>();
        var cells = new List<SourceCell>();
        var value = new StringBuilder();
        var raw = new StringBuilder();
        var inQuotes = false;
        var afterQuote = false;
        var quoted = false;
        var recordNumber = 1;
        var physicalLine = 1;
        var recordStartLine = 1;
        var touched = false;

        void AddCell()
        {
            var textValue = value.ToString();
            cells.Add(new SourceCell(
                cells.Count + 1,
                textValue.Length == 0 ? SourceValueKind.Empty : SourceValueKind.Text,
                raw.ToString(),
                textValue,
                quoted));
            value.Clear();
            raw.Clear();
            quoted = false;
            afterQuote = false;
            touched = false;
        }

        void AddRow()
        {
            AddCell();
            rows.Add(new SourceRow(recordNumber++, recordStartLine, cells.ToArray()));
            cells.Clear();
        }

        for (var index = 0; index < text.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var character = text[index];

            if (inQuotes)
            {
                if (character == dialect.Quote)
                {
                    raw.Append(character);
                    if (index + 1 < text.Length && text[index + 1] == dialect.Quote)
                    {
                        raw.Append(text[++index]);
                        value.Append(dialect.Quote);
                    }
                    else
                    {
                        inQuotes = false;
                        afterQuote = true;
                    }
                    continue;
                }

                if (character == '\r' || character == '\n')
                {
                    if (character == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                    {
                        raw.Append("\r\n");
                        value.Append("\r\n");
                        index++;
                    }
                    else
                    {
                        raw.Append(character);
                        value.Append(character);
                    }
                    physicalLine++;
                    continue;
                }

                raw.Append(character);
                value.Append(character);
                continue;
            }

            if (afterQuote)
            {
                if (character == dialect.Delimiter)
                {
                    AddCell();
                    continue;
                }

                if (character == '\r' || character == '\n')
                {
                    AddRow();
                    if (character == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                        index++;
                    physicalLine++;
                    recordStartLine = physicalLine;
                    continue;
                }

                throw ImportRules.Error("IMP_CSV_INVALID", "Import.InvalidCsv");
            }

            if (character == dialect.Quote)
            {
                if (value.Length != 0 || raw.Length != 0)
                    throw ImportRules.Error("IMP_CSV_INVALID", "Import.InvalidCsv");
                raw.Append(character);
                inQuotes = true;
                quoted = true;
                touched = true;
                continue;
            }

            if (character == dialect.Delimiter)
            {
                AddCell();
                touched = true;
                continue;
            }

            if (character == '\r' || character == '\n')
            {
                AddRow();
                if (character == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                    index++;
                physicalLine++;
                recordStartLine = physicalLine;
                continue;
            }

            raw.Append(character);
            value.Append(character);
            touched = true;
        }

        if (inQuotes)
            throw ImportRules.Error("IMP_CSV_INVALID", "Import.InvalidCsv");

        if (afterQuote || touched || cells.Count != 0 || value.Length != 0 || raw.Length != 0)
            AddRow();

        return rows;
    }
}
