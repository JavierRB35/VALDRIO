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
using Valdrio.Application;
using Valdrio.Infrastructure;
using Valdrio.Infrastructure.CompanyFiles;

namespace Valdrio.Desktop;

public partial class ImportColumnMappingWindow
{
    internal async Task RunDebitCreditChecks(string directory, QueuedImportSourceDialogs files,
        QueuedCompanyFileDialogs companyFiles, MainWindowViewModel main)
    {
        var vm = (ImportColumnMappingViewModel)DataContext; var company = main.Company!; var studies = main.Studies!;
        var window = (MainWindow)Owner; var panel = window.StudyPanel;
        var output = Path.Combine(directory, "import-debit-credit"); Directory.CreateDirectory(output);
        var checks = new List<string>();
        void Check(bool value, string label)
        { if (!value) throw new InvalidOperationException("Debit/credit WPF: " + label); checks.Add(label); }
        async Task Click(Button button)
        {
            GuidedUi.Reveal(button);
            if (!button.IsEnabled) throw new InvalidOperationException("Disabled debit/credit button " + button.Name);
            ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            if (button.Command is IAsyncRelayCommand command && command.ExecutionTask is { } task) await task.WaitAsync(TimeSpan.FromSeconds(60));
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle); UpdateLayout();
        }
        void Select(ComboBox box, object value)
        { box.SetCurrentValue(ComboBox.SelectedValueProperty, value); box.GetBindingExpression(ComboBox.SelectedValueProperty)!.UpdateSource(); }
        void SetText(TextBox box, string value)
        { box.SetCurrentValue(TextBox.TextProperty, value); box.GetBindingExpression(TextBox.TextProperty)!.UpdateSource(); }
        void Approve()
        {
            SetText(ApprovalReferenceBox, "Synthetic checked example:00123 debit180-credit20=160; CREDIT 0-7.5=-7.5; ZERO 0-0=0");
            if (ApproveInterpretationBox.IsChecked != true)
                ((IToggleProvider)new CheckBoxAutomationPeer(ApproveInterpretationBox).GetPattern(PatternInterface.Toggle)).Toggle();
            ApproveInterpretationBox.GetBindingExpression(CheckBox.IsCheckedProperty)!.UpdateSource();
        }
        async Task Capture(string name)
        {
            RevealImportElement(ConfirmImportButton); ConfirmImportButton.BringIntoView(); await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render); UpdateLayout();
            VisualEvidenceCapture.Capture(this, Path.Combine(output, name));
        }
        static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
        var service = new SqliteCompanyFileService(new JsonCurrencyCatalog(Path.Combine(AppContext.BaseDirectory, "reference/currency-catalog-example.json")), new SystemClock());
        var previousPath = company.ActivePath!; var previousHash = Hash(previousPath); var path = Path.Combine(output, "import.valdrio");
        using (var read = service.Open(previousPath, CompanyFileOpenMode.ReadOnly)) read.Backup(path);
        await company.OpenPath(path); main.ApplyPresentationPreset("es", "en-US", "UTC", false, false);
        studies.Name = "Debit credit period import"; studies.Currency = "EUR";
        studies.PeriodStart = new DateTime(2026, 10, 1); studies.PeriodEnd = new DateTime(2026, 10, 31);
        studies.ModelChoice = studies.PublishedModels.Single(); await Click(panel.CreateStudyButton);
        var study = studies.Current ?? throw new InvalidOperationException(studies.Status);
        await Click(panel.CaptureStudyButton); var originalPhoto = studies.SnapshotChoice!.Value; await studies.AttachImportTarget();
        var csv = Path.Combine(output, "debit-credit.csv");
        File.WriteAllText(csv, "Credit;Ignored;Account;Debit\n20;not money;00123;180\n7,50;x;CREDIT;0\n0;x;ZERO;0"); var csvHash = Hash(csv);
        files.Paths.Enqueue(csv); await Click(OpenSourceButton); await vm.ReadCommand.ExecuteAsync(null); Select(AmountLayoutBox, "debit-credit");
        Check(vm.IsDebitCredit && vm.DebitColumn is null && vm.CreditColumn is null && !vm.ValidateCommand.CanExecute(null),
            "debit and credit require explicit column selection");
        Check(PreviewGrid.Columns[2].Visibility == Visibility.Collapsed && PreviewGrid.Columns[3].Visibility == Visibility.Visible &&
            PreviewGrid.Columns[4].Visibility == Visibility.Visible, "both original preview columns visible");
        Select(AccountColumnBox, 3); Select(DebitColumnBox, 4); Select(CreditColumnBox, 1); vm.GroupSeparator = "";
        await vm.ValidateCommand.ExecuteAsync(null);
        Check(vm.IsReady && vm.ValidatedSelection!.Rows[0].Amount == 160m && vm.ValidatedSelection.Rows[1].Amount == -7.5m &&
            vm.ValidatedSelection.Rows[2].Amount == 0m && vm.Preview[0].OriginalDebit == "180" && vm.Preview[0].OriginalCredit == "20",
            "CSV arbitrary debit credit columns preserve originals and signed differences");
        Select(CreditColumnBox, 4); await vm.ValidateCommand.ExecuteAsync(null);
        Check(!vm.IsReady && vm.Status.StartsWith("IMP_COLUMN_SELECTION", StringComparison.Ordinal), "same debit and credit column blocked");
        Select(CreditColumnBox, 1); await vm.ValidateCommand.ExecuteAsync(null);
        var before = Hash(path); await PrepareForSmoke();
        Check(vm.Preparation is { CanConfirm: false } && !ConfirmImportButton.IsEnabled && Hash(path) == before,
            "debit credit approval required before company mutation");
        Check(vm.InterpretationPrompt.Contains("Debe menos Haber", StringComparison.Ordinal), "visible consent states debit minus credit");
        Approve(); await PrepareForSmoke();
        var prepared = vm.Preparation ?? throw new InvalidOperationException(vm.Status);
        Check(prepared.CanConfirm && prepared.Profile.SignConvention == ImportSignConvention.DebitMinusCredit &&
            prepared.Reconciliation.NetAmount == 152.5m && prepared.Reconciliation.PositiveAmount == 160m &&
            prepared.Reconciliation.NegativeAmount == -7.5m && prepared.Reconciliation.ZeroRows == 1 &&
            ConfirmationSummaryText.Text == vm.ConfirmationSummary &&
            ConfirmationSummaryText.TranslatePoint(new Point(), this).Y < ConfirmImportButton.TranslatePoint(new Point(), this).Y,
            "approved debit credit totals precede confirmation button");
        await Capture("csv-prepared-es.png"); var fingerprint = prepared.Fingerprint;
        main.ApplyPresentationPreset("en", "de-DE", "Europe/Madrid", true, true); FlowDirection = FlowDirection.RightToLeft;
        Check(vm.Preparation?.Fingerprint == fingerprint && vm.InterpretationApproved && DebitColumnBox.SelectedValue is 4 &&
            CreditColumnBox.SelectedValue is 1 && AmountLayoutBox.SelectedValue is "debit-credit",
            "language region pseudo and RTL preserve debit credit interpretation");
        await Click(ConfirmImportButton); var csvBatch = vm.ConfirmedImport ?? throw new InvalidOperationException(vm.Status);
        Check(csvBatch.Resources.Count == 3 && csvBatch.Resources.Single(r => r.Account == "00123").Functional.Amount == 160m &&
            csvBatch.Resources.Single(r => r.Account == "CREDIT").Functional.Amount == -7.5m && studies.Current?.Revision == 1,
            "native CSV confirmation saves positive credit and zero resources");
        Check(!ConfirmImportButton.IsEnabled, "debit credit double click cannot duplicate batch");
        Check(csvBatch.Prepared.Source.Rows[0].GetCell(4).RawLexical == "180" && csvBatch.Prepared.Source.Rows[0].GetCell(1).RawLexical == "20",
            "confirmed batch preserves separate debit and credit cells");
        Check(Hash(csv) == csvHash && Hash(previousPath) == previousHash, "CSV source and previous acceptance artifact remain unchanged");
        await Capture("csv-confirmed-pseudo.png");
        FlowDirection = FlowDirection.LeftToRight; main.ApplyPresentationPreset("es", "en-US", "UTC", false, false);
        await Click(panel.CaptureStudyButton);
        Check(studies.Snapshots.Single(p => p.Value.Id == originalPhoto.Id).Value.CanonicalJson == originalPhoto.CanonicalJson,
            "context snapshot remains unchanged after debit credit import");
        await studies.AttachImportTarget();
        var xlsx = Path.Combine(output, "debit-credit.xlsx");
        using (var document = SpreadsheetDocument.Create(xlsx, SpreadsheetDocumentType.Workbook))
        {
            var book = document.AddWorkbookPart(); book.Workbook = new Workbook(new Sheets());
            static Cell TextCell(string reference, string text) => new() { CellReference = reference, DataType = CellValues.InlineString, InlineString = new InlineString(new Text(text)) };
            foreach (var name in new[] { "Other", "Selected" })
            {
                var part = book.AddNewPart<WorksheetPart>();
                part.Worksheet = new Worksheet(new SheetData(
                    new Row(TextCell("A1", "Credit"), TextCell("B1", "Ignored"), TextCell("C1", "Account"), TextCell("D1", "Debit")) { RowIndex = 1U },
                    new Row(new Cell { CellReference = "A2", DataType = CellValues.Number, CellFormula = new CellFormula("10+10"), CellValue = new CellValue("20") },
                        TextCell("B2", "not money"), TextCell("C2", "00123"),
                        new Cell { CellReference = "D2", DataType = CellValues.Number, CellValue = new CellValue(name == "Selected" ? "180.00" : "999999") }) { RowIndex = 2U }));
                book.Workbook.GetFirstChild<Sheets>()!.Append(new Sheet { Name = name, SheetId = name == "Other" ? 1U : 2U, Id = book.GetIdOfPart(part) });
            }
        }
        var xlsxHash = Hash(xlsx); files.Paths.Enqueue(xlsx); await Click(OpenSourceButton);
        Check(!vm.ReadCommand.CanExecute(null) && vm.Worksheets.Count == 2, "Excel debit credit requires explicit worksheet");
        WorksheetBox.SetCurrentValue(ComboBox.SelectedItemProperty, "Selected"); WorksheetBox.GetBindingExpression(ComboBox.SelectedItemProperty)!.UpdateSource();
        await vm.ReadCommand.ExecuteAsync(null); Select(AccountColumnBox, 3); Select(DebitColumnBox, 4); Select(CreditColumnBox, 1); await vm.ValidateCommand.ExecuteAsync(null);
        Check(vm.IsReady && vm.ValidatedSelection!.Rows[0].Amount == 160m && vm.ValidatedSelection.Source.SheetName == "Selected" &&
            vm.ValidatedSelection.Source.Rows[0].GetCell(4).RawLexical == "180.00", "Excel selected worksheet uses exact stored debit and cached credit");
        Approve(); await PrepareForSmoke(); await Capture("xlsx-prepared-es.png");
        await Click(ConfirmImportButton); var xlsxBatch = vm.ConfirmedImport ?? throw new InvalidOperationException(vm.Status);
        Check(xlsxBatch.Resources.Count == 1 && xlsxBatch.Resources[0].Functional.Amount == 160m && studies.Current?.Revision == 2 && Hash(xlsx) == xlsxHash,
            "native Excel confirmation appends identified batch and revision");
        Check(panel.ImportHistoryText.Text.Contains(csvBatch.Prepared.Source.FileName, StringComparison.Ordinal) &&
            panel.ImportHistoryText.Text.Contains(xlsxBatch.Prepared.Source.FileName, StringComparison.Ordinal), "both debit credit batches visible in study history");
        await Click(panel.CaptureStudyButton); var photo = studies.SnapshotChoice!.Value;
        Check(photo.Input is ImportedStudySnapshotInput imported && imported.Resources.Count == 4 &&
            imported.Imports.SelectMany(b => b.Resources).Sum(r => r.Functional.Amount) == 312.5m,
            "native v2 snapshot freezes four actual resources from both sources");
        using (var read = service.Open(path, CompanyFileOpenMode.ReadOnly))
        {
            var saved = read.Imports.ReadImports(study.CompanyId, study.Id);
            Check(saved.Count == 2 && saved.Single(b => b.Id == csvBatch.Id).PreparedFingerprint == fingerprint &&
                saved.Single(b => b.Id == xlsxBatch.Id).Prepared.Source.Rows[0].GetCell(1).Formula == "10+10",
                "independent reader verifies both original source profiles and cached formula");
        }
        await Click(window.CompanyPanel.CloseButton); companyFiles.Paths.Enqueue(path); await Click(window.CompanyPanel.OpenButton);
        studies.StudyChoice = studies.Studies.Single(s => s.Value.Id == study.Id); await Click(panel.LoadStudyButton);
        Check(studies.Current?.Revision == 2 && studies.ImportedAmounts.Contains(xlsxBatch.Prepared.Source.FileName, StringComparison.Ordinal),
            "close and reopen retain debit credit batches and revision");
        var backup = Path.Combine(output, "import.valdrio-backup"); var restored = Path.Combine(output, "restored.valdrio");
        companyFiles.Paths.Enqueue(backup); await Click(window.CompanyPanel.BackupButton);
        companyFiles.Paths.Enqueue(backup); companyFiles.Paths.Enqueue(restored); await Click(window.CompanyPanel.RestoreButton);
        studies.StudyChoice = studies.Studies.Single(s => s.Value.Id == study.Id); await Click(panel.LoadStudyButton);
        Check(studies.Snapshots.Count == 3 && studies.ImportedAmounts.Contains(csvBatch.Prepared.Source.FileName, StringComparison.Ordinal) &&
            studies.ImportedAmounts.Contains(xlsxBatch.Prepared.Source.FileName, StringComparison.Ordinal), "backup restore retain both debit credit imports and snapshots");
        Select(AmountLayoutBox, "amount");
        Check(vm.Preparation is null && !vm.InterpretationApproved && vm.DebitColumn is null && vm.CreditColumn is null && vm.AmountColumn is null,
            "changing interpretation clears preparation columns and approval");
        var snapshot = Path.Combine(output, "snapshot.json"); File.WriteAllText(snapshot, CanonicalSnapshotJson.Serialize(photo));
        File.WriteAllText(Path.Combine(output, "interaction.json"), JsonSerializer.Serialize(new { Status = "passed", PassedChecks = checks }));
        File.WriteAllText(Path.Combine(output, "manifest.json"), JsonSerializer.Serialize(new { SchemaVersion = SqliteCompanyFileService.SchemaVersion,
            Source = "synthetic native WPF debit minus credit import", StudyId = study.Id.ToString(),
            BatchIds = new[] { csvBatch.Id.ToString(), xlsxBatch.Id.ToString() },
            Files = new[] { path, backup, restored, snapshot, csv, xlsx }.Select(p => new { Name = Path.GetFileName(p), Sha256 = Hash(p) }) }));
    }
}
