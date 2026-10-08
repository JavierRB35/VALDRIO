using Valdrio.Application;
using Valdrio.Desktop;
using Valdrio.Domain;
using Valdrio.Localization;

namespace Valdrio.Tests;

public sealed partial class T06ImportConfirmationTests
{
    private sealed class ResourceDialogs : ICompanyFileDialogs, IModelEditorDialogs
    {
        public PendingEditChoice Next = PendingEditChoice.Cancel;
        public int Prompts;
        public PendingEditChoice ResolvePending(string title, string body) { Prompts++; return Next; }
        public string? SelectNewFile() => null;
        public string? SelectFile() => null;
        public string? SelectBackupDestination() => null;
        public string? SelectBackupSource() => null;
        public string? SelectRestoreDestination() => null;
    }
    private async Task<ResourceUi> ResourceUiReady(string csv = "Amount;Ignored;Account\n1000;x;6000")
    {
        EntityId studyId;
        using (var file = Baseline()) studyId = (await ImportedResources(file, csv)).Study.Id;
        var ui = new ResourceUi(Service(), Catalog);
        await ui.Company.OpenPath(FileOf("current.valdrio"));
        ui.Studies.StudyChoice = ui.Studies.Studies.Single(s => s.Value.Id == studyId);
        await ui.Studies.LoadCommand.ExecuteAsync(null); await ui.Resources.EnterAsync(); return ui;
    }
    private sealed class ResourceUi : IDisposable
    {
        public PresentationPreferencesService Preferences { get; } = new(new LanguageCatalog(), UserPresentationPreferences.Default);
        public ResourceDialogs Dialogs { get; } = new();
        public CompanyFileViewModel Company { get; }
        public StudyWorkspaceViewModel Studies { get; }
        public ResourceWorkspaceViewModel Resources { get; }
        public ResourceUi(ICompanyFileService service, ICurrencyCatalog catalog)
        {
            var formatter = new PresentationFormatter(new DisplayTimeZoneResolver()); var texts = new UiTextCatalog();
            Company = new(service, Dialogs, catalog, texts, Preferences, formatter);
            Studies = new(Company, catalog, texts, Preferences, formatter, Dialogs);
            Resources = new(Company, Studies, catalog, texts, Preferences, Dialogs);
        }
        public void Dispose() { Resources.Dispose(); Studies.Dispose(); Company.Dispose(); }
    }
    [TestMethod]
    public async Task T07_UiGuidesBlocksUnappliedFormsAndPersistsOnce()
    {
        using var ui = await ResourceUiReady(); var vm = ui.Resources;
        Assert.AreEqual(0, vm.Stage); Assert.AreEqual(1, vm.Rows.Count);
        await vm.AdvanceAsync(); Assert.AreEqual(2, vm.Stage);
        vm.AdjustmentForm["Amount"] = "-100"; vm.AdjustmentForm["Type"] = "Correction"; vm.AdjustmentForm["Reason"] = "Non-cost item";
        await vm.AdvanceAsync(); Assert.AreEqual(2, vm.Stage);
        vm.AddAdjustmentCommand.Execute(null); Assert.AreEqual(1, vm.Adjustments.Count, vm.Status);
        await vm.AdvanceAsync(); Assert.AreEqual(2, vm.Stage);
        vm.PartChoice = vm.Parts.Single(); vm.LoadPartCommand.Execute(null);
        vm.PartForm["Amount"] = "900"; vm.PartForm.Route = ResourceRoutingKind.Excluded; vm.PartForm["Exclusion"] = "Outside the perimeter";
        vm.ApplyPartCommand.Execute(null); Assert.AreEqual(900m, vm.Parts.Single().Value.Amount.Amount, vm.Status);
        await vm.AdvanceAsync(); Assert.AreEqual(2, vm.Stage);
        var before = Hash(ui.Company.ActivePath!); Assert.IsFalse(await vm.SaveAsync()); Assert.AreEqual(before, Hash(ui.Company.ActivePath!));
        vm.Reason = "Reviewed imported account"; Assert.IsTrue(await vm.SaveAsync(), vm.Status);
        Assert.IsFalse(vm.IsDirty); Assert.AreEqual(0, vm.Stage); Assert.AreEqual(vm["Saved"], vm.Status);
        Assert.AreEqual(2L, ui.Studies.Current!.Revision);
        Assert.IsFalse(await vm.SaveAsync());
        Assert.AreEqual(1, ui.Company.ActiveResources!.ReadResourceHistory(ui.Company.Snapshot!.Id, ui.Studies.Current.Id).Count);
    }
    [TestMethod]
    public async Task T07_UiNewObjectDoesNotLosePendingPartAndCanBeSelectedImmediately()
    {
        using var ui = await ResourceUiReady(); var vm = ui.Resources;
        await vm.AdvanceAsync(); await vm.AdvanceAsync();
        vm.PartChoice = vm.Parts.Single(); vm.LoadPartCommand.Execute(null);
        vm.PartForm["Amount"] = "1000"; vm.PartForm.Route = ResourceRoutingKind.DirectObject;
        vm.ObjectForm["Code"] = "SVC"; vm.ObjectForm["Name"] = "Service"; vm.ObjectKind = ResourceObjectKind.Service;
        await vm.PutObjectCommand.ExecuteAsync(null);
        Assert.AreEqual("1000", vm.PartForm["Amount"], vm.Status); Assert.AreEqual(ResourceRoutingKind.DirectObject, vm.PartForm.Route);
        Assert.AreEqual(2, vm.Stage); vm.PartForm.ObjectId = vm.Objects.Single().Value;
        vm.ApplyPartCommand.Execute(null); Assert.AreEqual(ResourceRoutingKind.DirectObject, vm.Parts.Single().Value.Routing, vm.Status);
        await vm.AdvanceAsync(); vm.Reason = "Explicit direct assignment"; Assert.IsTrue(await vm.SaveAsync(), vm.Status);
    }
    [TestMethod]
    public async Task T07_UiCancelStudyChangePreservesDraftAndDiscardIsExplicit()
    {
        using var ui = await ResourceUiReady(); var vm = ui.Resources;
        await vm.AdvanceAsync(); vm.AdjustmentForm["Amount"] = "-100";
        var original = ui.Studies.Current; await ui.Studies.NewCommand.ExecuteAsync(null);
        Assert.AreEqual(original, ui.Studies.Current); Assert.AreEqual("-100", vm.AdjustmentForm["Amount"]); Assert.IsTrue(vm.IsDirty);
        Assert.AreEqual(1, ui.Dialogs.Prompts);
        ui.Dialogs.Next = PendingEditChoice.Discard; await ui.Studies.NewCommand.ExecuteAsync(null);
        Assert.IsNull(ui.Studies.Current); Assert.IsFalse(vm.IsDirty);
    }
    [TestMethod]
    public async Task T07_UiRegionChangeRequiresExplicitCapturedCultureAndRetainsExactValue()
    {
        using var ui = await ResourceUiReady(); var vm = ui.Resources;
        ui.Preferences.Update(ui.Preferences.Current with { FormatCultureTag = "es-ES" });
        await vm.AdvanceAsync(); vm.AdjustmentForm["Amount"] = "-10,25"; vm.AdjustmentForm["Type"] = "Correction"; vm.AdjustmentForm["Reason"] = "Exact amount";
        ui.Preferences.Update(ui.Preferences.Current with { FormatCultureTag = "en-US", UiLanguageTag = "en", DisplayTimeZoneId = "UTC" });
        Assert.IsTrue(vm.CultureChanged); vm.AddAdjustmentCommand.Execute(null); Assert.AreEqual(0, vm.Adjustments.Count);
        vm.AcknowledgeCultureCommand.Execute(null); vm.AddAdjustmentCommand.Execute(null);
        Assert.AreEqual(-10.25m, vm.Adjustments.Single().Value.Conversion.OriginalMoney.Amount, vm.Status);
        Assert.AreEqual(0, ui.Company.ActiveResources!.ReadResourceHistory(ui.Company.Snapshot!.Id, ui.Studies.Current!.Id).Count);
        vm.DiscardCommand.Execute(null);
    }

