using System.Globalization;
using Valdrio.Calculation;

namespace Valdrio.Desktop;

/// <summary>
/// Monetary report projection only. Never writes rounded numbers into the economic
/// ledger or a persisted allocation. Uses VALDRIO T02 CurrencyProjector (balanced
/// largest remainder, stable row IDs, midpoint-to-even on the source total).
/// </summary>
public static class FinancialDisplayProjection
{
    public static decimal[] Project(IReadOnlyList<decimal> amounts, decimal reportQuantum)
    {
        if (amounts.Count == 0) return [];
        var lines = amounts.Select((amount, index) =>
            new CurrencyProjectionInputLine(new StableRoundingId(index.ToString("D8", CultureInfo.InvariantCulture)), amount)).ToArray();
        var projection = new CurrencyProjector().Project(amounts.Sum(), lines, reportQuantum);
        return projection.Lines.OrderBy(line => line.DestinationId.Value, StringComparer.Ordinal)
            .Select(line => line.ProjectedAmount).ToArray();
    }

    public static string Format(decimal projected, string currency, decimal reportQuantum, CultureInfo culture)
    {
        int digits=0;
        for(decimal q=reportQuantum; q<1m && digits<8; q*=10m)digits++;
        return projected.ToString("N"+digits.ToString(CultureInfo.InvariantCulture), culture)+" "+currency;
    }
}
