using System.Windows;
using System.Windows.Controls;

namespace Valdrio.Desktop;

internal static class GuidedUi
{
    internal static bool Contains(DependencyObject root, DependencyObject target)
    {
        if (root == target) return true;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            if (Contains(child, target)) return true;
        return false;
    }
    internal static void Reveal(FrameworkElement element)
    {
        if (System.Windows.Application.Current.MainWindow is not MainWindow main) return;
        var import = main.OwnedWindows.OfType<ImportColumnMappingWindow>()
            .FirstOrDefault(w => Contains(w, element));
        if (import is not null) import.RevealImportElement(element);
        else main.RevealWorkflowElement(element);
        DependencyObject? parent = element;
        while (parent is not null)
        {
            if (parent is Expander expander) expander.IsExpanded = true;
            parent = LogicalTreeHelper.GetParent(parent);
        }
        element.UpdateLayout();
    }
}
