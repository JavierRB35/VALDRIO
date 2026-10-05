using Valdrio.Application;
using Valdrio.Calculation;
using Valdrio.Domain;

namespace Valdrio.Runner;

internal static class T02AFxVerifier
{
    public static IReadOnlyDictionary<string, object> Verify()
    {
        var eur = new CurrencyCode("EUR");
        var usd = new CurrencyCode("USD");
        var jpy = new CurrencyCode("JPY");
        var converter = new FxConverter();

        var c33 = converter.Convert(
            "C33",
            new Money(100m, usd),
            eur,
            Rate("c33", usd, eur, 0.9m, "rs-old"));

        var c34 = converter.Convert(
            "C34",
            new Money(100m, eur),
            eur,
            null);

        var c35 = converter.Convert(
            "C35",
            new Money(1000m, jpy),
            eur,
            Rate("c35", jpy, eur, 0.006m, "rs-old"));

        var c36 = CaptureCode(() =>
            converter.Convert(
                "C36",
                new Money(100m, usd),
                eur,
                null));

        var c37 = converter.Convert(
            "C37",
            new Money(-100m, usd),
            eur,
            Rate("c37", usd, eur, 0.9m, "rs-old"));

        var oldApp = converter.Convert(
            "I14-old",
            new Money(100m, usd),
            eur,
            Rate("i14-old", usd, eur, 0.9m, "rs-old"));
        var newApp = converter.Convert(
            "I14-new",
            new Money(100m, usd),
            eur,
            Rate("i14-new", usd, eur, 0.95m, "rs-new"));

        var oldHash = FxConversionHash.Compute(oldApp);
        var newHash = FxConversionHash.Compute(newApp);

        if (c33.ConvertedOriginalMoney.Amount != 90m
            || c34.ConvertedOriginalMoney.Amount != 100m
            || c34.Quote != 1m
            || c35.ConvertedOriginalMoney.Amount != 6m
            || c36 != "FX_RATE_MISSING"
            || c37.ConvertedOriginalMoney.Amount != -90m
            || oldApp.ConvertedOriginalMoney.Amount != 90m
            || newApp.ConvertedOriginalMoney.Amount != 95m
            || oldHash == newHash)
        {
            throw new InvalidOperationException(
                "T02A FX references differ from expected corpus.");
        }

        return new Dictionary<string, object>
        {
            ["C33"] = "100 USD -> 90 EUR",
            ["C34"] = "100 EUR -> 100 EUR quote 1",
            ["C35"] = "1000 JPY -> 6 EUR",
            ["C36"] = c36,
            ["C37"] = "-100 USD -> -90 EUR",
            ["I14_old"] = "90 EUR",
            ["I14_new"] = "95 EUR",
            ["I14_hash_changed"] = true
        };
    }

    private static FxRate Rate(
        string id,
        CurrencyCode source,
        CurrencyCode target,
        decimal quote,
        string version) =>
        new(
            new FxRateId(id),
            source,
            target,
            quote,
            BusinessDate.ParseIso("2026-10-04"),
            "manual-fixture",
            version);

    private static string CaptureCode(Action action)
    {
        try
        {
            action();
        }
        catch (DomainRuleException exception)
        {
            return exception.Diagnostic.Code;
        }

        throw new InvalidOperationException(
            "Expected FX diagnostic.");
    }
}
