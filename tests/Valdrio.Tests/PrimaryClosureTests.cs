using Valdrio.Application;
using Valdrio.Desktop;
using Valdrio.Domain;
using Valdrio.Infrastructure;
using Valdrio.Localization;

namespace Valdrio.Tests;

public sealed partial class T06ImportConfirmationTests
{
    [TestMethod]
    public async Task UnusedKeyDeletionIsAuditedAndSurvivesReopenButReferencedKeyCannotBeDeleted()
    {
        EntityId studyId, keptId;
        using (var file = Baseline())
        {
            var w = await ImportedResources(file); studyId = w.Study.Id;
            var command = KeyCommand(w, publish: false, lines: []);
            var key = file.Allocations.SaveAllocationKey(command).After;
            var delete = command with { ExpectedRevision = key.Revision, RequestId = Id(), Delete = true };
            var receipt = file.Allocations.SaveAllocationKey(delete);
            Assert.IsTrue(receipt.After.IsDeleted);
            Assert.AreEqual(0, file.Allocations.ReadAllocationKeys(w.Study.CompanyId, w.Model.Structure.Id).Count);
            Assert.AreEqual(CanonicalSnapshotJson.Serialize(receipt), CanonicalSnapshotJson.Serialize(file.Allocations.SaveAllocationKey(delete)));
            Assert.AreEqual("KEY_DELETED", Assert.ThrowsExactly<DomainRuleException>(() => file.Allocations.SaveAllocationKey(command with
                { ExpectedRevision = receipt.After.Revision, RequestId = Id() })).Diagnostic.Code);
            command = KeyCommand(w); key = file.Allocations.SaveAllocationKey(command).After; keptId = key.Id;
            file.Allocations.ChangeAllocations(AllocationCommand(w, new([], [], [new(w.Resources.Single().Parts.Single().Id, key.Id)], [], [])));
            Assert.AreEqual("KEY_DELETE_USED", Assert.ThrowsExactly<DomainRuleException>(() => file.Allocations.SaveAllocationKey(command with
                { ExpectedRevision = key.Revision, RequestId = Id(), Delete = true })).Diagnostic.Code);
        }
        using var reopened = Service().Open(FileOf("current.valdrio"));
        Assert.AreEqual(keptId, reopened.Allocations.ReadAllocations(reopened.Read().Id, studyId).Keys.Single().Id);
    }

    [TestMethod]
    public async Task PrimaryCloseRequiresCompleteAllocationLocksAmountsAndPersistsUntilExplicitReopen()
    {
        EntityId studyId;
        using (var file = Baseline())
        {
            var w = await ImportedResources(file); studyId = w.Study.Id;
            Assert.AreEqual("PRIMARY_INCOMPLETE", Assert.ThrowsExactly<DomainRuleException>(() => file.Allocations.ChangeAllocations(
                AllocationCommand(w, AllocationConfiguration.Empty.WithPrimaryClosed(true)))).Diagnostic.Code);
            var key = file.Allocations.SaveAllocationKey(KeyCommand(w)).After;
            var config = new AllocationConfiguration([], [], [new(w.Resources.Single().Parts.Single().Id, key.Id)], [], []);
            file.Allocations.ChangeAllocations(AllocationCommand(w, config.WithPrimaryClosed(true)));
            var closed = file.Allocations.ReadAllocations(w.Study.CompanyId, studyId);
            Assert.IsTrue(closed.Configuration.PrimaryClosed);
            var correction = ResourceCorrectionCommand.Prepare(closed.Resources, w.Resources.Single().Id, closed.Resources.Study.Revision,
                Eur(-100), Catalog.GetRequired(new("EUR")).Freeze(), Id(), Id(), Id(), "test")!;
            Assert.AreEqual("PRIMARY_CLOSED", Assert.ThrowsExactly<DomainRuleException>(() => file.Resources.ChangeResources(correction)).Diagnostic.Code);
            Assert.AreEqual("PRIMARY_CLOSED", Assert.ThrowsExactly<DomainRuleException>(() => file.Allocations.ChangeAllocations(
                AllocationCommand(closed.Resources, AllocationConfiguration.Empty))).Diagnostic.Code);
        }
        using var reopened = Service().Open(FileOf("current.valdrio"));
        var current = reopened.Allocations.ReadAllocations(reopened.Read().Id, studyId);
        Assert.IsTrue(current.Configuration.PrimaryClosed);
        reopened.Allocations.ChangeAllocations(AllocationCommand(current.Resources, current.Configuration.WithPrimaryClosed(false)));
        current = reopened.Allocations.ReadAllocations(reopened.Read().Id, studyId);
        var change = ResourceCorrectionCommand.Prepare(current.Resources, current.Resources.Resources.Single().Id, current.Resources.Study.Revision,
            Eur(-100), Catalog.GetRequired(new("EUR")).Freeze(), Id(), Id(), Id(), "test")!;
        reopened.Resources.ChangeResources(change);
        Assert.AreEqual(900m, reopened.Resources.ReadResources(reopened.Read().Id, studyId).Preparation.Coverage.Single().Effective.Amount);
    }

