using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace Valdrio.Desktop;

public partial class ImportColumnMappingWindow
{
    internal async Task RunImportMappingChecks(string directory, QueuedImportSourceDialogs files, MainWindowViewModel main)
    {
        var vm = (ImportColumnMappingViewModel)DataContext;
        var output = Path.Combine(directory, "import-columns"); Directory.CreateDirectory(output);
        var passed = new List<string>();
        void Check(bool value, string label) { if (!value) throw new InvalidOperationException("Import mapping WPF: " + label); passed.Add(label); }
        async Task Click(Button button)
        {
            if (!button.IsEnabled) throw new InvalidOperationException("Disabled import button " + button.Name);
            ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            if (button.Command is IAsyncRelayCommand command && command.ExecutionTask is { } task) await task.WaitAsync(TimeSpan.FromSeconds(45));
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle); UpdateLayout();
        }
        void Select(ComboBox box, object value)
        { box.SetCurrentValue(ComboBox.SelectedValueProperty, value); box.GetBindingExpression(ComboBox.SelectedValueProperty)!.UpdateSource(); }
        var companyPath = main.Company!.ActivePath!;
        static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
        var companyHash = Hash(companyPath);
        var csv = Path.Combine(output, "manual-columns.csv");
        File.WriteAllText(csv, "Amount;Unused;Account\n1.250,50;not an amount;00123\n0;unused;00456");
        var sourceHash = Hash(csv);
        files.Paths.Enqueue(csv); await Click(OpenSourceButton); await Click(ReadSourceButton);
        Check(vm.Columns.Count == 3 && vm.AccountColumn is null && vm.AmountColumn is null, "no automatic column assignment");
        Select(AccountColumnBox, 3); Select(AmountColumnBox, 1); vm.GroupSeparator = ".";
        await Click(ValidateMappingButton);
        Check(vm.IsReady && vm.ValidatedSelection!.Rows.Count == 2 && vm.ValidatedSelection.Rows[0].Amount == 1250.50m,
            "CSV arbitrary columns validated by controls");
        Check(vm.Preview[0].Account == "00123" && vm.ValidatedSelection!.Rows[1].Amount == 0m, "leading zeros and explicit zero preserved");
        Select(AmountColumnBox, 3); await Click(ValidateMappingButton);
        Check(!vm.IsReady && vm.Status.StartsWith("IMP_COLUMN_SELECTION", StringComparison.Ordinal), "same column blocked by controls");
        Select(AmountColumnBox, 1); await Click(ValidateMappingButton);
        var selection = vm.ValidatedSelection;
        main.ApplyPresentationPreset("es", "en-US", "UTC", false, false);
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle); UpdateLayout();
        Check(vm.IsReady && ReferenceEquals(selection, vm.ValidatedSelection) && vm.AccountColumn == 3 && vm.AmountColumn == 1,
            "language and region preserve validated mapping");
        Check(AccountColumnBox.SelectedValue is 3 && AmountColumnBox.SelectedValue is 1, "visible selections survive reformat");
        Check(PreviewGrid.Columns[1].Header?.ToString() == vm["AccountColumn"], "localized preview headers");
        VisualEvidenceCapture.Capture(this, Path.Combine(output, "csv-es-enUS.png"));
        vm.Delimiter = ",";
        Check(!vm.HasColumns && !vm.IsReady && vm.AccountColumn is null, "dialect change invalidates source and mapping");
        vm.Delimiter = ";";
        var xlsx = Path.Combine(output, "manual-columns.xlsx");
        using (var doc = SpreadsheetDocument.Create(xlsx, SpreadsheetDocumentType.Workbook))
        {
            var book = doc.AddWorkbookPart(); book.Workbook = new Workbook(new Sheets());
            foreach (var name in new[] { "Other", "Selected" })
            {
                static Cell TextCell(string reference, string value) => new() { CellReference = reference,
                    DataType = CellValues.InlineString, InlineString = new InlineString(new Text(value)) };
                var part = book.AddNewPart<WorksheetPart>();
                part.Worksheet = new Worksheet(new SheetData(
                    new Row(TextCell("A1", "Amount"), TextCell("B1", "Unused"), TextCell("C1", "Account")) { RowIndex = 1U },
                    new Row(new Cell { CellReference = "A2", DataType = CellValues.Number, CellValue = new CellValue(name == "Selected" ? "1250.5" : "99999") },
                        TextCell("B2", "unused"), TextCell("C2", "00123")) { RowIndex = 2U }));
                book.Workbook.GetFirstChild<Sheets>()!.Append(new Sheet { Name = name, SheetId = (uint)(name == "Other" ? 1 : 2), Id = book.GetIdOfPart(part) });
            }
        }
        files.Paths.Enqueue(xlsx); await Click(OpenSourceButton);
        Check(!ReadSourceButton.IsEnabled && vm.Worksheets.Count == 2, "Excel requires explicit sheet selection");
        WorksheetBox.SetCurrentValue(ComboBox.SelectedItemProperty, "Selected");
        WorksheetBox.GetBindingExpression(ComboBox.SelectedItemProperty)!.UpdateSource();
        await Click(ReadSourceButton); Select(AccountColumnBox, 3); Select(AmountColumnBox, 1); await Click(ValidateMappingButton);
        Check(vm.IsReady && vm.ValidatedSelection!.Source.SheetName == "Selected" && vm.ValidatedSelection.Rows[0].Amount == 1250.5m,
            "Excel second sheet and stored amount validated by controls");
        main.ApplyPresentationPreset("en", "es-ES", "Europe/Madrid", true, true);
        FlowDirection = FlowDirection.RightToLeft;
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle); UpdateLayout();
        Check(vm.IsReady && vm.AccountColumn == 3 && vm.AmountColumn == 1, "pseudo and RTL preserve mapping");
        VisualEvidenceCapture.Capture(this, Path.Combine(output, "xlsx-pseudo.png"));
        Check(Hash(csv) == sourceHash && Hash(companyPath) == companyHash, "source and company bytes unchanged");
        files.Paths.Enqueue(Path.Combine(output, "missing.csv")); await Click(OpenSourceButton);
        Check(!vm.IsReady && !vm.HasColumns && !vm.HasFile, "failed replacement clears stale ready state");
        main.ApplyPresentationPreset("en", "es-ES", "Europe/Madrid", false, false);
        FlowDirection = FlowDirection.LeftToRight;
        File.WriteAllText(Path.Combine(output, "interaction.json"), JsonSerializer.Serialize(new { Status = "passed", PassedChecks = passed }));
    }
}
