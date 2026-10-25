using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Valdrio.Desktop;

internal sealed class PhysicalHistoryPage : IntegratedWorkspaceWindow
{
    internal PhysicalHistoryPage(PhysicalActivityEditor editor)
    {
        Title = editor.HistoryLabel;
        var layout = new DockPanel { Margin = new Thickness(24) };
        var heading = new TextBlock { Text = editor.HistoryLabel + " · " + editor.SelectedContext,
            TextWrapping = TextWrapping.Wrap, FontSize = 22, Margin = new Thickness(0,0,0,12) };
        DockPanel.SetDock(heading, Dock.Top); layout.Children.Add(heading);
        var close = new Button { Content = editor.CloseLabel, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0,12,0,0) };
        close.Click += (_, _) => Close(); DockPanel.SetDock(close, Dock.Bottom); layout.Children.Add(close);
        var grid = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false,
            CanUserDeleteRows = false, SelectionMode = DataGridSelectionMode.Extended,
            ClipboardCopyMode = DataGridClipboardCopyMode.IncludeHeader, ItemsSource = editor.HistoryRows() };
        void Column(string key, string property, double width)
        {
            var style = new Style(typeof(TextBlock));
            style.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
            grid.Columns.Add(new DataGridTextColumn { Header = editor.Label("Activity." + key),
                Binding = new Binding(property), Width = width, ElementStyle = style });
        }
        Column("HistoryVersion", "Version", 85); Column("HistoryDate", "Recorded", 180);
        Column("Normal", "Normal", 130); Column("Real", "Real", 130); Column("Unit", "Unit", 90);
        Column("Status", "State", 140); Column("HistoryReason", "Reason", 350);
        layout.Children.Add(grid); Content = layout;
    }
}
