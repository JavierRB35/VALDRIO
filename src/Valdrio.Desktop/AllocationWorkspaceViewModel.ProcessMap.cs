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
    public string RouteProposalStatus
    {
        get
        {
            if (workspace is null) return this["RouteProposalUnavailable"];
            var nodes = workspace.Resources.Model.Structure.Nodes;
            var links = ProcessRouteGraph.Read(nodes);
            if (links.Count == 0) return this["RouteProposalNoLinks"];
            if (!ProcessRouteGraph.Check(nodes).IsValid) return this["RouteProposalInvalidLinks"];
            if (RouteProposals.Count == 0) return this["RouteProposalNoServices"];
            var count = string.Format(Format, this["RouteProposalCount"], RouteProposals.Count);
            return ProcessRouteGraph.IsValidated(nodes)
                ? count : count + " " + this["RouteProposalReviewMap"];
        }
    }
    public bool CanUseRouteProposal => SelectedRouteProposal is not null &&
        !IsDirty && !IsSecondaryClosed && CanManageSecondary && Stage == AllocationStage.Secondary;

    private void RebuildRouteProposals()
    {
        RouteProposals.Clear();
        SelectedRouteProposal = null;
        if (workspace is not { } w)
        {
            OnPropertyChanged(nameof(RouteProposalStatus));
            return;
        }
        var nodes = w.Resources.Model.Structure.Nodes.Where(n => n.IsActive).ToDictionary(n => n.Id);
        foreach (var proposal in ProcessRouteGraph.SuggestAuxiliaryDestinations(w.Resources.Model.Structure.Nodes)
            .OrderBy(p => nodes[p.AuxiliaryNodeId].Code, StringComparer.Ordinal))
        {
            RouteProposals.Add(new(proposal.AuxiliaryNodeId, NodeLabel(nodes[proposal.AuxiliaryNodeId]),
                proposal.DestinationNodeIds,
                string.Join(" · ", proposal.DestinationNodeIds.Select(id => NodeLabel(nodes[id])))));
        }
        SelectedRouteProposal = RouteProposals.FirstOrDefault();
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
