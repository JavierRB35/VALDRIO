using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Valdrio.Desktop;

// Interactive source-centre selection is owned by the bound WPF table.
// This read-only graphic renders the exact same displayed contributions.
public partial class AllocationWorkspacePanel
{
    private void PrincipalFlowChanged(object? sender, EventArgs e) => RedrawPrincipalCostFlow();

    private void RedrawPrincipalCostFlow()
    {
        if (PrincipalFlowCanvas is null) return;
        PrincipalFlowCanvas.Children.Clear();
        if (!secondaryOnly || viewModel is not { SelectedPrincipalCostRow: { } centre } vm) return;
        var lines = vm.PrincipalOriginRows.ToArray();
        if (lines.Length == 0) return;

        var accounts = lines.GroupBy(line => line.Account).OrderBy(group => group.Key, StringComparer.CurrentCulture).ToArray();
        var primary = lines.GroupBy(line => line.PrimaryNode).OrderBy(group => group.Key, StringComparer.CurrentCulture).ToArray();
        var height = Math.Max(330, Math.Max(accounts.Length, primary.Length) * 112 + 100);
        PrincipalFlowCanvas.Width = 1155;
        PrincipalFlowCanvas.Height = height;
        var ink = TryFindResource("Brand.DeepGreen") as Brush ?? Brushes.DarkGreen;
        var muted = TryFindResource("Text.Primary") as Brush ?? Brushes.Black;
        var panel = TryFindResource("Surface.Subtle") as Brush ?? Brushes.WhiteSmoke;
        var accent = TryFindResource("Status.SuccessBg") as Brush ?? Brushes.Honeydew;
        AddHeader(vm["PrincipalFlowAccounts"], 20, ink);
        AddHeader(vm["PrincipalFlowPrimary"], 418, ink);
        AddHeader(vm["PrincipalFlowDestination"], 830, ink);

        const double boxWidth = 285;
        const double boxHeight = 76;
        var accountY = accounts.Select((g, i) => (g.Key, Y: 76d + 112d * i))
            .ToDictionary(item => item.Key, item => item.Y, StringComparer.Ordinal);
        var primaryY = primary.Select((g, i) => (g.Key, Y: 76d + 112d * i))
            .ToDictionary(item => item.Key, item => item.Y, StringComparer.Ordinal);
        var destinationY = Math.Max(76d, (height - boxHeight) / 2d);

        foreach (var line in lines)
            AddFlowArrow(new(20 + boxWidth, accountY[line.Account] + boxHeight / 2),
                new(418, primaryY[line.PrimaryNode] + boxHeight / 2),
                line.Account + " → " + line.PrimaryNode + ": " + line.Amount, ink);
        foreach (var group in primary)
            AddFlowArrow(new(418 + boxWidth, primaryY[group.Key] + boxHeight / 2),
                new(830, destinationY + boxHeight / 2),
                group.Key + " → " + centre.Node + ": " + vm.FormatBatchAmount(group.Sum(line => line.NumericAmount)), ink);
        foreach (var account in accounts)
            AddFlowCard(account.Key, vm.FormatBatchAmount(account.Sum(line => line.NumericAmount)),
                20, accountY[account.Key], boxWidth, boxHeight, panel, muted, ink);
        foreach (var node in primary)
            AddFlowCard(node.Key, vm.FormatBatchAmount(node.Sum(line => line.NumericAmount)),
                418, primaryY[node.Key], boxWidth, boxHeight, panel, muted, ink);
        AddFlowCard(centre.Node, centre.Total, 830, destinationY,
            boxWidth, boxHeight, accent, muted, ink);
    }

    private void AddHeader(string title, double x, Brush ink)
    {
        var label = new TextBlock { Text = title, FontSize = 16,
            FontWeight = FontWeights.SemiBold, Foreground = ink };
        Canvas.SetLeft(label, x);
        Canvas.SetTop(label, 19);
        PrincipalFlowCanvas.Children.Add(label);
    }

    private void AddFlowArrow(Point from, Point to, string detail, Brush ink)
    {
        var distance = (to.X - from.X) * .5;
        var segment = new BezierSegment(
            new(from.X + distance, from.Y), new(to.X - distance, to.Y), to, true);
        var path = new System.Windows.Shapes.Path
        {
            Data = new PathGeometry([new PathFigure(from, [segment], false)]),
            Stroke = ink, StrokeThickness = 1.6, Opacity = .65,
            ToolTip = detail
        };
        PrincipalFlowCanvas.Children.Add(path);
        var head = new Polygon
        {
            Points = new PointCollection([new(to.X - 8, to.Y - 4),
                                          new(to.X, to.Y), new(to.X - 8, to.Y + 4)]),
            Fill = ink, Opacity = .75,
            ToolTip = detail
        };
        PrincipalFlowCanvas.Children.Add(head);
    }

    private void AddFlowCard(string title, string amount, double x, double y,
        double width, double height, Brush background, Brush text, Brush border)
    {
        var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(new TextBlock
        {
            Text = title, TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.CharacterEllipsis, TextAlignment = TextAlignment.Center,
            Foreground = text, MaxHeight = 42, ToolTip = title
        });
        panel.Children.Add(new TextBlock
        {
            Text = amount, TextAlignment = TextAlignment.Center,
            FontWeight = FontWeights.SemiBold, Foreground = text,
            Margin = new Thickness(0, 5, 0, 0), ToolTip = amount
        });
        var card = new Border
        {
            Width = width, Height = height, Padding = new Thickness(8),
            Background = background, BorderBrush = border,
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(7),
            Child = panel,
            ToolTip = title + " — " + amount
        };
        Canvas.SetLeft(card, x);
        Canvas.SetTop(card, y);
        PrincipalFlowCanvas.Children.Add(card);
    }
}
