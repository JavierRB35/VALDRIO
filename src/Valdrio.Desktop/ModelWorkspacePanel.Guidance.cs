using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;

namespace Valdrio.Desktop;

public partial class ModelWorkspacePanel
{
    private FrameworkElement? unitCreationPanel;
    private CompanyFilePanel? unitCompanyPanel;

    internal void AttachUnitCreationPanel(CompanyFilePanel companyPanel)
    {
        unitCompanyPanel = companyPanel;
        var units = companyPanel.UnitCreationPanel;
        if (units.Parent is Panel panel)
            panel.Children.Remove(units);
        else if (units.Parent is ContentControl host)
            host.Content = null;

        units.SetBinding(DataContextProperty, new Binding("DataContext") { Source = companyPanel });
        unitCreationPanel = units;
        companyPanel.AddUnitButton.Click += UnitCreatedFromModel;
        PlaceUnitCreationPanel();
    }

    private void PlaceUnitCreationPanel()
    {
        if (unitCreationPanel is null) return;
        UnitsCreationHost.Content = null;
        NodeUnitsCreationHost.Content = null;
        if (ModelSteps.SelectedIndex == 2)
            NodeUnitsCreationHost.Content = unitCreationPanel;
        else
            UnitsCreationHost.Content = unitCreationPanel;
    }

    private async void UnitCreatedFromModel(object sender, RoutedEventArgs e)
    {
        if (ModelSteps.SelectedIndex != 2 ||
            DataContext is not ModelWorkspaceViewModel vm ||
            unitCompanyPanel?.DataContext is not CompanyFileViewModel company)
            return;

        var requestedCode = company.UnitCode.Trim();
        var previousIds = vm.Units.Select(u => u.Value.Definition.Id).ToHashSet();

        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        if (company.AddUnitCommand.ExecutionTask is { } task)
            await task;
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);

        var created = vm.Units.FirstOrDefault(u =>
            !previousIds.Contains(u.Value.Definition.Id) &&
            string.Equals(u.Value.Definition.Code, requestedCode, StringComparison.Ordinal));

        if (created is null) return;

