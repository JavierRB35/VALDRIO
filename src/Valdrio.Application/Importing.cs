using System.Numerics;
using Valdrio.Domain;

namespace Valdrio.Application;

public enum ImportSourceKind { Csv = 0, Xlsx = 1 }
public enum ImportAmountMode { PeriodAmount = 0, ClosingBalance = 1, CumulativeExpenseDifference = 2 }
public enum ImportSignConvention { SignedAmount = 0, DebitMinusCredit = 1 }
public enum ImportDuplicatePolicy { Reject = 0, AggregateWithProvenance = 1 }
public enum ImportZeroPolicy { Keep = 0, Exclude = 1 }
public enum ImportAccountSelectionKind { All = 0, ExplicitList = 1 }
public enum ImportProfileApprovalState { SyntheticFixture = 0, PendingBusinessApproval = 1, ApprovedBusiness = 2 }
public enum CsvBomPolicy { Forbidden = 0, Optional = 1, Required = 2 }
public enum XlsxDateSystem { Date1900 = 0, Date1904 = 1 }
public enum XlsxNumericAccountPolicy { Reject = 0, PreserveStoredLexical = 1 }
public enum ImportDiagnosticSeverity { Information = 0, Warning = 1, Blocking = 2 }

[Flags]
public enum ImportNegativeSyntax
{
    None = 0,
    LeadingMinus = 1,
    Parentheses = 2,
    TrailingMinus = 4
}

public enum SourceValueKind
{
    Missing = 0,
    Empty = 1,
    Text = 2,
    Number = 3,
    DateSerial = 4,
    FormulaWithCachedValue = 5,
    FormulaWithoutCachedValue = 6,
    Error = 7
}

public static class ImportRules
{
    public static DomainRuleException Error(
        string code,
        string messageKey,
        params DiagnosticArgument[] arguments) =>
        new(new DomainDiagnostic(code, messageKey, arguments));
}

public sealed record CsvSourceDialect
{
    public CsvSourceDialect(string encodingName, char delimiter, char quote, CsvBomPolicy bomPolicy)
    {
        if (string.IsNullOrWhiteSpace(encodingName) || delimiter == quote ||
            delimiter is '\r' or '\n' || quote is '\r' or '\n' || !Enum.IsDefined(bomPolicy))
            throw ImportRules.Error("IMP_PROFILE_INVALID", "Import.InvalidProfile");
        EncodingName = encodingName;
        Delimiter = delimiter;
        Quote = quote;
        BomPolicy = bomPolicy;
    }

    public string EncodingName { get; }
    public char Delimiter { get; }
    public char Quote { get; }
    public CsvBomPolicy BomPolicy { get; }
}

public sealed record SourceNumberFormat
{
    public SourceNumberFormat(
        string decimalSeparator,
        string? groupSeparator,
        IReadOnlyList<int> groupSizes,
        ImportNegativeSyntax negativeSyntax,
        bool allowLeadingPlus = false)
    {
        ArgumentNullException.ThrowIfNull(groupSizes);
        if (string.IsNullOrEmpty(decimalSeparator) || decimalSeparator.Length != 1 ||
            groupSeparator is { Length: not 1 } ||
            string.Equals(decimalSeparator, groupSeparator, StringComparison.Ordinal))
            throw ImportRules.Error("IMP_AMBIGUOUS_FORMAT", "Import.AmbiguousFormat");

        var knownFlags = ImportNegativeSyntax.LeadingMinus | ImportNegativeSyntax.Parentheses | ImportNegativeSyntax.TrailingMinus;
        if ((negativeSyntax & ~knownFlags) != 0 ||
            groupSeparator is null && groupSizes.Count != 0 ||
            groupSeparator is not null && (groupSizes.Count == 0 || groupSizes.Any(size => size is < 1 or > 9)))
            throw ImportRules.Error("IMP_PROFILE_INVALID", "Import.InvalidProfile");

        DecimalSeparator = decimalSeparator;
        GroupSeparator = groupSeparator;
        GroupSizes = FrozenList.Copy(groupSizes);
        NegativeSyntax = negativeSyntax;
        AllowLeadingPlus = allowLeadingPlus;
    }

