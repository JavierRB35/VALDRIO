using System.Globalization;
using Valdrio.Calculation;
using Valdrio.Domain;

namespace Valdrio.Reporting;

// Read-only currency projection. Economic ledger facts and their internal precision
// are never changed: only the amounts displayed in financial result tables.
public static class AllocationCurrencyProjection
{
    public static IReadOnlyList<decimal> Project(
        IReadOnlyList<decimal> ledgerAmounts, decimal reportQuantum, PrecisionPolicy precision)
    {
        if (ledgerAmounts.Count == 0) return [];
        var lines = ledgerAmounts.Select((amount, i) => new CurrencyProjectionInputLine(
            new StableRoundingId((i + 1).ToString("D8", CultureInfo.InvariantCulture)), amount)).ToArray();
        var projected = new CurrencyProjector(precision)
            .Project(ledgerAmounts.Sum(), lines, reportQuantum).Lines
            .ToDictionary(line => line.DestinationId.Value, line => line.ProjectedAmount);
        return lines.Select(line => projected[line.DestinationId.Value]).ToArray();
    }

    // Some totals have already been projected in another table. Match that same
    // presented total without ever mutating or reposting the input ledger lines.
    public static IReadOnlyList<decimal> ProjectToTarget(
        IReadOnlyList<decimal> ledgerAmounts, decimal displayedTarget,
        decimal reportQuantum, PrecisionPolicy precision)
    {
        if (ledgerAmounts.Count == 0)
        {
            if (displayedTarget != 0m) throw new ArgumentException("A nonzero total needs rows.");
            return [];
        }
        var displayed = Project(ledgerAmounts, reportQuantum, precision).ToArray();
        var outstanding = checked((displayedTarget - displayed.Sum()) / reportQuantum);
        if (outstanding != decimal.Truncate(outstanding))
            throw new InvalidOperationException("Display projection is not in whole currency units.");
        while (outstanding != 0m)
        {
            var step = outstanding > 0m ? reportQuantum : -reportQuantum;
            var index = Enumerable.Range(0, displayed.Length)
                .OrderBy(i => Math.Abs(displayed[i] + step - ledgerAmounts[i])
                    - Math.Abs(displayed[i] - ledgerAmounts[i]))
                .ThenBy(i => i).First();
            displayed[index] = checked(displayed[index] + step);
            outstanding += outstanding > 0m ? -1m : 1m;
        }
        return displayed;
    }

    // Reconcile both the displayed rows (direct + auxiliary = final) and columns
    // (sum direct, sum auxiliary and sum final), in integer currency minor units.
    // Stable input order breaks equally good ties. The projection is not a posting.
    public static IReadOnlyList<(decimal Direct, decimal Auxiliary, decimal Final)> ProjectBreakdown(
        IReadOnlyList<(decimal Direct, decimal Auxiliary)> ledgerRows,
        decimal reportQuantum, PrecisionPolicy precision)
    {
        if (ledgerRows.Count == 0) return [];
        var final = Project(ledgerRows.Select(row => checked(row.Direct + row.Auxiliary)).ToArray(),
            reportQuantum, precision);
        var direct = ledgerRows.Select(row =>
            CurrencyProjector.RoundToQuantum(row.Direct, reportQuantum)).ToArray();
        var directTarget = CurrencyProjector.RoundToQuantum(ledgerRows.Sum(row => row.Direct), reportQuantum);
        var outstanding = checked((directTarget - direct.Sum()) / reportQuantum);
        if (outstanding != decimal.Truncate(outstanding))
            throw new InvalidOperationException("Display projection is not in whole currency units.");
        while (outstanding != 0m)
        {
            var change = outstanding > 0m ? reportQuantum : -reportQuantum;
            var target = Enumerable.Range(0, ledgerRows.Count)
                .OrderBy(i => Error(i, direct[i] + change) - Error(i, direct[i]))
                .ThenBy(i => i).First();
            direct[target] = checked(direct[target] + change);
            outstanding += outstanding > 0m ? -1m : 1m;
        }
        return Enumerable.Range(0, ledgerRows.Count).Select(i =>
            (Direct: direct[i], Auxiliary: checked(final[i] - direct[i]), Final: final[i])).ToArray();

        decimal Error(int i, decimal directAmount) =>
            Math.Abs(directAmount - ledgerRows[i].Direct) +
            Math.Abs((final[i] - directAmount) - ledgerRows[i].Auxiliary);
    }
}
