using System.ComponentModel;
using System.Windows;

namespace Valdrio.Desktop;

// Reuse the existing XAML, namescopes and handlers as an embedded surface.
// A working page never calls Show/ShowDialog, so no native window is created.
// Window compatibility is retained for the existing isolated visual test harnesses.
public class IntegratedWorkspaceWindow : Window
{
    internal MainWindow? WorkspaceHost { get; private set; }
    internal FrameworkElement? Surface { get; private set; }
    internal Action? Returned { get; private set; }
    public void ShowIntegrated(Action? returned = null)
    {
        var main = Owner as MainWindow ?? (Owner as IntegratedWorkspaceWindow)?.WorkspaceHost
            ?? System.Windows.Application.Current.MainWindow as MainWindow
            ?? throw new InvalidOperationException("A working page requires the main workspace.");
        WorkspaceHost = main;
        Returned = returned;
        Surface = (FrameworkElement)Content;
        Content = null;
        Surface.DataContext = DataContext;
        Surface.Resources.MergedDictionaries.Add(Resources);
        DisableNativeCancel(Surface);
        Surface.Loaded += SurfaceLoaded;
        Surface.KeyDown += (_, args) =>
        {
            if (args.Key != System.Windows.Input.Key.Escape) return;
            args.Handled = true;
            Close();
        };
        main.PushWorkspace(this);
    }
    private static void DisableNativeCancel(DependencyObject node)
    {
        // IsCancel routes to the containing Window; here Escape belongs to the page.
        if (node is System.Windows.Controls.Button button) button.IsCancel = false;
        foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())
            DisableNativeCancel(child);
    }
    private void SurfaceLoaded(object sender, RoutedEventArgs args)
    {
        ((FrameworkElement)sender).Loaded -= SurfaceLoaded;
        OnContentRendered(EventArgs.Empty);
    }
    internal bool CanLeave()
    {
        var args = new CancelEventArgs();
        OnClosing(args);
        return !args.Cancel;
    }
    public new void Close()
    {
        if (WorkspaceHost is { } main) main.PopWorkspace(this);
        else base.Close();
    }
    public new bool? DialogResult
    {
        get => WorkspaceHost is null ? base.DialogResult : embeddedResult;
        set
        {
            if (WorkspaceHost is null) base.DialogResult = value;
            else { embeddedResult = value; Close(); }
        }
    }
    private bool? embeddedResult;
    private bool releasing;
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!releasing) base.OnClosing(e);
    }
    internal void Release()
    {
        releasing = true;
        Surface = null;
        Returned = null;
        base.Close();
    }
}
