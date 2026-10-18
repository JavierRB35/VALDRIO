using Valdrio.Application;
using Valdrio.Calculation;
using Valdrio.Domain;

namespace Valdrio.Reporting;

public sealed record SecondaryPresentedContribution(SecondaryAllocationFact Fact,
    EntityId PrimaryNodeId, IReadOnlyList<SecondaryExplanationLink> Links, decimal PostedAmount);

public static class SecondaryContributionProjector
{
    // Attribute an already posted terminal fact to its primary origins. This is
    // an explanatory projection, never a new economic run or gross circulation.
    public static IReadOnlyList<SecondaryPresentedContribution> Build(
        IReadOnlyList<SecondaryAllocationFact> facts, IReadOnlyList<SecondaryExplanationLink> links,
        IReadOnlyDictionary<EntityId, CalculationNodeDefinition> nodes)
    {
        var result = new List<SecondaryPresentedContribution>();
        var byFact = links.ToLookup(l => (l.ResourceId, l.PartId, l.TerminalNodeId));
        foreach (var fact in facts)
        {
            var groups = byFact[(fact.ResourceId, fact.PartId, fact.TerminalNodeId)]
                .Where(l => (l.SourceBehavior is null || l.SourceBehavior == fact.Behavior) && l.AssessmentId == fact.AssessmentId && l.CapacityNotEvaluated == fact.CapacityNotEvaluated)
                .GroupBy(l => l.PrimaryNodeId).OrderBy(g => g.Key).ToArray();
            if (groups.Length == 0) throw new InvalidOperationException("Published cost has no explanatory origins.");
            var raw = groups.ToDictionary(g => g.Key, g => g.Sum(l => l.RawContribution));
            // Keep costs already assigned to principal nodes intact. Attribute
            // the terminal fact's numerical/rounding residual to an auxiliary,
            // using a stable largest-contribution tie break.
            var adjustmentTarget = groups.OrderByDescending(g => nodes[g.Key].Role == NodeRole.Auxiliary)
                .ThenByDescending(g => Math.Abs(raw[g.Key])).ThenBy(g => g.Key).First().Key;
            raw[adjustmentTarget] += fact.PostedAmount.Amount - raw.Values.Sum();
            var rounded = new BalancedRounder(fact.PostedAmount.PrecisionPolicy).Round(fact.PostedAmount.Amount,
                raw.Select(p => new RoundingContribution(new(p.Key.ToString()), p.Value)).ToArray(),
                fact.PostedAmount.PrecisionPolicy.InternalLedgerQuantum).Lines.ToDictionary(l => l.DestinationId.Value);
            foreach (var group in groups)
                result.Add(new(fact, group.Key, group.ToArray(), rounded[group.Key.ToString()].PostedAmount));
        }
        return result;
    }
}
