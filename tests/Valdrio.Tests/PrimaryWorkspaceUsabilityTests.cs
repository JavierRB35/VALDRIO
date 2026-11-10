using Valdrio.Application;
using Valdrio.Calculation;
using Valdrio.Desktop;
using Valdrio.Domain;
using Valdrio.Localization;

namespace Valdrio.Tests;

public sealed partial class T06ImportConfirmationTests
{
    [TestMethod]
    public async Task PrimaryKeyCanChangeBeforeUseAndRenameAfterUseWithoutRewritingHistory()
    {
        EntityId studyId;
        string snapshotJson;
        using (var file = Baseline())
        {
            var w = await ImportedResources(file); studyId = w.Study.Id;
            var command = KeyCommand(w);
            var key = file.Allocations.SaveAllocationKey(command).After;
            var edited = command with { ExpectedRevision = key.Revision, RequestId = Id(), Name = "Edited unused",
                Mode = AllocationKeyMode.EqualShares, Lines = [new(w.Model.Structure.Nodes.Last().Id, 1m)] };
            key = file.Allocations.SaveAllocationKey(edited).After;
            var pending = new PrimaryRule(Id(), w.Study.CompanyId, w.Model.Structure.Id, AccountSelectorKind.Exact, w.Origins.Single().Code, 0, key.Id, false);
            file.Allocations.ChangeAllocations(AllocationCommand(w, new([pending], [], [], [], [])));
            var live = file.Allocations.ReadAllocations(w.Study.CompanyId, studyId);
            Assert.IsFalse(live.UsedKeyIds.Contains(key.Id));
            Assert.IsTrue(live.ProtectedKeyIds.Contains(key.Id),
                "Even an inactive rule persists a snapshot reference and protects the key from deletion.");
            key = file.Allocations.SaveAllocationKey(edited with { ExpectedRevision = key.Revision, RequestId = Id(), Lines = command.Lines.Select(l => l with { Value = 1m }).ToArray() }).After;
            file.Allocations.ChangeAllocations(AllocationCommand(live.Resources, new([pending with { IsActive = true }], [], [], [], [])));
            live = file.Allocations.ReadAllocations(w.Study.CompanyId, studyId);
            Assert.IsTrue(live.UsedKeyIds.Contains(key.Id));
            var facts = CanonicalSnapshotJson.Serialize(PrimaryAllocationService.Calculate(live).Facts);
            snapshotJson = file.Studies.Capture(new(w.Study.CompanyId, studyId, live.Resources.Study.Revision, Id(), "test")).CanonicalJson;
            var rename = edited with { ExpectedRevision = key.Revision, RequestId = Id(), Lines = key.Lines, Name = "Renamed used" };
            var renamed = file.Allocations.SaveAllocationKey(rename).After;
            Assert.AreEqual("Renamed used", renamed.Name);
            Assert.AreEqual(facts, CanonicalSnapshotJson.Serialize(PrimaryAllocationService.Calculate(file.Allocations.ReadAllocations(w.Study.CompanyId, studyId)).Facts));
            var before = Hash(file.Path);
            Assert.AreEqual("KEY_IMMUTABLE", Assert.ThrowsExactly<DomainRuleException>(() => file.Allocations.SaveAllocationKey(rename with
                { ExpectedRevision = renamed.Revision, RequestId = Id(), Lines = [new(w.Model.Structure.Nodes.Last().Id, 1m)] })).Diagnostic.Code);
            Assert.AreEqual(before, Hash(file.Path));
        }
        using var reopened = Service().Open(FileOf("current.valdrio"));
        Assert.AreEqual(snapshotJson, reopened.Studies.ReadSnapshots(reopened.Read().Id, studyId).Single().CanonicalJson);
        Assert.AreEqual("Renamed used", reopened.Allocations.ReadAllocations(reopened.Read().Id, studyId).Keys.Single().Name);
    }

