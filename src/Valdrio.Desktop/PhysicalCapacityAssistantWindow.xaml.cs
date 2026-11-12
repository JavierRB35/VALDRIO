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
            if (editor.Host.IsBusy) { args.Cancel = true; return; }
            if (WorkspaceHost is not null && (editor.Host.SelectedRow?.HasChanges == true || editor.HasPendingProposal) &&
                MessageBox.Show(WorkspaceHost, host.UnsavedBody, host.UnsavedTitle,
                    MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
                args.Cancel = true;
        };
        Closed += (_, _) => editor.Detach();
        ContentRendered += (_, _) =>
        {
            if (editor.CalendarVisible) DaysInput.Focus(); else NormalInput.Focus();
        };
    }
    private async void SaveActivityDraft(object sender, RoutedEventArgs e) => await SaveActivityAsync(false);
    private async void ConfirmActivity(object sender, RoutedEventArgs e) => await SaveActivityAsync(true);
    internal async Task SaveActivityAsync(bool confirm)
    {
        var saved = false;
        await RunActivityAsync(async () => saved = await editor.SaveAsync(confirm));
        if (confirm && saved) Close();
    }
    private void Preview(object sender, RoutedEventArgs e) => editor.Preview();
    private void Adopt(object sender, RoutedEventArgs e)
    {
        editor.Adopt();
        // Adoption updates the shared row; saving/confirming remains explicit here.
    }
    private void Cancel(object sender, RoutedEventArgs e) => Close();
}
