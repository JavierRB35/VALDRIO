using System.Windows;

namespace Valdrio.Desktop;

public partial class MainWindow
{
    private readonly List<IntegratedWorkspaceWindow> workspacePages = [];
    internal void PushWorkspace(IntegratedWorkspaceWindow page)
    {
        workspacePages.Add(page);
        WorkspaceContent.Content = page.Surface;
        WorkspaceContent.Visibility = Visibility.Visible;
        RootScrollViewer.Visibility = Visibility.Collapsed;
        UpdateWorkflow();
        page.Surface!.MoveFocus(new(System.Windows.Input.FocusNavigationDirection.First));
    }
    internal void PopWorkspace(IntegratedWorkspaceWindow page)
    {
        if (workspacePages.Count == 0 || workspacePages[^1] != page || !page.CanLeave()) return;
        workspacePages.RemoveAt(workspacePages.Count - 1);
        WorkspaceContent.Content = workspacePages.LastOrDefault()?.Surface;
        WorkspaceContent.Visibility = workspacePages.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        RootScrollViewer.Visibility = workspacePages.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        var returned = page.Returned;
        page.Release();
        returned?.Invoke();
        UpdateWorkflow();
    }
    internal bool LeaveIntegratedWorkspace()
    {
        // Check every draft before removing any page, so Cancel retains the stack.
        foreach (var page in workspacePages.AsEnumerable().Reverse())
            if (!page.CanLeave()) return false;
        WorkspaceContent.Content = null;
        foreach (var page in workspacePages.AsEnumerable().Reverse()) page.Release();
        workspacePages.Clear();
        WorkspaceContent.Visibility = Visibility.Collapsed;
        RootScrollViewer.Visibility = Visibility.Visible;
        return true;
    }
}
