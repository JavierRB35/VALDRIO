using System.Globalization;
using Valdrio.Desktop;
using Valdrio.Domain;
namespace Valdrio.Tests;

public sealed partial class T06ImportConfirmationTests
{
    private static CultureInfo EsNumbers => CultureInfo.GetCultureInfo("es-ES");
    [TestMethod]
    public async Task T07_InlineCorrectionSavesTotalsDeltasAndReopensWithoutChangingOriginal()
    {
        using var ui = await ResourceUiReady(); var vm = ui.Resources;
        Assert.IsTrue(await vm.SaveCorrectionAsync(vm.Rows.Single(), "-100,25", EsNumbers), vm.Status);
        Assert.AreEqual("899,75 EUR", vm.Rows.Single().Effective);
        Assert.AreEqual("899,75 EUR", vm.Rows.Single().Pending);
        Assert.AreEqual("0 EUR", vm.Rows.Single().Difference);
        Assert.AreEqual(vm["CorrectionSaved"], vm.Status);
        Assert.IsFalse(vm.IsDirty);
        Assert.IsTrue(await vm.SaveCorrectionAsync(vm.Rows.Single(), "-150,25", EsNumbers), vm.Status);
        var current = ui.Studies.Current!;
        var read = ui.Company.ActiveResources!.ReadResources(current.CompanyId, current.Id);
        CollectionAssert.AreEquivalent(new[] { -100.25m, -50m }, read.Resources.Single().Adjustments.Select(a => a.Conversion.OriginalMoney.Amount).ToArray());
        Assert.IsTrue(read.Resources.Single().Adjustments.All(a => a.Conversion.IsIdentity && a.Type.Length > 0 && a.Reason.Length > 0 && a.CurrencyMetadata!.AlphaCode.Value == "EUR"));
        Assert.AreEqual(1000m, read.Resources.Single().Sources.Single().Conversion.OriginalMoney.Amount);
        Assert.AreEqual(3L, current.Revision);
        var path = ui.Company.ActivePath!;
        await ui.Company.CloseCommand.ExecuteAsync(null); await ui.Company.OpenPath(path);
        ui.Studies.StudyChoice = ui.Studies.Studies.Single(s => s.Value.Id == current.Id);
        await ui.Studies.LoadCommand.ExecuteAsync(null); await vm.EnterAsync(true);
        Assert.AreEqual("-150,25 EUR", vm.Rows.Single().Adjustments);
        Assert.AreEqual("849,75 EUR", vm.Rows.Single().Effective);
        Assert.AreEqual(2, ui.Company.ActiveResources!.ReadResourceHistory(current.CompanyId, current.Id).Count);
    }
    [TestMethod]
    public async Task T07_InlineZeroAndRepeatedValueDoNotDuplicateCorrections()
    {
        using var ui = await ResourceUiReady(); var vm = ui.Resources;
        var initial = Hash(ui.Company.ActivePath!);
        Assert.IsTrue(await vm.SaveCorrectionAsync(vm.Rows.Single(), "0", EsNumbers));
        Assert.AreEqual(initial, Hash(ui.Company.ActivePath!));
        Assert.IsTrue(await vm.SaveCorrectionAsync(vm.Rows.Single(), "50", EsNumbers));
        var saved = Hash(ui.Company.ActivePath!);
        Assert.IsTrue(await vm.SaveCorrectionAsync(vm.Rows.Single(), "50,00", EsNumbers));
        Assert.AreEqual(saved, Hash(ui.Company.ActivePath!));
        Assert.IsTrue(await vm.SaveCorrectionAsync(vm.Rows.Single(), "0", EsNumbers));
        Assert.AreEqual("1000 EUR", vm.Rows.Single().Effective);
        Assert.AreEqual("0 EUR", vm.Rows.Single().Adjustments);
        var study = ui.Studies.Current!;
        CollectionAssert.AreEquivalent(new[] { 50m, -50m }, ui.Company.ActiveResources!.ReadResources(study.CompanyId, study.Id)
            .Resources.Single().Adjustments.Select(a => a.Conversion.OriginalMoney.Amount).ToArray());
    }
    [TestMethod]
    public async Task T07_InlineInvalidOverflowPrecisionAndStaleRowNeverChangeFile()
    {
        using var ui = await ResourceUiReady(); var vm = ui.Resources;
        var initial = Hash(ui.Company.ActivePath!); var row = vm.Rows.Single();
        foreach (var value in new[] { "", "EUR 12", "12.34", "NaN", "79228162514264337593543950336", "0,000000001" })
        {
            Assert.IsFalse(await vm.SaveCorrectionAsync(row, value, EsNumbers), value);
            Assert.AreEqual(initial, Hash(ui.Company.ActivePath!), value);
            StringAssert.Contains(vm.CorrectionLockMessage, vm["CorrectionNotSaved"]);
        }
        Assert.IsTrue(await vm.SaveCorrectionAsync(row, "-10", EsNumbers), vm.Status);
        var saved = Hash(ui.Company.ActivePath!);
        Assert.IsFalse(await vm.SaveCorrectionAsync(row, "-20", EsNumbers));
        Assert.AreEqual(saved, Hash(ui.Company.ActivePath!));
        Assert.AreEqual("990 EUR", vm.Rows.Single().Effective);
    }
    [TestMethod]
    public async Task T07_InlineCapturedCultureAndNegativeEffectiveRemainExact()
    {
        using var ui = await ResourceUiReady(); var vm = ui.Resources; var row = vm.Rows.Single();
        ui.Preferences.Update(ui.Preferences.Current with { FormatCultureTag = "en-US", UiLanguageTag = "en" });
        Assert.IsTrue(await vm.SaveCorrectionAsync(row, "-1.200,25", EsNumbers), vm.Status);
        Assert.AreEqual("-200.25 EUR", vm.Rows.Single().Effective);
        Assert.AreEqual("-200.25 EUR", vm.Rows.Single().Pending);
        Assert.AreEqual("0 EUR", vm.Rows.Single().Difference);
    }
    [TestMethod]
    public async Task T07_InlineCorrectionsPreserveAssignedDestinationsAndUseSignedPendingRemainder()
    {
        using var ui = await ResourceUiReady(); var vm = ui.Resources;
        await vm.AdvanceAsync(); Assert.AreEqual(2, vm.Stage);
        vm.PartChoice = vm.Parts.Single(); vm.LoadPartCommand.Execute(null);
        vm.PartForm.Route = ResourceRoutingKind.Excluded; vm.PartForm["Amount"] = "1000"; vm.PartForm["Exclusion"] = "Outside perimeter";
        vm.ApplyPartCommand.Execute(null); vm.Reason = "Explicit exclusion"; Assert.IsTrue(await vm.SaveAsync(), vm.Status);
        var study = ui.Studies.Current!;
        var part = ui.Company.ActiveResources!.ReadResources(study.CompanyId, study.Id).Resources.Single().Parts.Single();
        Assert.IsTrue(await vm.SaveCorrectionAsync(vm.Rows.Single(), "-100", EsNumbers), vm.Status);
        var read = ui.Company.ActiveResources.ReadResources(study.CompanyId, study.Id);
        var retained = read.Resources.Single().Parts.Single(p => p.Id == part.Id);
        Assert.AreEqual(part.Amount, retained.Amount); Assert.AreEqual(part.ExclusionReason, retained.ExclusionReason);
        Assert.AreEqual(-100m, read.Preparation.Coverage.Single().Pending.Amount);
        Assert.AreEqual(0m, read.Preparation.Coverage.Single().Difference.Amount);
        Assert.IsTrue(await vm.SaveCorrectionAsync(vm.Rows.Single(), "-150", EsNumbers), vm.Status);
        read = ui.Company.ActiveResources.ReadResources(study.CompanyId, study.Id);
        Assert.AreEqual(2, read.Resources.Single().Parts.Count);
        Assert.AreEqual(-150m, read.Preparation.Coverage.Single().Pending.Amount);
    }
    [TestMethod]
    public async Task T07_InlineBlocksUnsavedDestinationDraftWithoutLosingIt()
    {
        using var ui = await ResourceUiReady(); var vm = ui.Resources;
        await vm.AdvanceAsync(); vm.PartForm["Amount"] = "400";
        Assert.IsFalse(vm.CanEditCorrections);
        var hash = Hash(ui.Company.ActivePath!);
        Assert.IsFalse(await vm.SaveCorrectionAsync(vm.Rows.Single(), "100", EsNumbers));
        Assert.AreEqual(hash, Hash(ui.Company.ActivePath!)); Assert.AreEqual("400", vm.PartForm["Amount"]);
        Assert.IsTrue(vm.ShowList && vm.ShowParts && vm.ShowReview && !vm.ShowAdjustments);
        vm.DiscardCommand.Execute(null); Assert.IsTrue(vm.CanEditCorrections);
    }
    [TestMethod]
    public async Task T07_InlineClosedStudyAndStaleStoredRevisionRejectWithoutChangingSavedFile()
    {
        using var ui = await ResourceUiReady(); var vm = ui.Resources; var row = vm.Rows.Single();
        await ui.Studies.ToggleStateCommand.ExecuteAsync(null);
        var closedHash = Hash(ui.Company.ActivePath!);
        // The editor still has its old revision; the persistence layer must reject it.
        Assert.IsFalse(await vm.SaveCorrectionAsync(row, "100", EsNumbers));
        Assert.AreEqual(closedHash, Hash(ui.Company.ActivePath!));
        await vm.EnterAsync(true);
        Assert.IsTrue(vm.CorrectionsReadOnly);
        Assert.IsFalse(await vm.SaveCorrectionAsync(vm.Rows.Single(), "100", EsNumbers));
        Assert.AreEqual(closedHash, Hash(ui.Company.ActivePath!));
        await ui.Studies.ToggleStateCommand.ExecuteAsync(null); await vm.EnterAsync(true);
        Assert.IsTrue(vm.CanEditCorrections);
        Assert.IsTrue(await vm.SaveCorrectionAsync(vm.Rows.Single(), "100", EsNumbers), vm.Status);
    }
    [TestMethod]
    public async Task T07_InlineExistingForeignCorrectionRetainsFrozenFxAndAddsOnlyFunctionalDelta()
    {
        using var ui = await ResourceUiReady(); var vm = ui.Resources;
        await vm.AdvanceAsync();
        vm.AdjustmentForm["Amount"] = "-50"; vm.AdjustmentForm["Currency"] = "USD";
        vm.AdjustmentForm["Quote"] = "2"; vm.AdjustmentForm["RateSource"] = "Historical synthetic rate";
        vm.AdjustmentForm["Type"] = "Historical correction"; vm.AdjustmentForm["Reason"] = "Historical example";
        vm.AddAdjustmentCommand.Execute(null);
        vm.PartChoice = vm.Parts.Single(); vm.LoadPartCommand.Execute(null);
        vm.PartForm["Amount"] = "900"; vm.ApplyPartCommand.Execute(null);
        vm.Reason = "Synthetic historical correction"; Assert.IsTrue(await vm.SaveAsync(), vm.Status);
        var study = ui.Studies.Current!;
        var previous = ui.Company.ActiveResources!.ReadResources(study.CompanyId, study.Id).Resources.Single().Adjustments.Single();
        Assert.IsTrue(await vm.SaveCorrectionAsync(vm.Rows.Single(), "-150", EsNumbers), vm.Status);
        var read = ui.Company.ActiveResources.ReadResources(study.CompanyId, study.Id);
        Assert.AreEqual(previous, read.Resources.Single().Adjustments.Single(a => a.Id == previous.Id));
        var added = read.Resources.Single().Adjustments.Single(a => a.Id != previous.Id);
        Assert.AreEqual(-50m, added.Conversion.OriginalMoney.Amount);
        Assert.AreEqual("EUR", added.Conversion.OriginalMoney.Currency.Value);
        Assert.IsTrue(added.Conversion.IsIdentity);
        Assert.AreEqual(850m, read.Preparation.Coverage.Single().Effective.Amount);
        Assert.AreEqual(0m, read.Preparation.Coverage.Single().Difference.Amount);
    }

}
