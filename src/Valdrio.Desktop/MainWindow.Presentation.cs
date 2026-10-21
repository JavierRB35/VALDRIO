using System.Windows;
using System.Windows.Automation;

namespace Valdrio.Desktop;

public partial class MainWindow
{
    private void ToggleNavigation(object sender, RoutedEventArgs e)
    {
        var show = Sidebar.Visibility != Visibility.Visible;
        Sidebar.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        SidebarColumn.Width = show ? (GridLength)FindResource("Sidebar.Width") : new GridLength(0);
        UpdatePresentationLabels();
    }
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
        var english = vm.SelectedUiLanguageTag.StartsWith("en", StringComparison.OrdinalIgnoreCase);
        ContextHelpButton.Content = english ? "Help (F1)" : "Ayuda (F1)";
        ContextHelpButton.ToolTip = english ? "Help for this screen (F1)" : "Ayuda de esta pantalla (F1)";
        AutomationProperties.SetName(ContextHelpButton, ContextHelpButton.ToolTip.ToString());
        AutomationProperties.SetName(ContextDetailsButton, ContextDetailsButton.Content.ToString());
        NavigationToggle.Content = vm.Guide("NavigationMenu");
        NavigationToggle.ToolTip = vm.Guide(Sidebar.Visibility == Visibility.Visible ? "HideNavigation" : "ShowNavigation");
        AutomationProperties.SetName(NavigationToggle, NavigationToggle.ToolTip.ToString());
    }
}
