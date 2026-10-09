using System.Globalization;
using Valdrio.Application;
using Valdrio.Desktop;
using Valdrio.Domain;
using Valdrio.Localization;

namespace Valdrio.Tests;

public sealed partial class T06ImportConfirmationTests
{
    [TestMethod]
    [DataRow("1")]
    [DataRow("-1")]
    public async Task T11_ComponentRoundingPreservesAccountTotalsAcrossNodes(string amount)
    {
        using var file = Baseline();
        var w = await ImportedResources(file, $"Amount;Ignored;Account\n{amount};x;00001");
        var resource = w.Resources.Single(); var part = resource.Parts.Single();
        file.Resources.ChangeResources(Mutation(w, ResourceMutationKind.ClassifyBatch,
            replacements: [new(resource.Id, [part.WithClassification(part.Classification with
                { Behavior = ResourceBehavior.Mixed, VariablePercentage = 0.000001m })])]));
        w = file.Resources.ReadResources(w.Study.CompanyId, w.Study.Id);
        var destinations = w.Model.Structure.Nodes.Where(n => n.IsActive).Take(2).ToArray();
        Assert.AreEqual(2, destinations.Length);
        var key = file.Allocations.SaveAllocationKey(KeyCommand(w,
            lines: destinations.Select(n => new AllocationKeyLine(n.Id, 50m)).ToArray())).After;
        file.Allocations.ChangeAllocations(AllocationCommand(w, new AllocationConfiguration([], [],
            [new ExplicitPrimaryAssignment(part.Id, key.Id)], [], [])));
        var result = PrimaryAllocationService.Calculate(file.Allocations.ReadAllocations(w.Study.CompanyId, w.Study.Id));
        Assert.IsTrue(result.IsComplete);
        foreach (var behavior in new[] { ResourceBehavior.Fixed, ResourceBehavior.Variable })
            Assert.AreEqual(decimal.Parse(amount, CultureInfo.InvariantCulture) * (behavior == ResourceBehavior.Variable ? 0.00000001m : 0.99999999m),
                result.Facts.Where(f => f.Behavior == behavior).Sum(f => f.PostedAmount.Amount));
    }

    [TestMethod]
    [DataRow(100)]
    [DataRow(-100)]
    public async Task T11_WeightedCompositionSurvivesPrimaryReciprocalCloseAndReopen(int amount)
    {
        EntityId companyId, studyId;
        string publication;
        using (var file = Baseline())
        {
            var w = await ImportedResources(file, $"Amount;Ignored;Account\n{amount};x;00001");
            companyId = w.Study.CompanyId; studyId = w.Study.Id;
            var resource = w.Resources.Single(); var part = resource.Parts.Single();
            var mixed = part.Classification with { Behavior = ResourceBehavior.Mixed, VariablePercentage = 30m };
            file.Resources.ChangeResources(Mutation(w, ResourceMutationKind.ClassifyBatch,
                replacements: [new(resource.Id, [part.WithClassification(mixed)])]));
            w = file.Resources.ReadResources(companyId, studyId);
            var aux = w.Model.Structure.Nodes.First(n => n.Role == NodeRole.Auxiliary);
            var principal = w.Model.Structure.Nodes.First(n => n.Role == NodeRole.Principal);
            var primaryKey = file.Allocations.SaveAllocationKey(KeyCommand(w, lines: [new(aux.Id, 100m)])).After;
            var secondaryKey = file.Allocations.SaveAllocationKey(KeyCommand(w, stage: AllocationStage.Secondary,
                lines: [new(aux.Id, 50m), new(principal.Id, 50m)]) with { SelfAllocation = SelfAllocationPolicy.Allowed }).After;
            var config = new AllocationConfiguration([], [new SecondaryRule(Id(), companyId, w.Model.Structure.Id, aux.Id, "all", secondaryKey.Id, true)],
                [new ExplicitPrimaryAssignment(part.Id, primaryKey.Id)], [], []);
            file.Allocations.ChangeAllocations(AllocationCommand(w, config));
            var workspace = file.Allocations.ReadAllocations(companyId, studyId);
            var primary = PrimaryAllocationService.Calculate(workspace);
            Assert.IsTrue(primary.IsComplete);
            Assert.AreEqual(amount * .7m, primary.Facts.Where(f => f.Behavior == ResourceBehavior.Fixed).Sum(f => f.PostedAmount.Amount));
            Assert.AreEqual(amount * .3m, primary.Facts.Where(f => f.Behavior == ResourceBehavior.Variable).Sum(f => f.PostedAmount.Amount));
            file.Allocations.ChangeAllocations(AllocationCommand(workspace.Resources, config.WithPrimaryClosed(true)));
            workspace = file.Allocations.ReadAllocations(companyId, studyId);
            var secondary = SecondaryAllocationService.Calculate(workspace);
            Assert.IsTrue(secondary.IsComplete);
            Assert.AreEqual(amount * .7m, secondary.Facts.Where(f => f.Behavior == ResourceBehavior.Fixed).Sum(f => f.PostedAmount.Amount));
            Assert.AreEqual(amount * .3m, secondary.Facts.Where(f => f.Behavior == ResourceBehavior.Variable).Sum(f => f.PostedAmount.Amount));
            Assert.AreEqual(1, secondary.Reconciliations.Count);
            Assert.AreEqual(0m, secondary.ResourceBridges.Single().Difference.Amount);
            Assert.IsTrue(secondary.ExplanationLinks.All(l => l.SourceBehavior.HasValue));
            var closed = file.Allocations.ChangeAllocations(AllocationCommand(workspace.Resources,
                workspace.Configuration.WithSecondaryClosed(true, SecondaryAllocationService.ResultFingerprint(secondary, workspace.Configuration))));
            publication = CanonicalSnapshotJson.Serialize(closed.SecondaryPublication!);
            var snapshot = file.Studies.Capture(new(companyId, studyId, closed.ConfirmedStudy.Revision, Id(), "test"));
            snapshot.EnsureValid();
            Assert.AreEqual(CanonicalSnapshotJson.Serialize(secondary.Facts),
                CanonicalSnapshotJson.Serialize(SecondaryAllocationService.Calculate((AllocationStudySnapshotInput)snapshot.Input).Facts));
        }
        using var reopened = Service().Open(FileOf("current.valdrio"));
        var result = reopened.Allocations.ReadAllocations(companyId, studyId);
        Assert.AreEqual(publication, CanonicalSnapshotJson.Serialize(result.SecondaryPublication!));
        Assert.AreEqual(30m, result.Resources.Resources.Single().Parts.Single().Classification.VariablePercentage);
    }