    public string DecimalSeparator { get; }
    public string? GroupSeparator { get; }
    public IReadOnlyList<int> GroupSizes { get; }
    public ImportNegativeSyntax NegativeSyntax { get; }
    public bool AllowLeadingPlus { get; }
}

public sealed record ImportColumnMap
{
    public ImportColumnMap(int account, int? description = null, int? amount = null, int? debit = null, int? credit = null)
    {
        var values = new[] { account, description ?? 0, amount ?? 0, debit ?? 0, credit ?? 0 }.Where(value => value != 0).ToArray();
        if (account < 1 || values.Any(value => value < 1) || values.Distinct().Count() != values.Length)
            throw ImportRules.Error("IMP_PROFILE_INVALID", "Import.InvalidProfile");
        Account = account;
        Description = description;
        Amount = amount;
        Debit = debit;
        Credit = credit;
    }

    public int Account { get; }
    public int? Description { get; }
    public int? Amount { get; }
    public int? Debit { get; }
    public int? Credit { get; }
}

public sealed record ImportAccountSelection
{
    public ImportAccountSelection(ImportAccountSelectionKind kind, IReadOnlyList<string> accounts)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        if (!Enum.IsDefined(kind) ||
            accounts.Any(string.IsNullOrWhiteSpace) ||
            accounts.Distinct(StringComparer.Ordinal).Count() != accounts.Count ||
            kind == ImportAccountSelectionKind.All && accounts.Count != 0 ||
            kind == ImportAccountSelectionKind.ExplicitList && accounts.Count == 0)
            throw ImportRules.Error("IMP_PROFILE_INVALID", "Import.InvalidProfile");
        Kind = kind;
        Accounts = FrozenList.Copy(accounts);
    }

    public ImportAccountSelectionKind Kind { get; }
    public IReadOnlyList<string> Accounts { get; }

    public static ImportAccountSelection All { get; } = new(ImportAccountSelectionKind.All, []);
}

public sealed record ImportProfileApproval
{
    public ImportProfileApproval(ImportProfileApprovalState state, string reference)
    {
        if (!Enum.IsDefined(state) || string.IsNullOrWhiteSpace(reference))
            throw ImportRules.Error("IMP_PROFILE_INVALID", "Import.InvalidProfile");
        State = state;
        Reference = reference;
    }

    public ImportProfileApprovalState State { get; }
    public string Reference { get; }
}

public sealed record SourceParseProfileVersion
{
    public const string CurrentSchema = "valdrio-source-parse-profile-v1";

