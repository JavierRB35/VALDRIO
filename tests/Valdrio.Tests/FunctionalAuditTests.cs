using Valdrio.Application;
using Valdrio.Desktop;
using Valdrio.Domain;
using Valdrio.Localization;
namespace Valdrio.Tests;
public sealed partial class T06ImportConfirmationTests
{
    [TestMethod]
    public async Task Audit_DirectKeysAreIdempotentAndDoNotAssignAccounts()
    {
        using var ui = await ResourceUiReady();
        using var vm = new AllocationWorkspaceViewModel(ui.Company, ui.Studies, new UiTextCatalog(), ui.Preferences, ui.Dialogs);
        await vm.EnterAsync();
        var before = ui.Company.ActiveAllocations!.ReadAllocations(ui.Studies.Current!.CompanyId, ui.Studies.Current.Id);
        await vm.GenerateDirectKeysCommand.ExecuteAsync(null);
        var first = vm.Keys.Select(k => k.Value).ToArray();
        Assert.AreEqual(before.Resources.Model.Structure.Nodes.Count(n => n.IsActive), first.Length, vm.Status);
        Assert.IsTrue(first.All(k => k.Status == ModelStatus.Published && k.Lines.Count == 1 && k.Lines[0].Value == 100));
        await vm.GenerateDirectKeysCommand.ExecuteAsync(null);
        CollectionAssert.AreEquivalent(first.Select(k => k.Id).ToArray(), vm.Keys.Select(k => k.Value.Id).ToArray());
        var after = ui.Company.ActiveAllocations.ReadAllocations(ui.Studies.Current.CompanyId, ui.Studies.Current.Id);
        Assert.AreEqual(CanonicalSnapshotJson.Serialize(before.Configuration), CanonicalSnapshotJson.Serialize(after.Configuration));
    }
    [TestMethod]
    public async Task Audit_PrimaryReportIsAvailableWithoutSavingRun()
    {
        using var ui = await ResourceUiReady();
        var report = new CostFlowViewModel(ui.Company, ui.Studies, new UiTextCatalog(), ui.Preferences);
        await report.EnterAsync();
        Assert.IsTrue(report.UseCurrentPrimary);
        Assert.AreEqual(0, report.Runs.Count);
        Assert.IsFalse(string.IsNullOrWhiteSpace(report.ExecutiveSummary));
        Assert.IsTrue(report.CurrentState.Contains(report["CurrentPrimary"]));
        Assert.AreEqual(0, ui.Company.ActiveRuns!.ReadRuns(ui.Studies.Current!.CompanyId, ui.Studies.Current.Id).Runs.Count);
    }
    [TestMethod]
    public async Task Audit_MissingAllocationIsIncompleteNotTechnicalFailure()
    {
        using var ui = await ResourceUiReady();
        var report = new CostFlowViewModel(ui.Company, ui.Studies, new UiTextCatalog(), ui.Preferences);
        await report.EnterAsync();
        await report.CalculateCommand.ExecuteAsync(null);
        Assert.IsNull(report.SelectedRun, "An unconfigured study must not invent a saved execution.");
        Assert.IsFalse(report.HasError);
        Assert.IsTrue(report.CurrentState.Contains(report["Partial"]));
        Assert.IsTrue(report.Diagnostics.Contains("6000"));
        report.UseCurrentPrimary = true;
        Assert.IsFalse(report.HasError);
        Assert.IsTrue(report.CurrentState.Contains(report["CurrentPrimary"]));
    }
    [TestMethod]
    public async Task Audit_ReportExportsFrozenSourcesAndBalancedBridge()
    {
        EntityId studyId;
        using (var file = Baseline())
        {
            var workspace = await T12Fixture(file);
            var study = workspace.Resources.Study; studyId = study.Id;
            var run = file.Runs.PrepareRun(new(study.CompanyId, study.Id, study.Revision, new(Guid.NewGuid()), "test"));
            file.Runs.PublishRun(run.Id, CalculationRunService.Calculate(run));
        }
        using var ui = new ResourceUi(Service(), Catalog);
        await ui.Company.OpenPath(FileOf("current.valdrio"));
        ui.Studies.StudyChoice = ui.Studies.Studies.Single(s => s.Value.Id == studyId);
        await ui.Studies.LoadCommand.ExecuteAsync(null);
        var report = new CostFlowViewModel(ui.Company, ui.Studies, new UiTextCatalog(), ui.Preferences);
        await report.EnterAsync(); report.UseCurrentPrimary = false;
        var frozen = CanonicalSnapshotJson.Serialize(report.SelectedRun!.Run);
        Assert.AreEqual(6500m, report.CenterBalances.Sum(r => r.FinalValue));
        Assert.IsTrue(report.NodeBalances.All(r => r.PrimaryValue + r.SecondaryNetValue == r.FinalValue));
        Assert.IsTrue(report.NodeBalances.All(r => r.Received.Length > 0 && r.Ceded.Length > 0 && r.Adjustment.Length > 0));
        var exported = string.Join("\n", report.ExportRows().Select(r => string.Join(";", r)));
        Assert.IsTrue(exported.Contains(report["ImportedSource"]));
        Assert.IsTrue(exported.Contains(report["Reconciliation"]));
        report.GroupByCenter = true; report.ReportFilter = "not-a-center";
        Assert.AreEqual(0, report.Concentration.Count);
        Assert.AreEqual(frozen, CanonicalSnapshotJson.Serialize(report.SelectedRun.Run));
    }
    [TestMethod]
    public async Task Audit_DeleteUnusedCenterButPreserveReferencedCenter()
    {
        using var file = Baseline();
        var company = file.Read();
        var center = file.Models.CreateCenter(new(company.Id, "AUDIT_EMPTY", "Prueba", "test"));
        var renamed = file.Models.ChangeCenter(new(company.Id, center.Id, center.Revision, "AUDIT_RENAMED", "Renombrado", true, "test"));
        file.Models.DeleteCenter(new(company.Id, renamed.Id, renamed.Revision, "test"));
        Assert.IsFalse(file.Models.ReadCenters().Any(c => c.Id == center.Id));
        var w = await T12Fixture(file);
        var used = file.Models.ReadCenters().Single(c => c.Id == w.Resources.Model.Structure.Nodes[0].PhysicalCenterId);
        var error = Assert.ThrowsExactly<DomainRuleException>(() => file.Models.DeleteCenter(new(company.Id, used.Id, used.Revision, "test")));
        Assert.AreEqual("MODEL_CENTER_REFERENCED", error.Diagnostic.Code);
        Assert.IsTrue(file.Models.ReadCenters().Any(c => c.Id == used.Id));
    }
}
