using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using Valdrio.Domain;

namespace Valdrio.Desktop;

public partial class PhysicalActivityWindow : Window
{
    private readonly PhysicalActivityEditor editor;
    private bool suppressStudyChange;

    public PhysicalActivityWindow(StudyWorkspaceViewModel study)
    {
        InitializeComponent();
        editor = new PhysicalActivityEditor(study);
        DataContext = editor;
        SetStudyChoice();
        Closing += GuardUnsavedOnClose;
        NodeColumn.Header = editor.NodeLabel;
        UnitColumn.Header = editor.UnitLabel;
        NormalColumn.Header = editor.NormalLabel;
        RealColumn.Header = editor.RealLabel;
        UtilizationColumn.Header = editor.UtilizationLabel;
        CostColumn.Header = editor.CostLabel;
        StatusColumn.Header = editor.StatusLabel;
    }

    private void SetStudyChoice()
    {
        suppressStudyChange = true;
        try { StudySelector.SelectedItem = editor.CurrentStudyChoice; }
        finally { suppressStudyChange = false; }
    }

    private void StudySelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppressStudyChange || editor is null || StudySelector.SelectedItem is not PhysicalStudyChoice choice)
            return;
        if (choice.Value.Id == editor.Study.Id) return;
        CommitTableEdit();
        if (editor.HasPendingEdits &&
            MessageBox.Show(this, editor.UnsavedBody, editor.UnsavedTitle,
                MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
        {
            SetStudyChoice();
            return;
        }
        try
        {
            editor.ChangeStudy(choice.Value.Id, discardPendingEdits: true);
            SetStudyChoice();
        }
        catch (DomainRuleException error)
        {
            MessageBox.Show(this, editor.Label(error.Diagnostic.MessageKey), editor.Title,
                MessageBoxButton.OK, MessageBoxImage.Warning);
            SetStudyChoice();
        }
        catch (Exception error)
        {
            System.Diagnostics.Trace.TraceError("T14 switch study: {0}", error);
            MessageBox.Show(this, editor.Label("Activity.SwitchFailed"), editor.Title,
                MessageBoxButton.OK, MessageBoxImage.Warning);
            SetStudyChoice();
        }
    }

    private void GuardUnsavedOnClose(object? sender, CancelEventArgs e)
    {
        CommitTableEdit();
        if (editor.HasPendingEdits &&
            MessageBox.Show(this, editor.UnsavedBody, editor.UnsavedTitle,
                MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
            e.Cancel = true;
    }

    private void CommitTableEdit()
    {
        ActivityTable.CommitEdit(DataGridEditingUnit.Cell, true);
        ActivityTable.CommitEdit(DataGridEditingUnit.Row, true);
    }

    private void AssistNormal(object sender, RoutedEventArgs e)
    {
        CommitTableEdit();
        if (sender is FrameworkElement { DataContext: PhysicalActivityRow row }) editor.SelectedRow = row;
        if (editor.SelectedRow is not null)
            new PhysicalCapacityAssistantWindow(editor) { Owner = this }.ShowDialog();
    }

    private void OpenPhysicalImport(object sender, RoutedEventArgs e)
    {
        CommitTableEdit();
        if (editor.HasPendingEdits)
        {
            MessageBox.Show(this, editor.OtherRowsPendingLabel, editor.UnsavedTitle,
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        new PhysicalImportWindow(editor) { Owner = this }.ShowDialog();
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