    public SourceParseProfileVersion(
        string schemaVersion,
        EntityId id,
        int version,
        string name,
        ImportSourceKind sourceKind,
        string? worksheetName,
        int headerRow,
        int firstDataRow,
        ImportColumnMap columns,
        CsvSourceDialect? csv,
        SourceNumberFormat numberFormat,
        string datePattern,
        ImportAmountMode amountMode,
        ImportSignConvention signConvention,
        BusinessDate periodStart,
        BusinessDate periodEnd,
        CurrencyCode? fixedCurrency,
        int? currencyColumn,
        ImportDuplicatePolicy duplicatePolicy,
        ImportZeroPolicy zeroPolicy,
        ImportAccountSelection accountSelection,
        ImportProfileApproval approval,
        bool signConventionConfirmed,
        string? cultureTag = null,
        XlsxNumericAccountPolicy xlsxNumericAccountPolicy = XlsxNumericAccountPolicy.Reject)
    {
        id.EnsureValid();
        if (schemaVersion != CurrentSchema || version < 1 || string.IsNullOrWhiteSpace(name) ||
            !Enum.IsDefined(sourceKind) || !Enum.IsDefined(amountMode) || !Enum.IsDefined(signConvention) ||
            !Enum.IsDefined(duplicatePolicy) || !Enum.IsDefined(zeroPolicy) ||
            !Enum.IsDefined(xlsxNumericAccountPolicy) ||
            headerRow < 1 || firstDataRow <= headerRow || string.IsNullOrWhiteSpace(datePattern) ||
            periodStart.Value > periodEnd.Value ||
            (fixedCurrency.HasValue == currencyColumn.HasValue) ||
            currencyColumn is < 1 ||
            sourceKind == ImportSourceKind.Csv && (csv is null || worksheetName is not null) ||
            sourceKind == ImportSourceKind.Xlsx && (csv is not null || string.IsNullOrWhiteSpace(worksheetName)) ||
            fixedCurrency is { IsValid: false })
            throw ImportRules.Error("IMP_PROFILE_INVALID", "Import.InvalidProfile");

        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(numberFormat);
        ArgumentNullException.ThrowIfNull(accountSelection);
        ArgumentNullException.ThrowIfNull(approval);

        if (signConvention == ImportSignConvention.SignedAmount &&
            (columns.Amount is null || columns.Debit is not null || columns.Credit is not null) ||
            signConvention == ImportSignConvention.DebitMinusCredit &&
            (columns.Amount is not null || columns.Debit is null || columns.Credit is null))
            throw ImportRules.Error("IMP_PROFILE_INVALID", "Import.InvalidProfile");

        var occupied = new[] { columns.Account, columns.Description ?? 0, columns.Amount ?? 0, columns.Debit ?? 0, columns.Credit ?? 0, currencyColumn ?? 0 }
            .Where(value => value != 0)
            .ToArray();
        if (occupied.Distinct().Count() != occupied.Length)
            throw ImportRules.Error("IMP_PROFILE_INVALID", "Import.InvalidProfile");

        SchemaVersion = schemaVersion;
        Id = id;
        Version = version;
        Name = name;
        SourceKind = sourceKind;
        WorksheetName = worksheetName;
        HeaderRow = headerRow;
        FirstDataRow = firstDataRow;
        Columns = columns;
        Csv = csv;
        NumberFormat = numberFormat;
        DatePattern = datePattern;
        AmountMode = amountMode;
        SignConvention = signConvention;
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        FixedCurrency = fixedCurrency;
        CurrencyColumn = currencyColumn;
        DuplicatePolicy = duplicatePolicy;
        ZeroPolicy = zeroPolicy;
        AccountSelection = accountSelection;
        Approval = approval;
        SignConventionConfirmed = signConventionConfirmed;
        CultureTag = cultureTag;
        XlsxNumericAccountPolicy = xlsxNumericAccountPolicy;
    }

    public string SchemaVersion { get; }
    public EntityId Id { get; }
    public int Version { get; }
    public string Name { get; }
    public ImportSourceKind SourceKind { get; }
    public string? WorksheetName { get; }
    public int HeaderRow { get; }
    public int FirstDataRow { get; }
    public ImportColumnMap Columns { get; }
    public CsvSourceDialect? Csv { get; }
    public SourceNumberFormat NumberFormat { get; }
    public string DatePattern { get; }
    public ImportAmountMode AmountMode { get; }
    public ImportSignConvention SignConvention { get; }
    public BusinessDate PeriodStart { get; }
    public BusinessDate PeriodEnd { get; }
    public CurrencyCode? FixedCurrency { get; }
    public int? CurrencyColumn { get; }
    public ImportDuplicatePolicy DuplicatePolicy { get; }
    public ImportZeroPolicy ZeroPolicy { get; }
    public ImportAccountSelection AccountSelection { get; }
    public ImportProfileApproval Approval { get; }
    public bool SignConventionConfirmed { get; }
    public string? CultureTag { get; }
    public XlsxNumericAccountPolicy XlsxNumericAccountPolicy { get; }
}

public sealed record SourceCell(
    int Column,
    SourceValueKind Kind,
    string? RawLexical,
    string? TextValue,
    bool WasQuoted,
    string? Formula = null,
    SourceValueKind? CachedValueKind = null,
    BusinessDate? DateValue = null)
{
    public static SourceCell Missing(int column) => new(column, SourceValueKind.Missing, null, null, false);
}

