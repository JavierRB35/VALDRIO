using System.Globalization;
using System.Runtime.ExceptionServices;
using Valdrio.Application;
using Valdrio.Desktop;
using Valdrio.Domain;
using Valdrio.Localization;

namespace Valdrio.Tests;

// Also catches exceptions that the UI swallows: those still interrupt a debugger.
internal sealed class InputExceptionProbe : IDisposable
{
    private readonly List<string> errors = [];
    public InputExceptionProbe() => AppDomain.CurrentDomain.FirstChanceException += Seen;
    private void Seen(object? sender, FirstChanceExceptionEventArgs e)
    {
        if (e.Exception is DomainRuleException or FormatException or InvalidDataException)
            lock (errors) errors.Add(e.Exception.Message);
    }
    public void AssertEmpty() { lock (errors) Assert.AreEqual(0, errors.Count, string.Join("; ", errors)); }
    public void Dispose() => AppDomain.CurrentDomain.FirstChanceException -= Seen;
}

public sealed partial class T06ImportConfirmationTests
{
    [TestMethod]
    public async Task InputValidation_StudyMissingDatesModelCurrencyAndInvertedDatesNeverThrow()
    {
        using var ui = await ResourceUiReady();
        var vm = ui.Studies;
        await vm.NewCommand.ExecuteAsync(null);
        var model = vm.ModelChoice;
        var before = Hash(ui.Company.ActivePath!);
        using var probe = new InputExceptionProbe();
        vm.Name = "Next period";
        await vm.CreateCommand.ExecuteAsync(null);
        Assert.AreEqual("STUDY_DATES_REQUIRED", vm.DiagnosticCode);
        vm.PeriodStart = new(2027, 2, 2); vm.PeriodEnd = new(2027, 2, 1);
        await vm.CreateCommand.ExecuteAsync(null);
        Assert.AreEqual("STUDY_PERIOD_INVALID", vm.DiagnosticCode);
        vm.PeriodEnd = new(2027, 2, 28); vm.ModelChoice = null;
        await vm.CreateCommand.ExecuteAsync(null);
        Assert.AreEqual("STUDY_MODEL_NOT_PUBLISHED", vm.DiagnosticCode);
        vm.ModelChoice = model; vm.Currency = "";
        await vm.CreateCommand.ExecuteAsync(null);
        Assert.AreEqual("STUDY_CURRENCY_INVALID", vm.DiagnosticCode);
        Assert.AreEqual(before, Hash(ui.Company.ActivePath!));
        Assert.IsTrue(vm.IsDirty); Assert.IsFalse(vm.IsBusy);
        probe.AssertEmpty();
    }

    [TestMethod]
    public async Task InputValidation_AllocationIncompleteNumbersDoNotThrowWhileTypingOrSaving()
    {
        using var ui = await ResourceUiReady();
        using var vm = new AllocationWorkspaceViewModel(ui.Company, ui.Studies, new UiTextCatalog(), ui.Preferences, ui.Dialogs);
        await vm.EnterAsync(); vm.Name = "Distribution";
        var before = Hash(ui.Company.ActivePath!);
        using var probe = new InputExceptionProbe();
        vm.Lines.First().Included = true;
        foreach (var input in new[] { "", "-", "abc", "-1" })
        {
            vm.Lines.First().Value = input;
            _ = vm.Total; _ = vm.Validation;
            Assert.IsFalse(await vm.SaveKeyAsync(false));
            Assert.IsFalse(await vm.SaveKeyAsync(true));
        }
        Assert.AreEqual(before, Hash(ui.Company.ActivePath!));
        vm.Lines.First().Value = "100";
        Assert.IsTrue(await vm.SaveKeyAsync(true), vm.Status);
        probe.AssertEmpty();
    }

