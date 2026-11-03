using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Valdrio.Application;
using Valdrio.Domain;

namespace Valdrio.Desktop;

public sealed class ImportRateDraft : ObservableObject
{
    private string id = "", currency = "", quote = "", date = "", source = "";
    public string Id { get => id; set => SetProperty(ref id, value); }
    public string Currency { get => currency; set => SetProperty(ref currency, value); }
    public string Quote { get => quote; set => SetProperty(ref quote, value); }
    public string Date { get => date; set => SetProperty(ref date, value); }
    public string Source { get => source; set => SetProperty(ref source, value); }
}
public sealed record ImportFxPreview(string Record, string DocumentDate, string Original, string Converted,
    string Rate, string RateDate, string Source, string Adjustment);

public sealed partial class ImportColumnMappingViewModel
{
    private readonly ICurrencyCatalog? currencyCatalog;
    private bool international, currencyFromColumn;
    private int? currencyColumn, dateColumn;
    private string datePattern = "yyyy-MM-dd", rateSetName = "", rateToApply = "";
    private DateTime? allowedRateStart, allowedRateEnd;
    public ObservableCollection<ImportRateDraft> Rates { get; } = [];
    public IRelayCommand AddRateCommand { get; private set; } = null!;
    public IRelayCommand ApplyRateCommand { get; private set; } = null!;
    public bool International { get => international; set { if (SetProperty(ref international, value)) ConversionChanged(); } }
    public bool CurrencyFromColumn { get => currencyFromColumn; set { if (SetProperty(ref currencyFromColumn, value)) ConversionChanged(); } }
    public int? CurrencyColumn { get => currencyColumn; set { if (SetProperty(ref currencyColumn, value)) ConversionChanged(); } }
    public int? DateColumn { get => dateColumn; set { if (SetProperty(ref dateColumn, value)) ConversionChanged(); } }
    public string DatePattern { get => datePattern; set { if (SetProperty(ref datePattern, value)) ConversionChanged(); } }
    public string RateSetName { get => rateSetName; set { if (SetProperty(ref rateSetName, value)) ConversionChanged(); } }
    public string RateToApply { get => rateToApply; set => SetProperty(ref rateToApply, value); }
    public DateTime? AllowedRateStart { get => allowedRateStart; set { if (SetProperty(ref allowedRateStart, value)) ConversionChanged(); } }
    public DateTime? AllowedRateEnd { get => allowedRateEnd; set { if (SetProperty(ref allowedRateEnd, value)) ConversionChanged(); } }
    private void ConversionChanged()
    {
        cancellation?.Cancel(); InvalidatePreparation(true); RecalculateSelection(); Refresh();
    }
    private void InitializeConversion()
    {
        AddRateCommand = new RelayCommand(() => Rates.Add(new()), () => !busy);
        ApplyRateCommand = new RelayCommand(() =>
        {
            foreach (var choice in accountChoices.Where(c => c.Included)) choice.RateId = rateToApply;
        }, () => !busy);
        Rates.CollectionChanged += (_, e) =>
        {
            if (e.OldItems is not null) foreach (ImportRateDraft rate in e.OldItems) rate.PropertyChanged -= RateChanged;
            if (e.NewItems is not null) foreach (ImportRateDraft rate in e.NewItems) rate.PropertyChanged += RateChanged;
            ConversionChanged();
        };
    }
    private void RateChanged(object? sender, PropertyChangedEventArgs e) => ConversionChanged();
    private bool ConversionInputValid()
    {
        bool Reject() { diagnostic = new("IMP_CONVERSION_INVALID", "Import.ConversionInvalid", []); return false; }
        if (currencyCatalog is null || targetStudy is null || !allowedRateStart.HasValue || !allowedRateEnd.HasValue || allowedRateStart > allowedRateEnd) return Reject();
        var codes = currencyCatalog.GetAll().Where(c => c.IsMonetary && c.MinorUnitDigits.HasValue).Select(c => c.AlphaCode.ToString()).ToHashSet();
        if (!currencyFromColumn && !codes.Contains(sourceCurrency) || currencyFromColumn && !currencyColumn.HasValue) return Reject();
        if (Rates.Count > 0 && string.IsNullOrWhiteSpace(rateSetName) || Rates.Select(r => r.Id).Distinct(StringComparer.Ordinal).Count() != Rates.Count) return Reject();
        foreach (var r in Rates)
        {
            if (string.IsNullOrWhiteSpace(r.Id) || r.Id != r.Id.Trim() || !codes.Contains(r.Currency) || r.Currency == targetStudy.Currency.FunctionalCurrencyCode.ToString() ||
                string.IsNullOrWhiteSpace(r.Source) || !DateOnly.TryParseExact(r.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _) ||
                !decimal.TryParse(r.Quote, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var quote) || quote <= 0 || CanonicalDecimal.Scale(quote) > 18) return Reject();
        }
        return true;
    }
    private ImportConversionPolicy BuildConversion()
    {
        if (currencyCatalog is null || !allowedRateStart.HasValue || !allowedRateEnd.HasValue)
            throw ImportRules.Error("IMP_CONVERSION_INVALID", "Import.ConversionInvalid");
        var start = new BusinessDate(DateOnly.FromDateTime(allowedRateStart.Value));
        var end = new BusinessDate(DateOnly.FromDateTime(allowedRateEnd.Value));
        var functional = targetStudy?.Currency.FunctionalCurrencyCode ?? throw ImportRules.Error("IMP_CONVERSION_INVALID", "Import.ConversionInvalid");
        var rates = Rates.Select(r => new FxRate(new(r.Id), new(r.Currency), functional,
            SourceAmountParser.Parse(r.Quote, new SourceNumberFormat(".", null, [], ImportNegativeSyntax.LeadingMinus)).Value
                ?? throw ImportRules.Error("FX_RATE_INVALID", "Import.ConversionInvalid"),
            BusinessDate.ParseIso(r.Date), r.Source, rateSetName)).ToArray();
        var set = rates.Length == 0 ? null : new FxRateSetVersion(rateSetName, end, "manual", rates);
        return new(set, accountChoices.Where(c => c.Included && !string.IsNullOrWhiteSpace(c.RateId))
            .Select(c => new ImportRateSelection(c.RecordNumber, new(c.RateId))).ToArray(),
            currencyCatalog.GetAll().Where(c => c.IsMonetary && c.MinorUnitDigits.HasValue).ToArray(), start, end,
            string.IsNullOrWhiteSpace(approvalReference) ? "pending" : approvalReference, dateColumn);
    }
    public bool HasConversion => preparedImport?.ConversionBridge is not null;
    private string DisplayMoney(Money money) => money.Amount.ToString(CultureInfo.GetCultureInfo(DateLanguageTag)) + " " + money.Currency;
    public IReadOnlyList<ImportFxPreview> ConversionRows => preparedImport?.ConversionBridge is not { } bridge ? [] :
        bridge.Rows.Select(r => (r, earlier: false)).Concat(bridge.EarlierRows.Select(r => (r, earlier: true)))
            .Select(x => new ImportFxPreview((x.earlier ? this["EarlierAmount"] + " · " : "") + x.r.RecordNumber,
                x.r.DocumentDate?.ToString() ?? "—", DisplayMoney(x.r.Application.OriginalMoney), DisplayMoney(x.r.Application.ConvertedOriginalMoney),
                (x.r.Application.RateId?.Value ?? "1") + " · " + CanonicalDecimal.Format(x.r.Application.Quote),
                x.r.Application.RateDate?.ToString() ?? "—", x.r.Application.Source,
                CanonicalDecimal.Format(x.r.Application.ConversionRoundingAdjustment.Amount))).ToArray();
    public string ConversionTotals => preparedImport?.ConversionBridge is not { } bridge ? "" :
        string.Join("\n", bridge.OriginalTotals.Select(t => string.Format(Ui, this["CurrencyTotals"], t.Currency,
            t.Positive.ToString(CultureInfo.GetCultureInfo(DateLanguageTag)), t.Negative.ToString(CultureInfo.GetCultureInfo(DateLanguageTag)),
            t.Net.ToString(CultureInfo.GetCultureInfo(DateLanguageTag))))) + "\n" +
        string.Format(Ui, this["FunctionalTotal"], bridge.FunctionalTotal.ToString(CultureInfo.GetCultureInfo(DateLanguageTag)),
            targetStudy!.Currency.FunctionalCurrencyCode, CanonicalDecimal.Format(bridge.RoundingAdjustment));
}