    [TestMethod]
    public async Task T07_UiReturningToSameAccountKeepsUnappliedFormWithoutPrompt()
    {
        using var ui = await ResourceUiReady(); var vm = ui.Resources;
        await vm.AdvanceAsync(); vm.AdjustmentForm["Amount"] = "-12";
        Assert.IsTrue(vm.PreviousStep()); await vm.AdvanceAsync();
        Assert.AreEqual(2, vm.Stage); Assert.AreEqual("-12", vm.AdjustmentForm["Amount"]);
        Assert.AreEqual(0, ui.Dialogs.Prompts); Assert.IsTrue(vm.IsDirty);
    }

    [TestMethod]
    public async Task T07_UiRevaluationPreviewsReconcilesAndSavesAllAmountsAtomically()
    {
        using var ui = await ResourceUiReady("Amount;Ignored;Account\n1000;x;6000\n200;y;6001"); var vm = ui.Resources;
        vm.ResourceChoice = vm.Rows.Single(r => r.Code == "6000");
        await vm.AdvanceAsync();
        vm.AdjustmentForm["Amount"] = "-50"; vm.AdjustmentForm["Currency"] = "USD"; vm.AdjustmentForm["Quote"] = "2";
        vm.AdjustmentForm["RateSource"] = "Synthetic"; vm.AdjustmentForm["Type"] = "Correction"; vm.AdjustmentForm["Reason"] = "Synthetic delta";
        vm.AddAdjustmentCommand.Execute(null); await vm.AdvanceAsync();
        vm.PartChoice = vm.Parts.Single(); vm.LoadPartCommand.Execute(null); vm.PartForm["Amount"] = "900"; vm.ApplyPartCommand.Execute(null);
        await vm.AdvanceAsync(); vm.Reason = "Initial foreign adjustment"; Assert.IsTrue(await vm.SaveAsync(), vm.Status);
        vm.StartRevalueCommand.Execute(null); Assert.AreEqual(1, vm.RevalueRows.Count, vm.Status);
        vm.RevalueRows.Single().Form["Quote"] = "3"; vm.PrepareRevalueCommand.Execute(null);
        Assert.IsFalse(vm.CanEditRates);
        Assert.IsTrue(vm.IsRevaluing, vm.Status); var before = Hash(ui.Company.ActivePath!);
        vm.RevalueReason = "Explicit new rate"; await vm.CommitRevalueCommand.ExecuteAsync(null);
        Assert.AreEqual(before, Hash(ui.Company.ActivePath!)); Assert.IsTrue(vm.IsRevaluing);
        await vm.AdvanceAsync(); await vm.AdvanceAsync();
        vm.PartChoice = vm.Parts.Single(); vm.LoadPartCommand.Execute(null); vm.PartForm["Amount"] = "850";
        Assert.IsTrue(vm.PreviousStep());
        vm.ResourceChoice = vm.Rows.Single(r => r.Code == "6001"); await vm.AdvanceAsync();
        Assert.AreEqual("6000", vm.CurrentAccount); Assert.AreEqual("850", vm.PartForm["Amount"]);
        Assert.AreEqual(vm["RevalueFinishAccount"], vm.Status);
        await vm.AdvanceAsync(); vm.ApplyPartCommand.Execute(null);
        await vm.AdvanceAsync();
        Assert.AreEqual(0, vm.Stage, vm.Status); Assert.IsTrue(vm.IsRevaluing);
        await vm.CommitRevalueCommand.ExecuteAsync(null); Assert.IsFalse(vm.IsRevaluing, vm.Status); Assert.IsFalse(vm.IsDirty);
        var saved = ui.Company.ActiveResources!.ReadResources(ui.Company.Snapshot!.Id, ui.Studies.Current!.Id);
        Assert.AreEqual(850m, saved.Preparation.Coverage.Single(c => c.ResourceId == saved.Origins.Single(o => o.Code == "6000").Id).Effective.Amount);
        Assert.AreEqual(200m, saved.Preparation.Coverage.Single(c => c.ResourceId == saved.Origins.Single(o => o.Code == "6001").Id).Effective.Amount);
        Assert.AreEqual(3L, saved.Study.Revision);
    }
}
