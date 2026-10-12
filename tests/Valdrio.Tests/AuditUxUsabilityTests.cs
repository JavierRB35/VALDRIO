using System.Text.RegularExpressions;
using Valdrio.Desktop;
using Valdrio.Domain;
using Valdrio.Localization;

namespace Valdrio.Tests;

public sealed partial class T06ImportConfirmationTests
{
    [TestMethod]
    public async Task Audit_BulkPrimaryAssignmentIsAtomicRespectsExistingRuleAndManualPart()
    {
        using var ui = await ResourceUiReady("Amount;Ignored;Account\n100;x;6000\n200;x;6001\n300;x;6400");
        using var vm = new AllocationWorkspaceViewModel(ui.Company, ui.Studies,
            new UiTextCatalog(), ui.Preferences, ui.Dialogs);
        await vm.EnterAsync();
        vm.Name = "Shared source key";
        vm.Lines[0].Included = true;
        vm.Lines[0].Value = "100";
        await vm.SaveKeyCommand.ExecuteAsync(null);
        var key = vm.Keys.Single().Value.Id;

        // Preserve an already configured exact account rule.
        vm.RuleAccount = "6000"; vm.RuleKey = key;
        await vm.ApplyRuleCommand.ExecuteAsync(null);
        Assert.AreEqual(1, vm.PrimaryRules.Count);

        // Preserve a manual part override on another account.
        var manual = vm.Coverage.Single(r => r.Source.StartsWith("6001", StringComparison.Ordinal));
        await vm.AssignCoverageAsync(manual.Id, key);
        Assert.AreEqual(key, manual.SelectedKeyId);

        vm.BulkPrimaryKey = key;
        Assert.AreEqual(1, vm.UnassignedAccountCount);
        Assert.IsTrue(vm.BulkApplyPrimaryKeyCommand.CanExecute(null));
        await vm.BulkApplyPrimaryKeyCommand.ExecuteAsync(null);

        Assert.AreEqual(2, vm.PrimaryRules.Count, "Bulk adds one exact account rule only.");
        Assert.AreEqual(0, vm.UnassignedAccountCount);
        Assert.AreEqual(key, vm.Coverage.Single(r => r.Id == manual.Id).SelectedKeyId);
        Assert.IsFalse(vm.BulkApplyPrimaryKeyCommand.CanExecute(null));
        Assert.AreEqual(3, vm.Coverage.Count);
        Assert.AreEqual(3, vm.PrimaryResults.Count, "All three imported accounts are covered.");

        // Two fixed digits in the user-facing EUR table, no persisted rounding.
        foreach (var amount in vm.PrimaryResults.Select(row => row.Amount))
            Assert.IsTrue(Regex.IsMatch(amount, @"[.,]\d{2} EUR$"), "Unformatted amount: " + amount);
        Assert.IsTrue(vm.TogglePrimaryCommand.CanExecute(null), "Bulk allocation is immediately reviewable.");
    }
}
