namespace Valdrio.Tests;

public sealed partial class T06ImportConfirmationTests
{
    [TestMethod]
    public async Task GuidedReviewRequiresExplicitMeaningAndRevokesPreparedSaveWhenWithdrawn()
    {
        using var file = Baseline(); var study = Study(file);
        using var vm = await Vm(file, study);
        var original = Hash(file.Path);
        Assert.IsFalse(vm.CanReviewInGuide);
        Assert.AreEqual(vm.Guide("NeedMeaning"), vm.ReviewRequirement);
        vm.AcknowledgeAmountMeaning();
        Assert.IsTrue(vm.CanReviewInGuide);
        await vm.PrepareCommand.ExecuteAsync(null);
        Assert.IsTrue(vm.ConfirmCommand.CanExecute(null));
        vm.HasAmountMeaning = false;
        Assert.IsFalse(vm.CanReviewInGuide);
        Assert.IsNull(vm.Preparation);
        Assert.IsFalse(vm.ConfirmCommand.CanExecute(null));
        Assert.AreEqual(original, Hash(file.Path));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task InspectionOrReadOnlyDestinationExplainsRestrictionWithoutLosingRows(bool readOnly)
    {
        using var file = Baseline(); var study = Study(file);
        using var vm = await Vm(file, study);
        var selected = vm.AccountChoices.Where(r => r.Included).Select(r => r.RecordNumber).ToArray();
        var original = Hash(file.Path);
        vm.AcknowledgeAmountMeaning();
        vm.BindTarget(readOnly ? file.Imports : null, readOnly ? study : null, false, null);
        Assert.IsFalse(vm.HasImportDestination);
        Assert.IsFalse(vm.CanReviewInGuide);
        Assert.IsFalse(vm.ConfirmCommand.CanExecute(null));
        Assert.AreEqual(vm.Guide("PreviewOnly"), vm.ReviewRequirement);
        CollectionAssert.AreEqual(selected, vm.AccountChoices.Where(r => r.Included).Select(r => r.RecordNumber).ToArray());
        Assert.AreEqual(original, Hash(file.Path));
    }
}
