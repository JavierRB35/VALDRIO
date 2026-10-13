using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Valdrio.Desktop;

// One rounded card per active node in each of three economic stages.
// Curved connectors explain direct primary, reciprocal gross circulation,
// and final absorption without inventing any extra posted cost.
public partial class AllocationWorkspacePanel
{
    private sealed record PositionedFlowNode(SecondaryFlowNode Node, Point Position);
    private sealed record GroupedFlowLink(SecondaryFlowStage Stage, string SourceId,
        string TargetId, decimal Amount, IReadOnlyList<SecondaryFlowArc> Details);

    private void PrincipalFlowChanged(object? sender, EventArgs e) => RedrawPrincipalCostFlow();
    private void CompleteFlowChanged(object? sender, EventArgs e) => RedrawPrincipalCostFlow();

    private void RedrawPrincipalCostFlow()
    {
        if (PrincipalFlowCanvas is null) return;
        PrincipalFlowCanvas.Children.Clear();
        if (!secondaryOnly || viewModel is not { } vm) return;

        const double width = 1780d, top = 210d, rowStep = 140d;
        const double cardHeight = 112d, cardWidth = 344d;
        const double originX = 24d, auxiliaryX = 696d, principalX = 1405d;

        PrincipalFlowCanvas.Width = width;
        var nodes = vm.SecondaryFlowNodes.ToArray();
        if (nodes.Length == 0)
        {
            PrincipalFlowCanvas.Height = 170;
            DrawGraphNote(vm.SecondaryFlowNotice, 25, 34, width - 60);
            return;
        }

        var origins = nodes.OrderBy(n => n.ShortName, StringComparer.CurrentCulture)
            .ThenBy(n => n.Id, StringComparer.Ordinal).ToArray();
        var auxiliaries = origins.Where(n => n.IsAuxiliary).ToArray();
        var principals = origins.Where(n => !n.IsAuxiliary).ToArray();
        var maxRows = Math.Max(1, Math.Max(origins.Length,
            Math.Max(auxiliaries.Length, principals.Length)));
        var canvasHeight = top + maxRows * rowStep + 116d;
        PrincipalFlowCanvas.Height = canvasHeight;

        var foreground = TryFindResource("Text.Primary") as Brush ?? Brushes.Black;
        var edgeInk = TryFindResource("Brand.DeepGreen") as Brush ?? Brushes.DarkGreen;
        var primaryBrush = TryFindResource("Surface.Subtle") as Brush ?? Brushes.WhiteSmoke;
        var auxiliaryBrush = Brushes.AliceBlue;
        var finalBrush = TryFindResource("Status.SuccessBg") as Brush ?? Brushes.Honeydew;
        var reciprocalInk = Brushes.SteelBlue;
        var directInk = Brushes.SlateGray;

        var originalCards = PlaceGraphColumn(origins, originX, top, maxRows, rowStep);
        var auxiliaryCards = PlaceGraphColumn(auxiliaries, auxiliaryX, top, maxRows, rowStep);
        var principalCards = PlaceGraphColumn(principals, principalX, top, maxRows, rowStep);

        var primaryByNode = vm.SecondaryFlowArcs
            .Where(a => a.Stage == SecondaryFlowStage.Primary)
            .GroupBy(a => a.SourceId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Sum(a => a.NumericAmount), StringComparer.Ordinal);
        var grossByAuxiliary = vm.SecondaryFlowArcs
            .Where(a => a.Stage is SecondaryFlowStage.Reciprocal or SecondaryFlowStage.Final)
            .GroupBy(a => a.SourceId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Sum(a => a.NumericAmount), StringComparer.Ordinal);
        var finalById = vm.PrincipalCosts.ToDictionary(p => p.NodeId.ToString(),
            StringComparer.Ordinal);

        var links = vm.SecondaryFlowArcs
            .GroupBy(a => (a.Stage, a.SourceId, a.TargetId))
            .Select(group => new GroupedFlowLink(group.Key.Stage,
                group.Key.SourceId, group.Key.TargetId,
                group.Sum(a => a.NumericAmount), group.ToArray()))
            .OrderBy(link => link.Stage)
            .ThenBy(link => link.SourceId, StringComparer.Ordinal)
            .ThenBy(link => link.TargetId, StringComparer.Ordinal).ToArray();
        var maxMagnitude = Math.Max(0.00000001m,
            links.Length == 0 ? 0m : links.Max(link => Math.Abs(link.Amount)));

        DrawGraphHeading(vm["SecondaryGraphTitle"], 25, 15, width - 50, edgeInk, 19);
        DrawGraphHeading(vm["SecondaryGraphOriginHeader"], originX, 77,
            cardWidth, edgeInk, 15);
        DrawGraphHeading(vm["SecondaryGraphAuxHeader"], auxiliaryX, 77,
            cardWidth, reciprocalInk, 15);
        DrawGraphHeading(vm["SecondaryGraphPrincipalHeader"], principalX, 77,
            cardWidth, edgeInk, 15);
        DrawGraphNote(vm["SecondaryGraphKey"], 25, 120, width - 50);

        // Paint connectors before cards. This preserves legible card text even
        // when a large model contains many directed relations.
        var directIndex = 0;
        var reciprocalIndex = 0;
        foreach (var link in links)
        {
            var detail = string.Join("\n", link.Details
                .OrderBy(x => x.Group, StringComparer.Ordinal)
                .Select(x => x.Group + ": " + x.Amount));
            var amount = vm.FormatBatchAmount(link.Amount);
            var stroke = 1.7d + 8.2d * (double)(Math.Abs(link.Amount) / maxMagnitude);
            if (link.Stage == SecondaryFlowStage.Primary)
            {
                if (!originalCards.TryGetValue(link.SourceId, out var from)) continue;
                if (auxiliaryCards.TryGetValue(link.TargetId, out var toAux))
                {
                    DrawGraphCurve(new(from.Position.X + cardWidth, from.Position.Y + 54),
                        new(toAux.Position.X, toAux.Position.Y + 54),
                        edgeInk, stroke, amount + "\n" + detail, 0);
                }
                else if (principalCards.TryGetValue(link.TargetId, out var toPrincipal))
                {
                    // Primary cost already absorbed by a principal bypasses
                    // auxiliary nodes along a distinctive top corridor.
                    var laneY = 166d - (directIndex++ % 5) * 11d;
                    DrawGraphDirectCurve(
                        new(from.Position.X + cardWidth, from.Position.Y + 44),
                        new(toPrincipal.Position.X, toPrincipal.Position.Y + 43),
                        laneY, directInk, Math.Max(1.7, stroke * .6),
                        amount + "\n" + detail);
                }
            }
            else if (link.Stage == SecondaryFlowStage.Final)
            {
                if (!auxiliaryCards.TryGetValue(link.SourceId, out var from) ||
                    !principalCards.TryGetValue(link.TargetId, out var to)) continue;
                DrawGraphCurve(new(from.Position.X + cardWidth, from.Position.Y + 77),
                    new(to.Position.X, to.Position.Y + 77),
                    edgeInk, stroke, amount + "\n" + detail, 1);
            }
            else
            {
                if (!auxiliaryCards.TryGetValue(link.SourceId, out var from) ||
                    !auxiliaryCards.TryGetValue(link.TargetId, out var to)) continue;
                // The directed loop lives in the aux/principal corridor.
                // Separate lanes make A->B and B->A visible as distinct arrows.
                var laneX = auxiliaryX + cardWidth + 65d +
                    (reciprocalIndex++ % 10) * 17d;
                DrawGraphReciprocalCurve(
                    new(from.Position.X + cardWidth, from.Position.Y + 28),
                    new(to.Position.X + cardWidth, to.Position.Y + 87),
                    laneX, reciprocalInk, stroke, amount + "\n" + detail);
            }
        }

        foreach (var card in originalCards.Values)
        {
            var entry = card.Node;
            var amount = primaryByNode.GetValueOrDefault(entry.Id);
            DrawGraphNodeCard(card.Position, cardWidth, cardHeight,
                entry.IsAuxiliary ? vm["SecondaryGraphInitialAux"] : vm["SecondaryGraphInitialPrincipal"],
                entry.ShortName, vm.FormatBatchAmount(amount), entry.FullName,
                primaryBrush, foreground, edgeInk, "primary:" + entry.Id);
        }
        foreach (var card in auxiliaryCards.Values)
        {
            var entry = card.Node;
            var initial = primaryByNode.GetValueOrDefault(entry.Id);
            var gross = grossByAuxiliary.GetValueOrDefault(entry.Id);
            DrawGraphNodeCard(card.Position, cardWidth, cardHeight,
                vm["SecondaryGraphAuxType"], entry.ShortName,
                vm["SecondaryGraphGross"] + ": " + vm.FormatBatchAmount(gross),
                entry.FullName + "\n" + vm["SecondaryGraphInitial"] +
                    ": " + vm.FormatBatchAmount(initial),
                auxiliaryBrush, foreground, reciprocalInk, "auxiliary:" + entry.Id);
        }
        foreach (var card in principalCards.Values)
        {
            var entry = card.Node;
            finalById.TryGetValue(entry.Id, out var total);
            DrawGraphNodeCard(card.Position, cardWidth, cardHeight,
                vm["SecondaryGraphPrincipalType"], entry.ShortName,
                total?.Total ?? vm.FormatBatchAmount(0m),
                entry.FullName + "\n" + vm["SecondaryGraphInitial"] +
                    ": " + (total?.Primary ?? vm.FormatBatchAmount(0m)) +
                    "\n" + vm["SecondaryGraphReceived"] +
                    ": " + (total?.Auxiliary ?? vm.FormatBatchAmount(0m)),
                finalBrush, foreground, edgeInk, "principal:" + entry.Id);
        }
        DrawGraphNote(vm.SecondaryFlowNotice, 25,
            canvasHeight - 67, width - 50);
    }

