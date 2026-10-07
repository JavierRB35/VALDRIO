using Valdrio.Domain;
using System.Text.Json.Serialization;

namespace Valdrio.Application;

public sealed record ImportComparisonLine(string Code, Money Earlier, Money Current, Money Difference, IReadOnlyList<int> EarlierRecords);
// Both original documents and their independent parsing rules are frozen in the profile.
public sealed class ImportComparison
{
    public ImportComparison(ImportSourceDocument source, SourceParseProfileVersion profile,
        BusinessDate fiscalYearStart, BusinessDate fiscalYearEnd, string scopeReference)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(profile);
        if (profile.Comparison is not null || profile.Grouping is null ||
            profile.AmountMode != ImportAmountMode.ClosingBalanceAsStudyAmount ||
            fiscalYearStart.Value > fiscalYearEnd.Value || fiscalYearEnd.Value >= fiscalYearStart.Value.AddYears(1) ||
            string.IsNullOrWhiteSpace(scopeReference))
            throw ImportRules.Error("IMP_COMPARISON_INVALID", "Import.ComparisonInvalid");
        Source = source; Profile = profile; FiscalYearStart = fiscalYearStart; FiscalYearEnd = fiscalYearEnd; ScopeReference = scopeReference;
    }
    public ImportSourceDocument Source { get; }
    public SourceParseProfileVersion Profile { get; }
    public BusinessDate FiscalYearStart { get; }
    public BusinessDate FiscalYearEnd { get; }
    public string ScopeReference { get; }
    [JsonIgnore] public string DuplicateFingerprint => PublishedModelStructure.Hash(CanonicalSnapshotJson.Serialize(new
    {
        Source.FileHash, Profile.SourceKind, Profile.WorksheetName, Profile.HeaderRow, Profile.FirstDataRow,
        Profile.Columns, Profile.Csv, Profile.NumberFormat, Profile.DatePattern, Profile.AmountMode, Profile.SignConvention,
        Profile.PeriodStart, Profile.PeriodEnd, Profile.FixedCurrency, Profile.CurrencyColumn, Profile.DuplicatePolicy,
        Profile.ZeroPolicy, Profile.AccountSelection, Profile.XlsxNumericAccountPolicy, Profile.Grouping,
        FiscalYearStart, FiscalYearEnd
    }));

    internal IReadOnlyList<PreparedImportGroup> Difference(StudyDefinition study, PrecisionPolicy precision,
        SourceParseProfileVersion current, IReadOnlyList<PreparedImportGroup> groups,
        List<ImportDiagnostic> diagnostics, CancellationToken token, out IReadOnlyList<ImportComparisonLine> comparisonRows)
    {
        comparisonRows = [];
        void Block(string code, string key) => diagnostics.Add(new(new(code, key, []), ImportDiagnosticSeverity.Blocking,
            new(Source.FileName, Source.SheetName, null, null, null)));
        if (Profile.PeriodStart != FiscalYearStart || Profile.PeriodEnd.Value.AddDays(1) != study.PeriodStart.Value ||
            Profile.PeriodEnd.Value < FiscalYearStart.Value || study.PeriodEnd.Value > FiscalYearEnd.Value ||
            Profile.FixedCurrency != current.FixedCurrency || Profile.CurrencyColumn is not null ||
            Profile.SignConvention != current.SignConvention)
        { Block("IMP_COMPARISON_INVALID", "Import.ComparisonInvalid"); return []; }
        var previousContext = new StudyDefinition(study.Id, study.CompanyId, study.Name, Profile.PeriodStart, Profile.PeriodEnd,
            study.Purpose, study.ScenarioKind, null, study.SelectedModelVersionId, study.Currency, study.CurrencyValidity,
            study.Revision, study.State, null, null, study.CreatedAtUtc);
        var previous = PreparedImport.Create(new EntityId(Guid.Parse("00000000-0000-0000-0000-000000000001")),
            previousContext, precision, Source, Profile, token);
        diagnostics.AddRange(previous.Diagnostics);
        if (!previous.CanConfirm) return [];
        var lookup = previous.Groups!.ToDictionary(g => g.Code, StringComparer.Ordinal);
        if (!lookup.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(groups.Select(g => g.Code)))
        { Block("IMP_COMPARISON_MISSING", "Import.ComparisonMissing"); return []; }
        var result = new List<PreparedImportGroup>();
        var details = new List<ImportComparisonLine>();
        foreach (var group in groups)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var earlier = lookup[group.Code];
                var difference = new Money(checked(group.Amount.Amount - earlier.Amount.Amount), group.Amount.Currency, precision);
                result.Add(new(group.Code, difference, group.Records));
                details.Add(new(group.Code, earlier.Amount, group.Amount, difference, earlier.Records));
            }
            catch (OverflowException) { Block("IMP_TOTAL_OVERFLOW", "Import.TotalOverflow"); }
            catch (DomainRuleException ex) { diagnostics.Add(new(ex.Diagnostic, ImportDiagnosticSeverity.Blocking, new(Source.FileName, Source.SheetName, null, null, null))); }
        }
        comparisonRows = FrozenList.Copy(details);
        return FrozenList.Copy(result);
    }
}
