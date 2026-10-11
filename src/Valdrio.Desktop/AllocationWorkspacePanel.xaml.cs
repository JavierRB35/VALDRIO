using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
namespace Valdrio.Desktop;
public partial class AllocationWorkspacePanel : UserControl
{
    private AllocationWorkspaceViewModel? viewModel;
    public AllocationWorkspacePanel()
    {
        InitializeComponent();
        ConfigurePage(false);
        DataContextChanged += (_, e) =>
        {
            if (viewModel is not null) viewModel.PropertyChanged -= Changed;
            viewModel = e.NewValue as AllocationWorkspaceViewModel;
            if (viewModel is not null) viewModel.PropertyChanged += Changed;
            Headers();
        };
    }
    public bool SecondaryOnly { set => ConfigurePage(value); }
    private void ConfigurePage(bool secondary)
    {
        // Each page has its own visual tree; shared controls retain the same commands and validation.
        static void Card(FrameworkElement section, bool visible) =>
            ((FrameworkElement)section.Parent).Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        Card(CompositionSection, !secondary);
        Card(AccountsSection, !secondary);
        Card(ResultsSection, !secondary);
        Card(SecondaryResultsSection, secondary);
        SecondaryPhaseBar.Visibility = secondary ? Visibility.Visible : Visibility.Collapsed;
        ((FrameworkElement)((FrameworkElement)TogglePrimaryButton.Parent).Parent).Visibility = secondary ? Visibility.Collapsed : Visibility.Visible;
        for (var i = 0; i < RuleTabs.Items.Count; i++)
            ((TabItem)RuleTabs.Items[i]).Visibility = (i == 1) == secondary ? Visibility.Visible : Visibility.Collapsed;
        RuleTabs.SelectedIndex = secondary ? 1 : 0;
        RulesSection.IsExpanded = true;
    }
    private void Changed(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == "Item[]") Headers();
    }
    private void Headers()
    {
        if (viewModel is null) return;
        Set(KeyGrid, "Name", "State"); Set(LinesGrid, "Include", "Destination", "NodeUnit", "Weight"); Set(ReciprocalGrid, "ReciprocalGroup", "ReciprocalStatus", "ReciprocalDetail"); Set(SecondaryRulesGrid, "Auxiliary", "ReciprocalGroup", "AppliedKey"); Set(SecondaryResultsGrid, "SecondarySourceNode", "SecondaryPrincipalDestination", "PrimaryAmount", "Behavior"); Set(SecondaryCirculationGrid, "Auxiliary", "SecondaryGross");  Set(SecondaryGroupAssignmentsGrid, "SecondarySourcePart", "SecondarySelectedGroup");
        Set(PrincipalCostsGrid, "SecondaryPrincipalDestination", "PrincipalPrimary", "PrincipalAuxiliary", "PrincipalTotal");
        Set(SecondaryExplanationGrid, "PrimarySource", "ExplainPrimaryAmount", "ExplainEffectiveShare", "PrimaryAmount", "ExplainKeys");
        CompositionGrid.Columns[0].Header = viewModel["CompositionAccount"];
        CompositionGrid.Columns[1].Header = viewModel["CompositionAmount"];
        CompositionGrid.Columns[2].Header = viewModel["CompositionBehavior"];
        CompositionGrid.Columns[3].Header = viewModel["CompositionFixedWeight"];
        CompositionGrid.Columns[4].Header = viewModel["CompositionVariableWeight"];
        CompositionGrid.Columns[5].Header = viewModel["CompositionReview"];
        Set(CoverageGrid, "Source", "Amount", "AppliedKey", "Result"); Set(PrimaryResultsGrid, "PrimarySource", "PrimaryDestination", "PrimaryAmount", "Behavior"); Set(PrimaryGrid, "Account", "AppliedKey", "State");
        Set(AbsoluteGrid, "Include", "Destination", "Amount");
        void Set(DataGrid grid, params string[] keys) { for (var i = 0; i < keys.Length; i++) grid.Columns[i].Header = viewModel[keys[i]]; }
    }
    private async void OpenKeyDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ItemsControl.ContainerFromElement(KeyGrid, e.OriginalSource as DependencyObject) is DataGridRow && viewModel?.LoadKeyCommand.CanExecute(null) == true)
            await viewModel.LoadKeyCommand.ExecuteAsync(null);
    }
    private async void OpenKeyKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && viewModel?.LoadKeyCommand.CanExecute(null) == true)
        { e.Handled = true; await viewModel.LoadKeyCommand.ExecuteAsync(null); }
    }
    internal Task CoverageAssignmentTask { get; private set; } = Task.CompletedTask;
    private async void CoverageKeyChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox { IsKeyboardFocusWithin: true, DataContext: AllocationCoverageRow row, SelectedValue: Valdrio.Domain.EntityId key } box || key == row.SelectedKeyId || viewModel is null) return;
        CoverageAssignmentTask = viewModel.AssignCoverageAsync(row.Id, key);
        await CoverageAssignmentTask;
        box.GetBindingExpression(ComboBox.SelectedValueProperty)?.UpdateTarget();
    }
    private bool restoringTab;
    private int previousTab;
    private void RuleTabChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.OriginalSource != RuleTabs || restoringTab) return;
        if (viewModel?.HasUnappliedRule == true)
        {
            restoringTab = true; RuleTabs.SelectedIndex = previousTab; restoringTab = false;
            viewModel.ExplainPendingRule(); return;
        }
        previousTab = RuleTabs.SelectedIndex;
    }
    private void SelectNumber(object sender, KeyboardFocusChangedEventArgs e) { if (sender is TextBox box) box.SelectAll(); }
}
