using System.Windows;
using System.Windows.Automation;

namespace Valdrio.Desktop;

public partial class MainWindow
{
    // Session-only presentation choices; never stored in an economic snapshot.
    private void ToggleContextDetails(object sender, RoutedEventArgs e)
    {
        ContextDetails.Visibility = ContextDetails.IsVisible ? Visibility.Collapsed : Visibility.Visible;
        UpdatePresentationLabels();
    }

    private void ChangeTableDensity(object sender, RoutedEventArgs e)
    {
        var compact = CompactTables.IsChecked == true;
        var resources = System.Windows.Application.Current.Resources;
        resources["Table.MinRowHeight"] = compact ? 28d : 36d;
        resources["Table.CellPadding"] = compact ? new Thickness(4, 1, 4, 1) : new Thickness(4, 4, 4, 4);
        resources["Table.NumberPadding"] = compact ? new Thickness(8, 2, 8, 2) : new Thickness(8, 5, 8, 5);
    }

    private void UpdatePresentationLabels()
    {
        if (viewModel is not { } vm) return;
        ContextDetailsButton.Content = vm.Guide(ContextDetails.Visibility == Visibility.Visible ? "HideContext" : "ShowContext");
        CompactTables.Content = vm.Guide("CompactTables");
        CompactTables.ToolTip = vm.Guide("CompactTablesHelp");
        AutomationProperties.SetName(ContextDetailsButton, vm.Guide("ShowContext"));
    }
}
