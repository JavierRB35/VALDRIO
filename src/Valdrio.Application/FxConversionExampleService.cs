using Valdrio.Calculation;
using Valdrio.Domain;

namespace Valdrio.Application;

// Development fixture only. Human text parsing and presentation stay outside Application.
public sealed class FxConversionExampleService
{
    private readonly FxConverter converter = new();
    public FxRateApplication Build(decimal quote, BusinessDate rateDate, string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        var usd = new CurrencyCode("USD");
        var eur = new CurrencyCode("EUR");
        var rate = new FxRate(new FxRateId("ui-usd-eur"), usd, eur, quote,
            rateDate, source, "ui-demo-v1");
        return converter.Convert("ui-demo", new Money(100m, usd), eur, rate);
    }
}
