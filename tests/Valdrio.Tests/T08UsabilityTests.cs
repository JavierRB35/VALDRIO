using Valdrio.Application;
using Valdrio.Desktop;
using Valdrio.Domain;
using Valdrio.Localization;
namespace Valdrio.Tests;
public sealed partial class T06ImportConfirmationTests
{
    [TestMethod]
    public async Task T08EditorDraftPublishVersionAndCapturedRegionalFormat()
    {
        using var ui = await ResourceUiReady();
        using var vm = new AllocationWorkspaceViewModel(ui.Company, ui.Studies, new UiTextCatalog(), ui.Preferences, ui.Dialogs);
        await vm.EnterAsync(); Assert.IsTrue(vm.CanEdit); Assert.IsFalse(vm.IsDirty);
        vm.Name = "Reparto de superficie";
        var destinations = vm.Lines.Take(2).ToArray();
        destinations[0].Included = destinations[1].Included = true;
        destinations[0].Value = "40"; destinations[1].Value = "40";
        Assert.IsTrue(await vm.SaveKeyAsync(false), vm.Status); Assert.IsFalse(await vm.SaveKeyAsync(true));
        destinations = vm.Lines.Where(l => l.Included).ToArray();
        destinations[0].Value = "40,5"; destinations[1].Value = "59,5";
        ui.Preferences.Update(ui.Preferences.Current with { UiLanguageTag = "en", FormatCultureTag = "en-US" });
        Assert.IsTrue(await vm.SaveKeyAsync(true), vm.Status);
        Assert.IsFalse(vm.CanEditKey); Assert.AreEqual(vm["Complete"], vm.Validation);
        Assert.AreEqual(100m, vm.Keys.Single().Value.Lines.Sum(l => l.Value));
        vm.SelectedKey = vm.Keys.Single(); await vm.NewVersionCommand.ExecuteAsync(null);
        Assert.IsTrue(vm.CanEditKey); Assert.IsTrue(vm.IsDirty);
        Assert.IsTrue(await vm.SaveKeyAsync(true), vm.Status);
        Assert.AreEqual(2, vm.Keys.Count); Assert.AreEqual(2, vm.Keys.Max(k => k.Value.VersionNumber));
    }
    [TestMethod]
    public async Task T08EditorRuleFieldsDoNotDisappearOnCancelOrStudyChange()
    {
        using var ui = await ResourceUiReady();
        using var vm = new AllocationWorkspaceViewModel(ui.Company, ui.Studies, new UiTextCatalog(), ui.Preferences, ui.Dialogs);
        await vm.EnterAsync(); vm.Name = "Working draft"; ui.Dialogs.Next = PendingEditChoice.Cancel;
        Assert.IsFalse(await vm.CanLeaveAsync()); Assert.AreEqual("Working draft", vm.Name);
        ui.Dialogs.Next = PendingEditChoice.Save; Assert.IsTrue(await vm.CanLeaveAsync(), vm.Status);
        vm.SelectorKind = AccountSelectorKind.Prefix; vm.Prefix = "600";
        Assert.IsFalse(await vm.CanLeaveAsync()); Assert.AreEqual(vm["ApplyRuleFirst"], vm.Status);
        Assert.AreEqual("600", vm.Prefix);
        ui.Dialogs.Next = PendingEditChoice.Discard; Assert.IsTrue(await vm.CanLeaveAsync()); Assert.IsFalse(vm.IsDirty);
    }
    [TestMethod]
    public async Task T08AbsoluteOtherRuleOrNameChangeDoesNotInvalidateUnchangedEconomicPart()
    {
        using var file = Baseline(); var w = RouteForKey(file, await ImportedResources(file)); var part = w.Resources.Single().Parts.Single();
        var absolute = new AbsolutePartAllocation(part.Id, w.EconomicRevision, part.Amount, [new(w.Model.Structure.Nodes[0].Id, Eur(1000))]);
        var config = new AllocationConfiguration([], [], [], [absolute], []);
        file.Allocations.ChangeAllocations(AllocationCommand(w, config));
        var current = file.Allocations.ReadAllocations(w.Study.CompanyId, w.Study.Id);
        file.Allocations.ChangeAllocations(AllocationCommand(current.Resources, config));
        current = file.Allocations.ReadAllocations(w.Study.CompanyId, w.Study.Id);
        Assert.IsNull(current.Coverage.Single(c => c.SourceId == part.Id).Diagnostic);
        var snapshot = file.Studies.Capture(new(w.Study.CompanyId, w.Study.Id, current.Resources.Study.Revision, Id(), "test"));
        snapshot.EnsureValid();
        Assert.IsNull(((AllocationStudySnapshotInput)snapshot.Input).Allocations.Coverage.Single(c => c.SourceId == part.Id).Diagnostic);
    }
}
