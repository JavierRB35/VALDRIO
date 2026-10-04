using Valdrio.Calculation;
using Valdrio.Domain;

namespace Valdrio.Application;

public sealed record T02RoundingExampleRow(
    string DestinationId,
    string RawAmount,
    string NumericAdjustment,
    string RoundingAdjustment,
    string PostedAmount);

public sealed record T02RoundingExample(
    string ReferenceCase,
    string SourceTotal,
    string Quantum,
    string PostedTotal,
    IReadOnlyList<T02RoundingExampleRow> Rows);

public sealed class T02RoundingExampleService
{
    public T02RoundingExample BuildC07()
    {
        var allocator = new ProportionalAllocator();
        var rounder = new BalancedRounder();

        var source = 0.01m;
        var quantum = 0.01m;

        var raw = allocator.FromWeights(
            source,
            [
                new(new StableRoundingId("C"), 1m),
                new(new StableRoundingId("A"), 1m),
                new(new StableRoundingId("B"), 1m)
            ]);

        var result = rounder.Round(
            source,
            raw,
            quantum);

        var rows = result.Lines
            .Select(line =>
                new T02RoundingExampleRow(
                    line.DestinationId.Value,
                    CanonicalDecimal.Format(line.RawAmount),
                    CanonicalDecimal.Format(line.NumericAdjustment),
                    CanonicalDecimal.Format(line.RoundingAdjustment),
                    CanonicalDecimal.Format(line.PostedAmount)))
            .ToArray();

        return new T02RoundingExample(
            ReferenceCase: "C07",
            SourceTotal: CanonicalDecimal.Format(source),
            Quantum: CanonicalDecimal.Format(quantum),
            PostedTotal: CanonicalDecimal.Format(result.PostedTotal),
            Rows: rows);
    }
}
