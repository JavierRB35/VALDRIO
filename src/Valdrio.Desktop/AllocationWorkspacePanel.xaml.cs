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
        DataContextChanged += (_, e) =>
        {
            if (viewModel is not null) viewModel.PropertyChanged -= Changed;
            viewModel = e.NewValue as AllocationWorkspaceViewModel;
            if (viewModel is not null) viewModel.PropertyChanged += Changed;
            Headers();
        };
    }
    private void Changed(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == "Item[]") Headers();
    }
    private void Headers()
    {
        if (viewModel is null) return;
        Set(KeyGrid, "Name", "State"); Set(LinesGrid, "Include", "Destination", "NodeUnit", "Weight");
        Set(CoverageGrid, "Source", "Amount", "AppliedKey", "Result"); Set(PrimaryResultsGrid, "PrimarySource", "PrimaryDestination", "PrimaryAmount", "PrimaryRule"); Set(PrimaryGrid, "Account", "AppliedKey", "Priority");
        Set(SecondaryGrid, "Auxiliary", "Group", "AppliedKey"); Set(AbsoluteGrid, "Include", "Destination", "Amount");
        void Set(DataGrid grid, params string[] keys) { for (var i = 0; i < keys.Length; i++) grid.Columns[i].Header = viewModel[keys[i]]; }
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