    private static Dictionary<string, PositionedFlowNode> PlaceGraphColumn(
        IReadOnlyList<SecondaryFlowNode> nodes, double x, double top,
        int maxRows, double step)
    {
        var pad = (maxRows - nodes.Count) * step / 2d;
        return nodes.Select((node, i) => new PositionedFlowNode(
                node, new Point(x, top + pad + i * step)))
            .ToDictionary(item => item.Node.Id, StringComparer.Ordinal);
    }

    private void DrawGraphCurve(Point from, Point to, Brush brush,
        double thickness, string tooltip, int variant)
    {
        var dx = (to.X - from.X) * .48d;
        var geometry = new PathGeometry([
            new PathFigure(from, [new BezierSegment(
                new(from.X + dx, from.Y), new(to.X - dx, to.Y), to, true)], false)]);
        DrawGraphLink(geometry, to, true, brush, thickness, tooltip,
            variant == 1 ? .68d : .50d, false);
    }

    private void DrawGraphDirectCurve(Point from, Point to,
        double laneY, Brush brush, double thickness, string tooltip)
    {
        var hubLeft = new Point(515, laneY);
        var hubRight = new Point(1274, laneY);
        var geometry = new PathGeometry([
            new PathFigure(from, [
                new BezierSegment(new(from.X + 70, from.Y),
                    new(hubLeft.X - 100, hubLeft.Y), hubLeft, true),
                new BezierSegment(new(740, laneY), new(1100, laneY),
                    hubRight, true),
                new BezierSegment(new(hubRight.X + 55, hubRight.Y),
                    new(to.X - 75, to.Y), to, true)
            ], false)]);
        DrawGraphLink(geometry, to, true, brush, thickness, tooltip, .57d, true);
    }

