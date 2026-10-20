using Microsoft.Data.Sqlite;
using Valdrio.Application;
using Valdrio.Infrastructure.CompanyFiles;
using Valdrio.Desktop;
using Valdrio.Localization;

namespace Valdrio.Tests;

// Regression cases for the user-reported old-file failure and the UX batch flow.
public sealed partial class T06ImportConfirmationTests
{
    [TestMethod]
    public async Task PreviousFormatFileNotifiesUserWithoutReplacingCompanyOrChangingSource()
    {
        using var ui = await ResourceUiReady();
        var currentPath = ui.Company.ActivePath!;
        var companyId = ui.Company.Snapshot!.Id;
        var older = FileOf("ux-previous-schema.valdrio");
        File.Copy(currentPath, older);

        // Deliberately change only the metadata on a disposable copy: Open must
        // reject it before attempting migrations or loading obsolete EF tables.
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
               { DataSource = older, Pooling = false }.ToString()))
        {
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE FileMetadata SET SchemaVersion=8";
            Assert.AreEqual(1, cmd.ExecuteNonQuery());
        }

        var before = Hash(older);
        string? warning = null;
        ui.Company.OpenFailure = message => warning = message;
        await ui.Company.OpenPath(older);

        Assert.AreEqual("FILE_UPGRADE_REQUIRED", ui.Company.DiagnosticCode);
        Assert.IsFalse(string.IsNullOrWhiteSpace(warning));
        Assert.AreEqual(companyId, ui.Company.Snapshot?.Id);
        Assert.AreEqual(currentPath, ui.Company.ActivePath);
        Assert.AreEqual(before, Hash(older));
        Assert.IsNotNull(ui.Company.ActiveModels); // The original session remains usable.
    }

    [TestMethod]
    public async Task OpeningGenuineOldFileOffersUpgradeToNewCopyWithoutReSelectingSource()
    {
        // Genuine versioned file, not just a deliberately changed schema number.
        var older = Fixture(3);
        var originalHash = Hash(older);
        Assert.IsTrue(Service().RequiresUpgrade(older));
        Assert.AreEqual(originalHash, Hash(older), "Read-only version probe must not mutate source.");

        var dialogs = new QueuedCompanyFileDialogs();
        var preferences = new PresentationPreferencesService(new LanguageCatalog(), UserPresentationPreferences.Default);
        using var vm = new CompanyFileViewModel(Service(), dialogs, Catalog, new UiTextCatalog(), preferences,
            new PresentationFormatter(new DisplayTimeZoneResolver()));
        string? alert = null;
        vm.OpenFailure = message => alert = message;

        await vm.OpenPath(older);
        Assert.AreEqual("FILE_UPGRADE_REQUIRED", vm.DiagnosticCode);
        Assert.IsTrue(vm.UpgradeSuggested);
        Assert.IsFalse(string.IsNullOrWhiteSpace(alert));
        Assert.IsNull(vm.Snapshot);
        Assert.AreEqual(originalHash, Hash(older));

        // Canceling the copy destination is harmless; source remains remembered.
        dialogs.Paths.Enqueue(null);
        await vm.UpgradeCommand.ExecuteAsync(null);
        Assert.IsNull(vm.Snapshot);
        Assert.AreEqual(originalHash, Hash(older));

        // Only a destination is queued. A second source-file picker would throw here.
        var target = FileOf("legacy-upgraded-from-open.valdrio");
        dialogs.Paths.Enqueue(target);
        await vm.UpgradeCommand.ExecuteAsync(null);
        Assert.IsNotNull(vm.Snapshot, vm.Status);
        Assert.AreEqual(target, vm.ActivePath);
        Assert.AreEqual(SqliteCompanyFileService.SchemaVersion, vm.Snapshot.SchemaVersion);
        Assert.IsFalse(vm.UpgradeSuggested);
        Assert.IsFalse(Service().RequiresUpgrade(target));
        Assert.AreEqual(originalHash, Hash(older), "The original must remain byte-for-byte unchanged.");
        using var reopened = Service().Open(target, CompanyFileOpenMode.ReadOnly);
        Assert.AreEqual(vm.Snapshot.Id, reopened.Read().Id);
    }

    [TestMethod]
    public async Task ConfirmingUpgradeFromOpenWarningStartsCopyWithoutReselectingSource()
    {
        var older = Fixture(3);
        var originalHash = Hash(older);
        var destination = FileOf("confirmed-upgrade-from-warning.valdrio");
        var dialogs = new QueuedCompanyFileDialogs();
        // The confirmation must request only a NEW destination, not another source.
        dialogs.Paths.Enqueue(destination);
        var preferences = new PresentationPreferencesService(new LanguageCatalog(), UserPresentationPreferences.Default);
        using var vm = new CompanyFileViewModel(Service(), dialogs, Catalog, new UiTextCatalog(), preferences,
            new PresentationFormatter(new DisplayTimeZoneResolver()));

        var warningShown = false;
        vm.OpenFailure = _ =>
        {
            warningShown = true;
            // Use the same ICommand path as the confirmation button.
            vm.UpgradeCommand.Execute(null);
        };

        await vm.OpenPath(older);
        Assert.IsTrue(warningShown, "Opening the previous-format file should offer a direct upgrade.");
        Assert.IsNotNull(vm.UpgradeCommand.ExecutionTask, "The confirmation must start the upgrade command.");
        await vm.UpgradeCommand.ExecutionTask!;

        Assert.IsNotNull(vm.Snapshot, vm.Status);
        Assert.AreEqual(destination, vm.ActivePath);
        Assert.AreEqual(SqliteCompanyFileService.SchemaVersion, vm.Snapshot.SchemaVersion);
        Assert.AreEqual(originalHash, Hash(older), "Upgrade confirmation must not alter the source.");
    }

    [TestMethod]
    public async Task FailureInPendingEditCoordinationCannotCrashCompanyOpen()
    {
        using var ui = await ResourceUiReady();
        var currentPath = ui.Company.ActivePath!;
        var companyId = ui.Company.Snapshot!.Id;
        string? warning = null;
        ui.Company.OpenFailure = message => warning = message;
        ui.Company.BeforeChangeAsync = () => throw new IOException("Simulated pending-editor failure");

        await ui.Company.OpenPath(FileOf("does-not-matter.valdrio"));

        Assert.AreEqual("FILE_IO_FAILED", ui.Company.DiagnosticCode);
        Assert.IsFalse(string.IsNullOrWhiteSpace(warning));
        Assert.AreEqual(companyId, ui.Company.Snapshot?.Id);
        Assert.AreEqual(currentPath, ui.Company.ActivePath);
    }

    [TestMethod]
    public async Task BatchAssignsOnlyPendingPartsAndNeverOverwritesManualCoverage()
    {
        using var ui = await ResourceUiReady(
            "Amount;Ignored;Account\n100;x;6000\n200;y;6001\n300;z;6002");
        using var vm = new AllocationWorkspaceViewModel(
            ui.Company, ui.Studies, new UiTextCatalog(), ui.Preferences, ui.Dialogs);
        await vm.EnterAsync();
        vm.Name = "Batch key";
        vm.Lines[0].Included = true;
        vm.Lines[0].Value = "100";
        await vm.SaveKeyCommand.ExecuteAsync(null);
        var key = vm.Keys.Single().Value.Id;
        var ids = vm.Coverage.Select(row => row.Id).ToArray();
        Assert.AreEqual(3, ids.Length);
        await vm.AssignCoverageAsync(ids[0], key);

        var preview = vm.PreviewCoverageBatch(ids, key);
        Assert.AreEqual(3, preview.SelectedCount);
        Assert.AreEqual(2, preview.EligibleCount);
        Assert.AreEqual(1, preview.SkippedCount);
        Assert.AreEqual(500m, preview.EligibleAmount);
        Assert.IsTrue(await vm.AssignCoverageBatchAsync(ids, key), vm.Status);
        Assert.IsTrue(vm.Coverage.All(row => row.SelectedKeyId == key));
        var companyId = ui.Company.Snapshot!.Id;
        var studyId = ui.Studies.Current!.Id;
        Assert.AreEqual(3, ui.Company.ActiveAllocations!
            .ReadAllocations(companyId, studyId).Configuration.ExplicitAssignments.Count);
        var after = Hash(ui.Company.ActivePath!);
        Assert.IsFalse(await vm.AssignCoverageBatchAsync(ids, key));
        Assert.AreEqual(after, Hash(ui.Company.ActivePath!), "Repeated batch must not create duplicate writes.");
    }

    [TestMethod]
    public async Task BatchSkipsRowsCoveredByActiveExactRule()
    {
        using var ui = await ResourceUiReady("Amount;Ignored;Account\n100;x;6000\n200;y;6001");
        using var vm = new AllocationWorkspaceViewModel(
            ui.Company, ui.Studies, new UiTextCatalog(), ui.Preferences, ui.Dialogs);
        await vm.EnterAsync();
        vm.Name = "Batch protection";
        vm.Lines[0].Included = true;
        vm.Lines[0].Value = "100";
        await vm.SaveKeyCommand.ExecuteAsync(null);
        var key = vm.Keys.Single().Value.Id;
        vm.RuleAccount = vm.Accounts.First(a => a.Value == "6000").Value;
        vm.RuleKey = key;
        await vm.ApplyRuleCommand.ExecuteAsync(null);

        var ids = vm.Coverage.Select(row => row.Id).ToArray();
        var preview = vm.PreviewCoverageBatch(ids, key);
        Assert.AreEqual(1, preview.EligibleCount);
        Assert.AreEqual(1, preview.SkippedCount);
        Assert.IsTrue(await vm.AssignCoverageBatchAsync(ids, key));
        var current = ui.Company.ActiveAllocations!.ReadAllocations(
            ui.Company.Snapshot!.Id, ui.Studies.Current!.Id);
        Assert.AreEqual(1, current.Configuration.PrimaryRules.Count(rule => rule.IsActive));
        Assert.AreEqual(1, current.Configuration.ExplicitAssignments.Count);
    }
}
