using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Valdrio.Application;

namespace Valdrio.Desktop;

public sealed class ImportAccountChoice(string account, decimal amount) : ObservableObject
{
    private bool included;
    private string formattedAmount = "";
    public string Account { get; } = account;
    public decimal Amount { get; } = amount;
    public bool Included { get => included; set => SetProperty(ref included, value); }
    public string FormattedAmount { get => formattedAmount; private set => SetProperty(ref formattedAmount, value); }
    internal void Reformat(CultureInfo culture) => FormattedAmount = Amount.ToString(culture);
}

public sealed partial class ImportColumnMappingViewModel
{
    private bool importAllAccounts = true;
    private IReadOnlyList<ImportAccountChoice> accountChoices = [];
    public IReadOnlyList<ImportAccountChoice> AccountChoices => accountChoices;
    public bool ImportAllAccounts
    {
        get => importAllAccounts;
        set
        {
            if (!SetProperty(ref importAllAccounts, value)) return;
            cancellation?.Cancel(); InvalidatePreparation(true); Refresh();
        }
    }
    public bool IsExplicitAccountSelection => !importAllAccounts;
    public bool HasAccountChoices => accountChoices.Count > 0;
    public bool IsAccountSelectionReady => importAllAccounts || accountChoices.Any(c => c.Included);
    public string AccountSelectionSummary => string.Format(Ui, this["AccountSelectionSummary"],
        importAllAccounts ? accountChoices.Count : accountChoices.Count(c => c.Included), accountChoices.Count);

    private ImportAccountSelection SelectedAccounts() => importAllAccounts ? ImportAccountSelection.All :
        new(ImportAccountSelectionKind.ExplicitList, accountChoices.Where(c => c.Included)
            .Select(c => c.Account).Order(StringComparer.Ordinal).ToArray());

    private void ResetAccountChoices()
    {
        foreach (var choice in accountChoices) choice.PropertyChanged -= AccountChoiceChanged;
        accountChoices = [];
        // Keep the user's all/explicit mode, but never carry account choices into a different mapping/source.
    }
    private void SetAccountChoices(ImportColumnSelectionResult result)
    {
        ResetAccountChoices();
        if (!result.IsValid) return;
        var culture = CultureInfo.GetCultureInfo(preferences.Current.FormatCultureTag);
        accountChoices = result.Rows.Select(r => new ImportAccountChoice(r.Account!, r.Amount!.Value))
            .OrderBy(c => c.Account, StringComparer.Ordinal).ToArray();
        foreach (var choice in accountChoices)
        {
            choice.Reformat(culture);
            choice.PropertyChanged += AccountChoiceChanged;
        }
    }
    private void AccountChoiceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ImportAccountChoice.Included)) return;
        cancellation?.Cancel(); InvalidatePreparation(true); Refresh();
    }
}
