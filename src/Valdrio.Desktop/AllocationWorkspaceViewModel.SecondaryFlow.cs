using System.Collections.ObjectModel;
using Valdrio.Application;
using Valdrio.Calculation;
using Valdrio.Domain;

namespace Valdrio.Desktop;

public enum SecondaryFlowStage { Primary, Reciprocal, Final }

// A directed arc of the *actual economic coefficient graph*. Amounts in the
// reciprocal stage are gross transfers; they must NOT be added to final costs.
public sealed record SecondaryFlowArc(
    SecondaryFlowStage Stage, string Group, string Source, string Target,
    string SourceFull, string TargetFull, bool TargetAuxiliary,
    decimal NumericAmount, string Amount)
{
    public string SourceId { get; init; } = "";
    public string TargetId { get; init; } = "";
}
public sealed record SecondaryFlowNode(
    string Id, string ShortName, string FullName, bool IsAuxiliary);


// Compatibility adapter: the portable engine owns coefficient arithmetic.
internal static class SecondaryFlowProjector
{
    internal sealed record Directed(SecondaryFlowStage Stage, string SourceId, string TargetId, decimal Amount);
    internal static IReadOnlyList<Directed> Project(ReciprocalInput graph, ReciprocalSolution solution) =>
        ReciprocalFlowProjector.Project(graph, solution)
            .Select(edge => new Directed(edge.Stage == ReciprocalFlowStage.Reciprocal
                ? SecondaryFlowStage.Reciprocal : SecondaryFlowStage.Final,
                edge.SourceId, edge.TargetId, edge.Amount)).ToArray();
}

public sealed partial class AllocationWorkspaceViewModel
{
    public ObservableCollection<SecondaryFlowArc> SecondaryFlowArcs { get; } = [];
    public ObservableCollection<SecondaryFlowNode> SecondaryFlowNodes { get; } = [];
    public string SecondaryFlowNotice { get; private set; } = "";
    public event EventHandler? SecondaryFullFlowChanged;

    private void ClearCompleteSecondaryFlow()
    {
        SecondaryFlowArcs.Clear();
        SecondaryFlowNodes.Clear();
        SecondaryFlowNotice = this["SecondaryFlowNoResult"];
        OnPropertyChanged(nameof(SecondaryFlowNotice));
        SecondaryFullFlowChanged?.Invoke(this, EventArgs.Empty);
    }

