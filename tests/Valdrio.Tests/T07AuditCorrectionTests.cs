using System.Globalization;
using Valdrio.Application;
using Valdrio.Calculation;
using Valdrio.Domain;

namespace Valdrio.Tests;

public sealed partial class T06ImportConfirmationTests
{
    private static ResourceMutation? Correct(ResourceWorkspaceSnapshot w, decimal total, long? revision = null) =>
        ResourceCorrectionCommand.Prepare(w, w.Resources.Single().Id, revision ?? w.Study.Revision,
            new(total, w.Study.Currency.FunctionalCurrencyCode, w.Precision),
            Catalog.GetRequired(w.Study.Currency.FunctionalCurrencyCode).Freeze(), Id(), Id(), Id(), "test");

    [TestMethod]
    public async Task T07_CorrectionCaseUseChanges50To20WithDeltaMinus30AndNoRepeatedWrite()
    {
        using var file = Baseline(); var w = await ImportedResources(file);
        file.Resources.ChangeResources(Correct(w, 50m)!);
        w = file.Resources.ReadResources(w.Study.CompanyId, w.Study.Id);
        var decrease = Correct(w, 20m)!;
        Assert.AreEqual(-30m, decrease.Adjustments.Single().OriginalDelta!.Value.Amount);
        file.Resources.ChangeResources(decrease);
        w = file.Resources.ReadResources(w.Study.CompanyId, w.Study.Id);
        Assert.AreEqual(20m, w.Preparation.Coverage.Single().AdjustmentTotal.Amount);
        Assert.AreEqual(1020m, w.Preparation.Coverage.Single().Pending.Amount);
        var hash = Hash(file.Path);
        Assert.IsNull(Correct(w, 20m));
        Assert.AreEqual(hash, Hash(file.Path));
        Assert.ThrowsExactly<DomainRuleException>(() => Correct(w, 20m, w.Study.Revision - 1));
        Assert.AreEqual(hash, Hash(file.Path));
    }

    [TestMethod]
    public async Task T07_CorrectionCalculationPreservesPendingIdentityAndClassification()
    {
        using var file = Baseline(); var w = await ImportedResources(file);
        var resource = w.Resources.Single(); var original = resource.Parts.Single();
        var classification = new ResourceClassification(ResourceBehavior.Unclassified, null,
            ResourceCostLayer.Structure, ResourceCapacityTreatment.None, null);
        var classified = original.WithClassification(classification);
        var draft = new ResourceDraft(resource.Id, resource.Sources, resource.Adjustments, [classified]);
        var correction = ResourceCorrectionRules.Prepare(draft, new(-1200m, new("EUR")), Id())!;
        Assert.AreEqual(original.Id, correction.Parts.Single().Id);
        Assert.AreEqual(classification, correction.Parts.Single().Classification);
        Assert.AreEqual(-200m, correction.Parts.Single().Amount.Amount);
        Assert.AreEqual(1000m, draft.Parts.Single().Amount.Amount);
        Assert.AreEqual(resource.Sources.Single().Conversion, draft.Sources.Single().Conversion);
    }