    [TestMethod]
    public async Task AttachingImportToPersistedClosedPrimaryDisablesConfirmationBeforeReview()
    {
        EntityId studyId;
        using (var file = Baseline())
        {
            var workspace = await ImportedResources(file);
            studyId = workspace.Study.Id;
            var key = file.Allocations.SaveAllocationKey(KeyCommand(workspace)).After;
            var configuration = new AllocationConfiguration([], [],
                [new(workspace.Resources.Single().Parts.Single().Id, key.Id)], [], []);
            file.Allocations.ChangeAllocations(
                AllocationCommand(workspace, configuration.WithPrimaryClosed(true)));
        }

        var before = Hash(FileOf("current.valdrio"));
        var texts = new UiTextCatalog();
        var preferences = new PresentationPreferencesService(
            new LanguageCatalog(), UserPresentationPreferences.Default);
        var formatter = new PresentationFormatter(new DisplayTimeZoneResolver());
        var dialogs = new ResourceDialogs();
        using var company = new CompanyFileViewModel(
            Service(), dialogs, Catalog, texts, preferences, formatter);
        using var import = new ImportColumnMappingViewModel(
            new ImportSourceSessionFactory(), new Dialogs(FileOf("unused.csv")), texts, preferences);
        using var studies = new StudyWorkspaceViewModel(
            company, Catalog, texts, preferences, formatter, dialogs) { ImportColumns = import };
        await company.OpenPath(FileOf("current.valdrio"));
        studies.StudyChoice = studies.Studies.Single(s => s.Value.Id == studyId);
        await studies.LoadCommand.ExecuteAsync(null);
        Assert.IsTrue(await studies.AttachImportTarget());

        Assert.IsTrue(import.HasClosedPrimaryDestination);
        Assert.IsFalse(import.CanSaveReviewedImport);
        Assert.IsFalse(import.ConfirmCommand.CanExecute(null));
        Assert.IsFalse(string.IsNullOrWhiteSpace(import.PrimaryClosedNotice));
        Assert.IsTrue(import.IsIdle);
        Assert.AreEqual(before, Hash(FileOf("current.valdrio")));
    }

    [TestMethod]
    public async Task ClosedPrimaryImportReviewIsVisibleButCannotBeSaved()
    {
        using var file = Baseline();
        var study = Study(file);
        using var vm = await Vm(file, study, csv: "Amount;Ignored;Account\n77;x;NEW");
        var prepared = vm.Preparation;
        var before = Hash(file.Path);
        Assert.IsTrue(vm.ConfirmCommand.CanExecute(null), vm.UserStatus);

        // The import editor can still inspect its prepared values, but a closed
        // destination must never offer the write operation.
        vm.BindTarget(file.Imports, study, true, null, primaryClosed: true);
        Assert.IsTrue(vm.HasClosedPrimaryDestination);
        Assert.IsFalse(vm.CanSaveReviewedImport);
        Assert.IsFalse(vm.ConfirmCommand.CanExecute(null));
        Assert.AreEqual(vm.PrimaryClosedNotice, vm.ReviewSaveRequirement);
        Assert.IsTrue(vm.HasUserError);
        Assert.AreSame(prepared, vm.Preparation);
        Assert.IsTrue(vm.IsIdle);
        Assert.AreEqual(before, Hash(file.Path));
    }