public sealed class SourceRow
{
    public SourceRow(int recordNumber, int physicalStartLine, IReadOnlyList<SourceCell> cells)
    {
        if (recordNumber < 1 || physicalStartLine < 1 || cells is null ||
            cells.Select(cell => cell.Column).Distinct().Count() != cells.Count)
            throw ImportRules.Error("IMP_SOURCE_INVALID", "Import.InvalidSource");
        RecordNumber = recordNumber;
        PhysicalStartLine = physicalStartLine;
        Cells = FrozenList.Copy(cells.OrderBy(cell => cell.Column));
    }

    public int RecordNumber { get; }
    public int PhysicalStartLine { get; }
    public IReadOnlyList<SourceCell> Cells { get; }

    public SourceCell GetCell(int column) =>
        Cells.FirstOrDefault(cell => cell.Column == column) ?? SourceCell.Missing(column);
}

public sealed class ImportSourceDocument
{
    public ImportSourceDocument(
        ImportSourceKind sourceKind,
        string fileName,
        string fileHash,
        string? sheetName,
        IReadOnlyList<string> headers,
        IReadOnlyList<SourceRow> rows,
        XlsxDateSystem? xlsxDateSystem = null)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(rows);
        if (!Enum.IsDefined(sourceKind) || string.IsNullOrWhiteSpace(fileName) ||
            fileHash.Length != 64 || fileHash.Any(character => !Uri.IsHexDigit(character)) ||
            sourceKind == ImportSourceKind.Csv && xlsxDateSystem is not null ||
            sourceKind == ImportSourceKind.Xlsx && xlsxDateSystem is null)
            throw ImportRules.Error("IMP_SOURCE_INVALID", "Import.InvalidSource");
        SourceKind = sourceKind;
        FileName = fileName;
        FileHash = fileHash.ToLowerInvariant();
        SheetName = sheetName;
        Headers = FrozenList.Copy(headers);
        Rows = FrozenList.Copy(rows);
        XlsxDateSystem = xlsxDateSystem;
    }

    public ImportSourceKind SourceKind { get; }
    public string FileName { get; }
    public string FileHash { get; }
    public string? SheetName { get; }
    public IReadOnlyList<string> Headers { get; }
    public IReadOnlyList<SourceRow> Rows { get; }
    public XlsxDateSystem? XlsxDateSystem { get; }
}

public interface IImportAdapter
{
    ImportSourceKind SourceKind { get; }

    Task<ImportSourceDocument> ReadAsync(
        Stream source,
        string fileName,
        SourceParseProfileVersion profile,
        CancellationToken cancellationToken = default);
}

public sealed record ImportFieldLocation(
    string FileName,
    string? SheetName,
    int? RecordNumber,
    int? PhysicalLine,
    int? Column);

public sealed record ImportDiagnostic(
    DomainDiagnostic Diagnostic,
    ImportDiagnosticSeverity Severity,
    ImportFieldLocation Location);

public sealed record SourceAmountParseResult(decimal? Value, DomainDiagnostic? Diagnostic)
{
    public bool IsValid => Value.HasValue && Diagnostic is null;
}

public static class SourceAmountParser
{
    private static readonly BigInteger MaximumCoefficient = (BigInteger.One << 96) - BigInteger.One;