    private void BuildCompleteSecondaryFlow(IReadOnlyList<SecondaryGroupCirculation> groups)
    {
        SecondaryFlowArcs.Clear();
        SecondaryFlowNodes.Clear();
        SecondaryFlowNotice = "";
        var primary = primaryForOverview;
        if (workspace is null || primary is null)
        {
            ClearCompleteSecondaryFlow();
            return;
        }
        try
        {
            var activeNodes = workspace.Resources.Model.Structure.Nodes
                .Where(node => node.IsActive).ToDictionary(node => node.Id.ToString(), StringComparer.Ordinal);
            var centers = workspace.Resources.Model.PhysicalCenters.ToDictionary(c => c.Id);
            string ShortName(string id)
            {
                if (!activeNodes.TryGetValue(id, out var n)) return id;
                var centre = centers[n.PhysicalCenterId];
                return centre.Code + " / " + n.Code;
            }
            string LongName(string id) =>
                activeNodes.TryGetValue(id, out var node) ? NodeLabel(node) : id;
            void Add(SecondaryFlowStage stage, string group, string source, string target, decimal amount)
            {
                if (amount == 0m) return;
                SecondaryFlowArcs.Add(new(stage, group, ShortName(source), ShortName(target),
                    LongName(source), LongName(target),
                    activeNodes.TryGetValue(target, out var n) && n.Role == NodeRole.Auxiliary,
                    amount, StudyMoney(amount))
                { SourceId = source, TargetId = target });
            }

            var assignedGroups = (workspace.Configuration.SecondaryPartRoutingGroups ?? [])
                .ToDictionary(x => x.PartId, x => x.ComponentRoutingGroup);
            string GroupFor(EntityId part) =>
                assignedGroups.TryGetValue(part, out var grp) ? grp : "all";
            var primaryTotals = primary.Facts
                .GroupBy(f => (Group: GroupFor(f.PartId), Node: f.NodeId.ToString()))
                .ToDictionary(g => g.Key, g => g.Sum(f => f.PostedAmount.Amount));

            // The leftmost stage contains *every initial node receiving cost in
            // primary*, not accounts. Principal costs pass directly to their
            // terminal; auxiliary costs enter the corresponding auxiliary pool.
            foreach (var group in primaryTotals
                .OrderBy(g => g.Key.Group, StringComparer.Ordinal).ThenBy(g => g.Key.Node, StringComparer.Ordinal))
            {
                var amount = group.Value;
                if (activeNodes.TryGetValue(group.Key.Node, out var node))
                    Add(SecondaryFlowStage.Primary, group.Key.Group,
                        group.Key.Node, group.Key.Node, amount);
            }

            // Exact Q and B coefficients, reconstructed from the referenced
            // published keys. This is projection, NOT recalculation of a run.
            foreach (var group in groups.OrderBy(g => g.ComponentRoutingGroup, StringComparer.Ordinal))
            {
                var graph = AuxiliaryCapacityService.Graph(workspace, group.ComponentRoutingGroup);
                var projected = SecondaryFlowProjector.Project(graph, group.Solution);
                var secondaryTotals = projected.Where(edge => edge.Stage == SecondaryFlowStage.Final)
                    .GroupBy(edge => edge.TargetId, StringComparer.Ordinal)
                    .ToDictionary(g => g.Key, g => g.Sum(edge => edge.Amount), StringComparer.Ordinal);
                // Check group-specific principal receipts against the published
                // LU solution before showing an allegedly complete network.
                // Even a later edited key cannot silently produce misleading arrows.
                for (var terminal = 0; terminal < group.Solution.TerminalIds.Count; terminal++)
                {
                    var id = group.Solution.TerminalIds[terminal];
                    var directlyAssigned = primaryTotals.GetValueOrDefault((group.ComponentRoutingGroup, id));
                    var secondaryReceived = secondaryTotals.GetValueOrDefault(id);
                    var expected = group.Solution.Final[terminal];
                    var difference = Math.Abs(expected - checked(directlyAssigned + secondaryReceived));
                    var tolerance = Math.Max(0.00000001m, Math.Abs(expected) * 0.000000000001m);
                    if (difference > tolerance)
                        throw new InvalidOperationException("Saved reciprocal coefficients differ from the published result.");
                }
                foreach (var edge in projected)
                    Add(edge.Stage, group.ComponentRoutingGroup, edge.SourceId, edge.TargetId, edge.Amount);
            }

            // The last column's totals are the persisted secondary results,
            // not the sum of internal reciprocal arrows.
            // All active nodes appear as unique cards even when their primary
            // cost or reciprocal coefficient is zero.
            foreach (var entry in activeNodes.OrderBy(p => ShortName(p.Key), StringComparer.CurrentCulture)
                .ThenBy(p => p.Key, StringComparer.Ordinal))
                SecondaryFlowNodes.Add(new(entry.Key, ShortName(entry.Key),
                    LongName(entry.Key), entry.Value.Role == NodeRole.Auxiliary));
            SecondaryFlowNotice = string.Format(Format, this["SecondaryFlowCompleteNotice"],
                SecondaryFlowArcs.Count,
                StudyMoney(PrincipalCosts.Sum(p => p.PresentedTotalAmount)));
            if (auxiliaryVarianceFacts.Count > 0) SecondaryFlowNotice += " " + string.Format(Format, Text("Aux.FlowVariance"), StudyMoney(auxiliaryVarianceFacts.Sum(f => f.PostedAmount.Amount)));
        }
        catch (Exception ex) when (ex is DomainRuleException or InvalidOperationException or OverflowException)
        {
            // Do not render a partial map with missing reciprocal arcs.
            SecondaryFlowArcs.Clear();
            SecondaryFlowNodes.Clear();
            SecondaryFlowNotice = this["SecondaryFlowUnavailable"];
        }
        OnPropertyChanged(nameof(SecondaryFlowNotice));
        SecondaryFullFlowChanged?.Invoke(this, EventArgs.Empty);
    }
}
