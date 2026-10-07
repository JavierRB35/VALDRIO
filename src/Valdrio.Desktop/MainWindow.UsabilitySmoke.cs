using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using Valdrio.Domain;

namespace Valdrio.Desktop;

public partial class MainWindow
{
    internal async Task RunGuidedWorkflowChecks(string directory, QueuedCompanyFileDialogs files, QueuedImportSourceDialogs sources)
    {
        var main = viewModel!; var company = main.Company!; var models = main.Models!; var studies = main.Studies!;
        var output = Path.Combine(directory, "guided-workflow"); Directory.CreateDirectory(output);
        var checks = new List<string>();
        var audit = new List<string>();
        void Audit(bool value, string label) { if (!value) throw new InvalidOperationException("Usability audit: " + label); audit.Add(label); }
        void Check(bool value, string label) { if (!value) throw new InvalidOperationException("Guided workflow: " + label); checks.Add(label); }
        void Text(TextBox box, string value)
        {
            GuidedUi.Reveal(box); box.SetCurrentValue(TextBox.TextProperty, value);
            box.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
        }
        void Select(ComboBox box, object value)
        {
            GuidedUi.Reveal(box); box.SetCurrentValue(ComboBox.SelectedItemProperty, value);
            box.GetBindingExpression(ComboBox.SelectedItemProperty)!.UpdateSource();
        }
        async Task Click(Button button)
        {
            GuidedUi.Reveal(button);
            ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            if (button.Command is IAsyncRelayCommand cmd && cmd.ExecutionTask is { } task) await task;
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        }
        main.ApplyPresentationPreset("es", "es-ES", "Europe/Madrid", false, false);
        await company.CloseCommand.ExecuteAsync(null);
        await NavigateWorkflow(3);
        Check(workflowStep == 0 && WorkflowRequirement.Text == main.Guide("NeedCompany"), "import navigation explains the missing company before source selection");
        var path = Path.Combine(output, "guided.valdrio");
        Text(CompanyPanel.NameBox, "Prueba sintética de recorrido guiado"); files.Paths.Enqueue(path); await AdvanceWorkflow();
        Audit(workflowStep == 1, "primary action creates the company and advances to structure");
        Check(CompanyPanel.Visibility == Visibility.Collapsed && ModelPanel.Visibility == Visibility.Visible, "only the selected workflow section is presented");
        Text(ModelPanel.DescriptionBox, "Estructura de prueba"); await AdvanceWorkflow();
        Audit(models.Current is not null && ModelPanel.ModelSteps.SelectedIndex == 1, "primary action creates the draft and advances to masters");
        await AdvanceWorkflow();
        Audit(ModelPanel.ModelSteps.SelectedIndex == 1 && WorkflowRequirement.Text == models.Guide("NeedCenter"), "missing centre keeps its form visible with a remedy");
        Text(ModelPanel.CenterCodeBox, "CENTRO"); Text(ModelPanel.CenterNameBox, "Centro de prueba"); await Click(ModelPanel.CreateCenterButton);
        await AdvanceWorkflow();
        Audit(ModelPanel.ModelSteps.SelectedIndex == 1 && WorkflowRequirement.Text == models.Guide("NeedUnit"), "missing unit keeps masters visible with a remedy");
        Text(CompanyPanel.UnitCodeBox, ""); await Click(CompanyPanel.AddUnitButton);
        Audit(CompanyPanel.UnitActionStatus.IsVisible && CompanyPanel.UnitActionStatus.Text == company.Status && models.Units.Count == 0,
            "invalid unit reports its error in the visible masters screen");
        Text(CompanyPanel.UnitCodeBox, "HORAS"); Text(CompanyPanel.DimensionBox, "time"); Text(CompanyPanel.SymbolBox, "h");
        Select(CompanyPanel.ScaleBox, 2); CompanyPanel.DiscreteBox.IsChecked = false; await Click(CompanyPanel.AddUnitButton);
        Check(GuidedUi.Contains(ModelPanel, CompanyPanel.UnitCreationPanel) && models.Units.Count == 1, "units are created beside centres without leaving structure setup");
        await AdvanceWorkflow();
        Audit(workflowStep == 1 && ModelPanel.ModelSteps.SelectedIndex == 2, "single advance enters nodes after masters are ready");
        await GoBackWorkflow();
        Audit(workflowStep == 1 && ModelPanel.ModelSteps.SelectedIndex == 1, "global back returns to the previous model step");
        await AdvanceWorkflow();
        Text(ModelPanel.NodeCodeBox, "AUX"); Select(ModelPanel.PhysicalBox, models.Centers.Single());
        Select(ModelPanel.UnitBox, models.Units.Single()); Select(ModelPanel.RoleBox, models.Roles.Single(r => r.Value == NodeRole.Auxiliary));
        await AdvanceWorkflow();
        Audit(ModelPanel.ModelSteps.SelectedIndex == 2 && models.Nodes.Count == 1 && WorkflowRequirement.Text == models.Guide("NeedPrincipal"),
            "auxiliary-only structure remains at nodes and explains the principal requirement");
        Text(ModelPanel.NodeCodeBox, "NODO"); Select(ModelPanel.PhysicalBox, models.Centers.Single());
        Select(ModelPanel.RoleBox, models.Roles.Single(r => r.Value == NodeRole.Principal));
        Check(models.NextRequirement == models.Guide("NeedNodeUnit"), "missing node unit has a specific actionable explanation");
        await ModelPanel.PublishForStudy();
        Check(!models.IsPublished && models.NodeCode == "NODO", "incomplete node cannot publish and its form is retained");
        Select(ModelPanel.UnitBox, models.Units.Single()); await Click(ModelPanel.ApplyNodeButton);
        ModelPanel.ModelSteps.SelectedIndex = 3;
        models.CenterName = "Centro revisado";
        await AdvanceWorkflow();
        Audit(ModelPanel.ModelSteps.SelectedIndex == 1 && models.HasPendingCenter && !models.IsPublished &&
            WorkflowNext.Content.ToString() == models.Guide("SaveCenterNext"), "pending centre is revealed before offering its explicit save action");
        await AdvanceWorkflow();
        Audit(!models.HasPendingCenter && models.Master!.Value.Name == "Centro revisado" && ModelPanel.ModelSteps.SelectedIndex == 2,
            "centre save persists the edit and resumes the model steps");
        await AdvanceWorkflow();
        Audit(ModelPanel.ModelSteps.SelectedIndex == 3 && WorkflowNext.Content.ToString() == models.Guide("PublishContinue"), "publication has one named primary action");
        VisualEvidenceCapture.Capture(this, Path.Combine(output, "structure-es.png"));
        await AdvanceWorkflow();
        Audit(workflowStep == 2, "publication automatically opens the study step");
        Check(models.IsPublished, "one explicit action saves validates and publishes the structure");
        await NavigateWorkflow(2);
        Check(studies.PublishedModels.Count == 1, "published models refresh when entering study step");
        Text(StudyPanel.StudyNameBox, "Saldos octubre 2026"); Select(StudyPanel.StudyModelBox, studies.PublishedModels.Single());
        Select(StudyPanel.StudyCurrencyBox, "EUR"); studies.PeriodStart = new(2026, 1, 1); studies.PeriodEnd = new(2026, 10, 31);
        await NavigateWorkflow(1); await NavigateWorkflow(2);
        Check(studies.Name == "Saldos octubre 2026" && studies.PeriodEnd == new DateTime(2026, 10, 31), "backward navigation preserves the unsaved study form");
        await NavigateWorkflow(3);
        Check(workflowStep == 2 && !studies.HasImportDestination, "import step redirects to unsaved study instead of opening a dead end");
        await AdvanceWorkflow();
        Check(studies.HasImportDestination && workflowStep == 3, "study is saved before guided import becomes available");
        await NavigateWorkflow(3); UpdateLayout();
        Audit(FieldHelpText.Text == main.Guide("HelpHint"), "changing workspace steps clears help from fields that are no longer visible");
        VisualEvidenceCapture.Capture(this, Path.Combine(output, "destination-es.png"));
        Check(workflowStep == 3 && SaveLocation.Text.Contains(path, StringComparison.Ordinal), "import destination and storage path are visible");
        var csv = Path.Combine(output, "source.csv"); File.WriteAllText(csv, "Cuenta;Descripcion;Saldo\n6005;primera;280\n6005;otra;78,35\n7000;excluir;-10");
        await studies.AttachImportTarget();
        // Use a fresh wizard view; the shared source state must be explicitly replaced.
        var wizard = new ImportColumnMappingWindow { Owner = this, DataContext = studies.ImportColumns }; wizard.Show();
        var vm = studies.ImportColumns!; sources.Paths.Enqueue(csv); await vm.OpenCommand.ExecuteAsync(null);
        await wizard.AdvanceImport();
        Check(wizard.ImportSteps.SelectedIndex == 1, "read and continue advances to column mapping");
        vm.International = false; vm.CurrencyFromColumn = false; vm.AmountLayout = "amount"; vm.AccountColumn = 1; vm.AmountColumn = 3; vm.DecimalSeparator = ","; vm.GroupSeparator = "";
        await wizard.AdvanceImport();
        Check(wizard.ImportSteps.SelectedIndex == 2, "validated columns advance to record selection");
        vm.DeselectAllRecordsCommand.Execute(null); await wizard.AdvanceImport();
        Check(wizard.ImportSteps.SelectedIndex == 2 && wizard.ImportStepHelp.Text == vm.Guide("NeedRecords"), "empty selection stays in records and explains how to proceed");
        vm.SelectAllRecordsCommand.Execute(null); vm.AccountChoices.Single(r => r.Account == "7000").Included = false;
        var selectedRows = vm.AccountChoices.ToArray();
        selectedRows[0].RateId = "manual-rate-reference";
        wizard.ImportSteps.SelectedIndex = 0;
        await wizard.AdvanceImport(); await wizard.AdvanceImport();
        Audit(wizard.ImportSteps.SelectedIndex == 2 && vm.AccountChoices.SequenceEqual(selectedRows) &&
            vm.SelectionTotals?.SelectedRows == 2 && selectedRows[0].RateId == "manual-rate-reference",
            "back through file and columns preserves record choices and manual rate assignments");
        await vm.ValidateCommand.ExecuteAsync(null);
        Audit(vm.AccountChoices.SequenceEqual(selectedRows) && vm.SelectionTotals?.SelectedRows == 2,
            "explicit validation of unchanged columns preserves the selection");
        selectedRows[0].RateId = "";
        await wizard.AdvanceImport();
        Check(wizard.ImportSteps.SelectedIndex == 3 && vm.SelectionGroups.Single().Amount == 358.35m, "all except one retains grouping by code independent of descriptions");
        vm.SourceCurrency = "EUR"; vm.AmountMode = "closing-full";
        vm.AcknowledgeAmountMeaning(); vm.ApprovalReference = "6005 = 358,35 EUR"; vm.InterpretationApproved = true;
        wizard.Activate(); wizard.UpdateLayout(); wizard.SourceCurrencyBox.BringIntoView();
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        System.Windows.Input.Keyboard.Focus(wizard.SourceCurrencyBox);
        Check(!string.IsNullOrEmpty(AutomationProperties.GetHelpText(wizard.SourceCurrencyBox)) &&
            wizard.ImportFieldHelp.Text == vm.Guide("Help." + nameof(vm.SourceCurrency)), "field explanation is available to keyboard and assistive technology");
        var busyLocked = false;
        void ObserveBusy(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (!vm.IsBusy) return;
            wizard.ImportSteps.GetBindingExpression(IsEnabledProperty)!.UpdateTarget();
            busyLocked |= !wizard.ImportSteps.IsEnabled && !wizard.SourceCurrencyBox.IsEnabled;
        }
        vm.PropertyChanged += ObserveBusy;
        await wizard.AdvanceImport();
        vm.PropertyChanged -= ObserveBusy;
        Audit(busyLocked && wizard.ImportSteps.IsEnabled, "source and meaning fields are locked during preparation and enabled afterwards");
        Check(wizard.ImportSteps.SelectedIndex == 4 && vm.Preparation?.CanConfirm == true && vm.ConfirmedImport is null, "review shows prepared totals without writing the import");
        var reviewed = vm.Preparation;
        wizard.ImportSteps.SelectedIndex = 0; await wizard.AdvanceImport(); await wizard.AdvanceImport();
        Audit(ReferenceEquals(reviewed, vm.Preparation) && vm.InterpretationApproved && vm.SelectionTotals?.SelectedRows == 2,
            "back and forward leaves unchanged approval and prepared totals intact");
        wizard.ImportSteps.SelectedIndex = 4;
        Audit(wizard.ImportFieldHelp.Text == vm.Guide("HelpHint"), "changing import steps clears help from the previous form");
        VisualEvidenceCapture.Capture(wizard, Path.Combine(output, "review-es.png"));
        main.ApplyPresentationPreset("en", "en-US", "UTC", true, true); wizard.FlowDirection = FlowDirection.RightToLeft;
        VisualEvidenceCapture.Capture(wizard, Path.Combine(output, "review-pseudo-rtl.png"));
        main.ApplyPresentationPreset("es", "es-ES", "Europe/Madrid", false, false); wizard.FlowDirection = FlowDirection.LeftToRight;
        await wizard.AdvanceImport();
        Check(vm.ConfirmedImport?.Resources.Single().Functional.Amount == 358.35m && wizard.ImportStepHelp.Text == vm.Guide("SavedImport"), "save action persists and shows unambiguous saved confirmation");
        wizard.ImportSteps.SelectedIndex = 3;
        Audit(wizard.ImportSteps.SelectedIndex == 3, "saved import can be inspected by going back without a navigation trap");
        vm.ApprovalReference += " revisado";
        Audit(vm.ConfirmedImport is null && vm.Status == vm["ReviewChanged"] && !vm.ConfirmCommand.CanExecute(null),
            "editing a saved review removes stale saved status and requires preparation again");
        var studyId = studies.Current!.Id; wizard.Close();
        await company.CloseCommand.ExecuteAsync(null); await company.OpenPath(path);
        studies.StudyChoice = studies.Studies.Single(s => s.Value.Id == studyId); await studies.LoadCommand.ExecuteAsync(null);
        Check(company.ActiveImports!.ReadImports(company.Snapshot!.Id, studyId).Single().Resources.Single().Functional.Amount == 358.35m,
            "reopened company contains the saved selected import");
        File.WriteAllText(Path.Combine(output, "audit.json"), JsonSerializer.Serialize(new { Status = "passed", PassedChecks = audit }, new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(Path.Combine(output, "interaction.json"), JsonSerializer.Serialize(new { Status = "passed", PassedChecks = checks }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