    public static SourceAmountParseResult Parse(string? text, SourceNumberFormat? format)
    {
        if (format is null)
            return Failure("IMP_AMBIGUOUS_FORMAT", "Import.AmbiguousFormat", text);

        if (string.IsNullOrEmpty(text))
            return Failure("IMP_INVALID_AMOUNT", "Import.InvalidAmount", text);

        var body = text;
        var negative = false;

        if (body.StartsWith("(", StringComparison.Ordinal) || body.EndsWith(")", StringComparison.Ordinal))
        {
            if (!(body.StartsWith("(", StringComparison.Ordinal) && body.EndsWith(")", StringComparison.Ordinal)))
                return Failure("IMP_INVALID_AMOUNT", "Import.InvalidAmount", text);
            if ((format.NegativeSyntax & ImportNegativeSyntax.Parentheses) == 0)
                return Failure("IMP_SIGN_UNCONFIRMED", "Import.SignUnconfirmed", text);
            negative = true;
            body = body[1..^1];
        }
        else if (body.StartsWith("-", StringComparison.Ordinal))
        {
            if ((format.NegativeSyntax & ImportNegativeSyntax.LeadingMinus) == 0)
                return Failure("IMP_SIGN_UNCONFIRMED", "Import.SignUnconfirmed", text);
            negative = true;
            body = body[1..];
        }
        else if (body.EndsWith("-", StringComparison.Ordinal))
        {
            if ((format.NegativeSyntax & ImportNegativeSyntax.TrailingMinus) == 0)
                return Failure("IMP_SIGN_UNCONFIRMED", "Import.SignUnconfirmed", text);
            negative = true;
            body = body[..^1];
        }
        else if (body.StartsWith("+", StringComparison.Ordinal))
        {
            if (!format.AllowLeadingPlus)
                return Failure("IMP_SIGN_UNCONFIRMED", "Import.SignUnconfirmed", text);
            body = body[1..];
        }

        if (body.Length == 0 || body.Contains('e') || body.Contains('E') ||
            body.Contains('+') || body.Contains('-') || body.Contains('(') || body.Contains(')'))
            return Failure("IMP_INVALID_AMOUNT", "Import.InvalidAmount", text);

        var decimalIndex = body.IndexOf(format.DecimalSeparator, StringComparison.Ordinal);
        if (decimalIndex >= 0 && body.IndexOf(format.DecimalSeparator, decimalIndex + format.DecimalSeparator.Length, StringComparison.Ordinal) >= 0)
            return Failure("IMP_INVALID_AMOUNT", "Import.InvalidAmount", text);

        var integerPart = decimalIndex < 0 ? body : body[..decimalIndex];
        var fractionalPart = decimalIndex < 0 ? "" : body[(decimalIndex + format.DecimalSeparator.Length)..];
        if (integerPart.Length == 0 || decimalIndex >= 0 && fractionalPart.Length == 0)
            return Failure("IMP_INVALID_AMOUNT", "Import.InvalidAmount", text);

        if (format.GroupSeparator is not null && fractionalPart.Contains(format.GroupSeparator, StringComparison.Ordinal))
            return Failure("IMP_INVALID_AMOUNT", "Import.InvalidAmount", text);

        string integerDigits;
        if (format.GroupSeparator is null || !integerPart.Contains(format.GroupSeparator, StringComparison.Ordinal))
        {
            integerDigits = integerPart;
        }
        else
        {
            var groups = integerPart.Split(format.GroupSeparator, StringSplitOptions.None);
            if (groups.Any(group => group.Length == 0 || !AsciiDigits(group)))
                return Failure("IMP_INVALID_AMOUNT", "Import.InvalidAmount", text);

            for (var index = groups.Length - 1, position = 0; index > 0; index--, position++)
            {
                var expected = format.GroupSizes[Math.Min(position, format.GroupSizes.Count - 1)];
                if (groups[index].Length != expected)
                    return Failure("IMP_INVALID_AMOUNT", "Import.InvalidAmount", text);
            }

            var leftMaximum = format.GroupSizes[Math.Min(groups.Length - 2, format.GroupSizes.Count - 1)];
            if (groups[0].Length > leftMaximum)
                return Failure("IMP_INVALID_AMOUNT", "Import.InvalidAmount", text);
            integerDigits = string.Concat(groups);
        }

        if (!AsciiDigits(integerDigits) || fractionalPart.Length != 0 && !AsciiDigits(fractionalPart) || fractionalPart.Length > 28)
            return Failure("IMP_INVALID_AMOUNT", "Import.InvalidAmount", text);

        var digits = integerDigits + fractionalPart;
        if (!BigInteger.TryParse(digits, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var coefficient) ||
            coefficient < BigInteger.Zero || coefficient > MaximumCoefficient)
            return Failure("IMP_INVALID_AMOUNT", "Import.InvalidAmount", text);

        var lo = (uint)(coefficient & uint.MaxValue);
        var mid = (uint)((coefficient >> 32) & uint.MaxValue);
        var hi = (uint)((coefficient >> 64) & uint.MaxValue);
        var value = new decimal(unchecked((int)lo), unchecked((int)mid), unchecked((int)hi), negative, (byte)fractionalPart.Length);
        return new(value, null);
    }

