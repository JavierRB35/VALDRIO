using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
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

public partial class MainWindow
{
    internal async Task RunResourceChecks(string directory)
    {
        var output = Path.Combine(directory, "t07-resources"); Directory.CreateDirectory(output);
        var main = viewModel!; var company = main.Company!; var studies = main.Studies!; var vm = main.Resources!; var panel = ResourcePanel;
        var checks = new List<string>();
        void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException("T07 native: " + message + " · " + vm.Status); checks.Add(message); }
        TextBox? CorrectionEditor(System.Windows.DependencyObject node)
        {
            if (node is TextBox box) return box;
            for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(node); i++)
                if (CorrectionEditor(System.Windows.Media.VisualTreeHelper.GetChild(node, i)) is { } child) return child;
            return null;
        }
        void Text(TextBox box, string value)
        {
            box.SetCurrentValue(TextBox.TextProperty, value); box.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
        }
        void Select(ComboBox box, object value)
        {
            box.SetCurrentValue(ComboBox.SelectedValueProperty, value); box.GetBindingExpression(ComboBox.SelectedValueProperty)!.UpdateSource();
        }
        async Task Click(Button button)
        {
            GuidedUi.Reveal(button); Check(button.IsEnabled, "enabled action: " + button.Name);
            ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            if (button.Command is IAsyncRelayCommand command && command.ExecutionTask is { } task) await task;
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        }
        main.ApplyPresentationPreset("es", "es-ES", "Europe/Madrid", false, false);
        await NavigateWorkflow(2); await studies.NewCommand.ExecuteAsync(null);
        studies.Name = "T07 · C04 sintético"; studies.PeriodStart = new(2026, 10, 1); studies.PeriodEnd = new(2026, 10, 31);
        studies.ModelChoice = studies.PublishedModels.Single(); studies.Currency = "EUR"; await AdvanceWorkflow();
        var study = studies.Current!;
        var sourcePath = Path.Combine(output, "synthetic.csv"); File.WriteAllText(sourcePath, "Account;Amount\n6000;1000");
        using (var sourceSession = await new ImportSourceSessionFactory().OpenAsync(sourcePath))
        {
            var csv = new CsvSourceDialect("utf-8", ';', '"', CsvBomPolicy.Optional);
            var source = await sourceSession.ReadAsync(new(ImportSourceKind.Csv, null, 1, 2, csv));
            var profile = new SourceParseProfileVersion(SourceParseProfileVersion.CurrentSchema, new(Guid.NewGuid()), 1, "T07 synthetic C04",
                ImportSourceKind.Csv, null, 1, 2, new(1, amount: 2), csv, new(",", null, [], ImportNegativeSyntax.LeadingMinus), "yyyy-MM-dd",
                ImportAmountMode.PeriodAmount, ImportSignConvention.SignedAmount, study.PeriodStart, study.PeriodEnd, new CurrencyCode("EUR"), null,
                ImportDuplicatePolicy.Reject, ImportZeroPolicy.Keep, ImportAccountSelection.All,
                new(ImportProfileApprovalState.ApprovedBusiness, "Synthetic account 6000 = 1000 EUR"), true);
            var prepared = company.ActiveImports!.PrepareImport(study.CompanyId, study.Id, study.Revision, new(Guid.NewGuid()), source, profile);
            var batch = company.ActiveImports.ConfirmImport(new(study.CompanyId, study.Id, study.Revision, new(Guid.NewGuid()), prepared.Fingerprint,
                prepared, ImportConfirmMode.AppendBatch, "native-test"));
            await studies.ApplyResourceReceiptAsync(batch.ConfirmedStudy);
        }
        await NavigateWorkflow(3); await ContinueAfterGuidedImport(0);
        Check(workflowStep == 4 && vm.Stage == 0 && panel.IsVisible, "newly saved import opens account review automatically");
        Check(vm.Rows.Count == 1 && vm.Rows.Single().Code == "6000", "account appears with converted original and pending coverage");
        Check(panel.ResourceGrid.Columns.Where(c => c != panel.CorrectionsColumn).All(c => c.IsReadOnly), "only correction column allows editing");
        GuidedUi.Reveal(panel.ResourceGrid);
        Check(WorkflowNext.Visibility == Visibility.Visible && WorkflowNext.Content.ToString() == main.Guide("ContinueAllocations"), "correction table continues explicitly to T08, never into optional destinations");
        await AdvanceWorkflow();
        Check(workflowStep == 5 && !vm.ShowParts, "continue opens allocation keys without creating or opening a resource part");
        await NavigateWorkflow(4);
        panel.ResourceGrid.ScrollIntoView(vm.Rows.Single(), panel.CorrectionsColumn);
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        var cellContent = panel.CorrectionsColumn.GetCellContent(vm.Rows.Single());
        cellContent!.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left)
            { RoutedEvent = System.Windows.Input.Mouse.PreviewMouseDownEvent });
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        var correctionBox = CorrectionEditor(panel.CorrectionsColumn.GetCellContent(vm.Rows.Single())!);
        Check(correctionBox is not null, "one click opens keyboard-accessible correction editor");
        Check(correctionBox!.Text == "0" && correctionBox.SelectionLength == correctionBox.Text.Length,
            "one click selects the full zero value for replacement");
        correctionBox.SelectedText = "-100";
        Check(correctionBox.Text == "-100", "typing replaces the selected zero rather than appending");
        panel.ResourceGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        if (panel.CorrectionSaveTask is { } save) Check(await save, "leaving the cell saves correction automatically");
        Check(vm.Rows.Single().Effective == "900 EUR" && vm.Rows.Single().Pending == "900 EUR", "inline correction updates effective and pending in the table");
        Check(company.ActiveResources!.ReadResources(study.CompanyId, study.Id).Resources.Single().Adjustments.Single().Conversion.OriginalMoney.Amount == -100m,
            "inline correction is persisted in analysis currency before opening destinations");
        async Task EditCorrection(string value, bool cancel = false)
        {
            panel.ResourceGrid.CurrentCell = new DataGridCellInfo(vm.Rows.Single(), panel.CorrectionsColumn);
            Check(panel.ResourceGrid.BeginEdit(), "correction edit starts: " + value);
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            CorrectionEditor(panel.CorrectionsColumn.GetCellContent(vm.Rows.Single())!)!.Text = value;
            if (cancel) panel.ResourceGrid.CancelEdit(); else panel.ResourceGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            if (!cancel && panel.CorrectionSaveTask is { } pending) await pending;
        }
        var correctionRevision = studies.Current!.Revision;
        await EditCorrection("-200", true);
        Check(studies.Current.Revision == correctionRevision && vm.Rows.Single().Adjustments == "-100 EUR", "cancelled cell edit leaves saved correction untouched");
        await EditCorrection("invalid");
        Check(studies.Current.Revision == correctionRevision && vm.Rows.Single().Adjustments == "-100 EUR" && vm.CorrectionLockMessage == vm["CorrectionNotSaved"],
            "invalid cell value leaves saved correction untouched and explains failure");
        await EditCorrection("-100");
        Check(studies.Current.Revision == correctionRevision && vm.Status == vm["CorrectionUnchanged"], "repeating saved cell value creates no duplicate adjustment");
        ResetScrollForCapture(); VisualEvidenceCapture.Capture(this, Path.Combine(output, "inline-corrections-es.png"));
        await Click(panel.OpenSelectedResourceButton);
        Check(vm.Stage == 2 && panel.ResourceGrid.IsVisible, "destinations open below the same account table without adjustment screen");
        Check(panel.BackToCorrectionsButton.IsVisible, "destinations provide explicit return to Corrections");
        Check(WorkflowBack.IsVisible && (string)WorkflowBack.Content == vm["BackToCorrectionsButton"],
            "fixed navigation always names return to Corrections while editing destinations");
        await Click(panel.BackToCorrectionsButton);
        Check(vm.Stage == 0 && vm.CanEditCorrections, "return closes destinations and restores editable corrections");
        await Click(panel.OpenSelectedResourceButton);
        Check(vm.Parts.Count == 1 && vm.Parts.Single().Value.Routing == ResourceRoutingKind.Pending,
            "opening optional destinations shows initial pending part without adding a new cost");
        Check(vm.Pools.All(p => p.Label.Contains("Centro") && p.Label.Contains("Nodo") && p.Label.Contains("Unidad de actividad")),
            "pool popup choices identify centre node role and activity unit");
        vm.PartChoice = vm.Parts.Single(); await Click(panel.LoadPartButton);
        Text(panel.PartForm_AmountBox, "300"); Select(panel.PartForm_RouteBox, ResourceRoutingKind.DirectObject);
        Text(panel.ObjectForm_CodeBox, "PROD"); Text(panel.ObjectForm_NameBox, "Producto de prueba"); Select(panel.ObjectKindBox, ResourceObjectKind.Product);
        await Click(panel.PutObjectButton);
        Check(vm.PartForm["Amount"] == "300" && vm.Stage == 2, "saving a direct cost object preserves the unfinished part form");
        Select(panel.PartForm_ObjectIdBox, vm.Objects.Single().Value);
        Select(panel.PartForm_BehaviorBox, ResourceBehavior.Fixed); Select(panel.PartForm_UnitIdBox, vm.Units.Single().Value);
        Text(panel.PartForm_MinimumBox, "0"); Text(panel.PartForm_MaximumBox, "100"); Select(panel.PartForm_LayerBox, ResourceCostLayer.Production);
        await Click(panel.ApplyPartButton);
        Text(panel.PartForm_AmountBox, "500"); Select(panel.PartForm_RouteBox, ResourceRoutingKind.AllocationPool); Select(panel.PartForm_PoolIdBox, vm.Pools.First().Value);
        GuidedUi.Reveal(panel.PartForm_PoolIdBox);
        panel.PartForm_PoolIdBox.IsDropDownOpen = true;
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        var poolPopup = panel.PartForm_PoolIdBox.Template.FindName("PART_Popup", panel.PartForm_PoolIdBox) as System.Windows.Controls.Primitives.Popup;
        Check(poolPopup is { IsOpen: true, Child: FrameworkElement }, "pool choices render in the actual dropdown");
        VisualEvidenceCapture.Capture((FrameworkElement)poolPopup!.Child, Path.Combine(output, "pool-choices-es.png"));
        panel.PartForm_PoolIdBox.IsDropDownOpen = false;
        Select(panel.PartForm_BehaviorBox, ResourceBehavior.Variable); Select(panel.PartForm_UnitIdBox, vm.Units.Single().Value);
        Text(panel.PartForm_MinimumBox, "0"); Text(panel.PartForm_MaximumBox, "100"); Select(panel.PartForm_LayerBox, ResourceCostLayer.Production);
        Select(panel.PartForm_CapacityBox, ResourceCapacityTreatment.AssessAtPool); Text(panel.PartForm_NormalBox, "100"); Text(panel.PartForm_ActualBox, "90");
        await Click(panel.ApplyPartButton);
        Text(panel.PartForm_AmountBox, "100"); Select(panel.PartForm_RouteBox, ResourceRoutingKind.Excluded);
        await Click(panel.ApplyPartButton); Check(vm.Parts.Count == 2, "exclusion without a reason is blocked and form retained");
        Text(panel.PartForm_ExclusionBox, "Fuera del perímetro del estudio"); await Click(panel.ApplyPartButton);
        Check(vm.Parts.Count == 3 && vm.Parts.Sum(p => p.Value.Amount.Amount) == 900m, "C04 routes 300 direct plus 500 pool plus 100 excluded");
        Text(panel.PartForm_AmountBox, "100"); await Click(panel.ApplyPartButton);
        Text(panel.ReasonBox, "Revisión C04 y comprobación de conservación");
        var revision = studies.Current!.Revision; await AdvanceWorkflow();
        Check(vm.Stage == 2 && vm.IsDirty && studies.Current.Revision == revision, "extra 100 cannot be saved and draft stays editable");
        vm.PartChoice = vm.Parts.Single(p => p.Value.Routing == ResourceRoutingKind.Pending); await Click(panel.RemovePartButton);
        panel.ReasonBox.Focus(); await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        Check(!string.IsNullOrWhiteSpace(AutomationProperties.GetHelpText(panel.ReasonBox)), "resource field explanation is exposed to keyboard and assistive technology");
        ResetScrollForCapture(); VisualEvidenceCapture.Capture(this, Path.Combine(output, "review-es.png"));
        main.ApplyPresentationPreset("en", "es-ES", "UTC", true, true); ResetScrollForCapture();
        VisualEvidenceCapture.Capture(this, Path.Combine(output, "review-pseudo-rtl.png"));
        main.ApplyPresentationPreset("es", "es-ES", "Europe/Madrid", false, false);
        await AdvanceWorkflow(); Check(!vm.IsDirty && vm.Stage == 0 && vm.Status == vm["Saved"], "primary save persists account with explicit saved confirmation");
        var current = studies.Current!; var snapshot = company.ActiveStudies!.Capture(new(current.CompanyId, current.Id, current.Revision, new(Guid.NewGuid()), "native-test"));
        snapshot.EnsureValid();
        File.WriteAllText(Path.Combine(output, "snapshot-receipt.json"), CanonicalSnapshotJson.Serialize(snapshot));
        var hash = snapshot.EconomicInputHash;
        main.ApplyPresentationPreset("en", "en-US", "UTC", false, false); await vm.EnterAsync(true);
        Check(company.ActiveStudies.ReadSnapshots(current.CompanyId, current.Id).Single().EconomicInputHash == hash, "language region and time zone preserve saved economics");
        ResetScrollForCapture(); VisualEvidenceCapture.Capture(this, Path.Combine(output, "saved-en.png"));
        var path = company.ActivePath!;
        await company.CloseCommand.ExecuteAsync(null); await company.OpenPath(path);
        studies.StudyChoice = studies.Studies.Single(s => s.Value.Id == current.Id); await studies.LoadCommand.ExecuteAsync(null);
        await NavigateWorkflow(4);
        var read = company.ActiveResources!.ReadResources(current.CompanyId, current.Id);
        Check(read.Preparation.IsValid && !read.Preparation.HasPendingParts && read.Preparation.Coverage.Single().Effective.Amount == 900m,
            "close and reopen retain all routes adjustments classification and capacity observations");
        Check(read.Resources.Single().Parts.Single(p => p.Routing == ResourceRoutingKind.AllocationPool).Classification.CapacityOwner!.Actual!.Value.Value == 90m,
            "capacity owner and its typed observation are persisted");
        var receiptJson = File.ReadAllText(Path.Combine(output, "snapshot-receipt.json"));
        Check(company.ActiveStudies.ReadSnapshots(current.CompanyId, current.Id).Single().CanonicalJson == snapshot.CanonicalJson,
            "historical snapshot remains byte-identical after reopen");
        File.WriteAllText(Path.Combine(output, "interaction.json"), JsonSerializer.Serialize(new { Status = "passed", SchemaVersion = company.Snapshot!.SchemaVersion,
            PassedChecks = checks, snapshot.EconomicInputHash, snapshot.FullSnapshotHash,
            ReceiptSha256 = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(receiptJson))).ToLowerInvariant() }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