        vm.DriverUnit = created;
        NodeUnitCreator.IsExpanded = false;
        UpdateLayout();
        UnitBox.BringIntoView();
        UnitBox.Focus();
    }

    private void InitializeGuidance()
    {
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is ModelWorkspaceViewModel old) old.PropertyChanged -= GuidanceChanged;
            if (e.NewValue is ModelWorkspaceViewModel vm) vm.PropertyChanged += GuidanceChanged;
            RefreshGuidance();
        };
    }
    private void GuidanceChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => RefreshGuidance();
    private void RefreshGuidance()
    {
        if (DataContext is not ModelWorkspaceViewModel vm) return;
        var keys = new[] { "DraftTab", "MastersTab", "NodesTab", "PublishTab" };
        for (var i = 0; i < 4; i++) ((TabItem)ModelSteps.Items[i]).Header = vm.Guide(keys[i]);
        MastersHelp.Text = vm.Guide("MasterHelp"); NodesHelp.Text = vm.Guide("NodesHelp");
        PlaceUnitCreationPanel();
        (Window.GetWindow(this) as MainWindow)?.UpdateWorkflow();
    }
    private void ModelStepChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source != ModelSteps) return;
        (Window.GetWindow(this) as MainWindow)?.ClearFieldHelp();
        PlaceUnitCreationPanel();
        RefreshGuidance();
    }
    internal string NextActionKey => DataContext is not ModelWorkspaceViewModel vm ? "Next" :
        !vm.CanWriteInGuide ? "ReadOnlyAction" :
        vm.HasPendingCenter ? ModelSteps.SelectedIndex == 1 ? "SaveCenterNext" : "ReviewCenter" :
        vm.Current is null ? string.IsNullOrWhiteSpace(vm.Description) && vm.ModelChoice is not null ? "LoadModelNext" : "CreateModelNext" :
        vm.IsPublished ? "ContinueStudy" : ModelSteps.SelectedIndex switch
        {
            2 when vm.HasPendingNode => "ApplyNodeNext",
            3 => "PublishContinue",
            _ => "Next"
        };
    internal bool PreviousModelStep()
    {
        if (ModelSteps.SelectedIndex <= 0) return false;
        ModelSteps.SelectedIndex--;
        return true;
    }
    private void FocusAt(int step, FrameworkElement field)
    {
        ModelSteps.SelectedIndex = step;
        UpdateLayout(); field.BringIntoView(); field.Focus();
    }
    internal void ShowNextRequiredStep()
    {
        if (DataContext is not ModelWorkspaceViewModel vm) return;
        if (vm.HasPendingCenter) FocusAt(1, CenterNameBox);
        else if (vm.Current is null) FocusAt(0, DescriptionBox);
        else if (!vm.HasActiveCenter) FocusAt(1, CenterCodeBox);
        else if (!vm.HasActiveUnit)
        {
            ModelSteps.SelectedIndex = 2;
            NodeUnitCreator.IsExpanded = true;
            UpdateLayout();
            FocusAt(2, unitCompanyPanel?.UnitCodeBox is FrameworkElement unitCodeBox ? unitCodeBox : UnitBox);
        }
        else if (vm.HasPendingNode || !vm.HasPrincipal)
            FocusAt(2, string.IsNullOrWhiteSpace(vm.NodeCode) ? NodeCodeBox :
                vm.Physical is null ? PhysicalBox : vm.DriverUnit is null ? UnitBox : RoleBox);
        else ModelSteps.SelectedIndex = 3;
    }
    internal async Task<bool> AdvanceModel()
    {
        if (DataContext is not ModelWorkspaceViewModel { InputsEnabled: true } vm || !vm.CanWriteInGuide) return false;
        // A centre is a separate saved entity. Reveal its form before offering its save action.
        if (vm.HasPendingCenter)
        {
            if (ModelSteps.SelectedIndex != 1) { FocusAt(1, CenterNameBox); return false; }
            if (vm.SaveCenterFormCommand.CanExecute(null))
                await vm.SaveCenterFormCommand.ExecuteAsync(null);
            if (vm.HasPendingCenter) { FocusAt(1, string.IsNullOrWhiteSpace(vm.CenterCode) ? CenterCodeBox : CenterNameBox); return false; }
        }
        if (vm.Current is null)
        {
            if (string.IsNullOrWhiteSpace(vm.Description) && vm.ModelChoice is not null)
                await vm.LoadCommand.ExecuteAsync(null);
            else if (!string.IsNullOrWhiteSpace(vm.Description)) await vm.NewDraftCommand.ExecuteAsync(null);
            if (vm.Current is null) { FocusAt(0, DescriptionBox); return false; }
            if (vm.IsPublished) return true;
            ModelSteps.SelectedIndex = 1;
            return false;
        }
        if (vm.IsPublished) return true;
        switch (ModelSteps.SelectedIndex)
        {
            case 0: ModelSteps.SelectedIndex = 1; break;
            case 1:
                if (!vm.HasActiveCenter) { ShowNextRequiredStep(); return false; }
                ModelSteps.SelectedIndex = 2;
                if (!vm.HasActiveUnit) NodeUnitCreator.IsExpanded = true;
                break;
            case 2:
                if (vm.HasPendingNode) await vm.SaveNodeCommand.ExecuteAsync(null);
                if (vm.HasPendingNode || !vm.HasPrincipal) { ShowNextRequiredStep(); return false; }
                ModelSteps.SelectedIndex = 3; break;
            case 3:
                if (vm.HasPendingNode || !vm.HasActiveCenter || !vm.HasActiveUnit || !vm.HasPrincipal)
                { ShowNextRequiredStep(); return false; }
                await PublishForStudy();
                return vm.IsPublished;
        }
        return false;
    }
    internal void RevealElement(FrameworkElement element)
    {
        foreach (TabItem tab in ModelSteps.Items)
            if (GuidedUi.Contains(tab, element)) { ModelSteps.SelectedItem = tab; break; }
    }
    internal async Task PublishForStudy()
    {
        if (DataContext is not ModelWorkspaceViewModel vm || !vm.CanEdit) return;
        if (vm.HasPendingCenter) { ShowNextRequiredStep(); return; }
        if (vm.SaveCommand.CanExecute(null)) await vm.SaveCommand.ExecuteAsync(null);
        if (vm.IsDirty) { ShowNextRequiredStep(); return; }
        if (vm.ValidateCommand.CanExecute(null)) await vm.ValidateCommand.ExecuteAsync(null);
        if (!string.IsNullOrWhiteSpace(vm.ValidationText)) return;
        if (vm.PublishCommand.CanExecute(null)) await vm.PublishCommand.ExecuteAsync(null);
    }
}
