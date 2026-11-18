using System.ComponentModel;
using System.Windows.Controls;

namespace Valdrio.Desktop;

public partial class ModelWorkspacePanel : UserControl
{
    private static void SelectEditorText(object? sender, DataGridPreparingCellForEditEventArgs e)
    {
        e.EditingElement.MoveFocus(new(System.Windows.Input.FocusNavigationDirection.First));
        if (System.Windows.Input.Keyboard.FocusedElement is TextBox text) text.SelectAll();
    }
    private void EditCellOnClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not DataGrid grid || grid.IsReadOnly) return;
        var element = e.OriginalSource as System.Windows.DependencyObject;
        while (element is not null && element is not DataGridCell)
            element = System.Windows.Media.VisualTreeHelper.GetParent(element);
        if (element is not DataGridCell cell || cell.IsReadOnly || cell.IsEditing ||
            cell.Column is not { } column || cell.DataContext is not { } item) return;

        // A row selection can rebuild NodeSummary's ItemsSource synchronously.
        // The original DataGridCell may already be detached after SelectedItem changes.
        // Resolve the *current* row on the dispatcher, never from that detached cell.
        grid.SelectedItem = item;
        _ = grid.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
        {
            if (!grid.IsLoaded || grid.IsReadOnly || !grid.Columns.Contains(column) ||
                grid.SelectedItem is not { } selected || !grid.Items.Contains(selected))
                return;
            grid.ScrollIntoView(selected, column);
            grid.CurrentCell = new DataGridCellInfo(selected, column);
            grid.Focus();
            grid.BeginEdit();
        }));
    }
    private Action? restoreStructureCell;
    private void BeginStructureCellEdit(object? sender, DataGridBeginningEditEventArgs e)
    {
        if (DataContext is not ModelWorkspaceViewModel vm) { e.Cancel = true; return; }
        if (sender == CenterGrid)
        {
            if (!vm.CanEditMasters || !Equals(vm.Master, e.Row.Item)) { e.Cancel = true; return; }
            var code = vm.CenterCode; var name = vm.CenterName;
            restoreStructureCell = () => { vm.CenterCode = code; vm.CenterName = name; };
        }
        else
        {
            if (!vm.CanEdit || !Equals(vm.SelectedNode, e.Row.Item)) { e.Cancel = true; return; }
            var code = vm.NodeCode; var description = vm.PoolDescription; var functions = vm.FunctionTags;
            var center = vm.Physical; var role = vm.Role; var unit = vm.DriverUnit;
            restoreStructureCell = () => { vm.NodeCode = code; vm.PoolDescription = description;
                vm.FunctionTags = functions; vm.Physical = center; vm.Role = role; vm.DriverUnit = unit; };
        }
    }
    private void EndStructureCellEdit(object? sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction == DataGridEditAction.Cancel) restoreStructureCell?.Invoke();
        restoreStructureCell = null;
    }
    private ModelWorkspaceViewModel? tableViewModel;

    public ModelWorkspacePanel()
    {
        InitializeComponent();
        CenterGrid.PreviewMouseLeftButtonDown += EditCellOnClick;
        NodeSummary.PreviewMouseLeftButtonDown += EditCellOnClick;
        NodeSummary.Columns[4].DisplayIndex = 0;
        NodeSummary.Columns[4].Width = new DataGridLength(1.3, DataGridLengthUnitType.Star);
        NodeSummary.Columns[4].MinWidth = 180;
        CenterGrid.PreparingCellForEdit += SelectEditorText;
        NodeSummary.PreparingCellForEdit += SelectEditorText;
        InitializeGuidance();
        DataContextChanged += (_, _) => BindTableHeaders();
        Loaded += (_, _) => BindTableHeaders();
        Unloaded += (_, _) =>
        {
            if (tableViewModel is not null)
                tableViewModel.PropertyChanged -= TableViewModelChanged;
        };
    }

    private void MapOptionsChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (ProcessMapCanvas is null || MapLinkKindBox is null) return;
        ProcessMapCanvas.ConnectMode = MapConnectToggle?.IsChecked == true;
        ProcessMapCanvas.RemoveMode = MapRemoveToggle?.IsChecked == true;
        ProcessMapCanvas.ConnectionKind = (MapLinkKindBox.SelectedItem as ComboBoxItem)?.Tag as string ==
            "AuxiliaryService"
            ? Valdrio.Domain.ProcessRouteKind.AuxiliaryService
            : Valdrio.Domain.ProcessRouteKind.Sequence;
        ProcessMapCanvas.ClearConnection();
    }

    private void MapZoomChanged(object sender,
        System.Windows.RoutedPropertyChangedEventArgs<double> e)
    {
        if (ProcessMapCanvas is not null) ProcessMapCanvas.Zoom = e.NewValue;
    }

    private void AutoArrangeProcessMap(object sender, System.Windows.RoutedEventArgs e) =>
        ProcessMapCanvas.AutoArrange();

    private async void ValidateProcessMap(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is ModelWorkspaceViewModel vm)
            await vm.ValidateProcessMapAsync();
    }

    private void ExportProcessMap(object sender, System.Windows.RoutedEventArgs e) =>
        ProcessMapCanvas.ExportPng();

    private void OpenModelSetup(object sender, System.Windows.RoutedEventArgs e)
    {
        // A new node needs a draft model; navigate, do not create or mutate one.
        ModelSteps.SelectedIndex = 0;
    }

    private void BindTableHeaders()
    {
        if (tableViewModel is not null)
            tableViewModel.PropertyChanged -= TableViewModelChanged;

        tableViewModel = DataContext as ModelWorkspaceViewModel;
        if (tableViewModel is not null)
            tableViewModel.PropertyChanged += TableViewModelChanged;

        RefreshTableHeaders();
    }

    private void TableViewModelChanged(object? sender, PropertyChangedEventArgs e) =>
        RefreshTableHeaders();

    private void RefreshTableHeaders()
    {
        if (tableViewModel is null)
            return;

        // Frequent property changes must not invalidate table layout unless the
        // language really changes a header.
        void Header(DataGridColumn column, string key)
        {
            var label = tableViewModel.Text(key);
            if (!Equals(column.Header, label)) column.Header = label;
        }

        Header(CenterGrid.Columns[0], "Model.ColumnCode");
        Header(CenterGrid.Columns[1], "Model.ColumnName");
        Header(CenterGrid.Columns[2], "Model.ColumnStatus");

        Header(NodeSummary.Columns[0], "Model.ColumnCenter");
        Header(NodeSummary.Columns[1], "Model.ColumnCode");
        Header(NodeSummary.Columns[2], "Model.ColumnRole");
        Header(NodeSummary.Columns[3], "Model.ColumnUnit");
        Header(NodeSummary.Columns[4], "Model.NodeDescription");
        Header(NodeSummary.Columns[5], "Model.ColumnFunctions");
        Header(NodeSummary.Columns[6], "Model.EconomicFunction");
    }
}
