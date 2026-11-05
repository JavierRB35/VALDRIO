using Valdrio.Application;
using Valdrio.Domain;

namespace Valdrio.Reporting;

public sealed record RunNodeBalance(EntityId NodeId, EntityId CenterId, NodeRole Role,
    decimal Primary, decimal SecondaryNet, decimal Final);

public sealed record RunFunctionBalance(NodeEconomicFunction Function, decimal Final, int NodeCount);

public static class RunCostBalanceProjector
{
    // Presentation-only grouping of posted terminal principal costs, by the
    // function of the FINAL node. The original resource cost layer remains
    // available in the facts for tracing cross-functional services.
    public static IReadOnlyList<RunFunctionBalance> GroupByFunction(
        IReadOnlyList<RunNodeBalance> balances, IReadOnlyList<CalculationNodeDefinition> nodes)
    {
        ArgumentNullException.ThrowIfNull(balances);
        ArgumentNullException.ThrowIfNull(nodes);
        var byId = nodes.ToDictionary(n => n.Id);
        var principals = balances.Where(b => b.Role == NodeRole.Principal).ToArray();
        if (principals.Any(b => !byId.ContainsKey(b.NodeId)))
            throw new ArgumentException("A terminal principal node is missing from the model.", nameof(nodes));
        return principals
            .GroupBy(b => NodeCostClassification.Function(byId[b.NodeId]))
            .OrderBy(g => g.Key)
            .Select(g => new RunFunctionBalance(g.Key, g.Sum(b => b.Final), g.Count()))
            .ToArray();
    }

    // Reconcile frozen posted facts, never reciprocal gross flows or current configuration.
    // Net secondary is a bridge between two stocks, not an additional economic cost.
    public static IReadOnlyList<RunNodeBalance> Build(CalculationRun run)
    {
        if (run.Status != CalculationRunStatus.Completed ||
            run.Snapshot.Input is not AllocationStudySnapshotInput input ||
            run.Output is not { Primary: { } primary, Secondary: { } secondary }) return [];

        var initial = primary.Facts.ToLookup(f => f.NodeId);
        var final = secondary.Facts.ToLookup(f => f.TerminalNodeId);
        return input.Allocations.Resources.Model.Structure.Nodes
            .Where(n => initial.Contains(n.Id) || final.Contains(n.Id))
            .Select(n =>
            {
                var p = initial[n.Id].Sum(f => f.PostedAmount.Amount);
                var f = final[n.Id].Sum(f => f.PostedAmount.Amount);
                return new RunNodeBalance(n.Id, n.PhysicalCenterId, n.Role, p, f - p, f);
            }).ToArray();
    }
}
