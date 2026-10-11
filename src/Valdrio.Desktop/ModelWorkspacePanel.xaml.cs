using System.ComponentModel;
using System.Windows.Controls;

namespace Valdrio.Desktop;

public partial class ModelWorkspacePanel : UserControl
{
    private ModelWorkspaceViewModel? tableViewModel;

    public ModelWorkspacePanel()
    {
        InitializeComponent();
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
        NodeSummary.Columns[4].Header = tableViewModel.Text("Model.ColumnPool");
        NodeSummary.Columns[5].Header = tableViewModel.Text("Model.ColumnFunctions");
    }
}