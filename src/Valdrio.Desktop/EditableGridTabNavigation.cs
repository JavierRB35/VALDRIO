using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Valdrio.Desktop;

/// <summary>
/// UI-03: Tab through live, enabled editors hosted in DataGridTemplateColumn cells.
/// Applied only to grids whose templates contain edit controls, not read-only
/// analytical grids that users browse using arrow keys and screen readers.
/// </summary>
public static class EditableGridTabNavigation
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(EditableGridTabNavigation),
        new PropertyMetadata(false, OnEnabledChanged));

    public static void SetEnabled(DependencyObject element, bool value) =>
        element.SetValue(EnabledProperty, value);

    public static bool GetEnabled(DependencyObject element) =>
        (bool)element.GetValue(EnabledProperty);

    private static void OnEnabledChanged(DependencyObject element, DependencyPropertyChangedEventArgs args)
    {
        if (element is not DataGrid grid) return;
        grid.PreviewKeyDown -= OnGridPreviewKeyDown;
        if (args.NewValue is true) grid.PreviewKeyDown += OnGridPreviewKeyDown;
    }

    private static void OnGridPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not DataGrid grid || e.Key != Key.Tab ||
            Keyboard.Modifiers is not (ModifierKeys.None or ModifierKeys.Shift) ||
            Keyboard.FocusedElement is not DependencyObject focused)
            return;

        var cell = Ancestor<DataGridCell>(focused);
        if (cell is null || Ancestor<DataGrid>(cell) != grid) return;

        // A dropdown owns its keyboard interaction until it has closed.
        if (Ancestor<ComboBox>(focused) is { IsDropDownOpen: true }) return;

        var row = Ancestor<DataGridRow>(cell);
        if (row is null) return;
        var currentRow = grid.Items.IndexOf(row.Item);
        if (currentRow < 0) return;
        var backwards = Keyboard.Modifiers == ModifierKeys.Shift;
        foreach (var next in EditableGridTabOrder.After(
                     grid.Items.Count, grid.Columns.Count, currentRow,
                     cell.Column.DisplayIndex, backwards))
        {
            var column = grid.Columns.FirstOrDefault(c => c.DisplayIndex == next.Column);
            if (column is not DataGridTemplateColumn || column.Visibility != Visibility.Visible ||
                column.IsReadOnly)
                continue;

            var item = grid.Items[next.Row];
            if (item == System.Windows.Data.CollectionView.NewItemPlaceholder) continue;

            grid.ScrollIntoView(item, column);
            grid.UpdateLayout();
            var candidate = GetCell(grid, item, column);
            var editor = candidate is null ? null : FindEditable(candidate);
            if (editor?.Focus() == true)
            {
                e.Handled = true;
                return;
            }
        }

        // Leave the grid at its edge rather than trapping focus on read-only cells.
        var direction = backwards ? FocusNavigationDirection.Previous : FocusNavigationDirection.Next;
        if (grid.MoveFocus(new TraversalRequest(direction))) e.Handled = true;
    }

    private static DataGridCell? GetCell(DataGrid grid, object item, DataGridColumn column)
    {
        if (grid.ItemContainerGenerator.ContainerFromItem(item) is not DataGridRow row)
            return null;
        row.ApplyTemplate();
        var presenter = Descendant<DataGridCellsPresenter>(row);
        presenter?.ApplyTemplate();
        return presenter?.ItemContainerGenerator.ContainerFromIndex(grid.Columns.IndexOf(column)) as DataGridCell;
    }

    private static UIElement? FindEditable(DependencyObject root)
    {
        if (root is FrameworkElement { IsVisible: false } ||
            root is UIElement { IsEnabled: false })
            return null;
        if (root is ComboBox { IsTabStop: true } combo) return combo;
        if (root is CheckBox { IsTabStop: true } check) return check;
        if (root is TextBox { IsReadOnly: false, IsTabStop: true } text) return text;

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            if (FindEditable(VisualTreeHelper.GetChild(root, i)) is { } editor)
                return editor;
        }
        return null;
    }

    private static T? Descendant<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) return match;
            if (Descendant<T>(child) is { } nested) return nested;
        }
        return null;
    }

    private static T? Ancestor<T>(DependencyObject? root) where T : DependencyObject
    {
        while (root is not null)
        {
            if (root is T found) return found;
            root = VisualTreeHelper.GetParent(root);
        }
        return null;
    }
}
