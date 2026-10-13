using Microsoft.Data.Sqlite;
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
