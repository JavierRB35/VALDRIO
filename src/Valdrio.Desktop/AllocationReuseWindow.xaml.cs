using System.Windows;
namespace Valdrio.Desktop;
public partial class AllocationReuseWindow : IntegratedWorkspaceWindow
{
    private readonly AllocationReuseEditor editor;
    public AllocationReuseWindow(AllocationReuseEditor editor)
    {
        InitializeComponent(); this.editor = editor; DataContext = editor;
        SelectorColumn.Header = editor.L("Reuse.Selector");
        KeyColumn.Header = editor.L("Reuse.Key"); StatusColumn.Header = editor.L("Activity.Status");
    }
    private void Preview(object sender, RoutedEventArgs e) => editor.Preview();
    private void Cancel(object sender, RoutedEventArgs e) => Close();
    private void Adopt(object sender, RoutedEventArgs e) { if (editor.Adopt()) DialogResult = true; }
}