    [TestMethod]
    public async Task T07_PendingZeroAndCompensatedPendingRoutesRemainVisible()
    {
        using var ui = await ResourceUiReady("Amount;Ignored;Account\n0;x;6000"); var vm = ui.Resources;
        Assert.AreEqual("0 EUR", vm.Rows.Single().Pending);
        Assert.AreEqual(vm["AssignmentPending"], vm.Rows.Single().AssignmentStatus);
        var study = ui.Studies.Current!; var w = ui.Company.ActiveResources!.ReadResources(study.CompanyId, study.Id);
        ui.Company.ActiveResources.ChangeResources(Mutation(w, ResourceMutationKind.EditResource,
            w.Resources.Single().Id, [Pending(100m), Pending(-100m)]));
        await ui.Studies.RefreshCommand.ExecuteAsync(null); await vm.EnterAsync(true);
        Assert.AreEqual("0 EUR", vm.Rows.Single().Pending);
        Assert.AreEqual(vm["AssignmentPending"], vm.Rows.Single().AssignmentStatus);
        foreach (var language in new[] { "es", "en" })
        {
            ui.Preferences.Update(ui.Preferences.Current with { UiLanguageTag = language });
            StringAssert.Contains(vm.ProgressSummary, language == "es" ? "1 de 1 cuentas conciliadas" : "1 of 1 accounts reconciled");
            Assert.AreEqual(vm["AssignmentPending"], vm.Rows.Single().AssignmentStatus);
            // Net zero must not hide the two unallocated parts in step 6.
            var allocation = ui.Company.ActiveAllocations!.ReadAllocations(study.CompanyId, study.Id);
            Assert.AreEqual(2, allocation.Coverage.Count(c => c.Account == "6000" && c.Diagnostic?.Code == "KEY_RULE_MISSING"));
        }
        w = ui.Company.ActiveResources.ReadResources(study.CompanyId, study.Id);
        ui.Company.ActiveResources.ChangeResources(Mutation(w, ResourceMutationKind.EditResource,
            w.Resources.Single().Id, [new(Id(), Eur(0), ResourceRoutingKind.Excluded, exclusionReason: "Explicit zero exclusion")]));
        await ui.Studies.RefreshCommand.ExecuteAsync(null); await vm.EnterAsync(true);
        Assert.AreEqual(vm["AssignmentComplete"], vm.Rows.Single().AssignmentStatus);
    }

    [TestMethod]
    public async Task T07_FailedRevaluePreviewKeepsRateEditingAndFileUnchanged()
    {
        using var ui = await ResourceUiReady(); var vm = ui.Resources;
        await vm.AdvanceAsync();
        vm.AdjustmentForm["Amount"] = "1"; vm.AdjustmentForm["Currency"] = "USD";
        vm.AdjustmentForm["Quote"] = "1"; vm.AdjustmentForm["RateSource"] = "Synthetic rate";
        vm.AdjustmentForm["Type"] = "Historical correction"; vm.AdjustmentForm["Reason"] = "Revalue regression";
        vm.AddAdjustmentCommand.Execute(null);
        vm.PartChoice = vm.Parts.Single(); vm.LoadPartCommand.Execute(null);
        vm.PartForm["Amount"] = "1001"; vm.ApplyPartCommand.Execute(null);
        vm.Reason = "Synthetic foreign correction"; Assert.IsTrue(await vm.SaveAsync(), vm.Status);
        var hash = Hash(ui.Company.ActivePath!);
        vm.StartRevalueCommand.Execute(null); vm.RevalueReason = "Keep my reason";
        var rate = vm.RevalueRows.Single(); rate.Form["Quote"] = "2000000000000";
        vm.PrepareRevalueCommand.Execute(null);
        Assert.IsTrue(vm.CanEditRates); Assert.IsFalse(vm.IsRevaluing);
        Assert.IsFalse(vm.CommitRevalueCommand.CanExecute(null));
        Assert.AreEqual("Keep my reason", vm.RevalueReason); Assert.AreEqual(hash, Hash(ui.Company.ActivePath!));
        rate.Form["Quote"] = "2"; vm.PrepareRevalueCommand.Execute(null);
        Assert.IsTrue(vm.IsRevaluing, vm.Status); Assert.IsFalse(vm.CanEditRates);
        Assert.AreEqual("1002 EUR", vm.Rows.Single().Effective);
    }

    [TestMethod]
    public async Task StudyContextUsesTheSavedModelInsteadOfTheEditedChoice()
    {
        using var ui = await ResourceUiReady(); var studies = ui.Studies;
        var saved = studies.Current!.SelectedModelVersionId;
        var expected = studies.PublishedModels.Single(m => m.Value.Structure.Id == saved).Value.Structure.Description;
        Assert.AreEqual(expected, studies.CurrentModelDescription);
        studies.ModelChoice = null;
        Assert.AreEqual(expected, studies.CurrentModelDescription);
    }
}
