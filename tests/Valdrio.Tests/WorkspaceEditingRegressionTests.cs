using Valdrio.Desktop;
using Valdrio.Domain;
using Valdrio.Localization;

namespace Valdrio.Tests;

public sealed partial class T06ImportConfirmationTests
{
    [TestMethod]
    public async Task ResourceSaveOnLeaveAppliesValidFormAndDoesNotPromptAgain()
    {
        using var ui = await ResourceUiReady();
        var vm = ui.Resources;
        await vm.OpenSelectedResourceCommand.ExecuteAsync(null);
        vm.PartChoice = vm.Parts.Single(); vm.LoadPartCommand.Execute(null);
        vm.PartForm.Route = ResourceRoutingKind.Excluded;
        vm.PartForm["Exclusion"] = "Outside the analysis";
        vm.Reason = "Reviewed account";
        ui.Dialogs.Next = PendingEditChoice.Save;
        Assert.IsTrue(await vm.CanLeaveAsync(), vm.Status);
        Assert.IsFalse(vm.IsDirty);
        Assert.IsTrue(await vm.CanLeaveAsync());
        Assert.AreEqual(1, ui.Dialogs.Prompts);
        var saved = ui.Company.ActiveResources!.ReadResources(ui.Company.Snapshot!.Id, ui.Studies.Current!.Id);
        Assert.AreEqual(ResourceRoutingKind.Excluded, saved.Resources.Single().Parts.Single().Routing);
        Assert.AreEqual(1, ui.Company.ActiveResources.ReadResourceHistory(saved.Study.CompanyId, saved.Study.Id).Count);
    }

    [TestMethod]
    public async Task ResourceInvalidSaveOnLeaveRetainsFormAndDoesNotWrite()
    {
        using var ui = await ResourceUiReady();
        var vm = ui.Resources;
        await vm.OpenSelectedResourceCommand.ExecuteAsync(null);
        vm.PartChoice = vm.Parts.Single(); vm.LoadPartCommand.Execute(null);
        vm.PartForm.Route = ResourceRoutingKind.Excluded;
        vm.Reason = "Reviewed account";
        var before = Hash(ui.Company.ActivePath!);
        ui.Dialogs.Next = PendingEditChoice.Save;
        Assert.IsFalse(await vm.CanLeaveAsync());
        Assert.IsTrue(vm.IsDirty);
        Assert.AreEqual(ResourceRoutingKind.Excluded, vm.PartForm.Route);
        Assert.AreEqual(before, Hash(ui.Company.ActivePath!));
    }

    [TestMethod]
    public async Task ResourceEditingKeepsSelectorSourcesStableAndUpdatesConditionalFields()
    {
        using var ui = await ResourceUiReady();
        var vm = ui.Resources;
        await vm.OpenSelectedResourceCommand.ExecuteAsync(null);
        var pools = vm.Pools; var units = vm.Units; var routes = vm.Routes;
        var changes = new List<string?>(); vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
        vm.PartForm.Route = ResourceRoutingKind.AllocationPool;
        vm.PartForm.PoolId = pools.First().Value;
        vm.PartForm["Amount"] = "1000";
        Assert.AreSame(pools, vm.Pools); Assert.AreSame(units, vm.Units); Assert.AreSame(routes, vm.Routes);
        Assert.IsTrue(vm.ShowPool); Assert.IsTrue(changes.Contains(nameof(vm.ShowPool)));
        Assert.IsFalse(changes.Any(string.IsNullOrEmpty));
        ui.Preferences.Update(ui.Preferences.Current with { UiLanguageTag = ui.Preferences.Current.UiLanguageTag == "en" ? "es" : "en" });
        Assert.AreNotSame(routes, vm.Routes);
        Assert.AreEqual(pools.First().Value, vm.PartForm.PoolId);
    }

    [TestMethod]
    public async Task AllocationTypingDoesNotRefreshCoverageOrSelectorSourcesAndSavesOnce()
    {
        var csv = "Amount;Ignored;Account\n" + string.Join("\n", Enumerable.Range(1, 200).Select(i => $"1000;x;{i:00000}"));
        using var ui = await ResourceUiReady(csv);
        using var vm = new AllocationWorkspaceViewModel(ui.Company, ui.Studies, new UiTextCatalog(), ui.Preferences, ui.Dialogs);
        await vm.EnterAsync();
        var accounts = vm.Accounts; var modes = vm.Modes; var parts = vm.Parts;
        var changes = new List<string?>(); vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
        foreach (var letter in "Distribution by consumption") vm.Name += letter;
        vm.Lines.First().Included = true; vm.Lines.First().Value = "100";
        Assert.AreSame(accounts, vm.Accounts); Assert.AreSame(modes, vm.Modes); Assert.AreSame(parts, vm.Parts);
        Assert.IsFalse(changes.Any(string.IsNullOrEmpty));
        Assert.IsFalse(changes.Contains(nameof(vm.CoverageSummary)));
        ui.Dialogs.Next = PendingEditChoice.Save;
        Assert.IsTrue(await vm.CanLeaveAsync(), vm.Status);
        Assert.IsFalse(vm.IsDirty); Assert.IsTrue(await vm.CanLeaveAsync());
        Assert.AreEqual(1, ui.Dialogs.Prompts);
        Assert.AreEqual("Distribution by consumption", vm.Keys.Single().Value.Name);
    }
}
