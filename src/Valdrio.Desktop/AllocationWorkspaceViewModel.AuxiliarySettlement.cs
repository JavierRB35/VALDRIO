using System.Collections.ObjectModel;
using Valdrio.Application;
using Valdrio.Domain;

namespace Valdrio.Desktop;

// Signed, read-only reconciliation of each auxiliary in a reciprocal routing group.
// Gross circulation is not an additional posted cost and is never added to principals.
public sealed record AuxiliarySettlementRow(
    string Auxiliary, string Group, string Initial, string Incoming, string GrossOut,
    string MathematicalBalance, decimal InitialAmount, decimal IncomingAmount, decimal GrossAmount);

public sealed partial class AllocationWorkspaceViewModel
{
    private IReadOnlyList<SecondaryAllocationFact> auxiliaryVarianceFacts = [];
    public ObservableCollection<AuxiliarySettlementRow> AuxiliarySettlements { get; } = [];
    public string AuxiliarySettlementResidual { get; private set; } = "";
    public string AuxiliarySettlementHelp => AuxiliarySettlements.Count == 0
        ? this["AuxiliarySettlementNotAvailable"]
        : this["AuxiliarySettlementExplanation"];

    private void ClearAuxiliarySettlement()
    {
        AuxiliarySettlements.Clear();
        AuxiliarySettlementResidual = "";
        OnPropertyChanged(nameof(AuxiliarySettlementHelp));
        OnPropertyChanged(nameof(AuxiliarySettlementResidual));
    }

    private void BuildAuxiliarySettlement(IReadOnlyList<SecondaryGroupCirculation> groups)
    {
        ClearAuxiliarySettlement();
        var primary = primaryForOverview;
        if (workspace is null || primary is null || groups.Count == 0) return;

        var nodes = workspace.Resources.Model.Structure.Nodes.ToDictionary(node => node.Id);
        var mappedGroups = (workspace.Configuration.SecondaryPartRoutingGroups ?? [])
            .ToDictionary(group => group.PartId, group => group.ComponentRoutingGroup);
        decimal maximumSolverResidual = 0m;
        foreach (var group in groups.OrderBy(group => group.ComponentRoutingGroup, StringComparer.Ordinal))
        {
            var solution = group.Solution;
            maximumSolverResidual = Math.Max(maximumSolverResidual, solution.AbsoluteResidual);
            for (var i = 0; i < solution.AuxiliaryIds.Count; i++)
            {
                var id = EntityId.Parse(solution.AuxiliaryIds[i]);
                if (!nodes.TryGetValue(id, out var node)) continue;
                var initial = primary.Facts
                    .Where(fact => fact.NodeId == id &&
                        (mappedGroups.TryGetValue(fact.PartId, out var selectedGroup) ?
                            selectedGroup : "all") == group.ComponentRoutingGroup)
                    .Sum(fact => fact.PostedAmount.Amount);
                initial -= auxiliaryVarianceFacts.Where(f => f.TerminalNodeId == id &&
                    (mappedGroups.TryGetValue(f.PartId,out var g) ? g : "all") == group.ComponentRoutingGroup).Sum(f=>f.PostedAmount.Amount);
                var gross = solution.Gross[i];
                // Balance equation: gross = primary + net receipts from reciprocal auxiliaries.
                // We cannot claim a per-hop ledger of signed receipts that T11 does not store.
                var incoming = checked(gross - initial);
                AuxiliarySettlements.Add(new(
                    NodeLabel(node),
                    group.ComponentRoutingGroup == "all" ? this["AllComponents"] : group.ComponentRoutingGroup,
                    StudyMoney(initial), StudyMoney(incoming), StudyMoney(gross),
                    this["AuxiliarySettlementTheoreticalZero"], initial, incoming, gross));
            }
        }
        AuxiliarySettlementResidual = string.Format(Format,
            this["AuxiliarySettlementSolverResidual"],
            maximumSolverResidual.ToString("0.############################", Format), Currency);
        OnPropertyChanged(nameof(AuxiliarySettlementHelp));
        OnPropertyChanged(nameof(AuxiliarySettlementResidual));
    }
}
