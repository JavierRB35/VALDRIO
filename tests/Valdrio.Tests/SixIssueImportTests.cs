using Valdrio.Application;
using Valdrio.Desktop;
using Valdrio.Domain;

namespace Valdrio.Tests;

public sealed partial class T06ImportConfirmationTests
{
    [TestMethod]
    public async Task ReviewWithoutRepeatedApprovalCanSaveAndOpenT07WithOriginalsIntact()
    {
        using var file = Baseline(); var study = Study(file);
        using var vm = await Vm(file, study);
        vm.InterpretationApproved = false; vm.ApprovalReference = "";
        vm.AmountMeaningSelection = "closing-full";
        Assert.IsTrue(vm.CanReviewInGuide);
        await vm.PrepareCommand.ExecuteAsync(null);
        var before = Hash(file.Path);
        Assert.IsTrue(vm.HasConfirmableReview);
        Assert.IsFalse(vm.Preparation!.CanConfirm);
        Assert.IsFalse(vm.ConfirmCommand.CanExecute(null));
        Assert.AreEqual("", vm.PreparationIssues);
        Assert.IsTrue(vm.CanOpenReview);
        vm.InterpretationApproved = true;
        Assert.IsNotNull(vm.Preparation, "Approval must keep the visible preview.");
        Assert.IsTrue(vm.ConfirmCommand.CanExecute(null));
        Assert.AreEqual(before, Hash(file.Path), "Approval alone cannot write economic data.");
        await vm.ConfirmCommand.ExecuteAsync(null);
        var batch = vm.ConfirmedImport!;
        Assert.AreEqual(ImportProfileApprovalState.ApprovedBusiness, batch.Prepared.Profile.Approval.State);
        Assert.IsTrue(batch.Prepared.Profile.SignConventionConfirmed);
        Assert.AreEqual(ImportAmountMode.ClosingBalanceAsStudyAmount, batch.Prepared.Profile.AmountMode);
        StringAssert.Contains(batch.Prepared.Profile.Approval.Reference, batch.Prepared.Source.FileHash);
        Assert.AreEqual(1250.5m, batch.Resources.Single(r => r.Account == "00123").Functional.Amount);
        var resources = file.Resources.ReadResources(study.CompanyId, study.Id);
        Assert.AreEqual(batch.Resources.Count, resources.Resources.Count);
        Assert.AreEqual(1250.5m, resources.Resources.Single(r => resources.Origins.Single(o => o.Id == r.Id).Code == "00123").Sources.Sum(s => s.Conversion.ConvertedOriginalMoney.Amount));
    }

    [TestMethod]
    public async Task ChangedSeparatorsRevokeApprovalAndRequireValidationBeforeReview()
    {
        using var file = Baseline(); var study = Study(file); using var vm = await Vm(file, study);
        vm.AmountMeaningSelection = "period";
        Assert.IsTrue(vm.CanOpenReview);
        vm.DecimalSeparator = "."; vm.GroupSeparator = ",";
        Assert.IsFalse(vm.IsReady); Assert.IsNull(vm.Preparation);
        Assert.IsFalse(vm.InterpretationApproved); Assert.IsFalse(vm.CanReviewInGuide);
        await vm.ValidateCommand.ExecuteAsync(null);
        Assert.IsFalse(vm.IsReady, "1.250,50 must not be silently read under the opposite profile.");
        Assert.IsFalse(vm.ConfirmCommand.CanExecute(null));
        vm.DecimalSeparator = ","; vm.GroupSeparator = ".";
        await vm.ValidateCommand.ExecuteAsync(null);
        Assert.IsTrue(vm.CanReviewInGuide);
        Assert.AreEqual(1250.5m, vm.AccountChoices.Single(r => r.Account == "00123").Amount);
    }

    [TestMethod]
    public async Task ReviewRequiresApprovalUsesStudyCurrencyAndCumulativeRequiresComparison()
    {
        using var file = Baseline(); var study = Study(file); using var vm = await Vm(file, study);
        vm.InterpretationApproved = false; vm.ApprovalReference = ""; vm.AmountMeaningSelection = "cumulative";
        Assert.IsFalse(vm.CanReviewInGuide);
        Assert.AreEqual(vm.Guide("NeedComparison"), vm.ReviewRequirement);
        vm.AmountMeaningSelection = "period"; vm.SourceCurrency = "USD";
        Assert.AreEqual(study.Currency.FunctionalCurrencyCode.ToString(), vm.SourceCurrency,
            "Ordinary imports cannot override the study currency.");
        await vm.PrepareCommand.ExecuteAsync(null);
        Assert.IsTrue(vm.HasConfirmableReview);
        Assert.AreEqual(study.Currency.FunctionalCurrencyCode, vm.Preparation!.Profile.FixedCurrency);
        Assert.IsFalse(vm.ConfirmCommand.CanExecute(null), "Final review approval is still mandatory.");
        Assert.AreEqual(0, file.Imports.ReadImports(study.CompanyId, study.Id).Count);
    }
    [TestMethod]
    public async Task RetainingComparisonDoesNotRequireApprovalOfTheCurrentBatch()
    {
        using var file = Baseline(); var study = Study(file); using var vm = await Vm(file, study);
        vm.InterpretationApproved = false; vm.ApprovalReference = "";
        vm.FiscalStart = new(2026, 1, 1); vm.FiscalEnd = new(2026, 12, 31);
        vm.PreviousEnd = new(2026, 9, 30); vm.ComparisonScope = "Same entity and selected expense accounts";
        vm.AmountMeaningSelection = "closing-full";
        Assert.IsTrue(vm.FreezeComparisonCommand.CanExecute(null));
        var before = Hash(file.Path);
        vm.FreezeComparisonCommand.Execute(null);
        Assert.IsFalse(vm.InterpretationApproved);
        Assert.AreEqual("", vm.ApprovalReference);
        vm.AmountMeaningSelection = "cumulative";
        Assert.IsTrue(vm.CanReviewInGuide, vm.ReviewRequirement);
        Assert.AreEqual(before, Hash(file.Path));
        Assert.AreEqual(0, file.Imports.ReadImports(study.CompanyId, study.Id).Count);
    }
}