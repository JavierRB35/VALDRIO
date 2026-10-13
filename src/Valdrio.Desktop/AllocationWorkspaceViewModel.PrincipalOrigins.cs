using System.Collections.ObjectModel;
using Valdrio.Domain;

namespace Valdrio.Desktop;

// Read-only attribution of published/previewed terminal facts. No recalculation,
// invented per-hop internal flows, or persistence is performed here.
public sealed record PrincipalOriginRow(
    string Account, string PrimaryNode, string Route, string Amount, string Keys, decimal NumericAmount)
{
    public string KeysPreview { get; init; } = "";
}

public sealed partial class AllocationWorkspaceViewModel
{
    public ObservableCollection<PrincipalOriginRow> PrincipalOriginRows { get; } = [];
    private PrincipalCostRow? selectedPrincipalCostRow;
    public PrincipalCostRow? SelectedPrincipalCostRow
    {
        get => selectedPrincipalCostRow;
        set { if (SetProperty(ref selectedPrincipalCostRow, value)) RebuildPrincipalOrigins(); }
    }
    public string PrincipalOriginSummary { get; private set; } = "";
    public string PrincipalOriginTotal => SelectedPrincipalCostRow is null ? "" :
        StudyMoney(PrincipalOriginRows.Sum(row => row.NumericAmount));
    public event EventHandler? PrincipalOriginsChanged;

    private void RebuildPrincipalOrigins()
    {
        PrincipalOriginRows.Clear();
        var selected = SelectedPrincipalCostRow;
        if (workspace is null || selected is null)
        {
            PrincipalOriginSummary = this["PrincipalOriginSelect"];
            OnPropertyChanged(nameof(PrincipalOriginSummary));
            OnPropertyChanged(nameof(PrincipalOriginTotal));
            PrincipalOriginsChanged?.Invoke(this, EventArgs.Empty);
            return;
        }
        var structureNodes = workspace.Resources.Model.Structure.Nodes.ToDictionary(n => n.Id);
        var accounts = workspace.Resources.Origins.ToDictionary(origin => origin.Id);
        var groups = secondaryContributions
            .Where(contribution => contribution.Fact.TerminalNodeId == selected.NodeId)
            .GroupBy(c => (c.Fact.ResourceId, c.PrimaryNodeId))
            .OrderBy(g => accounts.TryGetValue(g.Key.ResourceId, out var origin) ? origin.Code : g.Key.ResourceId.ToString(),
                StringComparer.Ordinal)
            .ThenBy(g => structureNodes[g.Key.PrimaryNodeId].Code, StringComparer.Ordinal)
            .ToArray();
        var presented = AllocationCurrencyDisplay.ProjectToTarget(
            groups.Select(g => g.Sum(c => c.PostedAmount)).ToArray(),
            selected.PresentedTotalAmount, DisplayCurrencyQuantum, workspace.Resources.Precision);
        for (var i = 0; i < groups.Length; i++)
        {
            var group = groups[i];
            var primaryNode = structureNodes[group.Key.PrimaryNodeId];
            var keys = group.SelectMany(contribution => contribution.Links)
                .SelectMany(link => (link.SecondaryKeyVersionIds ?? [])
                    .Concat(link.PrimaryKeyVersionId is { } id ? new EntityId[] { id } : []))
                .Distinct()
                .Select(id => workspace.Keys.FirstOrDefault(key => key.Id == id))
                .Where(key => key is not null)
                .Select(key => KeyLabel(key!))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal);
            var keyNames = keys.ToArray();
            var previewKeys = string.Join(", ", keyNames.Take(1));
            if (keyNames.Length > 1)
                previewKeys += " " + string.Format(Format, this["PrincipalOriginMoreKeys"], keyNames.Length - 1);
            var amount = presented[i];
            PrincipalOriginRows.Add(new(
                accounts.TryGetValue(group.Key.ResourceId, out var resource) ? AccountLabel(resource) : group.Key.ResourceId.ToString(),
                NodeLabel(primaryNode),
                this[primaryNode.Role == NodeRole.Auxiliary ? "PrincipalOriginAuxiliary" : "PrincipalOriginDirect"],
                StudyMoney(amount), string.Join(", ", keyNames), amount)
            { KeysPreview = previewKeys });
        }
        PrincipalOriginSummary = string.Format(Format,
            this["PrincipalOriginSummary"], selected.Node, selected.Total);
        OnPropertyChanged(nameof(PrincipalOriginSummary));
        OnPropertyChanged(nameof(PrincipalOriginTotal));
        PrincipalOriginsChanged?.Invoke(this, EventArgs.Empty);
    }
}
