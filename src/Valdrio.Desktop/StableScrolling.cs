using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Valdrio.Desktop;

/// <summary>The wheel belongs to the page until the user enters a table.
/// Reaching a table boundary never transfers the gesture to the page.</summary>
public static class StableScrolling
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(StableScrolling), new PropertyMetadata(false, Changed));
    public static void SetEnabled(DependencyObject target, bool value) => target.SetValue(EnabledProperty, value);
    public static bool GetEnabled(DependencyObject target) => (bool)target.GetValue(EnabledProperty);
    private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached(
        "State", typeof(State), typeof(StableScrolling));
    private static void Changed(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not Window window) return;
        if (window.GetValue(StateProperty) is State previous) previous.Detach();
        window.SetValue(StateProperty, (bool)args.NewValue ? new State(window) : null);
    }
    private sealed class State
    {
        private readonly Window window;
        private DataGrid? active;
        internal State(Window window)
        {
            this.window = window;
            window.PreviewMouseDown += MouseDown;
            window.PreviewGotKeyboardFocus += Focus;
            window.PreviewMouseWheel += Wheel;
        }
        internal void Detach()
        {
            window.PreviewMouseDown -= MouseDown;
            window.PreviewGotKeyboardFocus -= Focus;
            window.PreviewMouseWheel -= Wheel;
        }
        private void MouseDown(object sender, MouseButtonEventArgs e) => active = Ancestor<DataGrid>(e.OriginalSource as DependencyObject);
        private void Focus(object sender, KeyboardFocusChangedEventArgs e) => active = Ancestor<DataGrid>(e.NewFocus as DependencyObject);
        private void Wheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Handled || e.Delta == 0) return;
            var source = e.OriginalSource as DependencyObject;
            if (source is null) return;
            // Open pickers own their wheel. Closed pickers must not change values on hover.
            if (Ancestor<ComboBox>(source) is { IsDropDownOpen: true }) return;

            var horizontal = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            var page = PageScrollHost(source, horizontal);
            if (page is null) return;

            // A table is allowed to capture the wheel only when the pointer is actually
            // over THAT table. Keyboard focus or a previous click elsewhere on the page
            // must never divert the wheel into a different, off-pointer table.
            var hoveredGrid = Ancestor<DataGrid>(source);
            var target = hoveredGrid is { IsVisible: true, IsEnabled: true }
                && ReferenceEquals(active, hoveredGrid)
                && ReferenceEquals(PageScrollHost(Parent(hoveredGrid), horizontal), page)
                    ? Descendant<ScrollViewer>(hoveredGrid) ?? page
                    : page;

            var lines = SystemParameters.WheelScrollLines;
            var distance = lines < 0 ? target.ViewportHeight : lines * 16d;
            var increment = e.Delta / 120d * distance;
            if (horizontal)
                target.ScrollToHorizontalOffset(Math.Clamp(target.HorizontalOffset - increment, 0, target.ScrollableWidth));
            else
                target.ScrollToVerticalOffset(Math.Clamp(target.VerticalOffset - increment, 0, target.ScrollableHeight));
            // Consume once: a grid at its boundary cannot unexpectedly move the page.
            e.Handled = true;
        }

        private static ScrollViewer? PageScrollHost(DependencyObject? source, bool horizontal)
        {
            var viewer = Ancestor<ScrollViewer>(source);
            while (viewer is not null)
            {
                // A DataGrid has a private viewer and so does a TextBox. Neither
                // should become the page host merely because the pointer crosses it.
                if (Ancestor<DataGrid>(viewer) is { } grid)
                {
                    viewer = Ancestor<ScrollViewer>(Parent(grid));
                    continue;
                }
                if (Ancestor<TextBoxBase>(viewer) is { } editor)
                {
                    viewer = Ancestor<ScrollViewer>(Parent(editor));
                    continue;
                }

                // Results graphs scroll horizontally only. A vertical wheel over
                // their canvas must scroll the containing page, not disappear.
                var visibility = horizontal ? viewer.HorizontalScrollBarVisibility : viewer.VerticalScrollBarVisibility;
                // WPF templates add ScrollViewers even when they have no scroll range
                // (TreeView, tabs, etc.). Such a viewer must not swallow the page wheel.
                var range = horizontal ? viewer.ScrollableWidth : viewer.ScrollableHeight;
                if (visibility != ScrollBarVisibility.Disabled && range > 0)
                    return viewer;
                viewer = Ancestor<ScrollViewer>(Parent(viewer));
            }
            return null;
        }
    }
    private static DependencyObject? Parent(DependencyObject value) => value is Visual or Visual3D
        ? VisualTreeHelper.GetParent(value) : LogicalTreeHelper.GetParent(value);
    private static T? Ancestor<T>(DependencyObject? value) where T : DependencyObject
    {
        while (value is not null) { if (value is T match) return match; value = Parent(value); }
        return null;
    }
    private static T? Descendant<T>(DependencyObject value) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(value); i++)
        {
            var child = VisualTreeHelper.GetChild(value, i);
            if (child is T match) return match;
            if (Descendant<T>(child) is { } nested) return nested;
        }
        return null;
    }
}
