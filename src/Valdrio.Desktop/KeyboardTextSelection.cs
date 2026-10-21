using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Valdrio.Desktop;

/// <summary>
/// Select the full value when keyboard navigation enters an editable TextBox,
/// so typing replaces it without clicking or selecting manually.
/// Mouse entry retains the standard caret-positioning behaviour.
/// </summary>
public static class KeyboardTextSelection
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(KeyboardTextSelection),
        new PropertyMetadata(false, Changed));

    public static void SetEnabled(DependencyObject target, bool value) =>
        target.SetValue(EnabledProperty, value);

    public static bool GetEnabled(DependencyObject target) =>
        (bool)target.GetValue(EnabledProperty);

    private static void Changed(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is not TextBox box) return;

        box.GotKeyboardFocus -= OnGotKeyboardFocus;
        if (e.NewValue is true)
            box.GotKeyboardFocus += OnGotKeyboardFocus;
    }

    private static void OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is not TextBox box || !box.IsEnabled || box.IsReadOnly ||
            InputManager.Current.MostRecentInputDevice is not KeyboardDevice)
            return;

        // Run after WPF's own focus/caret processing. Do not select text if the
        // user has moved focus elsewhere before this dispatcher operation runs.
        box.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            if (box.IsKeyboardFocusWithin && box.IsEnabled && !box.IsReadOnly)
                box.SelectAll();
        }));
    }
}
