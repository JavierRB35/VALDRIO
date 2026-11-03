using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Valdrio.Desktop;

// Keep the actual controls (commands, names, handlers and focus) in the shell footer.
// A zero-height anchor retains the page/tab's context, visibility and enabled state.
public static class FixedPageActions
{
    public static readonly DependencyProperty PinProperty = DependencyProperty.RegisterAttached(
        "Pin", typeof(bool), typeof(FixedPageActions), new PropertyMetadata(false, Changed));
    private static readonly DependencyProperty MountedProperty = DependencyProperty.RegisterAttached(
        "Mounted", typeof(bool), typeof(FixedPageActions), new PropertyMetadata(false));
    public static bool GetPin(DependencyObject node) => (bool)node.GetValue(PinProperty);
    public static void SetPin(DependencyObject node, bool value) => node.SetValue(PinProperty, value);
    private static void Changed(DependencyObject node, DependencyPropertyChangedEventArgs args)
    {
        if (node is not FrameworkElement element) return;
        element.Loaded -= Mount;
        if ((bool)args.NewValue) element.Loaded += Mount;
    }
    private static void Mount(object sender, RoutedEventArgs args)
    {
        var element = (FrameworkElement)sender;
        if ((bool)element.GetValue(MountedProperty) || element.Parent is not Panel parent ||
            Window.GetWindow(element) is not MainWindow main) return;
        element.SetValue(MountedProperty, true);
        var index = parent.Children.IndexOf(element);
        var anchor = new Border { Height = 0 };
        var wrapper = new ContentControl { HorizontalContentAlignment = HorizontalAlignment.Right };
        wrapper.SetBinding(FrameworkElement.DataContextProperty, new Binding("DataContext") { Source = anchor });
        wrapper.SetBinding(UIElement.IsEnabledProperty, new Binding("IsEnabled") { Source = anchor });
        parent.Children.RemoveAt(index);
        parent.Children.Insert(index, anchor);
        wrapper.Content = element;
        main.FixedPageActionsHost.Children.Add(wrapper);
        void Visibility() => wrapper.Visibility = anchor.IsVisible ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        anchor.IsVisibleChanged += (_, _) => Visibility();
        anchor.Loaded += (_, _) => Visibility();
        anchor.Unloaded += (_, _) =>
        {
            // Restore on tab/page departure, so reopening does not retain old controls.
            main.FixedPageActionsHost.Children.Remove(wrapper);
            wrapper.Content = null;
            var position = parent.Children.IndexOf(anchor);
            if (position >= 0)
            {
                parent.Children.RemoveAt(position);
                parent.Children.Insert(position, element);
            }
            element.SetValue(MountedProperty, false);
        };
        Visibility();
    }
}
