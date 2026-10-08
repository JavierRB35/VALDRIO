using Valdrio.Desktop;
using Valdrio.Domain;
using Valdrio.Localization;

namespace Valdrio.Tests;

public sealed partial class WorkspaceAuditClosureTests
{
    [TestMethod]
    public async Task StructureSaveFeedbackAndReviewUseHistoricalCentresWithoutTechnicalNoise()
    {
        var preferences = new PresentationPreferencesService(new LanguageCatalog(), UserPresentationPreferences.Default);
        var texts = new UiTextCatalog(); var dialogs = new Dialogs();
        using var company = new CompanyFileViewModel(Service(), dialogs, Catalog, texts, preferences, new PresentationFormatter(new DisplayTimeZoneResolver()));
        using var vm = new ModelWorkspaceViewModel(company, texts, preferences, dialogs, new PresentationFormatter(new DisplayTimeZoneResolver()));
        await company.OpenPath(Baseline());
        Assert.AreEqual("Collapsed", vm.NewCenterVisibility);
        Assert.IsFalse(vm.NewCenterCommand.CanExecute(null));
        vm.Description = "New saved structure";
        await vm.NewDraftCommand.ExecuteAsync(null);
        Assert.AreEqual(vm.Text("Model.StructureCreated"), vm.Status);
        vm.Description = "Revised structure";
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.AreEqual(vm.Text("Model.StructureSaved"), vm.Status);
        Assert.IsFalse(vm.IsDirty);
        vm.Description = "Pending preserved";
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.AreEqual("Pending preserved", vm.Description);
        Assert.IsTrue(vm.IsDirty);
        await vm.SaveCommand.ExecuteAsync(null);
        vm.ModelChoice = vm.Models.First(m => m.Value.Status == ModelStatus.Published);
        await vm.LoadCommand.ExecuteAsync(null);
        var hash = vm.Current!.StructuralHash!;
        Assert.IsFalse(vm.Details.Contains(hash, StringComparison.Ordinal));
        Assert.IsFalse(vm.Details.Contains("SHA", StringComparison.Ordinal));
        Assert.IsTrue(vm.TechnicalDetails.Contains(hash, StringComparison.Ordinal));
        var sorted = vm.Nodes.OrderBy(n => n.CenterLabel, StringComparer.Ordinal).ThenBy(n => n.Node.Code, StringComparer.Ordinal).ThenBy(n => n.Node.Role == NodeRole.Principal ? 0 : 1).ToArray();
        CollectionAssert.AreEqual(sorted, vm.Nodes.ToArray());
        Assert.AreEqual(vm.Nodes.Count, vm.ReviewTree.Sum(c => c.Children.Count));
        var row = vm.Nodes.First(); var frozen = row.CenterLabel;
        vm.Master = vm.Centers.Single(c => c.Value.Id == row.Node.PhysicalCenterId);
        Assert.AreEqual("Visible", vm.NewCenterVisibility);
        vm.CenterName = "Renamed live master";
        await vm.SaveCenterFormCommand.ExecuteAsync(null);
        Assert.AreEqual(frozen, vm.Nodes.Single(n => n.Node.Id == row.Node.Id).CenterLabel);
        Assert.AreEqual(frozen, vm.ReviewTree.Single(c => c.Children.Any(n => n.Node.Id == row.Node.Id)).Label);
        Assert.AreEqual("Collapsed", vm.NewCenterVisibility);
    }
}
