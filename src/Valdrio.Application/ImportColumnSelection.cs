using Valdrio.Domain;

namespace Valdrio.Application;

// Physical source layout is independent of the later economic import contract.
public sealed record ImportSourceReadSettings
{
    public ImportSourceReadSettings(ImportSourceKind sourceKind, string? worksheetName,
        int headerRow, int firstDataRow, CsvSourceDialect? csv)
    {
        if (!Enum.IsDefined(sourceKind) || headerRow < 0 || firstDataRow < 1 || firstDataRow <= headerRow ||
            firstDataRow > 1_048_576 ||
            sourceKind == ImportSourceKind.Csv && (csv is null || worksheetName is not null) ||
            sourceKind == ImportSourceKind.Xlsx && (csv is not null || string.IsNullOrWhiteSpace(worksheetName)))
            throw ImportRules.Error("IMP_PROFILE_INVALID", "Import.InvalidProfile");
        SourceKind = sourceKind; WorksheetName = worksheetName;
        HeaderRow = headerRow; FirstDataRow = firstDataRow; Csv = csv;
    }
    public ImportSourceKind SourceKind { get; }
    public string? WorksheetName { get; }
    public int HeaderRow { get; }
    public int FirstDataRow { get; }
    public CsvSourceDialect? Csv { get; }
}

public interface IImportSourceSession : IDisposable
{
    string FileName { get; }
    ImportSourceKind SourceKind { get; }
    IReadOnlyList<string> WorksheetNames { get; }
    Task<ImportSourceDocument> ReadAsync(ImportSourceReadSettings settings, CancellationToken cancellationToken = default);
}

public interface IImportSourceSessionFactory
{
    Task<IImportSourceSession> OpenAsync(string path, CancellationToken cancellationToken = default);
}

public sealed record ImportSourceColumn(int Index, string Header);
public sealed record ImportMappedRow(int RecordNumber, int PhysicalLine, string? Account,
    string? AmountLexical, decimal? Amount);

public sealed class ImportColumnSelectionResult
{
    internal ImportColumnSelectionResult(ImportSourceDocument source, ImportSourceReadSettings settings,
        ImportColumnMap columns, SourceNumberFormat numberFormat, XlsxNumericAccountPolicy numericAccountPolicy,
        IReadOnlyList<ImportMappedRow> rows, IReadOnlyList<ImportDiagnostic> diagnostics)
    {
        Source = source; Settings = settings; Columns = columns; NumberFormat = numberFormat;
        NumericAccountPolicy = numericAccountPolicy;
        Rows = FrozenList.Copy(rows); Diagnostics = FrozenList.Copy(diagnostics);
    }
    public ImportSourceDocument Source { get; }
    public ImportSourceReadSettings Settings { get; }
    public ImportColumnMap Columns { get; }
    public SourceNumberFormat NumberFormat { get; }
    public XlsxNumericAccountPolicy NumericAccountPolicy { get; }
    public IReadOnlyList<ImportMappedRow> Rows { get; }
    public IReadOnlyList<ImportDiagnostic> Diagnostics { get; }
    public bool IsValid => Rows.Count > 0 && !Diagnostics.Any(d => d.Severity == ImportDiagnosticSeverity.Blocking);
}

public static class ImportColumnSelection
{
    private static readonly SourceNumberFormat StoredNumber = new(".", null, [], ImportNegativeSyntax.LeadingMinus, true);

    public static IReadOnlyList<ImportSourceColumn> GetColumns(ImportSourceDocument source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var maximum = Math.Max(source.Headers.Count, source.Rows.SelectMany(r => r.Cells)
            .Select(c => c.Column).DefaultIfEmpty(0).Max());
        return FrozenList.Copy(Enumerable.Range(1, maximum)
            .Select(i => new ImportSourceColumn(i, i <= source.Headers.Count ? source.Headers[i - 1] : "")));
    }

    public static ImportColumnSelectionResult Validate(ImportSourceDocument source, ImportSourceReadSettings settings,
        ImportColumnMap columns, SourceNumberFormat numberFormat, XlsxNumericAccountPolicy numericAccountPolicy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(numberFormat);
        if (source.SourceKind != settings.SourceKind || source.SheetName != settings.WorksheetName ||
            columns.Amount is null || columns.Debit is not null || columns.Credit is not null ||
            !Enum.IsDefined(numericAccountPolicy))
            throw ImportRules.Error("IMP_PROFILE_INVALID", "Import.InvalidProfile");
        var available = GetColumns(source).Select(c => c.Index).ToHashSet();
        if (!available.Contains(columns.Account) || !available.Contains(columns.Amount.Value) ||
            columns.Description is { } description && !available.Contains(description))
            throw ImportRules.Error("IMP_COLUMN_SELECTION", "Import.ColumnsRequired");

        var diagnostics = ImportSourceInspector.ValidateBasic(source, columns, numericAccountPolicy,
            ImportDuplicatePolicy.Reject, cancellationToken: cancellationToken).ToList();
        var rows = new List<ImportMappedRow>();
        foreach (var row in source.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var account = row.GetCell(columns.Account);
            var amount = row.GetCell(columns.Amount.Value);
            var kind = amount.Kind == SourceValueKind.FormulaWithCachedValue ? amount.CachedValueKind : amount.Kind;
            var parsed = kind is SourceValueKind.Text or SourceValueKind.Number
                ? SourceAmountParser.Parse(amount.TextValue, kind == SourceValueKind.Number ? StoredNumber : numberFormat)
                : new SourceAmountParseResult(null, new DomainDiagnostic("IMP_INVALID_AMOUNT", "Import.InvalidAmount",
                    [new DiagnosticArgument("value", DiagnosticArgumentType.Text, amount.RawLexical ?? "<missing>")]));
            if (parsed.Diagnostic is { } diagnostic)
                diagnostics.Add(new ImportDiagnostic(diagnostic, ImportDiagnosticSeverity.Blocking,
                    new ImportFieldLocation(source.FileName, source.SheetName, row.RecordNumber,
                        row.PhysicalStartLine, columns.Amount)));
            rows.Add(new ImportMappedRow(row.RecordNumber, row.PhysicalStartLine, account.TextValue,
                amount.TextValue, parsed.Value));
        }
        if (rows.Count == 0)
            diagnostics.Add(new ImportDiagnostic(new DomainDiagnostic("IMP_NO_DATA", "Import.NoData", []),
                ImportDiagnosticSeverity.Blocking, new ImportFieldLocation(source.FileName, source.SheetName, null, null, null)));
        return new ImportColumnSelectionResult(source, settings, columns, numberFormat, numericAccountPolicy, rows, diagnostics);
    }
}
