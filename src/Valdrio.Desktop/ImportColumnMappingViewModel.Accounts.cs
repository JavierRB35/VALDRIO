using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Valdrio.Application;

namespace Valdrio.Desktop;

public sealed record ImportSelectionGroup(string Code, IReadOnlyList<int> Records, decimal Amount, string Description = "");
public sealed record ImportSelectionTotals(int SelectedRows, int ExcludedRows, int Groups,
    decimal Positive, decimal Negative, decimal Net, decimal ExcludedNet);

public sealed class ImportAccountChoice(string account, decimal amount, int recordNumber, string description = "") : ObservableObject
{
    private bool included;
    private string rateId = "";
    public string RateId { get => rateId; set => SetProperty(ref rateId, value); }
    private string formattedAmount = "";
    public string Account { get; } = account;
    public string Description { get; } = description;
    public decimal Amount { get; } = amount;
    public int RecordNumber { get; } = recordNumber;
    public bool Included { get => included; set => SetProperty(ref included, value); }
    public string FormattedAmount { get => formattedAmount; private set => SetProperty(ref formattedAmount, value); }
    internal void Reformat(CultureInfo culture) => FormattedAmount = Amount.ToString(culture);
}

public sealed partial class ImportColumnMappingViewModel
{
    private bool importAllAccounts = true;
    private bool selectingRecords;
    private IReadOnlyList<ImportAccountChoice> accountChoices = [];
    private IReadOnlyList<ImportSelectionGroup> selectionGroups = [];
    public IReadOnlyList<ImportSelectionGroup> SelectionGroups => selectionGroups;
    public ImportSelectionTotals? SelectionTotals { get; private set; }
    public bool SelectionOverflow { get; private set; }
    public IReadOnlyList<ImportGroupPreview> SelectionPreview => selectionGroups.Select(g => new ImportGroupPreview(
        g.Code, string.Join(", ", g.Records), g.Amount.ToString(CultureInfo.GetCultureInfo(preferences.Current.FormatCultureTag)), g.Description)
        { NumericAmount = g.Amount }).ToArray();
    public string SelectionReconciliation
    {
        get
        {
            if (international) return this["InternationalSelectionTotals"];
            if (SelectionOverflow) return this["SelectionOverflow"];
            if (SelectionTotals is not { } totals) return "";
            var culture = CultureInfo.GetCultureInfo(preferences.Current.FormatCultureTag);
            return string.Format(culture, this["SelectionReconciliation"], totals.SelectedRows, totals.ExcludedRows,
                totals.Groups, totals.Positive.ToString(culture), totals.Negative.ToString(culture),
                totals.Net.ToString(culture), totals.ExcludedNet.ToString(culture));
        }
    }

    private void RecalculateSelection()
    {
        selectionGroups = []; SelectionTotals = null; SelectionOverflow = false;
        if (accountChoices.Count == 0 || international) return;
        try
        {
            var selected = accountChoices.Where(c => c.Included).ToArray();
            var groups = selected.GroupBy(c => c.Account, StringComparer.Ordinal)
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => new ImportSelectionGroup(g.Key, Array.AsReadOnly(g.Select(c => c.RecordNumber).ToArray()),
                    g.Sum(c => c.Amount), Describe(g.Select(c => c.Description)))).ToArray();
            var positive = selected.Where(c => c.Amount > 0).Sum(c => c.Amount);
            var negative = selected.Where(c => c.Amount < 0).Sum(c => c.Amount);
            var excluded = accountChoices.Where(c => !c.Included).Sum(c => c.Amount);
            SelectionTotals = new(selected.Length, accountChoices.Count - selected.Length, groups.Length,
                positive, negative, checked(positive + negative), excluded);
            selectionGroups = Array.AsReadOnly(groups);
        }
        catch (OverflowException) { SelectionOverflow = true; }
    }
    // Account code remains the identity. Preserve distinct source descriptions in the review.
    private string Describe(IEnumerable<string> values)
    {
        var names = values.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.Ordinal).ToArray();
        return names.Length == 0 ? this["DescriptionMissing"] : string.Join(" / ", names);
    }
    private string DescriptionForCode(string code) => Describe(accountChoices
        .Where(row => row.Account == code).Select(row => row.Description));
    public IReadOnlyList<ImportAccountChoice> AccountChoices => accountChoices;
    public bool ImportAllAccounts
    {
        get => importAllAccounts;
        set
        {
            importAllAccounts = value;
            selectingRecords = true;
            try { foreach (var choice in accountChoices) choice.Included = value; }
            finally { selectingRecords = false; }
            RecalculateSelection(); cancellation?.Cancel(); InvalidatePreparation(true); Refresh();
        }
    }
    public bool IsExplicitAccountSelection => !importAllAccounts;
    public bool HasAccountChoices => accountChoices.Count > 0;
    public bool IsAccountSelectionReady => accountChoices.Any(c => c.Included);
    public string AccountSelectionSummary => string.Format(Ui, this["AccountSelectionSummary"],
        accountChoices.Count(c => c.Included), accountChoices.Count);

    public IRelayCommand SelectAllRecordsCommand { get; private set; } = null!;
    public IRelayCommand DeselectAllRecordsCommand { get; private set; } = null!;
    private ImportGroupingPlan SelectedRecords() => new(accountChoices.Where(c => c.Included)
        .Select(c => new ImportRecordTarget(c.RecordNumber, c.Account)).ToArray());

    private void ResetAccountChoices()
    {
        foreach (var choice in accountChoices) choice.PropertyChanged -= AccountChoiceChanged;
        accountChoices = [];
        RecalculateSelection();
        // Keep the user's all/explicit mode, but never carry account choices into a different mapping/source.
    }
    private void SetAccountChoices(ImportColumnSelectionResult result)
    {
        ResetAccountChoices();
        if (!result.IsValid) return;
        var culture = CultureInfo.GetCultureInfo(preferences.Current.FormatCultureTag);
        accountChoices = result.Rows.Select(r =>
        {
            var sourceRow = result.Source.Rows.FirstOrDefault(row => row.RecordNumber == r.RecordNumber);
            var description = descriptionColumn is { } column && sourceRow is not null
                ? sourceRow.GetCell(column).TextValue ?? "" : "";
            return new ImportAccountChoice(r.Account!, r.Amount!.Value, r.RecordNumber, description)
                { Included = importAllAccounts };
        }).ToArray();
        foreach (var choice in accountChoices)
        {
            choice.Reformat(culture);
            choice.PropertyChanged += AccountChoiceChanged;
        }
        RecalculateSelection();
    }
    private void AccountChoiceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (selectingRecords || e.PropertyName is not (nameof(ImportAccountChoice.Included) or nameof(ImportAccountChoice.RateId))) return;
        importAllAccounts = accountChoices.All(c => c.Included);
        RecalculateSelection();
        cancellation?.Cancel(); InvalidatePreparation(true); Refresh();
    }
}
