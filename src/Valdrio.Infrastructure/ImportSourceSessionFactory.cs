using System.Xml;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Valdrio.Application;
using Valdrio.Domain;

namespace Valdrio.Infrastructure;

public sealed class ImportSourceSessionFactory : IImportSourceSessionFactory
{
    private readonly ImportReadLimits limits;
    public ImportSourceSessionFactory(ImportReadLimits? limits = null) => this.limits = limits ?? ImportReadLimits.Default;

    public async Task<IImportSourceSession> OpenAsync(string path, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(path);
        var kind = extension.Equals(".csv", StringComparison.OrdinalIgnoreCase) ? ImportSourceKind.Csv :
            extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase) ? ImportSourceKind.Xlsx :
            throw ImportRules.Error("IMP_SOURCE_INVALID", "Import.InvalidSource");
        using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var buffer = new MemoryStream();
        await BoundedImportRead.CopyAsync(source, buffer, limits, cancellationToken).ConfigureAwait(false);
        var bytes = buffer.ToArray();
        var sheets = kind == ImportSourceKind.Xlsx ? ReadSheets(buffer, cancellationToken) : [];
        return new Session(Path.GetFileName(path), kind, bytes, sheets, limits);
    }

    private IReadOnlyList<string> ReadSheets(MemoryStream buffer, CancellationToken token)
    {
        try
        {
            buffer.Position = 0; BoundedImportRead.ValidatePackage(buffer, limits, token);
            using var document = SpreadsheetDocument.Open(buffer, false,
                new OpenSettings { MaxCharactersInPart = limits.MaxPartBytes });
            if (document.DocumentType != SpreadsheetDocumentType.Workbook)
                throw ImportRules.Error("IMP_XLSX_UNSUPPORTED_TYPE", "Import.XlsxUnsupportedType");
            var part = document.WorkbookPart;
            var sheets = part?.Workbook?.GetFirstChild<Sheets>()?.Elements<Sheet>().ToArray();
            if (sheets is null || sheets.Length == 0) throw Invalid();
            var names = new List<string>();
            foreach (var sheet in sheets)
            {
                token.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(sheet.Name?.Value) || string.IsNullOrWhiteSpace(sheet.Id?.Value) ||
                    !part!.TryGetPartById(sheet.Id.Value, out var target) || target is not WorksheetPart ||
                    names.Contains(sheet.Name.Value, StringComparer.Ordinal)) throw Invalid();
                names.Add(sheet.Name.Value);
            }
            return FrozenList.Copy(names);
        }
        catch (Exception ex) when (ex is OpenXmlPackageException or InvalidDataException or XmlException or FormatException or IOException)
        { throw Invalid(); }
    }

    private static DomainRuleException Invalid() => ImportRules.Error("IMP_XLSX_INVALID", "Import.XlsxInvalid");

    private sealed class Session(string fileName, ImportSourceKind kind, byte[] content,
        IReadOnlyList<string> sheets, ImportReadLimits limits) : IImportSourceSession
    {
        private byte[]? bytes = content;
        public string FileName => fileName;
        public ImportSourceKind SourceKind => kind;
        public IReadOnlyList<string> WorksheetNames => sheets;
        public Task<ImportSourceDocument> ReadAsync(ImportSourceReadSettings settings, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(bytes is null, this);
            var captured = bytes;
            return Task.Run(async () =>
            {
                using var stream = new MemoryStream(captured, false);
                return kind == ImportSourceKind.Csv
                    ? await new CsvImportAdapter(limits).ReadAsync(stream, fileName, settings, cancellationToken).ConfigureAwait(false)
                    : await new XlsxImportAdapter(limits).ReadAsync(stream, fileName, settings, cancellationToken).ConfigureAwait(false);
            }, cancellationToken);
        }
        public void Dispose() => bytes = null;
    }
}