    private void DrawGraphReciprocalCurve(Point from, Point to,
        double laneX, Brush brush, double thickness, string tooltip)
    {
        // A->A has distinct source/destination ports and a visible loop.
        // Other reciprocal arcs return to the middle column (right-to-left
        // arrowhead), rather than being mistaken for final cost to principals.
        var geometry = new PathGeometry([
            new PathFigure(from, [new BezierSegment(
                new(laneX, from.Y), new(laneX, to.Y), to, true)], false)]);
        DrawGraphLink(geometry, to, false, brush, thickness, tooltip, .75d, true);
    }

    private void DrawGraphLink(PathGeometry geometry, Point end,
        bool rightward, Brush brush, double thickness, string tooltip,
        double opacity, bool dashed)
    {
        var path = new System.Windows.Shapes.Path
        {
            Data = geometry, Stroke = brush, StrokeThickness = thickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Opacity = opacity, ToolTip = tooltip,
            IsHitTestVisible = true
        };
        if (dashed) path.StrokeDashArray = new DoubleCollection([4d, 2d]);
        var dx = rightward ? -9d : 9d;
        var arrow = new Polygon
        {
            Points = new PointCollection([
                new(end.X + dx, end.Y - 5),
                end,
                new(end.X + dx, end.Y + 5)
            ]),
            Fill = brush, Opacity = opacity, ToolTip = tooltip
        };
        PrincipalFlowCanvas.Children.Add(path);
        PrincipalFlowCanvas.Children.Add(arrow);
    }

    private void DrawGraphNodeCard(Point position, double width,
        double height, string category, string label, string money,
        string tooltip, Brush background, Brush foreground, Brush border,
        string tag)
    {
        var panel = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        panel.Children.Add(new TextBlock
        {
            Text = category.ToUpperInvariant(), Foreground = border,
            FontWeight = FontWeights.SemiBold, FontSize = 11,
            Margin = new Thickness(0, 0, 0, 5)
        });
        panel.Children.Add(new TextBlock
        {
            Text = label, Foreground = foreground, TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontWeight = FontWeights.Medium, FontSize = 14, ToolTip = tooltip
        });
        panel.Children.Add(new TextBlock
        {
            Text = money, Foreground = foreground,
            FontWeight = FontWeights.Bold, FontSize = 16,
            Margin = new Thickness(0, 7, 0, 0)
        });
        var card = new Border
        {
            Width = width, Height = height, Padding = new Thickness(15, 9, 12, 9),
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(12), Background = background,
            BorderBrush = border, Child = panel,
            ToolTip = tooltip, Tag = tag
        };
        Canvas.SetLeft(card, position.X);
        Canvas.SetTop(card, position.Y);
        PrincipalFlowCanvas.Children.Add(card);
    }

    private void DrawGraphHeading(string text, double x, double y,
        double width, Brush ink, double size)
    {
        var label = new TextBlock
        {
            Text = text, Foreground = ink, FontSize = size,
            FontWeight = FontWeights.SemiBold,
            Width = width, TextWrapping = TextWrapping.Wrap
        };
        Canvas.SetLeft(label, x);
        Canvas.SetTop(label, y);
        PrincipalFlowCanvas.Children.Add(label);
    }

    private void DrawGraphNote(string text, double x, double y, double width)
    {
        var label = new TextBlock
        {
            Text = text, Width = width, TextWrapping = TextWrapping.Wrap,
            Foreground = TryFindResource("Text.Primary") as Brush ?? Brushes.Black,
            FontSize = 12
        };
        Canvas.SetLeft(label, x);
        Canvas.SetTop(label, y);
        PrincipalFlowCanvas.Children.Add(label);
    }
}
