using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using Valdrio.Application;
using Valdrio.Domain;
using Valdrio.Infrastructure;
using Valdrio.Infrastructure.CompanyFiles;

namespace Valdrio.Desktop;

public partial class ImportColumnMappingWindow
{
    internal async Task RunImportGroupingChecks(string directory, QueuedImportSourceDialogs files, MainWindowViewModel main)
    {
        var vm = (ImportColumnMappingViewModel)DataContext;
        var company = main.Company!; var studies = main.Studies!; var panel = ((MainWindow)Owner).StudyPanel;
        var output = Path.Combine(directory, "import-grouping"); Directory.CreateDirectory(output);
        var checks = new List<string>();
        void Check(bool value, string label) { if (!value) throw new InvalidOperationException("Grouping WPF: " + label); checks.Add(label); }
        async Task Click(Button button)
        {
            GuidedUi.Reveal(button);
            if (!button.IsEnabled) throw new InvalidOperationException("Disabled grouping button " + button.Name);
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
            SetText(ApprovalReferenceBox, "Synthetic: 6005 = 280 + 78.35 = 358.35 EUR");
            if (ApproveInterpretationBox.IsChecked != true)
                ((IToggleProvider)new CheckBoxAutomationPeer(ApproveInterpretationBox).GetPattern(PatternInterface.Toggle)).Toggle();
            ApproveInterpretationBox.GetBindingExpression(CheckBox.IsCheckedProperty)!.UpdateSource();
        }
        async Task Toggle(int record)
        {
            var choice = vm.AccountChoices.Single(r => r.RecordNumber == record);
            RevealImportElement(AccountSelectionGrid); AccountSelectionGrid.ScrollIntoView(choice); AccountSelectionGrid.UpdateLayout();
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            static CheckBox? Find(DependencyObject parent)
            {
                if (parent is CheckBox check) return check;
                for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
                    if (Find(VisualTreeHelper.GetChild(parent, i)) is { } child) return child;
                return null;
            }
            var box = Find((DataGridRow)AccountSelectionGrid.ItemContainerGenerator.ContainerFromItem(choice))!;
            ((IToggleProvider)new CheckBoxAutomationPeer(box).GetPattern(PatternInterface.Toggle)).Toggle();
            box.GetBindingExpression(CheckBox.IsCheckedProperty)!.UpdateSource();
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle); UpdateLayout();
        }
        static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
        var service = new SqliteCompanyFileService(new JsonCurrencyCatalog(Path.Combine(AppContext.BaseDirectory, "reference/currency-catalog-example.json")), new SystemClock());
        var path = Path.Combine(output, "grouping.valdrio");
        using (var previous = service.Open(company.ActivePath!, CompanyFileOpenMode.ReadOnly)) previous.Backup(path);
        await company.OpenPath(path); main.ApplyPresentationPreset("es", "es-ES", "Europe/Madrid", false, false);
        studies.Name = "Synthetic row grouping"; studies.Currency = "EUR";
        studies.PeriodStart = new DateTime(2026, 10, 1); studies.PeriodEnd = new DateTime(2026, 10, 6);
        studies.ModelChoice = studies.PublishedModels.Single(); await Click(panel.CreateStudyButton);
        var study = studies.Current!; await studies.AttachImportTarget();
        var csv = Path.Combine(output, "grouping.csv"); File.WriteAllText(csv, "Code;Description;Amount\n6005;First description;280\n6005;Different description;78,35\nGASTO;Credit;-5");
        files.Paths.Enqueue(csv); await Click(OpenSourceButton); await vm.ReadCommand.ExecuteAsync(null);
        Select(AmountLayoutBox, "amount"); Select(AccountColumnBox, 1); Select(AmountColumnBox, 3);
        vm.DecimalSeparator = ","; vm.GroupSeparator = ""; vm.ImportAllAccounts = true;
        await vm.ValidateCommand.ExecuteAsync(null);
        vm.BindTarget(null, null, false, null);
        Check(vm.IsReady && vm.AccountChoices.Count == 3 && vm.AccountChoices.All(r => r.Included) &&
            vm.SelectionGroups.Single(g => g.Code == "6005").Amount == 358.35m && vm.SelectionTotals?.Net == 353.35m &&
            !vm.PrepareCommand.CanExecute(null) && SelectionPreviewGrid.Items.Count == 2,
            "duplicate codes validate and live grouped totals work without a saved study target");
        await studies.AttachImportTarget();
        await Click(DeselectAllRecordsButton);
        Check(!vm.AccountChoices.Any(r => r.Included) && !vm.PrepareCommand.CanExecute(null) &&
            vm.SelectionTotals?.Net == 0m && vm.SelectionTotals.ExcludedNet == 353.35m && SelectionPreviewGrid.Items.Count == 0,
            "deselect all blocks empty import and reconciles all excluded amounts");
        await Toggle(3); SetText(SourceCurrencyBox, "EUR"); Select(AmountModeBox, "closing-full"); Approve(); await PrepareForSmoke();
        Check(vm.Preparation is { CanConfirm: true } && vm.Preparation.Groups!.Single().Amount.Amount == 78.35m,
            "only one of the duplicate records can be selected");
        await Click(SelectAllRecordsButton);
        Check(vm.AccountChoices.All(r => r.Included) && vm.Preparation is null && !vm.InterpretationApproved,
            "select all restores every row and revokes prior approval");
        await Toggle(2); Approve(); await PrepareForSmoke();
        Check(vm.Preparation!.Reconciliation.SelectedRows == 2 && vm.Preparation.Reconciliation.NetAmount == 73.35m &&
            vm.SelectionTotals?.Net == 73.35m && vm.SelectionTotals.ExcludedNet == 280m,
            "all except one imports only the remaining contributions");
        await Toggle(2); Approve(); await PrepareForSmoke();
        Check(vm.Preparation!.Groups!.Count == 2 && vm.Preparation.Groups.Single(g => g.Code == "6005").Amount.Amount == 358.35m &&
            vm.PreparedGroups.Single(g => g.Code == "6005").Amount == "358,35", "same code totals 358.35 despite differing source descriptions");
        RevealImportElement(SelectionPreviewGrid); SelectionPreviewGrid.BringIntoView(); await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render); UpdateLayout();
        VisualEvidenceCapture.Capture(this, Path.Combine(output, "records-es.png"));
        RevealImportElement(GroupedPreviewGrid); GroupedPreviewGrid.BringIntoView(); await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render); UpdateLayout();
        VisualEvidenceCapture.Capture(this, Path.Combine(output, "groups-es.png"));
        var fingerprint = vm.Preparation.Fingerprint;
        main.ApplyPresentationPreset("en", "en-US", "UTC", true, true); FlowDirection = FlowDirection.RightToLeft;
        Check(vm.Preparation.Fingerprint == fingerprint && vm.AccountChoices.All(r => r.Included), "language region and RTL preserve grouping and selection");
        RevealImportElement(GroupedPreviewGrid); GroupedPreviewGrid.BringIntoView(); await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render); UpdateLayout();
        VisualEvidenceCapture.Capture(this, Path.Combine(output, "groups-pseudo.png"));
        FlowDirection = FlowDirection.LeftToRight; main.ApplyPresentationPreset("es", "es-ES", "Europe/Madrid", false, false);
        await Click(ConfirmImportButton); var batch = vm.ConfirmedImport!;
        Check(batch.Resources.Count == 2 && batch.Resources.Single(r => r.Account == "6005").SourceRecords!.SequenceEqual(new[] { 2, 3 }),
            "confirmation persists both original links to the grouped resource");
        await Click(panel.CaptureStudyButton); var photo = studies.SnapshotChoice!.Value;
        Check(photo.PayloadSchema == ImportedStudySnapshotInput.GroupedSchema && photo.Input.Resources.Count == 2, "snapshot v3 retains grouped ledger");
        var backup = Path.Combine(output, "grouping.valdrio-backup"); var restored = Path.Combine(output, "restored.valdrio");
        using (var read = service.Open(path, CompanyFileOpenMode.ReadOnly)) read.Backup(backup);
        using (var read = service.Restore(backup, restored))
            Check(read.Imports.ReadImports(study.CompanyId, study.Id).Single().PreparedFingerprint == fingerprint,
                "backup and restored file preserve the grouped evidence");
        await Click(((MainWindow)Owner).CompanyPanel.CloseButton); await company.OpenPath(path);
        studies.StudyChoice = studies.Studies.Single(s => s.Value.Id == study.Id); await Click(panel.LoadStudyButton);
        Check(studies.Current?.Revision == 1 && studies.ImportedAmounts.Contains(batch.Prepared.Source.FileName, StringComparison.Ordinal), "grouped import survives UI close and reopen");
        var snapshot = Path.Combine(output, "snapshot.json"); File.WriteAllText(snapshot, CanonicalSnapshotJson.Serialize(photo));
        File.WriteAllText(Path.Combine(output, "interaction.json"), JsonSerializer.Serialize(new { Status = "passed", PassedChecks = checks }));
        File.WriteAllText(Path.Combine(output, "manifest.json"), JsonSerializer.Serialize(new { SchemaVersion = SqliteCompanyFileService.SchemaVersion,
            Files = new[] { path, backup, restored, csv, snapshot }.Select(p => new { Name = Path.GetFileName(p), Sha256 = Hash(p) }) }));
    }
}
