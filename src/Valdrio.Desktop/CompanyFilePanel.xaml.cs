using System.Windows.Controls;
namespace Valdrio.Desktop;
public partial class CompanyFilePanel : UserControl
{
    public CompanyFilePanel() => InitializeComponent();
    private void StartImportSetup(object sender, System.Windows.RoutedEventArgs e)
    {
        NameBox.BringIntoView();
        NameBox.Focus();
        if (DataContext is CompanyFileViewModel vm && System.Windows.Window.GetWindow(this) is MainWindow window)
            window.FieldHelpText.Text = vm.ImportStartHelp;
    }
}
