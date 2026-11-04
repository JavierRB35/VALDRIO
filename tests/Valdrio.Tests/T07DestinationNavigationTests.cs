using Valdrio.Desktop;
using Valdrio.Domain;
namespace Valdrio.Tests;

public sealed partial class T06ImportConfirmationTests
{
    [TestMethod]
    public async Task T07_ReturnFromCleanDestinationsRestoresCorrectionsWithoutCreatingPartOrHistory()
    {
        using var ui = await ResourceUiReady(); var vm = ui.Resources;
        var hash = Hash(ui.Company.ActivePath!);
        await vm.OpenSelectedResourceCommand.ExecuteAsync(null);
        Assert.AreEqual(2, vm.Stage); Assert.AreEqual(1, vm.Parts.Count);
        Assert.AreEqual(ResourceRoutingKind.Pending, vm.Parts.Single().Value.Routing);
        Assert.AreEqual(1000m, vm.Parts.Single().Value.Amount.Amount);
        Assert.AreEqual(hash, Hash(ui.Company.ActivePath!));
        Assert.IsTrue(await vm.CloseDestinationsAsync(), vm.Status);
        Assert.AreEqual(0, vm.Stage); Assert.IsTrue(vm.CanEditCorrections);
        Assert.IsFalse(vm.ShowParts); Assert.IsFalse(vm.IsDirty);
        Assert.AreEqual(0, ui.Dialogs.Prompts);
        Assert.IsTrue(await vm.SaveCorrectionAsync(vm.Rows.Single(), "10", EsNumbers), vm.Status);
        Assert.AreEqual("1.010 EUR", vm.Rows.Single().Effective);
    }
    [TestMethod]
    public async Task T07_ReturnWithUnappliedDestinationCanCancelThenDiscardWithoutLosingSavedCorrection()
    {
        using var ui = await ResourceUiReady(); var vm = ui.Resources;
        Assert.IsTrue(await vm.SaveCorrectionAsync(vm.Rows.Single(), "-50", EsNumbers));
        var hash = Hash(ui.Company.ActivePath!);
        await vm.OpenSelectedResourceCommand.ExecuteAsync(null);
        vm.PartChoice = vm.Parts.Single(); vm.LoadPartCommand.Execute(null);
        vm.PartForm.Route = ResourceRoutingKind.AllocationPool;
        Assert.IsFalse(vm.CanEditCorrections);
        Assert.IsFalse(await vm.CloseDestinationsAsync());
        Assert.AreEqual(2, vm.Stage); Assert.AreEqual(ResourceRoutingKind.AllocationPool, vm.PartForm.Route);
        Assert.AreEqual(hash, Hash(ui.Company.ActivePath!));
        ui.Dialogs.Next = PendingEditChoice.Discard;
        Assert.IsTrue(await vm.CloseDestinationsAsync());
        Assert.AreEqual(0, vm.Stage); Assert.IsTrue(vm.CanEditCorrections);
        Assert.AreEqual("-50 EUR", vm.Rows.Single().Adjustments);
        Assert.AreEqual(hash, Hash(ui.Company.ActivePath!));
        Assert.AreEqual(2, ui.Dialogs.Prompts);
    }
    [TestMethod]
    public async Task T07_ReturnCanSaveCompletedDestinationsAndRestoreCorrections()
    {
        using var ui = await ResourceUiReady(); var vm = ui.Resources;
        await vm.OpenSelectedResourceCommand.ExecuteAsync(null);
        vm.PartChoice = vm.Parts.Single(); vm.LoadPartCommand.Execute(null);
        vm.PartForm.Route = ResourceRoutingKind.Excluded; vm.PartForm["Exclusion"] = "Outside perimeter";
        vm.ApplyPartCommand.Execute(null); vm.Reason = "Reviewed exclusion";
        ui.Dialogs.Next = PendingEditChoice.Save;
        Assert.IsTrue(await vm.CloseDestinationsAsync(), vm.Status);
        Assert.IsTrue(vm.CanEditCorrections); Assert.IsFalse(vm.IsDirty);
        var study = ui.Studies.Current!;
        var saved = ui.Company.ActiveResources!.ReadResources(study.CompanyId, study.Id);
        Assert.AreEqual(ResourceRoutingKind.Excluded, saved.Resources.Single().Parts.Single().Routing);
        Assert.AreEqual(1000m, saved.Resources.Single().Parts.Single().Amount.Amount);
    }
    [TestMethod]
    public async Task T07_PoolChoicesDescribeFrozenCentreNodeRoleAndActivityUnit()
    {
        using var ui = await ResourceUiReady(); var vm = ui.Resources;
        var study = ui.Studies.Current!;
        var model = ui.Company.ActiveResources!.ReadResources(study.CompanyId, study.Id).Model;
        foreach (var choice in vm.Pools)
        {
            var pool = model.Structure.Pools.Single(p => p.Id == choice.Value);
            var node = model.Structure.Nodes.Single(n => n.Id == pool.NodeId);
            var center = model.PhysicalCenters.Single(c => c.Id == node.PhysicalCenterId);
            var unit = model.DriverUnits.Single(u => u.Id == pool.DriverUnitId);
            StringAssert.Contains(choice.Label, center.Code); StringAssert.Contains(choice.Label, center.Name);
            StringAssert.Contains(choice.Label, node.Code); StringAssert.Contains(choice.Label, unit.Code);
            StringAssert.Contains(choice.Label, unit.Symbol);
            StringAssert.Contains(choice.Label, string.IsNullOrWhiteSpace(pool.Description) ? vm["PoolWithoutDescription"] : pool.Description);
            Assert.IsTrue(choice.Label.Contains("Principal") || choice.Label.Contains("Auxiliar"));
        }
        Assert.IsTrue(vm.Pools.Count > 0);
    }
}
