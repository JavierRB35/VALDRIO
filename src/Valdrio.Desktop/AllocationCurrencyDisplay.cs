using Valdrio.Domain;
using Valdrio.Reporting;

namespace Valdrio.Desktop;

// Compatibility adapter. Portable reporting owns reconciled display arithmetic.
internal static class AllocationCurrencyDisplay
{
    internal static IReadOnlyList<decimal> Project(
        IReadOnlyList<decimal> ledgerAmounts, decimal reportQuantum, PrecisionPolicy precision) =>
        AllocationCurrencyProjection.Project(ledgerAmounts, reportQuantum, precision);

    internal static IReadOnlyList<decimal> ProjectToTarget(
        IReadOnlyList<decimal> ledgerAmounts, decimal displayedTarget,
        decimal reportQuantum, PrecisionPolicy precision) =>
        AllocationCurrencyProjection.ProjectToTarget(ledgerAmounts, displayedTarget, reportQuantum, precision);

    internal static IReadOnlyList<(decimal Direct, decimal Auxiliary, decimal Final)> ProjectBreakdown(
        IReadOnlyList<(decimal Direct, decimal Auxiliary)> ledgerRows,
        decimal reportQuantum, PrecisionPolicy precision) =>
        AllocationCurrencyProjection.ProjectBreakdown(ledgerRows, reportQuantum, precision);
}
