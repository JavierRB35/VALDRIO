using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;

namespace Valdrio.Desktop;

public partial class PhysicalImportWindow : IntegratedWorkspaceWindow
{
    private readonly PhysicalImportEditor editor;
    private bool applied;
    private readonly PhysicalActivityEditor activityHost;
    public PhysicalImportWindow(PhysicalActivityEditor host)
    {
        activityHost = host;
        InitializeComponent();
        editor = new PhysicalImportEditor(host);
        DataContext = editor;
        ChoiceColumn.Header = editor.ChooseLabel;
        NodeColumn.Header = editor.NodeLabel;
        UnitColumn.Header = editor.UnitLabel;
        NormalColumn.Header = editor.NormalLabel;
        RealColumn.Header = editor.RealLabel;
        ExistingColumn.Header = editor.ExistingLabel;
        WarningColumn.Header = editor.WarningLabel;
        Closing += (_, args) =>
        {
            if (editor.Busy) { args.Cancel = true; return; }
            if (WorkspaceHost is not null && !applied && editor.Lines.Any(line => line.Replace) &&
                new EconomicConfirmationWindow(host.UnsavedTitle, host.Label("Activity.Import.DiscardReview"),
                    host.Label("UX.DiscardLeave"), host.Label("UX.KeepEditing")) { Owner = WorkspaceHost }.ShowDialog() != true)
                args.Cancel = true;
        };
    }

    private void SaveTemplate(object sender, RoutedEventArgs e)
    {
        var picker = new SaveFileDialog { Filter = "CSV|*.csv", FileName = "VALDRIO-actividad-plantilla.csv" };
        if (picker.ShowDialog(WorkspaceHost ?? (Window)this) != true) return;
        try
        {
            System.IO.File.WriteAllText(picker.FileName, editor.CreateTemplate(), new System.Text.UTF8Encoding(true));
            editor.Feedback = activityHost.Label("Activity.Import.TemplateSaved");
        }
        catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException)
        { editor.Feedback = activityHost.Label("Activity.Import.TemplateFailed"); }
    }
    private async void Browse(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog { Filter = "Physical data (*.csv;*.xlsx)|*.csv;*.xlsx",
            CheckFileExists = true, Multiselect = false };
        if (picker.ShowDialog(WorkspaceHost ?? (Window)this) == true)
            await editor.LoadFile(picker.FileName);
    }
    private async void Preview(object sender, RoutedEventArgs e) => await editor.Preview();
    private void Apply(object sender, RoutedEventArgs e)
    {
        PreviewTable.CommitEdit(DataGridEditingUnit.Cell, true);
        PreviewTable.CommitEdit(DataGridEditingUnit.Row, true);
        if (editor.Apply()) { applied = true; DialogResult = true; }
    }
    private void CloseWindow(object sender, RoutedEventArgs e) => Close();
}
