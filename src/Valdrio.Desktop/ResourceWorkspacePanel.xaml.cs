using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Input;
namespace Valdrio.Desktop;
public partial class ResourceWorkspacePanel : UserControl
{
    private ResourceWorkspaceViewModel? vm;
    public ResourceWorkspacePanel()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Bind(); Loaded += (_, _) => Bind();
        Unloaded += (_, _) => { if (vm is not null) vm.PropertyChanged -= Changed; };
    }
    private void Bind()
    {
        if (vm is not null) vm.PropertyChanged -= Changed;
        vm = DataContext as ResourceWorkspaceViewModel;
        if (vm is not null) vm.PropertyChanged += Changed;
        RefreshHeaders();
    }
    private void Changed(object? sender, PropertyChangedEventArgs e) => RefreshHeaders();
    private async void OpenSelectedResource(object sender, MouseButtonEventArgs e)
    {
        if (vm?.OpenSelectedResourceCommand.CanExecute(null) == true)
            await vm.OpenSelectedResourceCommand.ExecuteAsync(null);
    }
    private void RefreshHeaders()
    {
        if (vm is null) return;
        void Headers(DataGrid grid, string[] keys) { for (var i = 0; i < keys.Length; i++) grid.Columns[i].Header = vm[keys[i]]; }
        Headers(ResourceGrid, ["Code", "Originals", "Converted", "Adjustments", "Effective", "PendingAmount", "Difference"]);
        Headers(AdjustmentGrid, ["Original", "Functional", "Type", "Reason", "State"]);
        Headers(PartGrid, ["Amount", "Route", "Destination", "Behavior", "Layer"]);
        Headers(ReviewGrid, ["Amount", "Route", "Destination", "Behavior", "Layer"]);
    }
}
