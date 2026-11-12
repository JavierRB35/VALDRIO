using System.Windows;
namespace Valdrio.Desktop;
public partial class AllocationReuseWindow : IntegratedWorkspaceWindow
{
    private readonly AllocationReuseEditor editor;
    public AllocationReuseWindow(AllocationReuseEditor editor)
    {
        InitializeComponent(); this.editor = editor; DataContext = editor;
        ContentRendered += async (_, _) => await InitializeAsync();
        SelectorColumn.Header = editor.L("Reuse.Selector");
        KeyColumn.Header = editor.L("Reuse.Key"); StatusColumn.Header = editor.L("Activity.Status");
    }
    private Task? initialization;
    internal override Task InitializeAsync() => initialization ??= RunActivityAsync(editor.LoadAsync);
    private async void Preview(object sender, RoutedEventArgs e) => await RunActivityAsync(editor.PreviewAsync);
    private void Cancel(object sender, RoutedEventArgs e) => Close();
    private async void Adopt(object sender, RoutedEventArgs e)
    {
        var saved = false;
        await RunActivityAsync(async () => saved = await editor.AdoptAsync());
        if (saved) DialogResult = true;
    }
}