    [TestMethod]
    public async Task InputValidation_ResourceFormsRetainInvalidEditsWithoutWriting()
    {
        using var ui = await ResourceUiReady();
        var vm = ui.Resources;
        await vm.OpenSelectedResourceCommand.ExecuteAsync(null);
        vm.PartChoice = vm.Parts.Single(); vm.LoadPartCommand.Execute(null);
        var before = Hash(ui.Company.ActivePath!);
        using var probe = new InputExceptionProbe();
        vm.PartForm["Amount"] = "-";
        vm.ApplyPartCommand.Execute(null);
        Assert.IsTrue(vm.HasStatusError); Assert.AreEqual("-", vm.PartForm["Amount"]);
        vm.PartForm["Amount"] = "1000"; vm.PartForm.Route = ResourceRoutingKind.Excluded;
        vm.PartForm["Exclusion"] = ""; vm.ApplyPartCommand.Execute(null);
        Assert.IsTrue(vm.HasStatusError);
        Assert.AreEqual(before, Hash(ui.Company.ActivePath!));
        Assert.IsTrue(vm.IsDirty);
        probe.AssertEmpty();
    }

    [TestMethod]
    public async Task InputValidation_EmptyCenterAndStructureNamesDoNotReachDomain()
    {
        using var ui = await ResourceUiReady();
        using var vm = new ModelWorkspaceViewModel(ui.Company, new UiTextCatalog(), ui.Preferences, ui.Dialogs,
            new PresentationFormatter(new DisplayTimeZoneResolver()));
        await vm.RefreshCommand.ExecuteAsync(null);
        var before = Hash(ui.Company.ActivePath!);
        using var probe = new InputExceptionProbe();
        vm.CenterCode = "NEW"; vm.CenterName = "";
        await vm.CreateCenterCommand.ExecuteAsync(null);
        StringAssert.Contains(vm.Status, new UiTextCatalog().Get("Model.CenterInvalid", CultureInfo.GetCultureInfo(ui.Preferences.Current.UiLanguageTag)));
        vm.CenterCode = ""; vm.Description = "";
        await vm.NewDraftCommand.ExecuteAsync(null);
        StringAssert.Contains(vm.Status, new UiTextCatalog().Get("Model.StructureInvalid", CultureInfo.GetCultureInfo(ui.Preferences.Current.UiLanguageTag)));
        Assert.AreEqual(before, Hash(ui.Company.ActivePath!));
        probe.AssertEmpty();
    }

    [TestMethod]
    public async Task InputValidation_ImportInvalidColumnsAndFormatsDoNotThrow()
    {
        using var file = Baseline(); var study = Study(file);
        using var vm = await Vm(file, study);
        var before = Hash(file.Path);
        using var probe = new InputExceptionProbe();
        vm.AccountColumn = null; await vm.ValidateCommand.ExecuteAsync(null);
        Assert.IsTrue(vm.HasUserError);
        vm.AccountColumn = 1; await vm.ValidateCommand.ExecuteAsync(null);
        Assert.IsTrue(vm.HasUserError);
        vm.AccountColumn = 3; vm.DecimalSeparator = "."; vm.GroupSeparator = ".";
        await vm.ValidateCommand.ExecuteAsync(null);
        Assert.IsTrue(vm.HasUserError);
        vm.HeaderRow = "-"; await vm.ReadCommand.ExecuteAsync(null);
        Assert.IsTrue(vm.HasUserError);
        Assert.AreEqual(before, Hash(file.Path));
        probe.AssertEmpty();
    }

