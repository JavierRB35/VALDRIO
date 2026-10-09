using Valdrio.Application;
using Valdrio.Domain;

namespace Valdrio.Desktop;

public sealed partial class ModelWorkspaceViewModel
{
    private StudyWorkspaceViewModel? nodeStudies;
    private ResourceWorkspaceSnapshot? nodeWorkspace;
    private bool studyNodesOpen;
    private bool EditingStudyNodes => Current?.Status == ModelStatus.Published && nodeWorkspace is not null &&
        nodeStudies?.Current?.Id == nodeWorkspace.Study.Id && Current.Structure.Id == nodeWorkspace.Model.Structure.Id;
    public bool CanEditStudyNodes => EditingStudyNodes && studyNodesOpen;
    public bool ShowPublishedNodeLock => IsPublished && !CanEditStudyNodes;

    public void AttachStudyNodes(StudyWorkspaceViewModel studies) => nodeStudies = studies;

    public async Task LoadStudyNodesAsync()
    {
        if (!await CanLeaveAsync()) return;
        nodeWorkspace = null; studyNodesOpen = false;
        if (nodeStudies?.Current is not { } study || company.ActiveAllocations is null) { Refresh(); return; }
        await Run(async token =>
        {
            var allocation = await Task.Run(() => company.ActiveAllocations.ReadAllocations(study.CompanyId, study.Id, token));
            nodeWorkspace = allocation.Resources;
            studyNodesOpen = study.State == StudyState.Open && !allocation.Configuration.PrimaryClosed;
            var published = await Task.Run(() => company.ActiveModels!.ReadModels(token));
            var original = published.Single(m => m.Structure.Id == study.SelectedModelVersionId);
            SetCurrent(original with { Structure = nodeWorkspace.Model.Structure });
        });
    }

    private async Task SaveStudyNodesAsync()
    {
        await Run(async token =>
        {
            var workspace = nodeWorkspace!;
            var study = workspace.Study;
            var structure = Structure();
            var command = new ResourceMutation(ResourceMutationKind.EditStudyNodes, study.CompanyId, study.Id,
                study.Revision, new EntityId(Guid.NewGuid()), "local-user", Text("Model.StudyNodesSaved"),
                null, [], [], null, null, [], []) { NodeEdits = new(structure.Nodes, structure.Pools) };
            var receipt = await Task.Run(() => company.ActiveResources!.ChangeResources(command, token));
            await nodeStudies!.ApplyResourceReceiptAsync(receipt.ConfirmedStudy);
            nodeWorkspace = await Task.Run(() => company.ActiveResources!.ReadResources(study.CompanyId, study.Id, token));
            SetCurrent(Current! with { Structure = nodeWorkspace.Model.Structure });
            statusKey = "Model.StudyNodesSaved";
        });
    }
}
