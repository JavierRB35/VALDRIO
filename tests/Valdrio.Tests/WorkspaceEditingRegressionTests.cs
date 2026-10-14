using Valdrio.Desktop;
using Valdrio.Domain;
using Valdrio.Localization;

namespace Valdrio.Tests;

public sealed partial class T06ImportConfirmationTests
{
    [TestMethod]
    public async Task IncompleteKeyIsRejectedBeforeDomainConstructionAndCanBeCompleted()
    {
        using var ui = await ResourceUiReady();
        using var vm = new AllocationWorkspaceViewModel(ui.Company, ui.Studies, new UiTextCatalog(), ui.Preferences, ui.Dialogs);
        await vm.EnterAsync();
        vm.Mode = AllocationKeyMode.DriverUnits; vm.Unit = vm.Units.First().Value;
        var before = Hash(ui.Company.ActivePath!);
        var invalidExceptions = 0;
        EventHandler<System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs> handler = (_, e) =>
        { if (e.Exception is DomainRuleException ex && ex.Diagnostic.Code == "KEY_INVALID") Interlocked.Increment(ref invalidExceptions); };
        AppDomain.CurrentDomain.FirstChanceException += handler;
        try
        {
            Assert.AreEqual(vm["NameRequired"], vm.Validation);
            Assert.IsFalse(vm.PublishCommand.CanExecute(null));
            Assert.IsFalse(vm.SaveDraftCommand.CanExecute(null));
            Assert.IsFalse(await vm.SaveKeyAsync(true));
            Assert.AreEqual(vm["NameRequired"], vm.Status);
            vm.Name = "   "; Assert.IsFalse(await vm.SaveKeyAsync(false));
            vm.Name = "Activity distribution"; vm.Unit = null;
            Assert.AreEqual(vm["UnitRequired"], vm.Validation);
            Assert.IsFalse(await vm.SaveKeyAsync(true));
            vm.Unit = vm.Units.First().Value;
            Assert.AreEqual(vm["DestinationsRequired"], vm.Validation);
            Assert.IsFalse(await vm.SaveKeyAsync(true));
            Assert.AreEqual(before, Hash(ui.Company.ActivePath!));
            Assert.IsTrue(vm.IsDirty);
            Assert.AreEqual(0, invalidExceptions);
        }
        finally { AppDomain.CurrentDomain.FirstChanceException -= handler; }
        // A named draft may still be incomplete; completing it allows publication.
        Assert.IsTrue(vm.SaveDraftCommand.CanExecute(null));
        Assert.IsTrue(await vm.SaveKeyAsync(false), vm.Status);
        vm.Mode = AllocationKeyMode.Percentage;
        vm.Lines.First().Included = true; vm.Lines.First().Value = "100";
        Assert.IsTrue(vm.PublishCommand.CanExecute(null));
        Assert.IsTrue(await vm.SaveKeyAsync(true), vm.Status);
        Assert.AreEqual("Activity distribution", vm.Keys.Single().Value.Name);
        Assert.IsFalse(vm.IsDirty);
    }

    [TestMethod]
    public async Task DriverKeyWithoutUnitIsPersistedAsDraftAndCanBeCompletedAfterReopen()
    {
        using var ui = await ResourceUiReady();
        using var vm = new AllocationWorkspaceViewModel(ui.Company, ui.Studies, new UiTextCatalog(), ui.Preferences, ui.Dialogs);
        await vm.EnterAsync();
        vm.Name = "Incomplete driver key";
        vm.Mode = AllocationKeyMode.DriverUnits;
        vm.Lines.First().Included = true;
        vm.Lines.First().Value = "10";
        Assert.IsNull(vm.Unit);
        Assert.IsFalse(vm.PublishCommand.CanExecute(null));
        Assert.IsTrue(vm.SaveDraftCommand.CanExecute(null));
        Assert.IsTrue(await vm.SaveKeyAsync(false), vm.Status);

        var draft = vm.Keys.Single().Value;
        Assert.AreEqual(ModelStatus.Draft, draft.Status);
        Assert.IsNull(draft.UnitId);
        Assert.AreEqual(10m, draft.Lines.Single().Value);
        Assert.AreEqual(0, vm.PublishedPrimary.Count);

        await vm.EnterAsync(true);
        Assert.AreEqual(draft.Id, vm.Keys.Single().Value.Id);
        Assert.IsNull(vm.Keys.Single().Value.UnitId);
        vm.SelectedKey = vm.Keys.Single();
        await vm.LoadKeyCommand.ExecuteAsync(null);
        vm.Unit = vm.Units.First().Value;
        Assert.IsTrue(vm.PublishCommand.CanExecute(null));
        Assert.IsTrue(await vm.SaveKeyAsync(true), vm.Status);
        Assert.AreEqual(ModelStatus.Published, vm.Keys.Single().Value.Status);
        Assert.AreEqual(1, vm.PublishedPrimary.Count);
    }

    [TestMethod]
    public async Task SavingAnIncompleteEditOfPublishedKeyCreatesDraftVersionWithoutChangingPublication()
    {
        using var ui = await ResourceUiReady();
        using var vm = new AllocationWorkspaceViewModel(ui.Company, ui.Studies, new UiTextCatalog(), ui.Preferences, ui.Dialogs);
        await vm.EnterAsync();
        vm.Name = "Published allocation";
        vm.Lines.First().Included = true;
        vm.Lines.First().Value = "100";
        Assert.IsTrue(await vm.SaveKeyAsync(true), vm.Status);
        var published = vm.Keys.Single().Value;

        vm.Lines.First().Value = "60";
        Assert.IsTrue(vm.SaveDraftCommand.CanExecute(null));
        Assert.IsFalse(vm.PublishCommand.CanExecute(null));
        Assert.IsTrue(await vm.SaveKeyAsync(false), vm.Status);
        Assert.AreEqual(2, vm.Keys.Count);

        var savedPublished = vm.Keys.Single(k => k.Value.Id == published.Id).Value;
        var draft = vm.Keys.Single(k => k.Value.Status == ModelStatus.Draft).Value;
        Assert.AreEqual(100m, savedPublished.Lines.Single().Value);
        Assert.AreEqual(ModelStatus.Published, savedPublished.Status);
        Assert.AreEqual(60m, draft.Lines.Single().Value);
        Assert.AreEqual(published.Id, draft.ParentVersionId);
        Assert.AreEqual(published.FamilyId, draft.FamilyId);
        Assert.AreEqual(2, draft.VersionNumber);
        Assert.AreEqual(published.Id, vm.PublishedPrimary.Single().Value);

        await vm.EnterAsync(true);
        Assert.AreEqual(2, vm.Keys.Count);
        Assert.AreEqual(60m, vm.Keys.Single(k => k.Value.Status == ModelStatus.Draft).Value.Lines.Single().Value);
        Assert.AreEqual(100m, vm.Keys.Single(k => k.Value.Status == ModelStatus.Published).Value.Lines.Single().Value);
    }

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
