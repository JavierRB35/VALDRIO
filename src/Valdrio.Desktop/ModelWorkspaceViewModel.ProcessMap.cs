using Valdrio.Domain;

namespace Valdrio.Desktop;

public sealed partial class ModelWorkspaceViewModel
{
    private string routeFeedback = "";
    public string RouteFeedback => routeFeedback;
    public bool CanEditProcessMap => Current?.Status == ModelStatus.Draft && CanEdit;
    public bool ProcessMapValidated => ProcessRouteGraph.IsValidated(draftNodes);
    public IReadOnlyList<CalculationNodeDefinition> ProcessMapNodes => draftNodes.ToArray();
    public IReadOnlyList<ProcessRouteLink> ProcessMapLinks => ProcessRouteGraph.Read(draftNodes);
    public ProcessRouteValidation ProcessMapCheck => ProcessRouteGraph.Check(draftNodes);
    public string ProcessMapSummary => draftNodes.Count == 0
        ? Text("Model.MapEmpty")
        : string.Format(Format, Text("Model.MapSummary"),
            draftNodes.Count, ProcessMapLinks.Count,
            ProcessMapCheck.Errors.Count, ProcessMapCheck.Warnings.Count) +
            (ProcessMapValidated ? " · " + Text("Model.MapValidated") : "");

    public bool TrySetProcessLink(ProcessRouteLink link, bool enabled)
    {
        if (!CanEditProcessMap) return false;
        var from = draftNodes.FindIndex(n => n.Id == link.Source);
        var dest = draftNodes.FirstOrDefault(n => n.Id == link.Destination);
        if (from < 0 || dest is null || !draftNodes[from].IsActive || !dest.IsActive)
        {
            routeFeedback = Text("Model.MapInvalidDestination");
            Refresh();
            return false;
        }
        var source = draftNodes[from];
        if (link.Kind == ProcessRouteKind.Sequence &&
            (source.Role != NodeRole.Principal || dest.Role != NodeRole.Principal ||
             source.Id == dest.Id) ||
            link.Kind == ProcessRouteKind.AuxiliaryService && source.Role != NodeRole.Auxiliary)
        {
            routeFeedback = Text("Model.MapInvalidConnection");
            Refresh();
            return false;
        }
        var changed = ProcessRouteGraph.WithLink(source, link, enabled);
        if (changed.FunctionTags.SequenceEqual(source.FunctionTags)) return true;
        draftNodes[from] = changed;
        dirty = true;
        routeFeedback = Text(enabled ? "Model.MapConnectionAdded" : "Model.MapConnectionRemoved");
        validation = [];
        Reformat();
        return true;
    }

    public void SetProcessPosition(EntityId id, ProcessRoutePoint position)
    {
        if (!CanEditProcessMap) return;
        var index = draftNodes.FindIndex(n => n.Id == id);
        if (index < 0) return;
        var changed = ProcessRouteGraph.WithPosition(draftNodes[index], position);
        if (changed.FunctionTags.SequenceEqual(draftNodes[index].FunctionTags)) return;
        draftNodes[index] = changed;
        dirty = true;
        Refresh();
    }

    public void AutoArrangeProcessMap()
    {
        if (!CanEditProcessMap) return;
        for (var i = 0; i < draftNodes.Count; i++)
            draftNodes[i] = ProcessRouteGraph.WithPosition(draftNodes[i], null);
        dirty = true;
        routeFeedback = Text("Model.MapArranged");
        Refresh();
    }

    public async Task ValidateProcessMapAsync()
    {
        if (!CanEditProcessMap) return;
        var check = ProcessMapCheck;
        if (!check.IsValid)
        {
            routeFeedback = string.Join(Environment.NewLine, check.Errors);
            Refresh();
            return;
        }
        var marked = ProcessRouteGraph.MarkValidated(draftNodes);
        draftNodes.Clear();
        draftNodes.AddRange(marked);
        dirty = true;
        await Save();
        routeFeedback = !IsDirty && diagnostic is null
            ? Text("Model.MapValidatedAndSaved")
            : Text("Model.MapSaveFailed");
        Refresh();
    }

    public void InvalidateProcessMapOnTagEdit()
    {
        // Changing a node's economic function or role invalidates an existing
        // map fingerprint without needing to rewrite the immutable marker.
        Refresh();
    }
}
