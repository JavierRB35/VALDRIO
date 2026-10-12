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
    internal async Task RunInternationalImportChecks(string directory, QueuedImportSourceDialogs files, MainWindowViewModel main)
    {
        var vm = (ImportColumnMappingViewModel)DataContext; var company = main.Company!; var studies = main.Studies!;
        var panel = ((MainWindow)Owner).StudyPanel;
        var output = Path.Combine(directory, "import-international"); Directory.CreateDirectory(output);
        var checks = new List<string>();
        void Check(bool ok, string label) { if (!ok) throw new InvalidOperationException("International WPF: " + label); checks.Add(label); }
        async Task Click(Button button)
        {
            GuidedUi.Reveal(button);
            if (!button.IsEnabled) throw new InvalidOperationException("Disabled international button: " + button.Name);
            ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            if (button.Command is IAsyncRelayCommand command && command.ExecutionTask is { } task) await task.WaitAsync(TimeSpan.FromSeconds(60));
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle); UpdateLayout();
        }
        var service = new SqliteCompanyFileService(new JsonCurrencyCatalog(Path.Combine(AppContext.BaseDirectory, "reference/currency-catalog-example.json")), new SystemClock());
        var path = Path.Combine(output, "international.valdrio");
        using (var previous = service.Open(company.ActivePath!, CompanyFileOpenMode.ReadOnly)) previous.Backup(path);
        await company.OpenPath(path);
        studies.Name = "Synthetic international import"; studies.Currency = "EUR";
        studies.PeriodStart = new(2026, 10, 1); studies.PeriodEnd = new(2026, 10, 31);
        studies.ModelChoice = studies.PublishedModels.Single(); await Click(panel.CreateStudyButton); await studies.AttachImportTarget();
        var study = studies.Current!; var csv = Path.Combine(output, "source.csv");
        File.WriteAllText(csv, "Code;Amount;Currency;Date\n00123;100;USD;01/10/2026\n00123;20;EUR;02/10/2026");
        files.Paths.Enqueue(csv); await Click(OpenSourceButton); await vm.ReadCommand.ExecuteAsync(null);
        vm.AccountColumn = 1; vm.AmountColumn = 2; vm.DecimalSeparator = "."; vm.GroupSeparator = ""; vm.ImportAllAccounts = true;
        await vm.ValidateCommand.ExecuteAsync(null); vm.ClearComparisonCommand.Execute(null); ComparisonPanel.IsExpanded = false; vm.AmountMode = "period";
        vm.International = true; vm.CurrencyFromColumn = true; vm.CurrencyColumn = 3; vm.DateColumn = 4; vm.DatePattern = "dd/MM/yyyy";
        vm.AllowedRateStart = new(2026, 10, 1); vm.AllowedRateEnd = new(2026, 10, 31); vm.RateSetName = "manual-example-v1";
        vm.ApprovalReference = "Synthetic 100 USD x 0.9 + 20 EUR = 110 EUR"; vm.InterpretationApproved = true;
        await PrepareForSmoke();
        Check(vm.Preparation is { CanConfirm: false } && !ConfirmImportButton.IsEnabled && vm.Preparation.Diagnostics.Any(d => d.Diagnostic.Code == "FX_RATE_MISSING"), "missing selected rate blocks confirmation");
        var rate = new ImportRateDraft { Id = "USD-EUR", Currency = "USD", Quote = "0.9", Date = "2026-10-01", Source = "Synthetic manual example" };
        vm.Rates.Add(rate); vm.AccountChoices[0].RateId = "USD-EUR"; vm.InterpretationApproved = true; await PrepareForSmoke();
        Check(vm.Preparation is { CanConfirm: true } && vm.Preparation.Reconciliation.NetAmount == 110m, "100 USD at 0.9 plus 20 EUR prepares 110 EUR");
        Check(vm.Preparation!.ConversionBridge!.OriginalTotals.Count == 2 && vm.SelectionTotals is null, "original currency totals remain separate");
        Check(vm.Preparation.ConversionBridge.Rows[0].DocumentDate?.ToString() == "2026-10-01", "source date pattern produces typed business date");
        RevealImportElement(ConversionPreviewPanel); ConversionPreviewPanel.BringIntoView(); await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        VisualEvidenceCapture.Capture(this, Path.Combine(output, "conversion-es.png"));
        var fingerprint = vm.Preparation.Fingerprint;
        main.ApplyPresentationPreset("en", "de-DE", "UTC", true, true); FlowDirection = FlowDirection.RightToLeft;
        Check(vm.Preparation.Fingerprint == fingerprint, "presentation and timezone leave prepared conversion unchanged");
        RevealImportElement(ConversionPreviewPanel); ConversionPreviewPanel.BringIntoView(); await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        VisualEvidenceCapture.Capture(this, Path.Combine(output, "conversion-pseudo.png"));
        FlowDirection = FlowDirection.LeftToRight; main.ApplyPresentationPreset("es", "es-ES", "Europe/Madrid", false, false);
        await Click(ConfirmImportButton); var first = vm.ConfirmedImport!; await Click(panel.CaptureStudyButton);
        var oldPhoto = studies.SnapshotChoice!.Value;
        Check(oldPhoto.PayloadSchema == ImportedStudySnapshotInput.InternationalSchema && oldPhoto.Input.AppliedFx.Count == 2, "snapshot v5 freezes conversions and identity");
        await studies.AttachImportTarget();
        // Compatibility-only regression: an explicit internal opt-in is required
        // again after attaching a source; normal end users have no FX controls.
        vm.International = true; vm.CurrencyFromColumn = true;
        vm.ReplaceBatch = true; vm.Replacement = vm.ExistingBatches.Single(b => b.Id == first.Id);
        rate.Quote = "0.95"; Check(vm.Preparation is null && !vm.InterpretationApproved, "rate edit clears preparation and approval");
        vm.ApprovalReference = "Synthetic replacement 100 USD x 0.95 + 20 EUR = 115 EUR";
        vm.InterpretationApproved = true; await PrepareForSmoke(); await Click(ConfirmImportButton); await Click(panel.CaptureStudyButton);
        var photo = studies.SnapshotChoice!.Value;
        Check(photo.Input.Resources.Sum(r => r.Functional.Amount) == 115m && oldPhoto.Input.Resources.Sum(r => r.Functional.Amount) == 110m,
            "replacement updates active total and preserves old snapshot");
        var snapshot = Path.Combine(output, "snapshot.json"); File.WriteAllText(snapshot, CanonicalSnapshotJson.Serialize(photo));
        var backup = Path.Combine(output, "international.valdrio-backup"); var restored = Path.Combine(output, "restored.valdrio");
        using (var read = service.Open(path, CompanyFileOpenMode.ReadOnly)) read.Backup(backup);
        using (var read = service.Restore(backup, restored))
            Check(read.Studies.ReadSnapshots(study.CompanyId, study.Id).Single(p => p.Id == photo.Id).FullSnapshotHash == photo.FullSnapshotHash,
                "backup restore preserves frozen conversion hash");
        await Click(((MainWindow)Owner).CompanyPanel.CloseButton); await company.OpenPath(path);
        studies.StudyChoice = studies.Studies.Single(s => s.Value.Id == study.Id); await Click(panel.LoadStudyButton);
        Check(studies.Current?.Revision == 2 && studies.ImportedAmounts.Contains(first.Prepared.Source.FileName, StringComparison.Ordinal) &&
            studies.ImportedAmounts.Contains("USD", StringComparison.Ordinal), "reopen retains conversion replacement history");
        File.WriteAllText(Path.Combine(output, "interaction.json"), JsonSerializer.Serialize(new { Status = "passed", PassedChecks = checks }));
        File.WriteAllText(Path.Combine(output, "manifest.json"), JsonSerializer.Serialize(new { SchemaVersion = 1,
            Files = new[] { path, backup, restored, snapshot, csv }.Select(p => new { Name = Path.GetFileName(p),
                Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))).ToLowerInvariant() }) }));
    }
}