    [TestMethod]
    public async Task PrimaryRulesSaveSeparatelyApplyAndRespectDirectTableAssignments()
    {
        using var ui = await ResourceUiReady();
        using var vm = new AllocationWorkspaceViewModel(ui.Company, ui.Studies, new UiTextCatalog(), ui.Preferences, ui.Dialogs);
        await vm.EnterAsync(); vm.Name = "First"; vm.Lines[0].Included = true; vm.Lines[0].Value = "100";
        await vm.SaveKeyCommand.ExecuteAsync(null); var first = vm.Keys.Single().Value.Id;
        await vm.NewKeyCommand.ExecuteAsync(null); vm.Name = "Second"; vm.Lines[1].Included = true; vm.Lines[1].Value = "100";
        await vm.SaveKeyCommand.ExecuteAsync(null); var second = vm.Keys.Single(k => k.Value.Name == "Second").Value.Id;
        vm.RuleAccount = vm.Accounts.First().Value; vm.RuleKey = first;
        await vm.SaveRuleCommand.ExecuteAsync(null);
        Assert.AreEqual(0, vm.PrimaryResults.Count); Assert.AreEqual(vm["RuleSavedOnly"], vm.Status);
        vm.SelectedKey = vm.Keys.Single(k => k.Value.Id == first);
        Assert.IsFalse(vm.DeleteKeyCommand.CanExecute(null),
            "An inactive saved rule protects its referenced key even though it does not allocate cost.");
        Assert.AreEqual(vm["DeletePrimaryAssigned"], vm.KeyDeleteHelp);
        await vm.ApplyRuleCommand.ExecuteAsync(null);
        Assert.AreEqual(1, vm.PrimaryResults.Count); var originalDestination = vm.PrimaryResults.Single().Destination;
        vm.RuleKey = second;
        await vm.SaveRuleCommand.ExecuteAsync(null);
        Assert.AreEqual(originalDestination, vm.PrimaryResults.Single().Destination);
        Assert.AreEqual(2, vm.PrimaryRules.Count);
        await vm.ApplyRuleCommand.ExecuteAsync(null);
        Assert.AreNotEqual(originalDestination, vm.PrimaryResults.Single().Destination);
        Assert.AreEqual(1, vm.PrimaryRules.Count);
        var part = vm.Coverage.Single().Id;
        await vm.AssignCoverageAsync(part, first);
        Assert.AreEqual(originalDestination, vm.PrimaryResults.Single().Destination);
        vm.SelectedRule = vm.PrimaryRules.Single(); vm.LoadRuleCommand.Execute(null);
        await vm.ApplyRuleCommand.ExecuteAsync(null);
        Assert.AreEqual(first, vm.Coverage.Single().SelectedKeyId);
        Assert.AreEqual(originalDestination, vm.PrimaryResults.Single().Destination);
        vm.SelectedKey = vm.Keys.Single(k => k.Value.Id == first); await vm.LoadKeyCommand.ExecuteAsync(null);
        Assert.IsTrue(vm.CanEditKey); Assert.IsFalse(vm.CanEditComposition);
        vm.Name = "Renamed in editor"; await vm.SaveKeyCommand.ExecuteAsync(null);
        Assert.AreEqual("Renamed in editor", vm.Coverage.Single().Key);
        Assert.IsFalse(vm.IsDirty);
    }

    [TestMethod]
    public async Task PrimaryKeyChangeAfterPartReplacementClearsObsoleteReferencesOnlyWithApproval()
    {
        EntityId companyId, studyId, oldPartId, newPartId, replacementKeyId;
        using (var file = Baseline())
        {
            var w = RouteForKey(file, await ImportedResources(file));
            companyId = w.Study.CompanyId;
            studyId = w.Study.Id;
            var oldPart = w.Resources.Single().Parts.Single();
            oldPartId = oldPart.Id;
            var first = file.Allocations.SaveAllocationKey(KeyCommand(w)).After;
            var second = file.Allocations.SaveAllocationKey(KeyCommand(w) with
            {
                KeyId = Id(), RequestId = Id(), Name = "Replacement key",
                Lines = [new(w.Model.Structure.Nodes.Last().Id, 100m)]
            }).After;
            replacementKeyId = second.Id;
            file.Allocations.ChangeAllocations(AllocationCommand(w,
                new AllocationConfiguration([], [], [new(oldPartId, first.Id)], [], [])));
            w = file.Resources.ReadResources(companyId, studyId);
            newPartId = Id();
            file.Resources.ChangeResources(Mutation(w, ResourceMutationKind.EditResource,
                w.Resources.Single().Id, [new ResourcePartDraft(newPartId, oldPart.Amount,
                    ResourceRoutingKind.AllocationPool, poolId: oldPart.PoolId)]));
            var changed = file.Allocations.ReadAllocations(companyId, studyId);
            Assert.IsTrue(changed.Coverage.Any(c => c.SourceId == oldPartId &&
                c.Diagnostic?.Code == "KEY_PART_CHANGED"));
        }

        using var ui = new ResourceUi(Service(), Catalog);
        await ui.Company.OpenPath(FileOf("current.valdrio"));
        ui.Studies.StudyChoice = ui.Studies.Studies.Single(s => s.Value.Id == studyId);
        await ui.Studies.LoadCommand.ExecuteAsync(null);
        using var vm = new AllocationWorkspaceViewModel(ui.Company, ui.Studies,
            new UiTextCatalog(), ui.Preferences, ui.Dialogs);
        await vm.EnterAsync();
        Assert.IsTrue(vm.HasObsolete);
        var before = Hash(ui.Company.ActivePath!);
        await vm.AssignCoverageAsync(newPartId, replacementKeyId);
        Assert.AreEqual(before, Hash(ui.Company.ActivePath!),
            "Declining cleanup must not alter the file or its historical references.");
        Assert.AreEqual(vm["ObsoleteSaveCancelled"], vm.Status);

        ui.Dialogs.ConfirmEconomic = true;
        await vm.AssignCoverageAsync(newPartId, replacementKeyId);
        Assert.IsFalse(vm.HasStatusError, vm.Status);
        Assert.IsFalse(vm.HasObsolete);
        Assert.AreEqual(replacementKeyId, vm.Coverage.Single().SelectedKeyId);
        var current = ui.Company.ActiveAllocations!.ReadAllocations(companyId, studyId);
        Assert.AreEqual(newPartId, current.Configuration.ExplicitAssignments.Single().PartId);
        Assert.IsFalse(current.Coverage.Any(c => c.Diagnostic?.Code == "KEY_PART_CHANGED"));
        Assert.AreEqual(1000m, PrimaryAllocationService.Calculate(current).Facts.Sum(f => f.PostedAmount.Amount));
        Assert.IsTrue(ui.Company.ActiveAllocations.ReadAllocationHistory(companyId, studyId)
            .Any(receipt => receipt.Request.Configuration.ExplicitAssignments.Any(a => a.PartId == oldPartId)),
            "Previous allocations must remain traceable in the saved history.");
    }
}
