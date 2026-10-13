using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Valdrio.Desktop;

// Complete, read-only secondary network: every primary-entry node, every
// reciprocal auxiliary transfer (including cycles/self-service), every
// auxiliary-to-principal transfer, and all reconciled terminal totals.
// Each arrow is an individual directed lane to avoid unreadable crossings.
public partial class AllocationWorkspacePanel
{
    private void PrincipalFlowChanged(object? sender, EventArgs e) => RedrawPrincipalCostFlow();
    private void CompleteFlowChanged(object? sender, EventArgs e) => RedrawPrincipalCostFlow();

    private void RedrawPrincipalCostFlow()
    {
        if (PrincipalFlowCanvas is null) return;
        PrincipalFlowCanvas.Children.Clear();
        if (!secondaryOnly || viewModel is not { } vm) return;

        const double width = 1190d, rowHeight = 75d, left = 18d;
        const double sourceX = left, sourceWidth = 330d, targetX = 835d;
        const double targetWidth = 331d;
        var ink = TryFindResource("Brand.DeepGreen") as Brush ?? Brushes.DarkGreen;
        var muted = TryFindResource("Text.Primary") as Brush ?? Brushes.Black;
        var panel = TryFindResource("Surface.Subtle") as Brush ?? Brushes.WhiteSmoke;
        var auxiliary = TryFindResource("Status.SuccessBg") as Brush ?? Brushes.Honeydew;
        var routes = vm.SecondaryFlowArcs.ToArray();
        PrincipalFlowCanvas.Width = width;
        if (routes.Length == 0)
        {
            PrincipalFlowCanvas.Height = 140;
            DrawLabel(vm.SecondaryFlowNotice, 20, 25, muted, 1100, false);
            return;
        }

        var y = 18d;
        DrawLabel(vm["SecondaryFlowFullTitle"], 20, y, ink, 1100, true);
        y += 48;
        foreach (var stage in new[] {
            SecondaryFlowStage.Primary, SecondaryFlowStage.Reciprocal, SecondaryFlowStage.Final })
        {
            var edges = routes.Where(route => route.Stage == stage)
                .OrderBy(route => route.Group, StringComparer.Ordinal)
                .ThenBy(route => route.Source, StringComparer.Ordinal)
                .ThenBy(route => route.Target, StringComparer.Ordinal).ToArray();

            var heading = stage switch
            {
                SecondaryFlowStage.Primary => vm["SecondaryFlowStagePrimary"],
                SecondaryFlowStage.Reciprocal => vm["SecondaryFlowStageReciprocal"],
                _ => vm["SecondaryFlowStageFinal"]
            };
            DrawSectionHeading(heading, y, ink, panel, width - 36);
            y += 48;
            if (edges.Length == 0)
            {
                DrawLabel(vm["SecondaryFlowNoRoutesStage"], left + 16, y + 2, muted, 1050, false);
                y += 40;
                continue;
            }
            var magnitude = Math.Max(0.0000000001m, edges.Max(edge => Math.Abs(edge.NumericAmount)));
            foreach (var route in edges)
            {
                var tooltip = route.SourceFull + " → " + route.TargetFull + "\n" +
                    route.Amount + "\n" + vm["ReciprocalGroup"] + ": " + route.Group +
                    "\n" + route.NumericAmount.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var targetLabel = stage == SecondaryFlowStage.Primary ?
                    (route.TargetAuxiliary ? vm["SecondaryFlowInitialAux"] :
                        vm["SecondaryFlowInitialPrincipal"]) + "\n" + route.Target : route.Target;
                DrawFlowCard(route.Source, stage == SecondaryFlowStage.Primary ? route.Amount : "",
                    sourceX, y + 4, sourceWidth, 61d, panel, muted, ink,
                    route.SourceFull);
                DrawFlowCard(targetLabel, stage == SecondaryFlowStage.Primary ? "" : route.Amount,
                    targetX, y + 4, targetWidth, 61d,
                    route.TargetAuxiliary ? panel : auxiliary, muted, ink, route.TargetFull);

                var midline = y + 34.5;
                var thickness = 2.0 + 10d * (double)(Math.Abs(route.NumericAmount) / magnitude);
                var stroke = new Line
                {
                    X1 = sourceX + sourceWidth + 12, X2 = targetX - 13,
                    Y1 = midline, Y2 = midline,
                    Stroke = ink, StrokeThickness = thickness,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    Opacity = stage == SecondaryFlowStage.Reciprocal ? 0.85 : 0.63,
                    ToolTip = tooltip
                };
                PrincipalFlowCanvas.Children.Add(stroke);
                var arrow = new Polygon
                {
                    Points = new PointCollection([
                        new(targetX - 21, midline - 5),
                        new(targetX - 12, midline),
                        new(targetX - 21, midline + 5)]),
                    Fill = ink, ToolTip = tooltip
                };
                PrincipalFlowCanvas.Children.Add(arrow);

                var amount = new TextBlock
                {
                    Text = route.Amount,
                    FontWeight = FontWeights.SemiBold,
                    TextAlignment = TextAlignment.Center,
                    Foreground = muted,
                    Background = panel,
                    Padding = new Thickness(4, 2, 4, 2),
                    MinWidth = 145, ToolTip = tooltip
                };
                Canvas.SetLeft(amount, 560);
                Canvas.SetTop(amount, y + 4);
                PrincipalFlowCanvas.Children.Add(amount);
                y += rowHeight;
            }
            y += 30;
        }
        // Principal totals are from the final T11 ledger and include direct
        // primary plus net secondary absorption. Gross reciprocal flows are
        // intentionally not added to these amounts.
        DrawSectionHeading(vm["SecondaryFlowTotalsTitle"], y, ink, panel, width - 36);
        y += 50;
        foreach (var centre in vm.PrincipalCosts.OrderBy(c => c.Node, StringComparer.Ordinal))
        {
            DrawFlowCard(centre.Node, centre.Primary,
                sourceX, y, sourceWidth, 62, panel, muted, ink, centre.Node);
            DrawLabel("+ " + centre.Auxiliary, 490, y + 17, muted, 280, true);
            DrawFlowCard(centre.Node, centre.Total, targetX, y, targetWidth, 62,
                auxiliary, muted, ink, centre.Node);
            y += rowHeight;
        }
        y += 8;
        DrawLabel(vm.SecondaryFlowNotice, left, y, muted, 1125, false);
        y += 45;
        PrincipalFlowCanvas.Height = y;
    }

