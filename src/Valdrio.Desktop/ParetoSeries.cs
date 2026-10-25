namespace Valdrio.Desktop;

public sealed record ParetoCost(string Label, decimal Amount);
public sealed record ParetoPoint(string Label, decimal Amount, decimal? CumulativePercent);

public static class ParetoSeries
{
    // Costs are already aggregated by node. Never group on a display label or add internal flows.
    public static IReadOnlyList<ParetoPoint> Build(IEnumerable<ParetoCost> costs)
    {
        var ordered = costs.OrderByDescending(x => x.Amount)
            .ThenBy(x => x.Label, StringComparer.Ordinal).ToArray();
        var total = ordered.Sum(x => x.Amount);
        decimal cumulative = 0;
        return ordered.Select(x =>
        {
            cumulative += x.Amount;
            return new ParetoPoint(x.Label, x.Amount, total > 0 ? cumulative / total * 100m : null);
        }).ToArray();
    }
}
