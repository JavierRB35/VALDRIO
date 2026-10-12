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
using Valdrio.Infrastructure;
using Valdrio.Infrastructure.CompanyFiles;

namespace Valdrio.Desktop;

public partial class ImportColumnMappingWindow
{
    internal async Task RunImportCompletionChecks(string directory, QueuedImportSourceDialogs files, MainWindowViewModel main)
    {
        var vm = (ImportColumnMappingViewModel)DataContext; var company = main.Company!; var studies = main.Studies!;
        var panel = ((MainWindow)Owner).StudyPanel;
        var output = Path.Combine(directory, "import-completion"); Directory.CreateDirectory(output);
        var checks = new List<string>();
        void Check(bool value, string label) { if (!value) throw new InvalidOperationException("Completion WPF: " + label); checks.Add(label); }
        async Task Click(Button button)
        {
            GuidedUi.Reveal(button);
            if (!button.IsEnabled) throw new InvalidOperationException("Disabled completion button " + button.Name);
            ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            if (button.Command is IAsyncRelayCommand command && command.ExecutionTask is { } task) await task.WaitAsync(TimeSpan.FromSeconds(60));
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle); UpdateLayout();
        }
        void Text(TextBox box, string value) { box.SetCurrentValue(TextBox.TextProperty, value); box.GetBindingExpression(TextBox.TextProperty)!.UpdateSource(); }
        void Date(DatePicker box, DateTime value) { box.SetCurrentValue(DatePicker.SelectedDateProperty, value); box.GetBindingExpression(DatePicker.SelectedDateProperty)!.UpdateSource(); }
        void Select(ComboBox box, object value) { box.SetCurrentValue(ComboBox.SelectedValueProperty, value); box.GetBindingExpression(ComboBox.SelectedValueProperty)!.UpdateSource(); }
        void Approve()
        {
            Text(ApprovalReferenceBox, "Synthetic matched scope: 160 - 100 = 60; corrected 190 - 100 = 90");
            ApproveInterpretationBox.SetCurrentValue(CheckBox.IsCheckedProperty, true); ApproveInterpretationBox.GetBindingExpression(CheckBox.IsCheckedProperty)!.UpdateSource();
        }
        async Task Read(string file)
        {
            files.Paths.Enqueue(file); await Click(OpenSourceButton); await vm.ReadCommand.ExecuteAsync(null);
            Select(AmountLayoutBox, "amount"); Select(AccountColumnBox, 1); Select(AmountColumnBox, 3);
            vm.DecimalSeparator = ","; vm.GroupSeparator = ""; vm.ImportAllAccounts = true;
            await vm.ValidateCommand.ExecuteAsync(null);
        }
        var service = new SqliteCompanyFileService(new JsonCurrencyCatalog(Path.Combine(AppContext.BaseDirectory, "reference/currency-catalog-example.json")), new SystemClock());
        var path = Path.Combine(output, "completion.valdrio");
        using (var previous = service.Open(company.ActivePath!, CompanyFileOpenMode.ReadOnly)) previous.Backup(path);
        await company.OpenPath(path);
        studies.Name = "Synthetic cumulative replacement"; studies.Currency = "EUR";
        studies.PeriodStart = new(2026, 10, 1); studies.PeriodEnd = new(2026, 10, 31);
        studies.ModelChoice = studies.PublishedModels.Single(); await Click(panel.CreateStudyButton); await studies.AttachImportTarget();
        var study = studies.Current!;
        var earlier = Path.Combine(output, "earlier.csv"); var current = Path.Combine(output, "current.csv"); var corrected = Path.Combine(output, "corrected.csv");
        File.WriteAllText(earlier, "Code;Description;Amount\n00123;Earlier;100");
        File.WriteAllText(current, "Code;Description;Amount\n00123;Current;160");
        File.WriteAllText(corrected, "Code;Description;Amount\n00123;Corrected;190");
        await Read(earlier); ComparisonPanel.IsExpanded = true;
        Date(FiscalStartPicker, new(2026, 1, 1)); Date(FiscalEndPicker, new(2026, 12, 31)); Date(PreviousEndPicker, new(2026, 9, 30));
        Text(ComparisonScopeBox, "Same synthetic entity, selected expense accounts and accounting basis");
        Select(AmountModeBox, "closing-full"); Approve(); await Click(FreezeComparisonButton);
        Check(vm.ComparisonStatus.Contains("earlier.csv", StringComparison.Ordinal), "earlier source retained by UI with explicit fiscal dates and scope");
        await Read(current); Select(AmountModeBox, "cumulative"); Approve(); await PrepareForSmoke();
        Check(vm.Preparation is { CanConfirm: true } && vm.Preparation.Reconciliation.NetAmount == 60m &&
            vm.Preparation.Profile.Comparison!.Source.FileHash != vm.Preparation.Source.FileHash, "160 minus 100 prepares 60 and retains both sources");
        RevealImportElement(ComparisonGrid); ComparisonGrid.BringIntoView(); await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        VisualEvidenceCapture.Capture(this, Path.Combine(output, "comparison-es.png"));
        var fingerprint = vm.Preparation!.Fingerprint;
        main.ApplyPresentationPreset("en", "de-DE", "UTC", true, true); FlowDirection = FlowDirection.RightToLeft;
        Check(fingerprint == vm.Preparation.Fingerprint && vm.Preparation.Reconciliation.NetAmount == 60m, "pseudo RTL and region preserve comparison fingerprint");
        VisualEvidenceCapture.Capture(this, Path.Combine(output, "comparison-pseudo.png"));
        FlowDirection = FlowDirection.LeftToRight; main.ApplyPresentationPreset("es", "es-ES", "Europe/Madrid", false, false);
        await Click(ConfirmImportButton); var first = vm.ConfirmedImport!;
        await Click(panel.CaptureStudyButton); var firstPhoto = studies.SnapshotChoice!.Value;
        Check(first.Resources.Single().Functional.Amount == 60m && firstPhoto.PayloadSchema == ImportedStudySnapshotInput.LedgerSchema,
            "comparison confirmed and captured in snapshot v4");
        await studies.AttachImportTarget(); await Read(corrected);
        ReplaceBatchBox.SetCurrentValue(CheckBox.IsCheckedProperty, true); ReplaceBatchBox.GetBindingExpression(CheckBox.IsCheckedProperty)!.UpdateSource();
        ReplacementBox.SetCurrentValue(ComboBox.SelectedItemProperty, vm.ExistingBatches.Single(b => b.Id == first.Id));
        ReplacementBox.GetBindingExpression(ComboBox.SelectedItemProperty)!.UpdateSource();
        Approve(); await PrepareForSmoke();
        Check(vm.Preparation!.Reconciliation.NetAmount == 90m, "replacement prepares corrected difference 190 minus 100");
        await Click(ConfirmImportButton); var replacement = vm.ConfirmedImport!;
        await Click(panel.CaptureStudyButton); var photo = studies.SnapshotChoice!.Value;
        Check(replacement.ReplacesBatchId == first.Id && photo.Input.Resources.Single().Functional.Amount == 90m &&
            ((ImportedStudySnapshotInput)photo.Input).Imports.Count == 2, "replacement retains both batches and only current 90 contributes");
        Check(firstPhoto.Input.Resources.Single().Functional.Amount == 60m, "prior snapshot remains 60");
        var snapshot = Path.Combine(output, "snapshot.json"); File.WriteAllText(snapshot, CanonicalSnapshotJson.Serialize(photo));
        var backup = Path.Combine(output, "completion.valdrio-backup"); var restored = Path.Combine(output, "restored.valdrio");
        using (var read = service.Open(path, CompanyFileOpenMode.ReadOnly)) read.Backup(backup);
        using (var read = service.Restore(backup, restored))
            Check(read.Studies.ReadSnapshots(study.CompanyId, study.Id).Single(p => p.Id == photo.Id).CanonicalJson == photo.CanonicalJson,
                "backup and restoration preserve replacement and comparison history");
        await Click(((MainWindow)Owner).CompanyPanel.CloseButton); await company.OpenPath(path);
        studies.StudyChoice = studies.Studies.Single(s => s.Value.Id == study.Id); await Click(panel.LoadStudyButton);
        Check(studies.Current?.Revision == 2 && studies.ImportedAmounts.Contains(first.Prepared.Source.FileName, StringComparison.Ordinal) &&
            studies.ImportedAmounts.Contains(replacement.Prepared.Source.FileName, StringComparison.Ordinal), "reopen shows both historical and active batches");
        File.WriteAllText(Path.Combine(output, "interaction.json"), JsonSerializer.Serialize(new { Status = "passed", PassedChecks = checks }));
        File.WriteAllText(Path.Combine(output, "manifest.json"), JsonSerializer.Serialize(new { SchemaVersion = SqliteCompanyFileService.SchemaVersion,
            Files = new[] { path, backup, restored, snapshot, earlier, current, corrected }.Select(p => new { Name = Path.GetFileName(p),
                Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))).ToLowerInvariant() }) }));
    }
}
