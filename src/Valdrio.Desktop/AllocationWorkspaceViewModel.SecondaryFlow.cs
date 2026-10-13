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
    string SourceFull, string TargetFull, decimal NumericAmount, string Amount);

// This projector uses existing solution.Gross and the exact published
// coefficients. No iterating, no factorization and no per-account fake ledger.
internal static class SecondaryFlowProjector
{
    internal sealed record Directed(SecondaryFlowStage Stage, string SourceId, string TargetId, decimal Amount);

    internal static IReadOnlyList<Directed> Project(ReciprocalInput graph, ReciprocalSolution solution)
    {
        var grossById = solution.AuxiliaryIds
            .Select((id, i) => (id, amount: solution.Gross[i]))
            .ToDictionary(x => x.id, x => x.amount, StringComparer.Ordinal);
        var arcs = new List<Directed>();
        for (var i = 0; i < graph.AuxiliaryIds.Length; i++)
        {
            var source = graph.AuxiliaryIds[i];
            // The published solution has a canonical sorted order, which need
            // not equal the published keys' source order.
            var gross = grossById[source];
            for (var j = 0; j < graph.AuxiliaryIds.Length; j++)
            {
                var coefficient = graph.Q[i][j];
                if (coefficient == 0m) continue;
                var amount = checked(gross * coefficient);
                if (amount != 0m)
                    arcs.Add(new(SecondaryFlowStage.Reciprocal, source, graph.AuxiliaryIds[j], amount));
            }
            for (var j = 0; j < graph.TerminalIds.Length; j++)
            {
                var coefficient = graph.B[i][j];
                if (coefficient == 0m) continue;
                var amount = checked(gross * coefficient);
                if (amount != 0m)
                    arcs.Add(new(SecondaryFlowStage.Final, source, graph.TerminalIds[j], amount));
            }
        }
        return arcs;
    }
}

public sealed partial class AllocationWorkspaceViewModel
{
    public ObservableCollection<SecondaryFlowArc> SecondaryFlowArcs { get; } = [];
    public string SecondaryFlowNotice { get; private set; } = "";
    public event EventHandler? SecondaryFullFlowChanged;

    private void ClearCompleteSecondaryFlow()
    {
        SecondaryFlowArcs.Clear();
        SecondaryFlowNotice = this["SecondaryFlowNoResult"];
        OnPropertyChanged(nameof(SecondaryFlowNotice));
        SecondaryFullFlowChanged?.Invoke(this, EventArgs.Empty);
    }

    private void BuildCompleteSecondaryFlow(IReadOnlyList<SecondaryGroupCirculation> groups)
    {
        SecondaryFlowArcs.Clear();
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
                    LongName(source), LongName(target), amount, StudyMoney(amount)));
            }

            var assignedGroups = (workspace.Configuration.SecondaryPartRoutingGroups ?? [])
                .ToDictionary(x => x.PartId, x => x.ComponentRoutingGroup);
            string GroupFor(EntityId part) =>
                assignedGroups.TryGetValue(part, out var grp) ? grp : "all";

            // The leftmost stage contains *every initial node receiving cost in
            // primary*, not accounts. Principal costs pass directly to their
            // terminal; auxiliary costs enter the corresponding auxiliary pool.
            foreach (var group in primary.Facts
                .GroupBy(fact => (Group: GroupFor(fact.PartId), Node: fact.NodeId.ToString()))
                .OrderBy(g => g.Key.Group, StringComparer.Ordinal).ThenBy(g => g.Key.Node, StringComparer.Ordinal))
            {
                var amount = group.Sum(f => f.PostedAmount.Amount);
                if (activeNodes.TryGetValue(group.Key.Node, out var node))
                    Add(SecondaryFlowStage.Primary, group.Key.Group,
                        group.Key.Node, group.Key.Node, amount);
            }

            // Exact Q and B coefficients, reconstructed from the referenced
            // published keys. This is projection, NOT recalculation of a run.
            foreach (var group in groups.OrderBy(g => g.ComponentRoutingGroup, StringComparer.Ordinal))
            {
                var graph = ReciprocalGraphBuilder.FromPublishedSecondaryKeys(
                    workspace.Resources.Model.Structure, workspace.Keys,
                    workspace.Configuration.SecondaryRules, workspace.Resources.Model.DriverUnits,
                    workspace.Resources.Precision, group.ComponentRoutingGroup);
                foreach (var edge in SecondaryFlowProjector.Project(graph, group.Solution))
                    Add(edge.Stage, group.ComponentRoutingGroup, edge.SourceId, edge.TargetId, edge.Amount);
            }

            // The last column's totals are the persisted secondary results,
            // not the sum of internal reciprocal arrows.
            SecondaryFlowNotice = string.Format(Format, this["SecondaryFlowCompleteNotice"],
                SecondaryFlowArcs.Count,
                StudyMoney(PrincipalCosts.Sum(p => p.PresentedTotalAmount)));
        }
        catch (Exception ex) when (ex is DomainRuleException or InvalidOperationException or OverflowException)
        {
            // Do not render a partial map with missing reciprocal arcs.
            SecondaryFlowArcs.Clear();
            SecondaryFlowNotice = this["SecondaryFlowUnavailable"];
        }
        OnPropertyChanged(nameof(SecondaryFlowNotice));
        SecondaryFullFlowChanged?.Invoke(this, EventArgs.Empty);
    }
}