    private static bool AsciiDigits(string value) =>
        value.Length != 0 && value.All(character => character is >= '0' and <= '9');

    private static SourceAmountParseResult Failure(string code, string key, string? value) =>
        new(null, new DomainDiagnostic(code, key,
        [
            new DiagnosticArgument("value", DiagnosticArgumentType.Text, value ?? "<missing>")
        ]));
}

public sealed record XlsxDateSerialParseResult(BusinessDate? Value, DomainDiagnostic? Diagnostic)
{
    public bool IsValid => Value.HasValue && Diagnostic is null;
}

public static class XlsxDateSerialParser
{
    public static XlsxDateSerialParseResult Parse(string? rawLexical, XlsxDateSystem dateSystem)
    {
        if (!Enum.IsDefined(dateSystem) || string.IsNullOrWhiteSpace(rawLexical) ||
            !decimal.TryParse(
                rawLexical,
                System.Globalization.NumberStyles.AllowLeadingSign | System.Globalization.NumberStyles.AllowDecimalPoint,
                System.Globalization.CultureInfo.InvariantCulture,
                out var serial) ||
            decimal.Truncate(serial) != serial ||
            serial < 0m ||
            serial > int.MaxValue)
            return Failure("IMP_XLSX_DATE_INVALID", "Import.XlsxDateInvalid", rawLexical);

        var days = decimal.ToInt32(serial);
        if (dateSystem == XlsxDateSystem.Date1900 && days == 60)
            return Failure("IMP_XLSX_DATE_SERIAL_60", "Import.XlsxDateSerial60", rawLexical);

        try
        {
            var value = dateSystem switch
            {
                XlsxDateSystem.Date1900 when days < 60 =>
                    new BusinessDate(new DateOnly(1899, 12, 31).AddDays(days)),
                XlsxDateSystem.Date1900 =>
                    new BusinessDate(new DateOnly(1899, 12, 31).AddDays(days - 1)),
                XlsxDateSystem.Date1904 =>
                    new BusinessDate(new DateOnly(1904, 1, 1).AddDays(days)),
                _ => throw new ArgumentOutOfRangeException(nameof(dateSystem))
            };
            return new(value, null);
        }
        catch (ArgumentOutOfRangeException)
        {
            return Failure("IMP_XLSX_DATE_INVALID", "Import.XlsxDateInvalid", rawLexical);
        }
    }

    private static XlsxDateSerialParseResult Failure(string code, string key, string? value) =>
        new(null, new DomainDiagnostic(code, key,
        [
            new DiagnosticArgument("value", DiagnosticArgumentType.Text, value ?? "<missing>")
        ]));
}

public static class ImportSourceInspector
{
    public static IReadOnlyList<ImportDiagnostic> ValidateBasic(
        ImportSourceDocument source,
        SourceParseProfileVersion profile)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(profile);
        if (source.SourceKind != profile.SourceKind)
            throw ImportRules.Error("IMP_PROFILE_INVALID", "Import.InvalidProfile");

        var diagnostics = new List<ImportDiagnostic>();
        var accounts = new Dictionary<string, SourceRow>(StringComparer.Ordinal);

