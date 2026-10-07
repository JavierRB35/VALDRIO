using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Valdrio.Desktop;

public partial class MainWindow
{
    private int workflowStep;
    private readonly List<INotifyPropertyChanged> workflowSources = [];
    private void InitializeGuidance()
    {
        var units = CompanyPanel.UnitCreationPanel;
        ((Panel)units.Parent).Children.Remove(units);
        units.SetBinding(DataContextProperty, new Binding("DataContext") { Source = CompanyPanel });
        ModelPanel.UnitsCreationHost.Content = units;
        Closed += (_, _) => { foreach (var source in workflowSources) source.PropertyChanged -= WorkflowChanged; };
    }
    private void BindGuidance()
    {
        foreach (var source in workflowSources) source.PropertyChanged -= WorkflowChanged;
        workflowSources.Clear();
        if (viewModel is null) return;
        foreach (var source in new INotifyPropertyChanged?[] { viewModel, viewModel.Company, viewModel.Models, viewModel.Studies })
            if (source is not null) { workflowSources.Add(source); source.PropertyChanged += WorkflowChanged; }
        UpdateWorkflow();
    }
    private void WorkflowChanged(object? sender, PropertyChangedEventArgs e) => UpdateWorkflow();
    private string? lastHelpKey;
    internal void ShowFieldHelp(string text, string? key = null) { lastHelpKey = key; FieldHelpText.Text = text; }
    internal void ClearFieldHelp() { lastHelpKey = null; UpdateWorkflow(); }
    internal void UpdateWorkflow()
    {
        if (viewModel is not { } vm) return;
        var keys = new[] { "Company", "Model", "Study", "Import", "Settings" };
        Button[] buttons = [CompanyStep, ModelStep, StudyStep, ImportStep, SettingsStep];
        for (var i = 0; i < buttons.Length; i++)
        {
            buttons[i].Content = vm.Guide(keys[i]);
            buttons[i].FontWeight = i == workflowStep ? FontWeights.Bold : FontWeights.Normal;
        }
        CompanyPanel.Visibility = workflowStep == 0 ? Visibility.Visible : Visibility.Collapsed;
        ModelPanel.Visibility = workflowStep == 1 ? Visibility.Visible : Visibility.Collapsed;
        StudyPanel.Visibility = workflowStep == 2 ? Visibility.Visible : Visibility.Collapsed;
        ImportHome.Visibility = workflowStep == 3 ? Visibility.Visible : Visibility.Collapsed;
        PreferencesPanel.Visibility = DevelopmentTools.Visibility = workflowStep == 4 ? Visibility.Visible : Visibility.Collapsed;
        WorkflowTitle.Text = vm.Guide(keys[workflowStep]);
        WorkflowRequirement.Text = workflowStep switch
        {
            0 => vm.Guide(vm.Company?.Snapshot is null ? "NeedCompany" : "CompanyReady"),
            1 => vm.Models?.NextRequirement ?? vm.Guide("NeedCompany"),
            2 or 3 => vm.Studies?.NextRequirement ?? vm.Guide("NeedCompany"),
            _ => vm.Guide("HelpHint")
        };
        SaveLocation.Text = vm.Company?.ActivePath is { Length: > 0 } path ? vm.Guide("StoredAt") + path : "";
        WorkflowBack.Content = vm.Guide("Back"); WorkflowBack.IsEnabled = workflowStep > 0 && vm.Company?.WorkspaceIdle != false;
        WorkflowNext.Content = vm.Guide(workflowStep switch
        {
            0 when vm.Company?.Snapshot is null => "CreateCompanyNext",
            1 => ModelPanel.NextActionKey,
            2 when vm.Company?.CanWrite == false => "ReadOnlyAction",
            2 => vm.Studies?.HasImportDestination == true ? "ContinueImport" : "SaveAndImport",
            3 => "OpenImport",
            _ => "Next"
        });
        WorkflowNext.Visibility = workflowStep == 4 ? Visibility.Collapsed : Visibility.Visible;
        WorkflowNext.IsEnabled = vm.Company?.WorkspaceIdle != false &&
            (workflowStep is not (1 or 2) || vm.Company?.CanWrite == true);
        FieldHelpText.Text = vm.Guide(lastHelpKey is null ? "HelpHint" : "Help." + lastHelpKey);
    }
    internal async Task NavigateWorkflow(int step)
    {
        if (viewModel is not { } vm) return;
        if (step is > 0 and < 4 && vm.Company?.Snapshot is null) step = 0;
        if (step == 3 && vm.Studies?.HasImportDestination != true) step = 2;
        lastHelpKey = null;
        workflowStep = step;
        if (step == 2 && vm.Studies?.RefreshCommand.CanExecute(null) == true)
            await vm.Studies.RefreshCommand.ExecuteAsync(null);
        UpdateWorkflow(); RootScrollViewer.ScrollToTop();
    }
    private async void ChooseCompany(object sender, RoutedEventArgs e) => await NavigateWorkflow(0);
    private async void ChooseModel(object sender, RoutedEventArgs e) => await NavigateWorkflow(1);
    private async void ChooseStudy(object sender, RoutedEventArgs e) => await NavigateWorkflow(2);
    private async void ChooseImport(object sender, RoutedEventArgs e) => await NavigateWorkflow(3);
    private async void ChooseSettings(object sender, RoutedEventArgs e) => await NavigateWorkflow(4);
    private async void PreviousStep(object sender, RoutedEventArgs e) => await GoBackWorkflow();
    internal async Task GoBackWorkflow()
    {
        if (viewModel?.Company?.WorkspaceIdle == false) return;
        if (workflowStep == 1 && ModelPanel.PreviousModelStep()) { UpdateWorkflow(); return; }
        await NavigateWorkflow(Math.Max(0, workflowStep - 1));
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
        else if (workflowStep == 3) await OpenGuidedImport();
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
        else await NavigateWorkflow(workflowStep + 1);
    }
    internal async Task OpenGuidedImport()
    {
        if (viewModel?.Studies is not { } studies) return;
        if (!studies.HasImportDestination) { await NavigateWorkflow(2); return; }
        if (await studies.AttachImportTarget())
            new ImportColumnMappingWindow { Owner = this, DataContext = studies.ImportColumns }.ShowDialog();
        UpdateWorkflow();
    }
    internal void RevealWorkflowElement(FrameworkElement element)
    {
        // Also used by native acceptance: navigate the real visible sections before capturing.
        if (element == CompanyPanel || GuidedUi.Contains(CompanyPanel, element)) workflowStep = 0;
        else if (element == ModelPanel || GuidedUi.Contains(ModelPanel, element)) { workflowStep = 1; ModelPanel.RevealElement(element); }
        else if (element == StudyPanel || GuidedUi.Contains(StudyPanel, element)) workflowStep = 2;
        else workflowStep = 4;
        UpdateWorkflow(); UpdateLayout();
    }
}