    [TestMethod]
    public async Task InputValidation_ProductAndEstimateFormsRejectIncompleteInputWithoutWriting()
    {
        using var ui = await ResourceUiReady();
        var composition = new CompositionEditor(ui.Studies);
        var estimates = new EstimateEditor(ui.Studies);
        await estimates.LoadAsync();
        var before = Hash(ui.Company.ActivePath!);
        using var probe = new InputExceptionProbe();
        composition.AddItem();
        Assert.IsFalse(await composition.SaveAsync(false));
        Assert.IsTrue(composition.Dirty);
        Assert.AreEqual(composition["InvalidItem"], composition.Feedback);
        estimates.Quantity = "-"; await estimates.PreviewAsync();
        Assert.AreEqual(estimates["Number"], estimates.Feedback);
        estimates.Quantity = "1"; estimates.Code = ""; await estimates.PreviewAsync();
        Assert.AreEqual(estimates["InvalidInput"], estimates.Feedback);
        Assert.IsFalse(estimates.CanAccept); Assert.IsFalse(estimates.Busy);
        Assert.AreEqual(before, Hash(ui.Company.ActivePath!));
        probe.AssertEmpty();
    }
}

public sealed partial class T14PhysicalActivityTests
{
    [TestMethod]
    public void InputValidation_CapacityTypingAndIncompleteConfirmationNeverThrow()
    {
        var (file, study, _, _) = Fixture();
        using (file)
        {
            var editor = Editor(file, study);
            using var probe = new InputExceptionProbe();
            var assistant = new PhysicalCapacityAssistantEditor(editor);
            foreach (var text in new[] { "", "-", "abc", "-1", "32", "1,5" })
            {
                assistant.OperatingDays = text; assistant.Preview(); assistant.Adopt();
                Assert.IsFalse(assistant.DaysError.StartsWith("Activity.", StringComparison.Ordinal));
                Assert.IsFalse(assistant.CanAdopt);
            }
            assistant.OperatingDays = "20"; assistant.ShiftsPerDay = "1"; assistant.HoursPerShift = "8";
            assistant.EquivalentResources = "1"; assistant.Stoppages = "0"; assistant.NormalFactor = "100";
            assistant.FactorReason = "Approved calendar";
            Assert.IsTrue(assistant.CanAdopt, assistant.Feedback);
            assistant.Adopt();
            var row = editor.SelectedRow!;
            row.RealText = "-"; editor.Save(true);
            Assert.AreEqual("-", row.RealText);
            row.RealText = ""; row.RealSource = ""; editor.Save(true);
            Assert.AreEqual(0, file.PhysicalActivity.ReadCurrent(study.CompanyId, study.Id).Count);
            row.RealText = "100"; row.RealSource = ""; editor.Save(true);
            Assert.AreEqual(1, file.PhysicalActivity.ReadCurrent(study.CompanyId, study.Id).Count);
            Assert.IsFalse(editor.HasPendingEdits, editor.Feedback);
            probe.AssertEmpty();
        }
    }
}

[TestClass]
public sealed class ProductImportInputValidationTests
{
    [TestMethod]
    public void InputValidation_ProductImportReturnsErrorsWithoutExceptionsOrPartialRows()
    {
        var path = Path.GetTempFileName();
        try
        {
            using var probe = new InputExceptionProbe();
            foreach (var csv in new[] { "", "a;b\n1;2", "\"unterminated", "Reference;Name;Unit;Unit cost\nA;Item;h;bad", "Reference;Name;Unit;Unit cost\nA;Item;h;1\nA;Duplicate;h;2" })
            {
                File.WriteAllText(path, csv);
                var result = ProductCostImport.ReadResult(path, CultureInfo.InvariantCulture);
                Assert.IsNull(result.Value); Assert.IsFalse(string.IsNullOrEmpty(result.Error));
            }
            File.WriteAllText(path, "Reference;Name;Unit;Unit cost\n001;Item;h;1.5");
            var valid = ProductCostImport.ReadResult(path, CultureInfo.InvariantCulture);
            Assert.AreEqual("001", valid.Value!.Single().Reference);
            Assert.IsNull(valid.Error);
            var merge = ProductCostImport.MergeResult([], valid.Value!, [], CompositionItemKind.Product, "test");
            Assert.IsNull(merge.Value); StringAssert.StartsWith(merge.Error!, "Unit:");
            probe.AssertEmpty();
        }
        finally { File.Delete(path); }
    }
}
