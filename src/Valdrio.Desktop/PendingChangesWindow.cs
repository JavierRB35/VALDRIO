using System.Windows;
using System.Windows.Controls;

namespace Valdrio.Desktop;

/// <summary>Explicit outcomes; Escape, close and initial focus always keep editing.</summary>
internal sealed class PendingChangesWindow : Window
{
    internal PendingEditChoice Choice { get; private set; } = PendingEditChoice.Cancel;
    internal Button KeepEditingButton { get; }
    internal PendingChangesWindow(string title, string body, string save, string discard, string keep)
    {
        Title = title; Width = 720; Height = 400; MinWidth = 400; MinHeight = 260;
        Owner = System.Windows.Application.Current.MainWindow;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false;
        SetResourceReference(BackgroundProperty, "Surface.Canvas");
        var layout = new DockPanel { Margin = new Thickness(24) };
        var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0,16,0,0) };
        void Add(string label, PendingEditChoice choice)
        {
            var button = new Button { Content = label, Margin = new Thickness(4), Padding = new Thickness(12,8,12,8) };
            button.Click += (_, _) => { Choice = choice; DialogResult = true; };
            actions.Children.Add(button);
        }
        Add(save, PendingEditChoice.Save); Add(discard, PendingEditChoice.Discard);
        KeepEditingButton = new Button { Content = keep, IsCancel = true, Margin = new Thickness(4) };
        actions.Children.Add(KeepEditingButton);
        DockPanel.SetDock(actions, Dock.Bottom); layout.Children.Add(actions);
        layout.Children.Add(new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new TextBox { Text = body, IsReadOnly = true, TextWrapping = TextWrapping.Wrap,
                BorderThickness = new Thickness(0) } });
        Content = layout; Loaded += (_, _) => KeepEditingButton.Focus();
    }
}
