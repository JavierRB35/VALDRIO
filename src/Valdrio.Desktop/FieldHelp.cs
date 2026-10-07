using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace Valdrio.Desktop;

/// <summary>One explanation for hover, keyboard focus and assistive technology.</summary>
public static class FieldHelp
{
    public static readonly DependencyProperty KeyProperty = DependencyProperty.RegisterAttached(
        "Key", typeof(string), typeof(FieldHelp), new PropertyMetadata(null, Attach));
    public static void SetKey(DependencyObject target, string value) => target.SetValue(KeyProperty, value);
    public static string? GetKey(DependencyObject target) => (string?)target.GetValue(KeyProperty);
    private static string Text(FrameworkElement element) => element.DataContext switch
    {
        MainWindowViewModel vm => vm.Guide("Help." + GetKey(element)),
        CompanyFileViewModel vm => vm.Guide("Help." + GetKey(element)),
        ModelWorkspaceViewModel vm => vm.Guide("Help." + GetKey(element)),
        StudyWorkspaceViewModel vm => vm.Guide("Help." + GetKey(element)),
        ResourceWorkspaceViewModel vm => vm.Guide("Help." + GetKey(element)),
        ImportColumnMappingViewModel vm => vm.Guide("Help." + GetKey(element)),
        _ => (Window.GetWindow(element)?.DataContext as MainWindowViewModel)?.Guide("Help." + GetKey(element)) ?? ""
    };
    private static void Attach(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not FrameworkElement element || args.OldValue is not null) return;
        INotifyPropertyChanged? observed = null;
        void Refresh(object? sender = null, PropertyChangedEventArgs? e = null)
        {
            var text = Text(element);
            element.ToolTip = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, MaxWidth = 420 };
            AutomationProperties.SetHelpText(element, text);
            if (element.IsKeyboardFocusWithin) Show();
        }
        void Bind()
        {
            if (observed is not null) observed.PropertyChanged -= Refresh;
            observed = element.DataContext as INotifyPropertyChanged;
            if (observed is not null) observed.PropertyChanged += Refresh;
            Refresh();
        }
        element.Loaded += (_, _) => Bind();
        element.Unloaded += (_, _) => { if (observed is not null) observed.PropertyChanged -= Refresh; observed = null; };
        element.DataContextChanged += (_, _) => { if (element.IsLoaded) Bind(); };
        ToolTipService.SetShowOnDisabled(element, true);
        ToolTipService.SetShowDuration(element, 60000);
        element.GotKeyboardFocus += (_, _) => Show();
        element.MouseEnter += (_, _) => Show();
        void Show()
        {
            var text = Text(element);
            switch (Window.GetWindow(element))
            {
                case MainWindow main: main.ShowFieldHelp(text, GetKey(element)); break;
                case ImportColumnMappingWindow import: import.ShowFieldHelp(text, GetKey(element)); break;
            }
        }
    }
}
