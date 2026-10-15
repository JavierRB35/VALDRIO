using System.Windows;
using System.Windows.Controls;

namespace Valdrio.Desktop;

public partial class PhysicalActivityWindow : Window
{
    private readonly PhysicalActivityEditor editor;

    public PhysicalActivityWindow(StudyWorkspaceViewModel study)
    {
        InitializeComponent();
        editor = new PhysicalActivityEditor(study);
        DataContext = editor;
        NodeColumn.Header = editor.NodeLabel;
        UnitColumn.Header = editor.UnitLabel;
        NormalColumn.Header = editor.NormalLabel;
        RealColumn.Header = editor.RealLabel;
        UtilizationColumn.Header = editor.UtilizationLabel;
        CostColumn.Header = editor.CostLabel;
        StatusColumn.Header = editor.StatusLabel;
    }

    private void CommitTableEdit()
    {
        ActivityTable.CommitEdit(DataGridEditingUnit.Cell, true);
        ActivityTable.CommitEdit(DataGridEditingUnit.Row, true);
    }

    private void AssistNormal(object sender, RoutedEventArgs e)
    {
        CommitTableEdit();
        if (editor.SelectedRow is not null)
            new PhysicalCapacityAssistantWindow(editor) { Owner = this }.ShowDialog();
    }

    private void SaveDraft(object sender, RoutedEventArgs e)
    {
        CommitTableEdit();
        editor.Save(confirm: false);
    }
    private void Confirm(object sender, RoutedEventArgs e)
    {
        CommitTableEdit();
        editor.Save(confirm: true);
    }
    private void Annul(object sender, RoutedEventArgs e)
    {
        CommitTableEdit();
        editor.Annul();
    }
    private void History(object sender, RoutedEventArgs e) =>
        MessageBox.Show(this, editor.History(), editor.HistoryLabel, MessageBoxButton.OK, MessageBoxImage.Information);
    private void CloseWindow(object sender, RoutedEventArgs e) => Close();
}
