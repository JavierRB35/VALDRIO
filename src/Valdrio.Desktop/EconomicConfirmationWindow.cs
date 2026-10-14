using System.Windows;
using System.Windows.Controls;

namespace Valdrio.Desktop;

internal sealed class EconomicConfirmationWindow : Window
{
    internal Button AcceptButton { get; }
    internal Button CancelButton { get; }
    public EconomicConfirmationWindow(string title, string body, string accept, string cancel)
    {
        Title = title; Width = 680; Height = 460; MinWidth = 400; MinHeight = 300;
        Owner = System.Windows.Application.Current.MainWindow;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        SetResourceReference(BackgroundProperty, "Surface.Canvas");
        var layout = new DockPanel { Margin = new Thickness(24) };
        var heading = new TextBlock { Text = title, FontSize = 20, FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 16) };
        DockPanel.SetDock(heading, Dock.Top); layout.Children.Add(heading);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0) };
        AcceptButton = new Button { Content = accept, MinWidth = 110 };
        AcceptButton.SetResourceReference(StyleProperty, "Button.Primary");
        CancelButton = new Button { Content = cancel, IsCancel = true, MinWidth = 110 };
        AcceptButton.Click += (_, _) => DialogResult = true;
        actions.Children.Add(AcceptButton); actions.Children.Add(CancelButton);
        DockPanel.SetDock(actions, Dock.Bottom); layout.Children.Add(actions);
        layout.Children.Add(new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new TextBlock { Text = body, TextWrapping = TextWrapping.Wrap } });
        Content = layout;
        Loaded += (_, _) => CancelButton.Focus();
    }
}
