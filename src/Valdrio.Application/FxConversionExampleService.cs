using Valdrio.Calculation;
using Valdrio.Domain;

namespace Valdrio.Application;

public sealed record FxConversionExample(
    string OriginalAmount,
    string OriginalCurrency,
    string Orientation,
    string Quote,
    string RateDate,
    string Source,
    string FunctionalAmount,
    string FunctionalCurrency,
    string RoundingAdjustment,
    string RateSetVersionId);

public sealed class FxConversionExampleService
{
    private readonly FxConverter converter = new();

    public FxConversionExample Build(
        string quoteText,
        string rateDateIso,
        string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        var quote = CanonicalDecimal.Parse(quoteText);
        var rateDate = BusinessDate.ParseIso(rateDateIso);
        var usd = new CurrencyCode("USD");
        var eur = new CurrencyCode("EUR");

        var rate = new FxRate(
            new FxRateId("ui-usd-eur"),
            usd,
            eur,
            quote,
            rateDate,
            source,
            "ui-demo-v1");

        var application = converter.Convert(
            "ui-demo",
            new Money(100m, usd),
            eur,
            rate);

        return new FxConversionExample(
            CanonicalDecimal.Format(application.OriginalMoney.Amount),
            application.OriginalMoney.Currency.Value,
            $"{application.ConvertedOriginalMoney.Currency.Value}/{application.OriginalMoney.Currency.Value}",
            CanonicalDecimal.Format(application.Quote),
            application.RateDate?.ToCanonicalString() ?? "—",
            application.Source,
            CanonicalDecimal.Format(application.ConvertedOriginalMoney.Amount),
            application.ConvertedOriginalMoney.Currency.Value,
            CanonicalDecimal.Format(application.ConversionRoundingAdjustment.Amount),
            application.RateSetVersionId ?? "IDENTITY");
    }
}