    private void DrawSectionHeading(string title, double y, Brush ink, Brush background, double width)
    {
        var box = new Border
        {
            Width = width, Height = 35, Background = background,
            Padding = new Thickness(9, 7, 9, 5),
            Child = new TextBlock { Text = title, Foreground = ink,
                FontSize = 15, FontWeight = FontWeights.SemiBold }
        };
        Canvas.SetLeft(box, 18);
        Canvas.SetTop(box, y);
        PrincipalFlowCanvas.Children.Add(box);
    }

    private void DrawLabel(string title, double x, double y, Brush ink, double width, bool strong)
    {
        var label = new TextBlock
        {
            Text = title, TextWrapping = TextWrapping.Wrap,
            Width = width, Foreground = ink,
            FontWeight = strong ? FontWeights.SemiBold : FontWeights.Normal,
            FontSize = strong ? 15 : 12
        };
        Canvas.SetLeft(label, x);
        Canvas.SetTop(label, y);
        PrincipalFlowCanvas.Children.Add(label);
    }

    private void DrawFlowCard(string title, string amount, double x, double y,
        double width, double height, Brush background, Brush text, Brush border, string tooltip)
    {
        var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(new TextBlock
        {
            Text = title, TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextAlignment = TextAlignment.Center,
            Foreground = text, MaxHeight = 39, ToolTip = tooltip
        });
        if (!string.IsNullOrWhiteSpace(amount))
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
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
            Child = panel, ToolTip = tooltip
        };
        Canvas.SetLeft(card, x);
        Canvas.SetTop(card, y);
        PrincipalFlowCanvas.Children.Add(card);
    }
}
