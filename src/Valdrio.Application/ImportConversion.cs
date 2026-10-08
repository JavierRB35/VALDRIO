using System.Globalization;
using Valdrio.Calculation;
using Valdrio.Domain;

namespace Valdrio.Application;

public sealed record ImportRateSelection(int RecordNumber, FxRateId RateId);

// A frozen, explicit choice, independent of display preferences or a live provider.
public sealed class ImportConversionPolicy
{
    public ImportConversionPolicy(FxRateSetVersion? rateSet, IReadOnlyList<ImportRateSelection> selections,
        IReadOnlyList<CurrencyDefinition> currencies, BusinessDate allowedRateStart, BusinessDate allowedRateEnd,
        string approvalReference, int? dateColumn = null)
    {
        ArgumentNullException.ThrowIfNull(selections); ArgumentNullException.ThrowIfNull(currencies);
        if (allowedRateStart.Value > allowedRateEnd.Value || string.IsNullOrWhiteSpace(approvalReference) || dateColumn is < 1 ||
            selections.Any(s => s.RecordNumber < 1 || !s.RateId.IsValid) ||
            selections.Select(s => s.RecordNumber).Distinct().Count() != selections.Count ||
            currencies.Count == 0 || currencies.Select(c => c.AlphaCode).Distinct().Count() != currencies.Count)
            throw ImportRules.Error("IMP_CONVERSION_INVALID", "Import.ConversionInvalid");
        foreach (var currency in currencies) currency.Freeze().EnsureValid();
        RateSet = rateSet; Selections = FrozenList.Copy(selections.OrderBy(s => s.RecordNumber));
        Currencies = FrozenList.Copy(currencies.OrderBy(c => c.AlphaCode.Value, StringComparer.Ordinal));
        AllowedRateStart = allowedRateStart; AllowedRateEnd = allowedRateEnd;
        ApprovalReference = approvalReference; DateColumn = dateColumn;
    }
    public FxRateSetVersion? RateSet { get; }
    public IReadOnlyList<ImportRateSelection> Selections { get; }
    public IReadOnlyList<CurrencyDefinition> Currencies { get; }
    public BusinessDate AllowedRateStart { get; }
    public BusinessDate AllowedRateEnd { get; }
    public string ApprovalReference { get; }
    public int? DateColumn { get; }
}

public sealed record ImportConvertedRow(int RecordNumber, BusinessDate? DocumentDate, FxRateApplication Application);
public sealed record ImportCurrencyTotal(CurrencyCode Currency, decimal Positive, decimal Negative, decimal Net);
public sealed record ImportConvertedGroup(string Code, Money Original, Money Functional, IReadOnlyList<int> Records,
    IReadOnlyList<int> EarlierRecords);
public sealed record ImportConversionBridge(IReadOnlyList<ImportConvertedRow> Rows, IReadOnlyList<ImportConvertedRow> EarlierRows,
    IReadOnlyList<ImportConvertedGroup> Groups, IReadOnlyList<ImportCurrencyTotal> OriginalTotals,
    decimal FunctionalTotal, decimal RoundingAdjustment);

