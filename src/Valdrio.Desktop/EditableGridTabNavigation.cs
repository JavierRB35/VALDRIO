using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Valdrio.Desktop;

/// <summary>
/// UI-03: Tab and Shift+Tab traverse *actual* enabled editors inside DataGrid
/// templates, not the read-only informational cells between them.
/// This navigation never creates rows, writes data or executes commands.
/// </summary>
public static class EditableGridTabNavigation
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(EditableGridTabNavigation),
        new PropertyMetadata(false, OnEnabledChanged));

    private static readonly KeyEventHandler TabHandler = OnGridPreviewKeyDown;

    public static void SetEnabled(DependencyObject element, bool value) =>
        element.SetValue(EnabledProperty, value);

    public static bool GetEnabled(DependencyObject element) =>
        (bool)element.GetValue(EnabledProperty);

    private static void OnEnabledChanged(DependencyObject element, DependencyPropertyChangedEventArgs args)
    {
        if (element is not DataGrid grid) return;

        // DataGrid can already have handled Tab before an ordinary event subscription runs.
        // Listen to preview keys even when a WPF class handler marked them handled.
        grid.RemoveHandler(UIElement.PreviewKeyDownEvent, TabHandler);
        if (args.NewValue is true)
            grid.AddHandler(UIElement.PreviewKeyDownEvent, TabHandler, true);
    }

    private static void OnGridPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not DataGrid grid || e.Key != Key.Tab ||
            Keyboard.Modifiers is not (ModifierKeys.None or ModifierKeys.Shift))
            return;

        var source = e.OriginalSource as DependencyObject ??
                     Keyboard.FocusedElement as DependencyObject;
        var cell = Ancestor<DataGridCell>(source);
        if (cell is null || Ancestor<DataGrid>(cell) != grid)
            return;

        // Keep normal ComboBox keyboard interaction while a dropdown is open.
        if (Ancestor<ComboBox>(source) is { IsDropDownOpen: true })
            return;

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
            if (column is not DataGridTemplateColumn || column.Visibility != Visibility.Visible)
                continue;

            // A parent DataGrid can be IsReadOnly=True yet host independently editable
            // CheckBox/TextBox/ComboBox controls in CellTemplate. Therefore do not
            // filter by grid.IsReadOnly or column.IsReadOnly.
            var item = grid.Items[next.Row];
            if (item == System.Windows.Data.CollectionView.NewItemPlaceholder)
                continue;

            grid.ScrollIntoView(item, column);
            grid.UpdateLayout();

            var targetCell = GetCell(grid, item, column);
            var editor = targetCell is null ? null : FindEditable(targetCell);
            if (editor is null || !editor.Focus())
                continue;

            e.Handled = true;
            if (editor is TextBox box)
                box.SelectAll();
            return;
        }

        // At the edge of a grid let normal keyboard navigation exit it.
        // No wraparound to previously visited or read-only columns.
        if (grid.MoveFocus(new TraversalRequest(
                backwards ? FocusNavigationDirection.Previous : FocusNavigationDirection.Next)))
            e.Handled = true;
    }

    private static DataGridCell? GetCell(DataGrid grid, object item, DataGridColumn column)
    {
        if (grid.ItemContainerGenerator.ContainerFromItem(item) is not DataGridRow row)
            return null;

        row.ApplyTemplate();
        var presenter = Descendant<DataGridCellsPresenter>(row);
        presenter?.ApplyTemplate();

        var columnIndex = grid.Columns.IndexOf(column);
        if (columnIndex < 0) return null;

        var generated = presenter?.ItemContainerGenerator.ContainerFromIndex(columnIndex) as DataGridCell;
        return generated?.Column == column
            ? generated
            : presenter is null ? null : FindCell(presenter, column);
    }

    private static DataGridCell? FindCell(DependencyObject root, DataGridColumn column)
    {
        if (root is DataGridCell cell && cell.Column == column)
            return cell;

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            if (FindCell(VisualTreeHelper.GetChild(root, i), column) is { } found)
                return found;
        }
        return null;
    }

    private static UIElement? FindEditable(DependencyObject root)
    {
        if (root is FrameworkElement { IsVisible: false } ||
            root is UIElement { IsEnabled: false })
            return null;

        if (root is TextBox { IsReadOnly: false, IsTabStop: true } text)
            return text;
        if (root is ComboBox { IsTabStop: true } combo)
            return combo;
        if (root is CheckBox { IsTabStop: true } check)
            return check;

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
