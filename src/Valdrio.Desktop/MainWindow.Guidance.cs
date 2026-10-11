using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace Valdrio.Desktop;

public partial class MainWindow
{
    private int workflowStep;
    private readonly List<INotifyPropertyChanged> workflowSources = [];
    private void InitializeGuidance()
    {
        ModelPanel.AttachUnitCreationPanel(CompanyPanel);
        Closed += (_, _) => { foreach (var source in workflowSources) source.PropertyChanged -= WorkflowChanged; };
    }
    private void BindGuidance()
    {
        foreach (var source in workflowSources) source.PropertyChanged -= WorkflowChanged;
        workflowSources.Clear();
        if (viewModel is null) return;
        foreach (var source in new INotifyPropertyChanged?[] { viewModel, viewModel.Company, viewModel.Models, viewModel.Studies, viewModel.Resources, viewModel.Allocations })
            if (source is not null) { workflowSources.Add(source); source.PropertyChanged += WorkflowChanged; }
        if (viewModel.Models is { } models && viewModel.Studies is { } studies) models.AttachStudyNodes(studies);
        UpdateWorkflow();
    }
    private void WorkflowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is ResourceWorkspaceViewModel or AllocationWorkspaceViewModel && !string.IsNullOrEmpty(e.PropertyName)
            && e.PropertyName is not ("Stage" or "IsBusy" or "CanAdvance" or "CanNavigate" or "Requirement" or "ShowParts")) return;
        UpdateWorkflow();
    }
    private string? lastHelpKey;
    private int lastResourceStage = -1;
    internal void ShowFieldHelp(string text, string? key = null) { lastHelpKey = key; FieldHelpText.Text = text; }
    internal void ClearFieldHelp() { lastHelpKey = null; UpdateWorkflow(); }
    internal void UpdateWorkflow()
    {
        if (viewModel is not { } vm) return;
        if (workflowStep == 4 && lastResourceStage != vm.Resources?.Stage) { lastHelpKey = null; lastResourceStage = vm.Resources?.Stage ?? -1; }
        NavigationGroupLabel.Text = vm.Guide("WorkspaceNavigation");
        var keys = new[] { "Company", "Model", "Study", "Import", "Resources", "Allocations", "Settings", "SecondaryAllocations" };
        Button[] buttons = [CompanyStep, ModelStep, StudyStep, ImportStep, ResourcesStep, AllocationsStep, SettingsStep, SecondaryStep];
        for (var i = 0; i < buttons.Length; i++)
        {
            buttons[i].Content = vm.Guide(keys[i]);
            buttons[i].FontWeight = i == workflowStep ? FontWeights.Bold : FontWeights.Normal;
        }

        var hasProject = vm.Company?.Snapshot is not null;
        var hasAnalysis = vm.Studies?.Current is not null;
        var hasImportDestination = vm.Studies?.HasImportDestination == true;
        var hasImports = vm.Studies?.HasConfirmedImports == true;

        CompanyStep.IsEnabled = true;
        ModelStep.IsEnabled = hasProject;
        StudyStep.IsEnabled = hasProject;
        ImportStep.IsEnabled = hasImportDestination;
        ResourcesStep.IsEnabled = hasImports;
        AllocationsStep.IsEnabled = hasImports;
        SecondaryStep.IsEnabled = hasImports && vm.Allocations?.IsPrimaryClosed == true;
        SecondaryStep.ToolTip = SecondaryStep.IsEnabled ? null : vm.Guide("NeedPrimaryClosedForSecondary");
        SettingsStep.IsEnabled = true;

        ModelStep.ToolTip = hasProject ? null : vm.Guide("NeedProjectShort");
        StudyStep.ToolTip = hasProject ? null : vm.Guide("NeedProjectShort");
        ImportStep.ToolTip = hasImportDestination ? null : vm.Guide("NeedAnalysisForImportShort");
        ResourcesStep.ToolTip = hasImports ? null : vm.Guide("NeedImportForResourcesShort");
        CompanyPanel.Visibility = workflowStep == 0 ? Visibility.Visible : Visibility.Collapsed;
        ModelPanel.Visibility = workflowStep == 1 ? Visibility.Visible : Visibility.Collapsed;
        StudyPanel.Visibility = workflowStep == 2 ? Visibility.Visible : Visibility.Collapsed;
        ResourcePanel.Visibility = workflowStep == 4 ? Visibility.Visible : Visibility.Collapsed;
        AllocationPanel.Visibility = workflowStep == 5 ? Visibility.Visible : Visibility.Collapsed;
        SecondaryPanel.Visibility = workflowStep == 7 ? Visibility.Visible : Visibility.Collapsed;
        DistributionDetails.Visibility = workflowStep == 5 ? Visibility.Visible : Visibility.Collapsed;
        AllocationPanel.IsEnabled = !DistributionDetails.IsExpanded;
        ImportHome.Visibility = workflowStep == 3 ? Visibility.Visible : Visibility.Collapsed;
        PreferencesPanel.Visibility = DevelopmentTools.Visibility = workflowStep == 6 ? Visibility.Visible : Visibility.Collapsed;
        WorkflowTitle.Text = vm.Guide(keys[workflowStep]);
        WorkflowRequirement.Text = workflowStep switch
        {
            0 => vm.Guide(vm.Company?.Snapshot is null ? "NeedCompany" : "CompanyReady"),
            1 => vm.Models?.NextRequirement ?? vm.Guide("NeedCompany"),
            2 or 3 => vm.Studies?.NextRequirement ?? vm.Guide("NeedCompany"),
            4 => vm.Resources?.Requirement ?? vm.Guide("NeedCompany"),
            7 => vm.Allocations?.SecondaryDiagnostic ?? vm.Guide("HelpHint"),
            5 => vm.Allocations?.Requirement ?? vm.Guide("NeedCompany"),
            _ => vm.Guide("HelpHint")
        };
        SaveLocation.Text = vm.Company?.ActivePath is { Length: > 0 } path ? vm.Guide("StoredAt") + path : "";

        var projectName = vm.Company?.Snapshot?.Name ?? "—";
        var structureName = workflowStep != 1 && vm.Studies?.Current is not null
            ? vm.Studies.CurrentModelDescription
            : vm.Models?.Current?.Structure.Description ?? "—";
        var analysisName = vm.Studies?.Current?.Name ?? "—";
        ProjectContextText.Text = hasProject
            ? string.Format(vm.Guide("Context"), projectName, structureName, analysisName)
            : "";

        ProjectTaskPanel.Visibility = workflowStep == 0 && hasProject ? Visibility.Visible : Visibility.Collapsed;
        ProjectTaskTitle.Text = vm.Guide("ProjectTasksTitle");
        ProjectTaskHelp.Text = vm.Guide("ProjectTasksHelp");
        TaskStructureButton.Content = vm.Guide("TaskStructure");
        TaskAnalysisButton.Content = vm.Guide("TaskAnalysis");
        TaskAnalysisButton.IsEnabled = vm.Studies?.HasPublishedModels == true;
        TaskAnalysisButton.ToolTip = TaskAnalysisButton.IsEnabled ? null : vm.Guide("NeedPublishedShort");
        TaskContinueButton.Visibility = hasAnalysis ? Visibility.Visible : Visibility.Collapsed;
        TaskContinueButton.Content = vm.Guide(hasImports ? "TaskContinueResources" :
            hasImportDestination ? "TaskContinueImport" : "TaskContinueAnalysis");
        WorkflowBack.Content = workflowStep == 4 && vm.Resources is { ShowParts: true } resourceEditor ? resourceEditor["BackToCorrectionsButton"] : vm.Guide("Back"); WorkflowBack.IsEnabled = workflowStep > 0 && vm.Company?.WorkspaceIdle != false;
        WorkflowNext.Content = vm.Guide(workflowStep switch
        {
            0 when vm.Company?.Snapshot is null => "CreateCompanyNext",
            0 => "ContinueStructure",
            1 => ModelPanel.NextActionKey,
            2 when vm.Company?.CanWrite == false => "ReadOnlyAction",
            2 => vm.Studies?.HasImportDestination == true ? "ContinueImport" : "SaveAndImport",
            3 => vm.Studies?.HasConfirmedImports == true ? "ContinueResources" : "OpenImport",
            5 => "ContinueSecondary",
            _ => "Next"
        });
        if (workflowStep == 4 && vm.Resources is { } resources) WorkflowNext.Content = resources.ShowParts ? resources.NextAction : vm.Guide("ContinueAllocations");
        WorkflowNext.Visibility = workflowStep is 6 or 7 ? Visibility.Collapsed : Visibility.Visible;
        WorkflowNext.IsEnabled = vm.Company?.WorkspaceIdle != false &&
            (workflowStep is not (1 or 2) || vm.Company?.CanWrite == true) &&
            (workflowStep != 4 || vm.Resources?.CanAdvance == true) &&
            (workflowStep != 5 || vm.Allocations?.IsPrimaryClosed == true);
        FieldHelpText.Text = vm.Guide(lastHelpKey is null ? "HelpHint" : "Help." + lastHelpKey);
    }
    internal async Task NavigateWorkflow(int step)
    {
        if (viewModel is not { } vm) return;
        if (step is > 0 and not 6 && vm.Company?.Snapshot is null) step = 0;
        if (step == 3 && vm.Studies?.HasImportDestination != true) step = 2;
        if (step is 4 or 5 or 7 && vm.Studies?.Current is null) step = 2;
        if (step is 4 or 5 or 7 && vm.Studies?.HasConfirmedImports != true) step = 3;
        if (step == 7 && vm.Allocations?.IsPrimaryClosed != true) step = 5;
        if (vm.Company?.WorkspaceIdle == false) return;
        if (workflowStep == 4 && step != 4 && vm.Resources is { } oldResources && !await oldResources.CanLeaveAsync())
        { ResourcePanel.ActionStatus.BringIntoView(); return; }
        if ((workflowStep is 5 or 7) && step != workflowStep && vm.Allocations is { } oldAllocations && !await oldAllocations.CanLeaveAsync())
        { AllocationPanel.ActionStatus.BringIntoView(); return; }
        if ((workflowStep is 5 or 7) && step != workflowStep && vm.Resources is { } distribution && !await distribution.CanLeaveAsync())
        { DistributionResourcePanel.ActionStatus.BringIntoView(); return; }
        if ((workflowStep is 5 or 7) && step != workflowStep && vm.Resources is { ShowParts: true } detail)
            await detail.CloseDestinationsAsync();
        if (workflowStep == 1 && step != 1 && vm.Models is { } modelEditor && !await modelEditor.CanLeaveAsync()) return;
        lastHelpKey = null;
        workflowStep = step;
        if (step == 1 && vm.Models is { } nodes) await nodes.LoadStudyNodesAsync();
        if (step == 2 && vm.Studies?.RefreshCommand.CanExecute(null) == true)
            await vm.Studies.RefreshCommand.ExecuteAsync(null);
        if (step == 4 && vm.Resources is { } resources) await resources.EnterAsync();
        if (step is 5 or 7 && vm.Allocations is { } allocations)
        { if (vm.Resources is { } source) await source.EnterAsync(); await allocations.EnterAsync(); allocations.SecondaryWorkspaceActive = step == 7; }
        UpdateWorkflow(); RootScrollViewer.ScrollToTop();
    }
    private async void ChooseCompany(object sender, RoutedEventArgs e) => await NavigateWorkflow(0);
    private async void ChooseModel(object sender, RoutedEventArgs e) => await NavigateWorkflow(1);
    private async void ChooseStudy(object sender, RoutedEventArgs e) => await NavigateWorkflow(2);
    private async void ChooseImport(object sender, RoutedEventArgs e) => await NavigateWorkflow(3);
    private async void ChooseResources(object sender, RoutedEventArgs e) => await NavigateWorkflow(4);
    private async void ChooseAllocations(object sender, RoutedEventArgs e) => await NavigateWorkflow(5);
    private async void ChooseSecondary(object sender, RoutedEventArgs e) => await NavigateWorkflow(7);
    private async void ChooseSettings(object sender, RoutedEventArgs e) => await NavigateWorkflow(6);
    private bool changingDistributionDetails;
    private async void OpenDistributionDetails(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource != DistributionDetails || changingDistributionDetails || viewModel?.Allocations is not { } allocations) return;
        if (!await allocations.CanLeaveAsync())
        { changingDistributionDetails = true; DistributionDetails.IsExpanded = false; changingDistributionDetails = false; return; }
        if (viewModel.Resources is { } resources) await resources.EnterAsync();
        UpdateWorkflow();
    }
    private async void CloseDistributionDetails(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource != DistributionDetails || changingDistributionDetails || viewModel?.Resources is not { } resources) return;
        if (!await resources.CanLeaveAsync())
        { changingDistributionDetails = true; DistributionDetails.IsExpanded = true; changingDistributionDetails = false; return; }
        if (viewModel.Allocations is { } allocations) await allocations.EnterAsync(true);
        UpdateWorkflow();
    }
    internal Task? DistributionSaveTask { get; private set; }
    private async void SaveDistributionDetails(object sender, RoutedEventArgs e)
    { DistributionSaveTask = SaveDistributionAsync(); await DistributionSaveTask; }
    private async Task SaveDistributionAsync()
    {
        if (viewModel?.Resources is { IsRevaluing: true } revalue) { await revalue.AdvanceAsync(); return; }
        if (viewModel?.Resources is not { } resources || !await resources.SavePendingAndLeaveAsync())
        { DistributionResourcePanel.ActionStatus.BringIntoView(); return; }
        if (viewModel.Allocations is { } allocations) await allocations.EnterAsync(true);
    }
    private async void ChooseProjectStructure(object sender, RoutedEventArgs e) => await NavigateWorkflow(1);
    private async void ChooseProjectAnalysis(object sender, RoutedEventArgs e) => await NavigateWorkflow(2);
    private async void ContinueProjectWork(object sender, RoutedEventArgs e) => await ContinueProjectWorkAsync();
    internal async Task ContinueProjectWorkAsync()
    {
        if (viewModel?.Studies?.Current is null) return;
        if (viewModel.Studies.HasConfirmedImports) await NavigateWorkflow(4);
        else if (viewModel.Studies.HasImportDestination) await NavigateWorkflow(3);
        else await NavigateWorkflow(2);
    }
    private async void OpenAnotherImport(object sender, RoutedEventArgs e) => await OpenGuidedImport();
    private async void PreviousStep(object sender, RoutedEventArgs e) => await GoBackWorkflow();
    internal async Task GoBackWorkflow()
    {
        if (viewModel?.Company?.WorkspaceIdle == false) return;
        if (workflowStep == 1 && ModelPanel.PreviousModelStep()) { UpdateWorkflow(); return; }
        if (workflowStep == 4 && viewModel?.Resources is { ShowParts: true } resources)
        { await resources.CloseDestinationsAsync(); UpdateWorkflow(); RootScrollViewer.ScrollToTop(); return; }
        await NavigateWorkflow(workflowStep == 7 ? 5 : Math.Max(0, workflowStep - 1));
    }
    private async void ContinueWorkflow(object sender, RoutedEventArgs e) => await AdvanceWorkflow();
    internal async Task AdvanceWorkflow()
    {
        if (viewModel is not { } vm || vm.Company?.WorkspaceIdle == false) return;
        if (workflowStep == 2)
        {
            if (vm.Studies is { } studies && await studies.SaveForImport()) await NavigateWorkflow(3);
            else { UpdateWorkflow(); StudyPanel.ActionStatus.BringIntoView(); }
        }
        else if (workflowStep == 3)
        {
            if (vm.Studies?.HasConfirmedImports == true) await NavigateWorkflow(4); else await OpenGuidedImport();
        }
        else if (workflowStep == 4 && vm.Resources is { } resources)
        { if (resources.ShowParts) { await resources.AdvanceAsync(); UpdateWorkflow(); RootScrollViewer.ScrollToTop(); } else await NavigateWorkflow(5); }
        else if (workflowStep == 1)
        {
            if (await ModelPanel.AdvanceModel()) await NavigateWorkflow(2);
            UpdateWorkflow();
        }
        else if (workflowStep == 0 && vm.Company?.Snapshot is null)
        {
            if (vm.Company is not { } company) return;
            if (string.IsNullOrWhiteSpace(company.CompanyName)) { CompanyPanel.NameBox.Focus(); return; }
            if (company.CreateCommand.CanExecute(null)) await company.CreateCommand.ExecuteAsync(null);
            if (company.Snapshot is not null) await NavigateWorkflow(1);
        }
        else await NavigateWorkflow(workflowStep == 5 ? 7 : workflowStep + 1);
    }
    internal async Task OpenGuidedImport()
    {
        if (viewModel?.Studies is not { } studies) return;
        if (!studies.HasImportDestination) { await NavigateWorkflow(2); return; }
        if (await studies.AttachImportTarget())
        {
            var importCountBefore = studies.ConfirmedImportCount;
            new ImportColumnMappingWindow { Owner = this, DataContext = studies.ImportColumns }.ShowDialog();
            await ContinueAfterGuidedImport(importCountBefore);
            return;
        }
        UpdateWorkflow();
    }
    internal async Task ContinueAfterGuidedImport(int importCountBefore)
    {
        if (viewModel?.Studies is { } studies && studies.ConfirmedImportCount > importCountBefore)
            await NavigateWorkflow(4);
        else
            UpdateWorkflow();
    }
    internal void RevealWorkflowElement(FrameworkElement element)
    {
        // Also used by native acceptance: navigate the real visible sections before capturing.
        if (element == CompanyPanel || GuidedUi.Contains(CompanyPanel, element)) workflowStep = 0;
        else if (element == ModelPanel || GuidedUi.Contains(ModelPanel, element)) { workflowStep = 1; ModelPanel.RevealElement(element); }
        else if (element == StudyPanel || GuidedUi.Contains(StudyPanel, element)) workflowStep = 2;
        else if (element == ResourcePanel || GuidedUi.Contains(ResourcePanel, element)) workflowStep = 4;
        else if (element == DistributionDetails || GuidedUi.Contains(DistributionDetails, element)) { workflowStep = 5; DistributionDetails.IsExpanded = true; }
        else if (element == AllocationPanel || GuidedUi.Contains(AllocationPanel, element)) workflowStep = 5;
        else if (element == SecondaryPanel || GuidedUi.Contains(SecondaryPanel, element)) workflowStep = 7;
        else workflowStep = 6;
        UpdateWorkflow(); UpdateLayout();
    }
}