        foreach (var row in source.Rows)
        {
            if (source.SourceKind == ImportSourceKind.Xlsx)
            {
                foreach (var column in MappedColumns(profile))
                {
                    var mapped = row.GetCell(column);
                    if (mapped.Kind == SourceValueKind.FormulaWithoutCachedValue)
                        diagnostics.Add(Diagnostic(
                            "IMP_XLSX_FORMULA_NO_CACHE",
                            "Import.XlsxFormulaNoCache",
                            source,
                            row,
                            column,
                            mapped.Formula ?? "<formula>"));
                    else if (mapped.Kind == SourceValueKind.Error ||
                             mapped.Kind == SourceValueKind.FormulaWithCachedValue &&
                             mapped.CachedValueKind == SourceValueKind.Error)
                        diagnostics.Add(Diagnostic(
                            "IMP_XLSX_CELL_ERROR",
                            "Import.XlsxCellError",
                            source,
                            row,
                            column,
                            mapped.RawLexical ?? "<error>"));
                }
            }

            var cell = row.GetCell(profile.Columns.Account);
            if (source.SourceKind == ImportSourceKind.Xlsx &&
                (cell.Kind == SourceValueKind.Number ||
                 cell.Kind == SourceValueKind.DateSerial ||
                 cell.Kind == SourceValueKind.FormulaWithCachedValue &&
                 cell.CachedValueKind is SourceValueKind.Number or SourceValueKind.DateSerial) &&
                profile.XlsxNumericAccountPolicy == XlsxNumericAccountPolicy.Reject)
            {
                diagnostics.Add(Diagnostic(
                    "IMP_XLSX_NUMERIC_ACCOUNT",
                    "Import.XlsxNumericAccount",
                    source,
                    row,
                    profile.Columns.Account,
                    cell.RawLexical ?? "<number>"));
                continue;
            }

            if (cell.Kind is SourceValueKind.Missing or SourceValueKind.Empty ||
                cell.Kind == SourceValueKind.FormulaWithoutCachedValue ||
                cell.Kind == SourceValueKind.Error ||
                string.IsNullOrEmpty(cell.TextValue))
            {
                if (cell.Kind is SourceValueKind.Missing or SourceValueKind.Empty)
                    diagnostics.Add(Diagnostic(
                        "IMP_ACCOUNT_REQUIRED",
                        "Import.AccountRequired",
                        source,
                        row,
                        profile.Columns.Account,
                        cell.TextValue ?? "<missing>"));
                continue;
            }

            var account = cell.TextValue;
            if (profile.DuplicatePolicy == ImportDuplicatePolicy.Reject && accounts.TryGetValue(account, out var first))
            {
                diagnostics.Add(new ImportDiagnostic(
                    new DomainDiagnostic(
                        "IMP_DUPLICATE_ACCOUNT",
                        "Import.DuplicateAccount",
                        [
                            new DiagnosticArgument("account", DiagnosticArgumentType.Text, account),
                            new DiagnosticArgument("firstRecord", DiagnosticArgumentType.Integer,
                                first.RecordNumber.ToString(System.Globalization.CultureInfo.InvariantCulture))
                        ]),
                    ImportDiagnosticSeverity.Blocking,
                    new ImportFieldLocation(source.FileName, source.SheetName, row.RecordNumber, row.PhysicalStartLine, profile.Columns.Account)));
            }
            else
            {
                accounts[account] = row;
            }
        }

        return FrozenList.Copy(diagnostics);
    }

    private static IReadOnlyList<int> MappedColumns(SourceParseProfileVersion profile) =>
        new[]
        {
            profile.Columns.Account,
            profile.Columns.Description ?? 0,
            profile.Columns.Amount ?? 0,
            profile.Columns.Debit ?? 0,
            profile.Columns.Credit ?? 0,
            profile.CurrencyColumn ?? 0
        }
        .Where(column => column > 0)
        .Distinct()
        .ToArray();

    private static ImportDiagnostic Diagnostic(
        string code,
        string key,
        ImportSourceDocument source,
        SourceRow row,
        int column,
        string value) =>
        new(
            new DomainDiagnostic(code, key,
            [
                new DiagnosticArgument("value", DiagnosticArgumentType.Text, value)
            ]),
            ImportDiagnosticSeverity.Blocking,
            new ImportFieldLocation(source.FileName, source.SheetName, row.RecordNumber, row.PhysicalStartLine, column));
}
