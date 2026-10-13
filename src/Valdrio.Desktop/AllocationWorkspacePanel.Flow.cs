using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Valdrio.Desktop;

// A legible summary of terminal contributions by the node that initially
// received each resource. The adjacent table remains the exhaustive
// resource-to-initial-node breakdown; internal reciprocal hops aren't invented.
public partial class AllocationWorkspacePanel
{
    private sealed record SummaryOrigin(string Name, decimal Amount, int Count);

    private void PrincipalFlowChanged(object? sender, EventArgs e) => RedrawPrincipalCostFlow();

    private void RedrawPrincipalCostFlow()
    {
        if (PrincipalFlowCanvas is null) return;
        PrincipalFlowCanvas.Children.Clear();
        if (!secondaryOnly || viewModel is not { SelectedPrincipalCostRow: { } centre } vm) return;

        var sources = vm.PrincipalOriginRows
            .GroupBy(row => row.PrimaryNode)
            .Select(group => new SummaryOrigin(group.Key, group.Sum(row => row.NumericAmount), group.Count()))
            .OrderByDescending(group => Math.Abs(group.Amount))
            .ThenBy(group => group.Name, StringComparer.CurrentCulture)
            .ToArray();
        if (sources.Length == 0) return;

        const int limit = 8;
        var shown = sources.Take(limit - 1).ToList();
        if (sources.Length > limit - 1)
        {
            var rest = sources.Skip(limit - 1).ToArray();
            shown.Add(new SummaryOrigin(string.Format(vm["PrincipalFlowOtherNodes"], rest.Length),
                rest.Sum(item => item.Amount), rest.Sum(item => item.Count)));
        }

        const double sourceX = 20, sourceWidth = 385, trackStart = 421, trunkX = 806;
        const double top = 155, step = 85, boxHeight = 64;
        var canvasHeight = Math.Max(550d, top + step * shown.Count + 28d);
        PrincipalFlowCanvas.Width = 1160;
        PrincipalFlowCanvas.Height = canvasHeight;

        var ink = TryFindResource("Brand.DeepGreen") as Brush ?? Brushes.DarkGreen;
        var text = TryFindResource("Text.Primary") as Brush ?? Brushes.Black;
        var background = TryFindResource("Surface.Subtle") as Brush ?? Brushes.WhiteSmoke;
        var selected = TryFindResource("Status.SuccessBg") as Brush ?? Brushes.Honeydew;

        AddFlowHeading(vm["PrincipalFlowPrimary"], sourceX, 17, ink);
        AddFlowHeading(vm["PrincipalFlowContribution"], trackStart, 17, ink);
        AddFlowHeading(vm["PrincipalFlowDestination"], 829, 17, ink);
        AddFlowCard(centre.Node, centre.Total, 831, 54, 302, 78, selected, text, ink);

        var magnitude = Math.Max(0.00000001m, sources.Sum(x => Math.Abs(x.Amount)));
        var firstY = top + boxHeight / 2;
        var lastY = top + step * (shown.Count - 1) + boxHeight / 2;
        // One vertical receiver spine plus separate horizontal lanes; there are
        // no intersecting Bezier curves or overlapping many-to-many pathways.
        var connector = new Line
        {
            X1 = trunkX, X2 = trunkX, Y1 = 135, Y2 = lastY,
            Stroke = ink, StrokeThickness = 2.5, Opacity = .6,
            ToolTip = centre.Node + " — " + centre.Total
        };
        PrincipalFlowCanvas.Children.Add(connector);

        for (var i = 0; i < shown.Count; i++)
        {
            var item = shown[i];
            var y = top + i * step;
            var laneY = y + boxHeight / 2;
            var amount = vm.FormatBatchAmount(item.Amount);
            AddFlowCard(item.Name, amount, sourceX, y,
                sourceWidth, boxHeight, background, text, ink);
            var thickness = 2d + 11d * (double)(Math.Abs(item.Amount) / magnitude);
            var track = new Line
            {
                X1 = trackStart, X2 = trunkX, Y1 = laneY, Y2 = laneY,
                Stroke = ink, StrokeThickness = thickness,
                Opacity = .68, StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                ToolTip = item.Name + " → " + centre.Node + ": " + amount
            };
            PrincipalFlowCanvas.Children.Add(track);
            var figure = new TextBlock
            {
                Text = amount, Foreground = text, FontWeight = FontWeights.SemiBold,
                Background = background, Padding = new Thickness(6, 2, 6, 2)
            };
            Canvas.SetLeft(figure, 568);
            Canvas.SetTop(figure, laneY - 28);
            PrincipalFlowCanvas.Children.Add(figure);
        }

        var note = new TextBlock
        {
            Text = vm["PrincipalFlowSummaryHelp"],
            TextWrapping = TextWrapping.Wrap, Foreground = text,
            MaxWidth = 1105
        };
        Canvas.SetLeft(note, sourceX);
        Canvas.SetTop(note, canvasHeight - 33);
        PrincipalFlowCanvas.Children.Add(note);
    }

    private void AddFlowHeading(string title, double x, double y, Brush ink)
    {
        var header = new TextBlock
        {
            Text = title, FontSize = 15, FontWeight = FontWeights.SemiBold,
            Foreground = ink
        };
        Canvas.SetLeft(header, x);
        Canvas.SetTop(header, y);
        PrincipalFlowCanvas.Children.Add(header);
    }

    private void AddFlowCard(string title, string amount, double x, double y,
        double width, double height, Brush background, Brush text, Brush border)
    {
        var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(new TextBlock
        {
            Text = title, TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.CharacterEllipsis, TextAlignment = TextAlignment.Center,
            Foreground = text, MaxHeight = 34, ToolTip = title
        });
        panel.Children.Add(new TextBlock
        {
            Text = amount, TextAlignment = TextAlignment.Center,
            FontWeight = FontWeights.SemiBold, Foreground = text,
            Margin = new Thickness(0, 3, 0, 0)
        });
        var card = new Border
        {
            Width = width, Height = height, Padding = new Thickness(5),
            Background = background, BorderBrush = border,
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(7),
            Child = panel, ToolTip = title + " — " + amount
        };
        Canvas.SetLeft(card, x);
        Canvas.SetTop(card, y);
        PrincipalFlowCanvas.Children.Add(card);
    }
}
