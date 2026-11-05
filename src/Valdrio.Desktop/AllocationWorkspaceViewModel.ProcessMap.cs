using System.Collections.ObjectModel;
using Valdrio.Domain;

namespace Valdrio.Desktop;

public sealed record RouteDestinationProposal(
    EntityId SourceId, string Source, IReadOnlyList<EntityId> DestinationIds, string Destinations);

public sealed partial class AllocationWorkspaceViewModel
{
    private RouteDestinationProposal? selectedRouteProposal;
    public ObservableCollection<RouteDestinationProposal> RouteProposals { get; } = [];
    public RouteDestinationProposal? SelectedRouteProposal
    {
        get => selectedRouteProposal;
        set
        {
            if (selectedRouteProposal == value) return;
            selectedRouteProposal = value;
            OnPropertyChanged(nameof(SelectedRouteProposal));
            OnPropertyChanged(nameof(CanUseRouteProposal));
        }
    }
    public string RouteProposalTitle => this["RouteProposalTitle"];
    public string RouteProposalHelp => this["RouteProposalHelp"];
    public string RouteProposalActionLabel => this["RouteProposalAction"];
    public string RouteProposalStatus => RouteProposals.Count == 0
        ? this["RouteProposalUnavailable"]
        : string.Format(Format, this["RouteProposalCount"], RouteProposals.Count);
    public bool CanUseRouteProposal => SelectedRouteProposal is not null &&
        !IsDirty && !IsSecondaryClosed && CanManageSecondary && Stage == AllocationStage.Secondary;

    private void RebuildRouteProposals()
    {
        RouteProposals.Clear();
        SelectedRouteProposal = null;
        if (workspace is not { } w || !ProcessRouteGraph.IsValidated(w.Resources.Model.Structure.Nodes))
        {
            OnPropertyChanged(nameof(RouteProposalStatus));
            return;
        }
        var nodes = w.Resources.Model.Structure.Nodes.Where(n => n.IsActive).ToDictionary(n => n.Id);
        foreach (var group in ProcessRouteGraph.Read(w.Resources.Model.Structure.Nodes)
            .Where(link => link.Kind == ProcessRouteKind.AuxiliaryService &&
                nodes.TryGetValue(link.Source, out var src) && src.Role == NodeRole.Auxiliary &&
                nodes.ContainsKey(link.Destination))
            .GroupBy(link => link.Source).OrderBy(g => nodes[g.Key].Code, StringComparer.Ordinal))
        {
            var targets = group.Select(link => link.Destination).Distinct().ToArray();
            RouteProposals.Add(new(group.Key, NodeLabel(nodes[group.Key]),
                targets, string.Join(" · ", targets.Select(id => NodeLabel(nodes[id])))));
        }
        OnPropertyChanged(nameof(RouteProposalStatus));
    }

    public void PrepareRouteProposal()
    {
        if (!CanUseRouteProposal || SelectedRouteProposal is not { } proposed) return;
        var targets = proposed.DestinationIds.ToHashSet();
        // No write here: copy the validated destination topology into the
        // ordinary editable KEY DRAFT. Weights remain zero/unprovided.
        // Existing published keys and secondary routing rules remain intact.
        LoadKey(null);
        Stage = AllocationStage.Secondary;
        Mode = AllocationKeyMode.Percentage;
        Name = this["RouteProposalPrefix"] + " · " + proposed.Source;
        foreach (var line in Lines)
        {
            line.Included = targets.Contains(line.Id);
            line.Value = "0";
        }
        keyDirty = true;
        statusKey = "RouteProposalPrepared";
        Refresh();
    }
}