public static class SourceBusinessDateParser
{
    public static BusinessDate Parse(SourceCell cell, string? pattern)
    {
        var kind = cell.Kind == SourceValueKind.FormulaWithCachedValue ? cell.CachedValueKind : cell.Kind;
        if (kind is SourceValueKind.DateSerial or SourceValueKind.DateIso && cell.DateValue is { } date) return date;
        if (kind == SourceValueKind.DateIso && cell.RawLexical is { Length: >= 10 } iso &&
            DateTime.TryParseExact(iso, ["yyyy-MM-dd", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK"],
                CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _))
            return BusinessDate.ParseIso(iso[..10]);
        if (string.IsNullOrWhiteSpace(pattern)) throw ImportRules.Error("IMP_AMBIGUOUS_FORMAT", "Import.AmbiguousFormat");
        try
        {
            if (kind == SourceValueKind.Text && DateOnly.TryParseExact(cell.TextValue, pattern,
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)) return new(parsed);
        }
        catch (FormatException) { throw ImportRules.Error("IMP_AMBIGUOUS_FORMAT", "Import.AmbiguousFormat"); }
        throw ImportRules.Error("IMP_DOCUMENT_DATE_INVALID", "Import.DocumentDateInvalid");
    }
}

internal static class ImportConversion
{
    internal sealed record Result(IReadOnlyList<PreparedAmountRow> Rows, ImportConversionBridge Bridge);
    internal static Result Prepare(StudyDefinition study, PrecisionPolicy precision, ImportSourceDocument source,
        SourceParseProfileVersion profile, List<ImportDiagnostic> diagnostics, CancellationToken token)
    {
        var policy = profile.Conversion!;
        void Error(DomainDiagnostic error, int? record = null, int? column = null) => diagnostics.Add(new(error,
            ImportDiagnosticSeverity.Blocking, new(source.FileName, source.SheetName, record,
                source.Rows.FirstOrDefault(r => r.RecordNumber == record)?.PhysicalStartLine, column)));
        void Block(string code, string key, int? record = null) => Error(new(code, key, []), record);
        var selection = ImportColumnSelection.Validate(source,
            new(profile.SourceKind, profile.WorksheetName, profile.HeaderRow, profile.FirstDataRow, profile.Csv),
            profile.Columns, profile.NumberFormat, profile.XlsxNumericAccountPolicy, token, profile.DuplicatePolicy);
        diagnostics.AddRange(selection.Diagnostics);
        diagnostics.AddRange(ImportSourceInspector.ValidateBasic(source, profile, token));
        var targets = profile.Grouping!.Records.ToDictionary(r => r.RecordNumber);
        var originals = new List<PreparedAmountRow>(); var converted = new List<ImportConvertedRow>();
        var sourceLookup = source.Rows.ToDictionary(r => r.RecordNumber);
        var currencies = policy.Currencies.ToDictionary(c => c.AlphaCode);
        var rates = policy.Selections.ToDictionary(s => s.RecordNumber, s => s.RateId);
        if (targets.Keys.Any(r => !sourceLookup.ContainsKey(r)) || rates.Keys.Any(r => !sourceLookup.ContainsKey(r)))
            Block("IMP_GROUPING_INVALID", "Import.GroupingInvalid");
        foreach (var row in selection.Rows)
        {
            token.ThrowIfCancellationRequested();
            Money? original = null; BusinessDate? date = null; var selected = targets.TryGetValue(row.RecordNumber, out var target);
            try
            {
                if (row.Debit is { } debit) precision.ValidateAmount(debit, "debit");
                if (row.Credit is { } credit) precision.ValidateAmount(credit, "credit");
                if (row.Amount is not { } amount) throw ImportRules.Error("IMP_CONVERSION_INVALID", "Import.ConversionInvalid");
                var sourceRow = sourceLookup[row.RecordNumber];
                if (policy.DateColumn is { } dateColumn) date = SourceBusinessDateParser.Parse(sourceRow.GetCell(dateColumn), profile.DatePattern);
                CurrencyCode currency;
                if (profile.FixedCurrency is { } fixedCurrency) currency = fixedCurrency;
                else
                {
                    var cell = sourceRow.GetCell(profile.CurrencyColumn!.Value);
                    var kind = cell.Kind == SourceValueKind.FormulaWithCachedValue ? cell.CachedValueKind : cell.Kind;
                    if (kind != SourceValueKind.Text || string.IsNullOrWhiteSpace(cell.TextValue))
                        throw ImportRules.Error("IMP_SOURCE_CURRENCY_INVALID", "Import.SourceCurrencyInvalid");
                    currency = new(cell.TextValue);
                }
                if (!currencies.TryGetValue(currency, out var definition) || !definition.IsMonetary ||
                    !definition.Validity.Contains(date?.Value ?? profile.PeriodEnd.Value))
                    throw ImportRules.Error("IMP_SOURCE_CURRENCY_INVALID", "Import.SourceCurrencyInvalid");
                original = new(amount, currency, precision);
                if (selected)
                {
                    if (target!.Code != row.Account) throw ImportRules.Error("IMP_GROUPING_INVALID", "Import.GroupingInvalid");
                    FxRate? rate = null;
                    if (currency != study.Currency.FunctionalCurrencyCode)
                    {
                        if (!rates.TryGetValue(row.RecordNumber, out var rateId) || policy.RateSet is null)
                            throw ImportRules.Error("FX_RATE_MISSING", "Import.ConversionRateMissing");
                        rate = policy.RateSet.GetRequired(rateId);
                        if (rate.RateDate.Value < policy.AllowedRateStart.Value || rate.RateDate.Value > policy.AllowedRateEnd.Value)
                            throw ImportRules.Error("IMP_RATE_DATE_INVALID", "Import.RateDateInvalid");
                    }
                    else if (rates.ContainsKey(row.RecordNumber))
                        throw ImportRules.Error("IMP_CONVERSION_INVALID", "Import.ConversionInvalid");
                    var application = new FxConverter().Convert("current:" + row.RecordNumber.ToString(CultureInfo.InvariantCulture),
                        original.Value, study.Currency.FunctionalCurrencyCode, rate);
                    converted.Add(new(row.RecordNumber, date, application));
                    foreach (var warning in application.Diagnostics) diagnostics.Add(new(warning, ImportDiagnosticSeverity.Warning,
                        new(source.FileName, source.SheetName, row.RecordNumber, row.PhysicalLine, profile.Columns.Amount)));
                }
            }
            catch (DomainRuleException ex) { Error(ex.Diagnostic, row.RecordNumber); selected = false; }
            catch (ArgumentException) { Block("IMP_SOURCE_CURRENCY_INVALID", "Import.SourceCurrencyInvalid", row.RecordNumber); selected = false; }
            originals.Add(new(row.RecordNumber, row.PhysicalLine, row.Account, original, selected && original.HasValue, date));
        }
        var groups = new List<ImportConvertedGroup>();
        try
        {
            foreach (var group in converted.GroupBy(r => (Code: targets[r.RecordNumber].Code, Currency: r.Application.OriginalMoney.Currency))
                .OrderBy(g => g.Key.Code, StringComparer.Ordinal).ThenBy(g => g.Key.Currency.Value, StringComparer.Ordinal))
            {
                token.ThrowIfCancellationRequested();
                groups.Add(new(group.Key.Code, new(group.Sum(r => r.Application.OriginalMoney.Amount), group.Key.Currency, precision),
                    new(group.Sum(r => r.Application.ConvertedOriginalMoney.Amount), study.Currency.FunctionalCurrencyCode, precision),
                    FrozenList.Copy(group.Select(r => r.RecordNumber)), []));
            }
        }
        catch (OverflowException) { Block("IMP_TOTAL_OVERFLOW", "Import.TotalOverflow"); }
        catch (DomainRuleException ex) { Error(ex.Diagnostic); }
        IReadOnlyList<ImportConvertedRow> earlierRows = [];
        if (profile.Comparison is { } comparison)
        {
            var previousProfile = comparison.Profile;
            if (previousProfile.Conversion is null || previousProfile.PeriodStart != comparison.FiscalYearStart ||
                previousProfile.PeriodEnd.Value.AddDays(1) != study.PeriodStart.Value ||
                study.PeriodEnd.Value > comparison.FiscalYearEnd.Value || previousProfile.SignConvention != profile.SignConvention)
                Block("IMP_COMPARISON_INVALID", "Import.ComparisonInvalid");
            else
            {
                var previousStudy = new StudyDefinition(study.Id, study.CompanyId, study.Name, previousProfile.PeriodStart,
                    previousProfile.PeriodEnd, study.Purpose, study.ScenarioKind, null, study.SelectedModelVersionId, study.Currency,
                    study.CurrencyValidity, study.Revision, study.State, null, null, study.CreatedAtUtc);
                var previous = PreparedImport.Create(new(Guid.Parse("00000000-0000-0000-0000-000000000001")), previousStudy,
                    precision, comparison.Source, previousProfile, token);
                diagnostics.AddRange(previous.Diagnostics);
                var lookup = previous.ConversionBridge!.Groups.ToDictionary(g => (g.Code, g.Original.Currency));
                earlierRows = FrozenList.Copy(previous.ConversionBridge.Rows.Select(r => r with
                    { Application = r.Application with { ApplicationId = "earlier:" + r.RecordNumber.ToString(CultureInfo.InvariantCulture) } }));
                if (!lookup.Keys.ToHashSet().SetEquals(groups.Select(g => (g.Code, g.Original.Currency))))
                    Block("IMP_COMPARISON_MISSING", "Import.ComparisonMissing");
                else
                {
                    try
                    {
                        groups = groups.Select(g =>
                        {
                            var earlier = lookup[(g.Code, g.Original.Currency)];
                            return g with { Original = new(g.Original.Amount - earlier.Original.Amount, g.Original.Currency, precision),
                                Functional = new(g.Functional.Amount - earlier.Functional.Amount, g.Functional.Currency, precision),
                                EarlierRecords = earlier.Records };
                        }).ToList();
                    }
                    catch (OverflowException) { Block("IMP_TOTAL_OVERFLOW", "Import.TotalOverflow"); }
                    catch (DomainRuleException ex) { Error(ex.Diagnostic); }
                }
            }
        }
        IReadOnlyList<ImportCurrencyTotal> totals = []; decimal functional = 0, adjustment = 0;
        try
        {
            // Period totals reconcile the selected records, before account aggregation can cancel signs.
            // Comparative totals reconcile the resulting differences, as in the historical T06 contract.
            var contributions = profile.Comparison is null ? converted.Select(r => r.Application.OriginalMoney) : groups.Select(g => g.Original);
            totals = FrozenList.Copy(contributions.GroupBy(m => m.Currency).OrderBy(g => g.Key.Value, StringComparer.Ordinal)
                .Select(g => new ImportCurrencyTotal(g.Key, g.Where(m => m.Amount > 0).Sum(m => m.Amount),
                    g.Where(m => m.Amount < 0).Sum(m => m.Amount), g.Sum(m => m.Amount))));
            functional = groups.Sum(g => g.Functional.Amount);
            adjustment = converted.Sum(r => r.Application.ConversionRoundingAdjustment.Amount) - earlierRows.Sum(r => r.Application.ConversionRoundingAdjustment.Amount);
        }
        catch (OverflowException) { Block("IMP_TOTAL_OVERFLOW", "Import.TotalOverflow"); }
        return new(FrozenList.Copy(originals), new(FrozenList.Copy(converted), earlierRows, FrozenList.Copy(groups), totals, functional, adjustment));
    }
}
