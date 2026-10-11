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
            var source = e.OriginalSource as DependencyObject;
            // An open drop-down retains its native navigation. A closed one must
            // not silently change an accounting choice when scrolling the page.
            if (Ancestor<ComboBox>(source) is { IsDropDownOpen: true }) return;
            var page = Ancestor<ScrollViewer>(source);
            while (page is not null)
            {
                if (Ancestor<DataGrid>(page) is { } owner)
                    page = Ancestor<ScrollViewer>(Parent(owner));
                else if (Ancestor<TextBoxBase>(page) is { } editor)
                    page = Ancestor<ScrollViewer>(Parent(editor));
                else break;
            }
            if (page is null) return;
            var target = active is { IsVisible: true, IsEnabled: true } &&
                Ancestor<ScrollViewer>(Parent(active)) == page ? Descendant<ScrollViewer>(active) ?? page : page;
            var lines = SystemParameters.WheelScrollLines;
            var distance = lines < 0 ? target.ViewportHeight : lines * 16d;
            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
                target.ScrollToHorizontalOffset(Math.Clamp(target.HorizontalOffset - e.Delta / 120d * distance, 0, target.ScrollableWidth));
            else
                target.ScrollToVerticalOffset(Math.Clamp(target.VerticalOffset - e.Delta / 120d * distance, 0, target.ScrollableHeight));
            e.Handled = true;
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
