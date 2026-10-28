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
        if (element is not DataGridCell cell || cell.IsReadOnly || cell.IsEditing) return;
        grid.SelectedItem = cell.DataContext;
        grid.CurrentCell = new DataGridCellInfo(cell);
        cell.Focus(); grid.BeginEdit();
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
        NodeSummary.Columns[4].Width = new DataGridLength(2, DataGridLengthUnitType.Star);
        NodeSummary.Columns[4].MinWidth = 220;
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

        CenterGrid.Columns[0].Header = tableViewModel.Text("Model.ColumnCode");
        CenterGrid.Columns[1].Header = tableViewModel.Text("Model.ColumnName");
        CenterGrid.Columns[2].Header = tableViewModel.Text("Model.ColumnStatus");

        NodeSummary.Columns[0].Header = tableViewModel.Text("Model.ColumnCenter");
        NodeSummary.Columns[1].Header = tableViewModel.Text("Model.ColumnCode");
        NodeSummary.Columns[2].Header = tableViewModel.Text("Model.ColumnRole");
        NodeSummary.Columns[3].Header = tableViewModel.Text("Model.ColumnUnit");
        NodeSummary.Columns[4].Header = tableViewModel.Text("Model.NodeDescription");
        NodeSummary.Columns[5].Header = tableViewModel.Text("Model.ColumnFunctions");
    }
}