    [TestMethod]
    public async Task T11_StudyNodeEditsPreservePublicationAndLockAtPrimaryClose()
    {
        EntityId companyId, studyId, nodeId;
        using (var file = Baseline())
        {
            var w = await ImportedResources(file);
            companyId = w.Study.CompanyId; studyId = w.Study.Id;
            var model = w.Model.Structure; var node = model.Nodes.First(n => n.Role == NodeRole.Principal); nodeId = node.Id;
            var baseline = CanonicalSnapshotJson.Serialize(file.Models.ReadModels().Single(m => m.Structure.Id == model.Id));
            var edits = new StudyNodeEdits(model.Nodes.Select(n => n.Id == node.Id ? new CalculationNodeDefinition(n.Id, n.ModelVersionId, n.PhysicalCenterId, "EDITADO", n.Role, n.FunctionTags, n.IsActive) : n).ToArray(),
                model.Pools.Select(p => p.NodeId == node.Id ? p with { Description = "Pool editado" } : p).ToArray());
            var mutation = Mutation(w, ResourceMutationKind.EditStudyNodes);
            mutation = new(mutation.Kind, mutation.CompanyId, mutation.StudyId, mutation.ExpectedRevision, mutation.RequestId,
                mutation.Actor, mutation.Reason, null, [], [], null, null, [], []) { NodeEdits = edits };
            file.Resources.ChangeResources(mutation);
            w = file.Resources.ReadResources(companyId, studyId);
            Assert.AreEqual("EDITADO", w.Model.Structure.Nodes.Single(n => n.Id == nodeId).Code);
            Assert.AreEqual(baseline, CanonicalSnapshotJson.Serialize(file.Models.ReadModels().Single(m => m.Structure.Id == model.Id)));
            var key = file.Allocations.SaveAllocationKey(KeyCommand(w, lines: [new(nodeId, 100m)]) with
                { ValidationStudyId = studyId, ValidationStudyRevision = w.Study.Revision }).After;
            var config = new AllocationConfiguration([], [], [new ExplicitPrimaryAssignment(w.Resources.Single().Parts.Single().Id, key.Id)], [], []);
            file.Allocations.ChangeAllocations(AllocationCommand(w, config));
            var current = file.Allocations.ReadAllocations(companyId, studyId);
            file.Allocations.ChangeAllocations(AllocationCommand(current.Resources, config.WithPrimaryClosed(true)));
            current = file.Allocations.ReadAllocations(companyId, studyId);
            var forbidden = new ResourceMutation(ResourceMutationKind.EditStudyNodes, companyId, studyId, current.Resources.Study.Revision,
                Id(), "test", "edit", null, [], [], null, null, [], []) { NodeEdits = edits };
            Assert.ThrowsExactly<DomainRuleException>(() => file.Resources.ChangeResources(forbidden));
            file.Studies.Capture(new(companyId, studyId, current.Resources.Study.Revision, Id(), "test")).EnsureValid();
        }
        using var reopened = Service().Open(FileOf("current.valdrio"));
        Assert.AreEqual("EDITADO", reopened.Resources.ReadResources(companyId, studyId).Model.Structure.Nodes.Single(n => n.Id == nodeId).Code);
    }

    [TestMethod]
    public async Task T11_ClassificationHistoryLoadsInBothLanguagesAndWeightsValidate()
    {
        using var ui = await ResourceUiReady();
        using var vm = new AllocationWorkspaceViewModel(ui.Company, ui.Studies, new UiTextCatalog(), ui.Preferences, ui.Dialogs);
        await vm.EnterAsync();
        vm.CompositionAccounts.Single().Selected = true;
        vm.CompositionFixedPercentage = "70"; vm.CompositionVariablePercentage = "40";
        var unchanged = Hash(ui.Company.ActivePath!);
        await vm.CompositionApplyWeightsCommand.ExecuteAsync(null);
        Assert.AreEqual(unchanged, Hash(ui.Company.ActivePath!));
        vm.CompositionVariablePercentage = "30";
        await vm.CompositionApplyWeightsCommand.ExecuteAsync(null);
        await ui.Resources.EnterAsync(true);
        Assert.IsFalse(string.IsNullOrWhiteSpace(ui.Resources.History));
        Assert.IsTrue(vm.CompositionAccounts.Single().Behavior.Contains("70"));
        var catalog = new UiTextCatalog();
        foreach (var culture in new[] { "es", "en" })
        foreach (var kind in Enum.GetValues<ResourceMutationKind>())
            Assert.IsFalse(string.IsNullOrWhiteSpace(catalog.Get("Resources." + kind, CultureInfo.GetCultureInfo(culture))));
    }
}
