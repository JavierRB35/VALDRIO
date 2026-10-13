using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Valdrio.Desktop;

// Explanatory view of the selected account's posted primary facts.
// Line thickness reflects the magnitude of the actual displayed amount.
// Never creates new allocation facts or changes the ledger.
public partial class AllocationWorkspacePanel
{
    private void PrimaryFlowChanged(object? sender, EventArgs e) => RedrawPrimaryFlow();

    private void RedrawPrimaryFlow()
    {
        if (PrimaryFlowCanvas is null) return;
        PrimaryFlowCanvas.Children.Clear();
        if (secondaryOnly || viewModel is not { SelectedPrimaryAccount: { } row } vm) return;
        // Do not create thousands of visual paths for a large account; the table
        // remains exhaustive while the diagram explicitly labels its limited preview.
        var all = row.Destinations;
        var details = all.OrderByDescending(d => Math.Abs(d.NumericAmount))
            .ThenBy(d => d.Destination, StringComparer.Ordinal).Take(32).ToArray();
        if (details.Length == 0) return;
        var width = 970d;
        var height = Math.Max(260d, 60d + 88d * details.Length + (all.Count > 32 ? 32d : 0d));
        PrimaryFlowCanvas.Width = width;
        PrimaryFlowCanvas.Height = height;
        var foreground = TryFindResource("Text.Primary") as Brush ?? Brushes.Black;
        var stroke = TryFindResource("Brand.DeepGreen") as Brush ?? Brushes.DarkGreen;
        var background = TryFindResource("Surface.Subtle") as Brush ?? Brushes.WhiteSmoke;
        var finalBackground = TryFindResource("Status.SuccessBg") as Brush ?? Brushes.Honeydew;
        var sourceY = (height - 84d) / 2d;
        var card = new Border
        {
            Width = 300, Height = 84, Background = background, CornerRadius = new CornerRadius(6),
            BorderBrush = stroke, BorderThickness = new Thickness(1), Padding = new Thickness(9),
            Child = new StackPanel
            {
                Children =
                {
                    new TextBlock { Text = row.Account, TextWrapping = TextWrapping.Wrap,
                        Foreground = foreground, MaxHeight = 44, ToolTip = row.Account },
                    new TextBlock { Text = row.Assigned, FontWeight = FontWeights.Bold,
                        TextAlignment = TextAlignment.Center, Foreground = foreground }
                }
            }
        };
        Canvas.SetLeft(card, 20);
        Canvas.SetTop(card, sourceY);
        var magnitude = Math.Max(0.000001m, details.Sum(d => Math.Abs(d.NumericAmount)));
        for (var i = 0; i < details.Count; i++)
        {
            var detail = details[i];
            var top = 20d + i * 88d;
            var from = new Point(320, sourceY + 42);
            var to = new Point(630, top + 33);
            var geometry = new PathGeometry([new PathFigure(from,
                [new BezierSegment(new(445, from.Y), new(530, to.Y), to, true)], false)]);
            var line = new System.Windows.Shapes.Path
            {
                Data = geometry, Stroke = stroke,
                StrokeThickness = 1.5 + 9d * (double)(Math.Abs(detail.NumericAmount) / magnitude),
                Opacity = 0.66, ToolTip = detail.Amount + " · " + detail.Criterion
            };
            PrimaryFlowCanvas.Children.Add(line);
            var arrow = new Polygon
            {
                Points = new PointCollection([new(to.X - 8, to.Y - 4),
                    new(to.X, to.Y), new(to.X - 8, to.Y + 4)]),
                Fill = stroke, ToolTip = detail.Amount
            };
            PrimaryFlowCanvas.Children.Add(arrow);
            var destination = new Border
            {
                Width = 305, Height = 68, BorderBrush = stroke,
                Background = finalBackground, BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6), Padding = new Thickness(7),
                ToolTip = detail.Criterion + " · " + detail.Driver + " · " + detail.Share,
                Child = new StackPanel
                {
                    Children =
                    {
                        new TextBlock { Text = detail.Destination, Foreground = foreground,
                            TextWrapping = TextWrapping.Wrap, MaxHeight = 34, ToolTip = detail.Destination },
                        new TextBlock { Text = detail.Amount, FontWeight = FontWeights.SemiBold,
                            Foreground = foreground, TextAlignment = TextAlignment.Right }
                    }
                }
            };
            Canvas.SetLeft(destination, 630);
            Canvas.SetTop(destination, top);
            PrimaryFlowCanvas.Children.Add(destination);
        }
        PrimaryFlowCanvas.Children.Add(card);
        if (all.Count > details.Length)
        {
            var note = new TextBlock
            {
                Text = string.Format(vm["PrimaryFlowLimited"], details.Length, all.Count),
                Foreground = foreground, TextWrapping = TextWrapping.Wrap,
                FontWeight = FontWeights.SemiBold
            };
            Canvas.SetLeft(note, 20);
            Canvas.SetTop(note, height - 30);
            PrimaryFlowCanvas.Children.Add(note);
        }
    }
}
