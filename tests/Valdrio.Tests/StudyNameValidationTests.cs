using Valdrio.Desktop;
using Valdrio.Domain;

namespace Valdrio.Tests;

public sealed partial class T06ImportConfirmationTests
{
    [TestMethod]
    public async Task NewPeriodRequiresNameWithoutThrowingAndRetainsStructureAndInput()
    {
        using var ui = await ResourceUiReady();
        var vm = ui.Studies;
        var original = vm.Current!;
        await vm.NewCommand.ExecuteAsync(null);
        Assert.IsNull(vm.Current);
        Assert.AreEqual(original.SelectedModelVersionId, vm.ModelChoice!.Value.Structure.Id);
        Assert.AreEqual(original.Currency.FunctionalCurrencyCode.ToString(), vm.Currency);
        Assert.AreEqual(original.ScenarioKind, vm.Scenario!.Value);
        Assert.AreEqual("", vm.Name);
        vm.PeriodStart = new(2027, 1, 1);
        vm.PeriodEnd = new(2027, 1, 31);
        var before = Hash(ui.Company.ActivePath!);
        var thrown = 0;
        EventHandler<System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs> handler = (_, e) =>
        { if (e.Exception is DomainRuleException ex && ex.Diagnostic.Code == "STUDY_NAME_REQUIRED") Interlocked.Increment(ref thrown); };
        AppDomain.CurrentDomain.FirstChanceException += handler;
        try
        {
            foreach (var name in new[] { "", "   " })
            {
                vm.Name = name;
                await vm.CreateCommand.ExecuteAsync(null);
                Assert.AreEqual("STUDY_NAME_REQUIRED", vm.DiagnosticCode);
                Assert.IsTrue(vm.HasStatusError);
                Assert.IsNull(vm.Current);
                Assert.IsTrue(vm.IsDirty);
                Assert.AreEqual(new DateTime(2027, 1, 1), vm.PeriodStart);
                Assert.AreEqual(before, Hash(ui.Company.ActivePath!));
            }
            ui.Dialogs.Next = PendingEditChoice.Save;
            Assert.IsFalse(await vm.CanLeaveAsync());
            Assert.AreEqual(0, thrown);
        }
        finally { AppDomain.CurrentDomain.FirstChanceException -= handler; }
        vm.Name = "January 2027";
        await vm.CreateCommand.ExecuteAsync(null);
        Assert.IsNull(vm.DiagnosticCode, vm.Status);
        Assert.IsFalse(vm.IsDirty);
        Assert.AreNotEqual(original.Id, vm.Current!.Id);
        Assert.AreEqual(original.SelectedModelVersionId, vm.Current.SelectedModelVersionId);
        Assert.AreEqual("January 2027", vm.Current.Name);
        Assert.AreEqual(original, ui.Company.ActiveStudies!.ReadStudies().Single(s => s.Id == original.Id));
    }

    [TestMethod]
    public async Task ExistingStudyRejectsBlankNameBeforeDomainAndCanBeSavedAfterCorrection()
    {
        using var ui = await ResourceUiReady();
        var vm = ui.Studies;
        var original = vm.Current!;
        var before = Hash(ui.Company.ActivePath!);
        var thrown = 0;
        EventHandler<System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs> handler = (_, e) =>
        { if (e.Exception is DomainRuleException ex && ex.Diagnostic.Code == "STUDY_NAME_REQUIRED") Interlocked.Increment(ref thrown); };
        AppDomain.CurrentDomain.FirstChanceException += handler;
        try
        {
            vm.Name = "   ";
            await vm.SaveCommand.ExecuteAsync(null);
            Assert.AreEqual("STUDY_NAME_REQUIRED", vm.DiagnosticCode);
            ui.Dialogs.Next = PendingEditChoice.Save;
            Assert.IsFalse(await vm.CanLeaveAsync());
            Assert.AreEqual(0, thrown);
            Assert.AreEqual(original, vm.Current);
            Assert.IsTrue(vm.IsDirty);
            Assert.AreEqual(before, Hash(ui.Company.ActivePath!));
        }
        finally { AppDomain.CurrentDomain.FirstChanceException -= handler; }
        vm.Name = "Corrected analysis";
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.IsNull(vm.DiagnosticCode, vm.Status);
        Assert.AreEqual(original.Revision + 1, vm.Current!.Revision);
        Assert.AreEqual("Corrected analysis", vm.Current.Name);
        Assert.IsFalse(vm.IsDirty);
    }
}
