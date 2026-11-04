using System.Globalization;

namespace Valdrio.Desktop;

// Presentation only. Cost and allocation calculations retain their original
// decimal values and the underlying ledger's precision policy.
public static class UiCompactNumbers
{
    public static string Number(decimal amount, CultureInfo culture) =>
        decimal.Round(amount, 2, MidpointRounding.AwayFromZero).ToString("#,##0.##", culture);

    public static string Money(decimal amount, string currency, CultureInfo culture) =>
        Number(amount, culture) + " " + currency;

    public static string Percent(decimal amount, CultureInfo culture) =>
        Number(amount, culture) + " %";
}
