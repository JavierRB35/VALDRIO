using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using Valdrio.Application;
using Valdrio.Domain;
using Valdrio.Infrastructure;
using Valdrio.Infrastructure.CompanyFiles;

namespace Valdrio.Desktop;

public partial class ImportColumnMappingWindow
{
    internal async Task RunImportConfirmationChecks(string directory, QueuedImportSourceDialogs files,
        QueuedCompanyFileDialogs companyFiles, MainWindowViewModel main)
    {
        var vm = (ImportColumnMappingViewModel)DataContext; var company = main.Company!; var studies = main.Studies!;
        var window = (MainWindow)Owner; var panel = window.StudyPanel;
        var output = Path.Combine(directory, "import-confirmation"); Directory.CreateDirectory(output);
        var checks = new List<string>();
        void Check(bool value, string label) { if (!value) throw new InvalidOperationException("Import confirmation WPF: " + label); checks.Add(label); }
        async Task Click(Button button)
        {
            if (!button.IsEnabled) throw new InvalidOperationException("Disabled confirmation button " + button.Name);
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
            SetText(ApprovalReferenceBox, "Synthetic example 00123=1250.5; ZERO=0");
            if (ApproveInterpretationBox.IsChecked != true)
                ((IToggleProvider)new CheckBoxAutomationPeer(ApproveInterpretationBox).GetPattern(PatternInterface.Toggle)).Toggle();
            ApproveInterpretationBox.GetBindingExpression(CheckBox.IsCheckedProperty)!.UpdateSource();
        }
        static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
        var catalog = new JsonCurrencyCatalog(Path.Combine(AppContext.BaseDirectory, "reference", "currency-catalog-example.json"));
        var service = new SqliteCompanyFileService(catalog, new SystemClock());
        var previousPath = company.ActivePath!; var previousHash = Hash(previousPath);
        var path = Path.Combine(output, "import.valdrio");
        using (var read = service.Open(previousPath, CompanyFileOpenMode.ReadOnly)) read.Backup(path);
        await company.OpenPath(path);
        studies.Name = "CSV period import"; studies.Currency = "EUR";
        studies.PeriodStart = new DateTime(2026, 10, 1); studies.PeriodEnd = new DateTime(2026, 10, 31);
        studies.ModelChoice = studies.PublishedModels.Single(); await Click(panel.CreateStudyButton);
        var study = studies.Current ?? throw new InvalidOperationException(studies.Status);
        await Click(panel.CaptureStudyButton); var originalPhoto = studies.SnapshotChoice!.Value;
        await studies.AttachImportTarget();
        var csv = Path.Combine(output, "period-amounts.csv"); File.WriteAllText(csv, "Amount;Ignored;Account\n\"1.250,50\";x;00123\n0;y;ZERO"); var csvHash = Hash(csv);
        files.Paths.Enqueue(csv); await Click(OpenSourceButton); await Click(ReadSourceButton);
        Select(AccountColumnBox, 3); Select(AmountColumnBox, 1); vm.GroupSeparator = "."; await Click(ValidateMappingButton);
        Check(vm.IsReady && !ConfirmImportButton.IsEnabled, "column validation alone cannot confirm");
        SetText(SourceCurrencyBox, "EUR"); var companyHash = Hash(path); await Click(PrepareImportButton);
        Check(vm.Preparation is { CanConfirm: false } && !ConfirmImportButton.IsEnabled && Hash(path) == companyHash,
            "approval required and preparation preserves company bytes");
        SetText(SourceCurrencyBox, "USD"); Approve(); await Click(PrepareImportButton);
        Check(vm.Preparation!.Diagnostics.Any(d => d.Diagnostic.Code == "IMP_FX_REQUIRED") && !ConfirmImportButton.IsEnabled,
            "foreign currency blocked without FX bridge");
        SetText(SourceCurrencyBox, "EUR"); Approve(); await Click(PrepareImportButton);
        Check(vm.Preparation is { CanConfirm: true } prepared && prepared.Reconciliation.NetAmount == 1250.5m &&
            prepared.Reconciliation.ZeroRows == 1 && ConfirmImportButton.IsEnabled && ConfirmationSummaryText.Text == vm.ConfirmationSummary &&
            ConfirmationSummaryText.TranslatePoint(new Point(), this).Y < ConfirmImportButton.TranslatePoint(new Point(), this).Y,
            "approved period source reconciles before confirmation");
        var fingerprint = vm.Preparation!.Fingerprint;
        main.ApplyPresentationPreset("es", "en-US", "UTC", false, false);
        Check(vm.Preparation.Fingerprint == fingerprint && vm.InterpretationApproved && AccountColumnBox.SelectedValue is 3,
            "language and region preserve prepared fingerprint and visible selection");
        ConfirmImportButton.BringIntoView(); await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render); UpdateLayout();
        VisualEvidenceCapture.Capture(this, Path.Combine(output, "prepared-es.png"));
        await Click(ConfirmImportButton); var batch = vm.ConfirmedImport ?? throw new InvalidOperationException(vm.Status);
        Check(batch.Resources.Count == 2 && batch.Resources.Single(r => r.Account == "00123").Functional.Amount == 1250.5m,
            "native confirmation saves mapped resources");
        Check(studies.Current?.Revision == 1 && vm.ConfirmedImport.ConfirmedStudy.Revision == 1,
            "confirmed study revision applied before refresh");
        Check(!ConfirmImportButton.IsEnabled, "double click cannot duplicate confirmed batch");
        Check(Hash(csv) == csvHash && Hash(previousPath) == previousHash, "source bytes and previous study artifact remain unchanged");
        await Click(panel.CaptureStudyButton); var photo = studies.SnapshotChoice!.Value;
        Check(photo.PayloadSchema == ImportedStudySnapshotInput.Schema && photo.Input.Resources.Count == 2,
            "native snapshot includes actual imported resources");
        Check(studies.Snapshots.Single(s => s.Value.Id == originalPhoto.Id).Value.CanonicalJson == originalPhoto.CanonicalJson,
            "context-only snapshot remains identical");
        Check(panel.ImportHistoryText.Text.Contains(batch.Id.ToString(), StringComparison.Ordinal), "saved import history visible in study panel");
        Check(StudyPreparationService.Evaluate(photo.Input).Diagnostics.Any(d => d.Code == "STUDY_PARTITIONS_PENDING") &&
            !StudyPreparationService.Evaluate(photo.Input).ReadyForCalculation, "import does not fabricate a completed cost calculation");
        using (var read = service.Open(path, CompanyFileOpenMode.ReadOnly))
            Check(read.Imports.ReadImports(study.CompanyId, study.Id).Single().PreparedFingerprint == fingerprint,
                "independent reader verifies confirmed source evidence");
        main.ApplyPresentationPreset("en", "es-ES", "Europe/Madrid", true, true); FlowDirection = FlowDirection.RightToLeft;
        ConfirmImportButton.BringIntoView(); await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render); UpdateLayout();
        VisualEvidenceCapture.Capture(this, Path.Combine(output, "confirmed-pseudo.png"));
        FlowDirection = FlowDirection.LeftToRight; main.ApplyPresentationPreset("en", "es-ES", "Europe/Madrid", false, false);
        await Click(window.CompanyPanel.CloseButton); companyFiles.Paths.Enqueue(path); await Click(window.CompanyPanel.OpenButton);
        studies.StudyChoice = studies.Studies.Single(s => s.Value.Id == study.Id); await Click(panel.LoadStudyButton);
        Check(studies.ImportedAmounts.Contains(batch.Id.ToString(), StringComparison.Ordinal) && studies.Current?.Revision == 1,
            "close and reopen retain imported resources and revision");
        var backup = Path.Combine(output, "import.valdrio-backup"); var restored = Path.Combine(output, "restored.valdrio");
        companyFiles.Paths.Enqueue(backup); await Click(window.CompanyPanel.BackupButton);
        companyFiles.Paths.Enqueue(backup); companyFiles.Paths.Enqueue(restored); await Click(window.CompanyPanel.RestoreButton);
        studies.StudyChoice = studies.Studies.Single(s => s.Value.Id == study.Id); await Click(panel.LoadStudyButton);
        Check(studies.ImportedAmounts.Contains(batch.Id.ToString(), StringComparison.Ordinal) && studies.Snapshots.Count == 2,
            "backup and restore retain imports and both snapshot schemas");
        studies.Currency = "USD"; await Click(panel.SaveStudyButton);
        Check(studies.IsDirty && studies.Status.StartsWith("IMP_CONTEXT_LOCKED", StringComparison.Ordinal), "functional currency cannot change under imported amounts");
        await Click(panel.DiscardStudyButton);
        files.Paths.Enqueue(Path.Combine(output, "missing.csv")); await Click(OpenSourceButton);
        Check(!vm.IsReady && studies.ImportedAmounts.Contains(batch.Id.ToString(), StringComparison.Ordinal), "failed next source preserves confirmed history");
        var snapshot = Path.Combine(output, "snapshot.json"); File.WriteAllText(snapshot, CanonicalSnapshotJson.Serialize(photo));
        File.WriteAllText(Path.Combine(output, "interaction.json"), JsonSerializer.Serialize(new { Status = "passed", PassedChecks = checks }));
        File.WriteAllText(Path.Combine(output, "manifest.json"), JsonSerializer.Serialize(new { SchemaVersion = 4, Source = "synthetic native WPF period import",
            StudyId = study.Id.ToString(), BatchId = batch.Id.ToString(), Files = new[] { path, backup, restored, snapshot, csv }
                .Select(p => new { Name = Path.GetFileName(p), Sha256 = Hash(p) }) }));
    }
}
