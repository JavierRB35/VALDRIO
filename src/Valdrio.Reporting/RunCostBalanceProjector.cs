using Valdrio.Application;
using Valdrio.Domain;

namespace Valdrio.Reporting;

public sealed record RunNodeBalance(EntityId NodeId, EntityId CenterId, NodeRole Role,
    decimal Primary, decimal SecondaryNet, decimal Final);

public static class RunCostBalanceProjector
{
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
