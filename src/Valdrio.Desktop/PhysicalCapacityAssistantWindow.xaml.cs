using System.Windows;

namespace Valdrio.Desktop;

public partial class PhysicalCapacityAssistantWindow : IntegratedWorkspaceWindow
{
    private readonly PhysicalCapacityAssistantEditor editor;
    public bool Adopted => editor.Adopted;
    public PhysicalCapacityAssistantWindow(PhysicalActivityEditor host)
    {
        InitializeComponent();
        editor = new PhysicalCapacityAssistantEditor(host);
        DataContext = editor;
        Closing += (_, args) =>
        {
            if (WorkspaceHost is not null && !editor.Adopted && editor.HasPendingProposal &&
                MessageBox.Show(WorkspaceHost, host.UnsavedBody, host.UnsavedTitle,
                    MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
                args.Cancel = true;
        };
        ContentRendered += (_, _) =>
        {
            var first = editor.CalendarVisible ? DaysInput :
                string.IsNullOrWhiteSpace(editor.DeclaredQuantity) ? DeclaredQuantityInput : DeclaredSourceInput;
            first.Focus();
        };
    }
    private void SaveActivityDraft(object sender, RoutedEventArgs e) => editor.Host.Save(false);
    private void ConfirmActivity(object sender, RoutedEventArgs e) => editor.Host.Save(true);
    private void Preview(object sender, RoutedEventArgs e) => editor.Preview();
    private void Adopt(object sender, RoutedEventArgs e)
    {
        editor.Adopt();
        // Adoption updates the shared row; saving/confirming remains explicit here.
    }
    private void Cancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