    [TestMethod]
    public async Task ClosingPrimaryAfterImportReviewReportsErrorWithoutLosingPreparedRowsOrBusyState()
    {
        using var file = Baseline();
        var resources = await ImportedResources(file);
        using var vm = await Vm(file, resources.Study,
            csv: "Amount;Ignored;Account\n77;x;NEW");
        Assert.IsTrue(vm.ConfirmCommand.CanExecute(null), vm.UserStatus);
        var prepared = vm.Preparation;

        var key = file.Allocations.SaveAllocationKey(KeyCommand(resources)).After;
        var config = new AllocationConfiguration([], [],
            [new(resources.Resources.Single().Parts.Single().Id, key.Id)], [], []);
        file.Allocations.ChangeAllocations(AllocationCommand(resources, config.WithPrimaryClosed(true)));
        var before = Hash(file.Path);
        var count = file.Imports.ReadImports(resources.Study.CompanyId, resources.Study.Id).Count;

        // The primary closure changed after the review was prepared.
        // Persistence rejects the write; the view model must handle it.
        await vm.ConfirmCommand.ExecuteAsync(null);

        Assert.AreEqual(before, Hash(file.Path));
        Assert.AreEqual(count, file.Imports.ReadImports(resources.Study.CompanyId, resources.Study.Id).Count);
        Assert.IsTrue(vm.Status.StartsWith("PRIMARY_CLOSED", StringComparison.Ordinal), vm.Status);
        Assert.IsTrue(vm.HasUserError);
        Assert.IsTrue(vm.HasClosedPrimaryDestination);
        Assert.IsTrue(vm.IsIdle);
        Assert.IsFalse(vm.ConfirmCommand.CanExecute(null));
        Assert.AreSame(prepared, vm.Preparation);
        Assert.IsNull(vm.ConfirmedImport);
    }

    [TestMethod]
    public async Task CoverageAssignmentsKeepRowIdentityAndOrderAndClosureLocksScreenFive()
    {
        using var ui = await ResourceUiReady("Amount;Ignored;Account\n" + string.Join("\n", Enumerable.Range(0, 30).Select(i => $"1000;x;{6000 + i}")));
        using var vm = new AllocationWorkspaceViewModel(ui.Company, ui.Studies, new UiTextCatalog(), ui.Preferences, ui.Dialogs);
        vm.AfterPrimaryStateChanged = () => ui.Resources.EnterAsync(true);
        await vm.EnterAsync(); vm.RuleAccount = null!; Assert.IsFalse(vm.IsDirty);
        vm.Name = "All accounts"; vm.Lines[0].Included = true; vm.Lines[0].Value = "100";
        await vm.SaveKeyCommand.ExecuteAsync(null); var key = vm.Keys.Single().Value.Id;
        var rows = vm.Coverage.ToArray();
        await vm.AssignCoverageAsync(rows[15].Id, key);
        CollectionAssert.AreEqual(rows, vm.Coverage.ToArray());
        Assert.AreEqual(key, rows[15].SelectedKeyId);
        Assert.IsFalse(vm.TogglePrimaryCommand.CanExecute(null));
        vm.SelectorKind = AccountSelectorKind.Prefix; vm.Prefix = "60"; vm.RuleKey = key;
        await vm.ApplyRuleCommand.ExecuteAsync(null);
        Assert.IsTrue(vm.TogglePrimaryCommand.CanExecute(null));
        await vm.TogglePrimaryCommand.ExecuteAsync(null);
        Assert.IsTrue(vm.IsPrimaryClosed, vm.Status); Assert.IsFalse(vm.CanAssignCoverage);
        Assert.IsFalse(ui.Resources.CanEditCorrections); Assert.IsTrue(ui.Resources.CorrectionsReadOnly);
        await vm.TogglePrimaryCommand.ExecuteAsync(null);
        Assert.IsFalse(vm.IsPrimaryClosed, vm.Status); Assert.IsTrue(ui.Resources.CanEditCorrections);
    }
}
