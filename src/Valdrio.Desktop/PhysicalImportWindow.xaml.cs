using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;

namespace Valdrio.Desktop;

public partial class PhysicalImportWindow : Window
{
    private readonly PhysicalImportEditor editor;
    public PhysicalImportWindow(PhysicalActivityEditor host)
    {
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
    }

    private async void Browse(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog { Filter = "Physical data (*.csv;*.xlsx)|*.csv;*.xlsx",
            CheckFileExists = true, Multiselect = false };
        if (picker.ShowDialog(this) == true)
            await editor.LoadFile(picker.FileName);
    }
    private async void Preview(object sender, RoutedEventArgs e) => await editor.Preview();
    private void Apply(object sender, RoutedEventArgs e)
    {
        PreviewTable.CommitEdit(DataGridEditingUnit.Cell, true);
        PreviewTable.CommitEdit(DataGridEditingUnit.Row, true);
        if (editor.Apply()) DialogResult = true;
    }
    private void CloseWindow(object sender, RoutedEventArgs e) => Close();
}
