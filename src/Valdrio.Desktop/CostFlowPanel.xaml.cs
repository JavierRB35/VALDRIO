using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Valdrio.Desktop;

public partial class CostFlowPanel : UserControl
{
    private const double NodeWidth = 236;
    private const double NodeHeight = 110;
    private const double GapX = 22;
    private const double RowStep = 132;
    private CostFlowViewModel? vm;
    private string? focusedNodeId;
    private FlowArrow? focusedArrow;

    public CostFlowPanel()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (vm is not null) vm.GraphChanged -= Changed;
            vm = DataContext as CostFlowViewModel;
            if (vm is not null) vm.GraphChanged += Changed;
            focusedNodeId = null;
            focusedArrow = null;
            Draw();
        };
    }

    private void Changed(object? sender, EventArgs e) => Draw();

    // Resizing only changes geometry. No model, persistence or calculations are touched.
    private void ResizeGraph(object sender, SizeChangedEventArgs e)
    {
        if (vm is not null && e.WidthChanged && Math.Abs(e.NewSize.Width - e.PreviousSize.Width) > 20)
            Draw();
    }

    private void Draw()
    {
        PrimaryGraphCanvas.Children.Clear();
        SecondaryGraphCanvas.Children.Clear();
        if (vm is null) return;
        var headers = new[] { "Stage", "Resource", "Source", "Destination", "Rule", "Assigned", "Received", "Terminal" };
        for (var i = 0; i < headers.Length; i++) DetailsGrid.Columns[i].Header = vm[headers[i]];
        headers = ["Resource", "Effective", "Direct", "Excluded", "Pending", "Terminal", "Difference"];
        for (var i = 0; i < headers.Length; i++) ReconciliationGrid.Columns[i].Header = vm[headers[i]];
        headers = ["Center", "PrimaryBalance", "SecondaryNet", "FinalBalance"];
        for (var i = 0; i < headers.Length; i++) CenterBalanceGrid.Columns[i].Header = vm[headers[i]];
        headers = ["Node", "Role", "PrimaryBalance", "SecondaryNet", "FinalBalance"];
        for (var i = 0; i < headers.Length; i++) NodeBalanceGrid.Columns[i].Header = vm[headers[i]];

        Render(PrimaryGraphCanvas, PrimaryGraphViewport, [0, 1], primary: true);
        Render(SecondaryGraphCanvas, SecondaryGraphViewport,
            vm.Nodes.Any(n => n.Stage == 2) ? [1, 2, 3] : [1, 3], primary: false);
    }

    private void Render(Canvas canvas, ScrollViewer viewport, int[] stages, bool primary)
    {
        var candidates = vm!.Nodes.Where(n => stages.Contains(n.Stage)).ToArray();
        double available = viewport.ActualWidth > 140 ? viewport.ActualWidth - 16 : 880;
        double width = Math.Max(550, available);
        // Keep long lists legible: wrap inside the available width before scrolling horizontally.
        int columns = Math.Max(1, (int)Math.Floor((width - 70) / (NodeWidth + GapX)));
        var positions = new Dictionary<string, Point>(StringComparer.Ordinal);
        bool horizontal = width >= 980;
        double stageWidth = width / stages.Length;
        double y = 62;
        int stageIndex = 0;
        foreach (int stage in stages)
        {
            var nodes = candidates.Where(n => n.Stage == stage)
                .OrderBy(n => n.Label, StringComparer.CurrentCulture).ToArray();
            AddStageTitle(canvas, stage, horizontal ? 20 : y - 39, horizontal ? 20 + stageIndex * stageWidth : 20,
                horizontal ? stageWidth - 40 : width - 40);
            for (int i = 0; i < nodes.Length; i++)
            {
                double x = horizontal ? 20 + stageIndex * stageWidth : 20 + (i % columns) * (NodeWidth + GapX);
                positions[nodes[i].Id] = new Point(x, horizontal ? 80 + i * RowStep : y + (i / columns) * RowStep);
            }
            y += Math.Max(1, (int)Math.Ceiling((double)nodes.Length / columns)) * RowStep + 95;
            stageIndex++;
        }
        canvas.Width = width;
        canvas.Height = horizontal
            ? Math.Max(340, 160 + stages.Max(stage => candidates.Count(n => n.Stage == stage)) * RowStep)
            : Math.Max(340, y - 48);
        if (candidates.Length == 0)
        {
            var empty = new TextBlock { Text = vm["NoGraphData"], Margin = new Thickness(22), Foreground = ThemeBrush("Text.Primary") };
            canvas.Children.Add(empty);
            return;
        }

        // Use the same edge objects and route selection for BOTH canvases.
        // Primary = origin -> primary destinations. Secondary = primary -> auxiliaries
        // -> terminals, including non-additive auxiliary reciprocal transfers.
        int edgeIndex = 0;
        foreach (var edge in vm.Arrows)
        {
            if (!positions.TryGetValue(edge.SourceId, out var source) ||
                !positions.TryGetValue(edge.DestinationId, out var destination)) continue;
            bool isPrimaryEdge = edge.SourceId.StartsWith("0:", StringComparison.Ordinal) &&
                                 edge.DestinationId.StartsWith("1:", StringComparison.Ordinal);
            if (primary != isPrimaryEdge) continue;

            bool active = vm.HighlightedArrows.Contains(edge);
            double opacity = vm.HasHighlightedRoute ? (active ? 1 : 0.13) : 0.75;
            var from = horizontal ? new Point(source.X + NodeWidth, source.Y + NodeHeight / 2)
                : new Point(source.X + NodeWidth / 2, source.Y + NodeHeight);
            var to = horizontal ? new Point(destination.X, destination.Y + NodeHeight / 2)
                : new Point(destination.X + NodeWidth / 2, destination.Y);
            bool sameStage = edge.SourceId.StartsWith("2:", StringComparison.Ordinal) &&
                             edge.DestinationId.StartsWith("2:", StringComparison.Ordinal);
            bool skippingLayer = edge.SourceId.StartsWith("1:", StringComparison.Ordinal) &&
                                 edge.DestinationId.StartsWith("3:", StringComparison.Ordinal) && stages.Contains(2);
            Geometry geometry;
            if (horizontal)
            {
                if (sameStage)
                {
                    to = new Point(destination.X + NodeWidth, destination.Y + NodeHeight / 2);
                    double lane = from.X + 28 + (edgeIndex % 3) * 14;
                    geometry = new PathGeometry([new PathFigure(from,
                        [new BezierSegment(new Point(lane, from.Y), new Point(lane, to.Y), to, true)], false)]);
                }
                else if (skippingLayer)
                {
                    // Route retained primary cost around all intermediate boxes;
                    // a curve through them would suggest nonexistent transfers.
                    double exitLane = stageWidth - 24 - (edgeIndex % 3) * 14;
                    double entryLane = destination.X - 24 - (edgeIndex % 3) * 14;
                    double lane = canvas.Height - 30 - (edgeIndex % 3) * 10;
                    geometry = new PathGeometry([new PathFigure(from,
                        [new PolyLineSegment([new Point(exitLane, from.Y), new Point(exitLane, lane),
                            new Point(entryLane, lane), new Point(entryLane, to.Y), to], true)], false)]);
                }
                else
                {
                    double middle = (from.X + to.X) / 2;
                    geometry = new PathGeometry([new PathFigure(from,
                        [new BezierSegment(new Point(middle, from.Y), new Point(middle, to.Y), to, true)], false)]);
                }
            }
            else if (sameStage)
            {
                // Reciprocal flows use a right-side return lane, not a terminal-cost path.
                double lane = width - 18 - (edgeIndex % 3) * 9;
                geometry = new PathGeometry([new PathFigure(from,
                    [new PolyLineSegment([new Point(lane, from.Y + 23),
                                         new Point(lane, to.Y - 23), to], true)], false)]);
            }
            else if (skippingLayer)
            {
                double lane = width - 15 - (edgeIndex % 3) * 12;
                geometry = new PathGeometry([new PathFigure(from,
                    [new BezierSegment(new Point(lane, from.Y + 36),
                                       new Point(lane, to.Y - 36), to, true)], false)]);
            }
            else
            {
                double span = Math.Max(35, (to.Y - from.Y) / 2);
                geometry = new PathGeometry([new PathFigure(from,
                    [new BezierSegment(new Point(from.X, from.Y + span),
                                       new Point(to.X, to.Y - span), to, true)], false)]);
            }
            var stroke = ThemeBrush(active ? "Brand.Yellow" : edge.Internal ? "Accent.ActiveGreen" : "Brand.DeepGreen");
            var line = new Path { Data = geometry, Stroke = stroke, StrokeThickness = active ? 4 : 2,
                                  Opacity = opacity, Cursor = Cursors.Hand,
                                  ToolTip = edge.Label, Focusable = true, FocusVisualStyle = (Style)FindResource("Focus.Visible") };
            System.Windows.Automation.AutomationProperties.SetName(line, edge.Label);
            line.KeyDown += (_, e) =>
            {
                if (e.Key is not (Key.Enter or Key.Space)) return;
                focusedNodeId = null; focusedArrow = edge; vm.SelectArrow(edge); e.Handled = true;
            };
            if (edge.Internal) line.StrokeDashArray = [4, 3];
            line.MouseLeftButtonUp += (_, e) => { focusedNodeId = null; focusedArrow = edge; vm.SelectArrow(edge); e.Handled = true; };
            canvas.Children.Add(line);

            var tips = horizontal
                ? (sameStage ? new[] { new Point(to.X + 9, to.Y - 6), to, new Point(to.X + 9, to.Y + 6) }
                    : new[] { new Point(to.X - 9, to.Y - 6), to, new Point(to.X - 9, to.Y + 6) })
                : new[] { new Point(to.X - 6, to.Y - 9), to, new Point(to.X + 6, to.Y - 9) };
            var tip = new Polygon { Points = new PointCollection(tips),
                Fill = stroke, Opacity = opacity, IsHitTestVisible = false };
            canvas.Children.Add(tip);

            // The compact label is visible in the graph; the tooltip and detail grid
            // retain unrounded precision and the full rule/transfer explanation.
            double labelWidth = Math.Min(174, width - 30);
            double lx = Math.Clamp((from.X + to.X) / 2 - labelWidth / 2 +
                                  (edgeIndex % 3 - 1) * 12, 8, width - labelWidth - 8);
            double ly = Math.Max(18, (from.Y + to.Y) / 2 + (edgeIndex % 3 - 1) * 28);
            if (sameStage) { lx = horizontal ? Math.Min(width - labelWidth - 8, from.X + 12) : width - labelWidth - 8; ly = (from.Y + to.Y) / 2; }
            if (horizontal && skippingLayer) ly = canvas.Height - 65;
            var caption = new Button
            {
                Content = new TextBlock { Text = edge.DisplayLabel ?? edge.Label, TextWrapping = TextWrapping.Wrap,
                                          TextAlignment = TextAlignment.Center, FontSize = 11 },
                Width = labelWidth, Padding = new Thickness(3), MinHeight = 25,
                Background = ThemeBrush(active ? "Status.WarningBg" : "Brand.White"),
                BorderBrush = ThemeBrush(active ? "Brand.Yellow" : "Border.Soft"),
                Opacity = opacity, ToolTip = edge.Label,
                Cursor = Cursors.Hand
            };
            System.Windows.Automation.AutomationProperties.SetName(caption, edge.Label);
            caption.Click += (_, _) => { focusedNodeId = null; focusedArrow = edge; vm.SelectArrow(edge); };
            Canvas.SetLeft(caption, lx); Canvas.SetTop(caption, ly);
            // Overview has unobstructed routes; a selected transfer always exposes its value.
            // Every route remains keyboard-selectable and the exact data remains in the grid.
            if (vm.ShowAllFlowLabels || (active && ReferenceEquals(focusedArrow, edge))) canvas.Children.Add(caption);
            if (active && ReferenceEquals(focusedArrow, edge))
                Dispatcher.BeginInvoke(new Action(() => caption.Focus()), DispatcherPriority.Input);
            edgeIndex++;
        }

        foreach (var node in candidates)
        {
            var at = positions[node.Id];
            bool active = vm.HighlightedNodeIds.Contains(node.Id);
            double opacity = vm.HasHighlightedRoute ? (active ? 1 : 0.22) : 1;
            var content = new StackPanel();
            content.Children.Add(new TextBlock { Text = node.Label, TextWrapping = TextWrapping.Wrap,
                                                   TextAlignment = TextAlignment.Center, MaxHeight = 58 });
            content.Children.Add(new TextBlock { Text = node.DisplayAmount ?? node.Amount,
                FontWeight = FontWeights.SemiBold, TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 0) });
            var btn = new Button
            {
                Content = content, Width = NodeWidth, Height = NodeHeight, Padding = new Thickness(7),
                Background = ThemeBrush(active ? "Status.WarningBg" :
                    node.Stage == 3 ? "Status.SuccessBg" : "Surface.Subtle"),
                BorderBrush = ThemeBrush(active ? "Brand.Yellow" : "Border.Soft"),
                BorderThickness = new Thickness(active ? 3 : 1),
                Opacity = opacity, ToolTip = node.Label + Environment.NewLine + node.Amount
            };
            System.Windows.Automation.AutomationProperties.SetName(btn, node.Label + " " + node.Amount);
            btn.Click += (_, _) => { focusedNodeId = node.Id; focusedArrow = null; vm.SelectNode(node.Id); };
            Canvas.SetLeft(btn, at.X); Canvas.SetTop(btn, at.Y);
            canvas.Children.Add(btn);
            if (focusedNodeId == node.Id)
                Dispatcher.BeginInvoke(new Action(() => btn.Focus()), DispatcherPriority.Input);
        }
    }

    private void AddStageTitle(Canvas canvas, int stage, double y, double x, double maxWidth)
    {
        var text = new TextBlock
        {
            Text = vm!.Stages[stage], FontSize = 16, FontWeight = FontWeights.SemiBold,
            Foreground = ThemeBrush("Brand.DeepGreen"), Background = ThemeBrush("Surface.Canvas"),
            TextWrapping = TextWrapping.Wrap, MaxWidth = maxWidth
        };
        Canvas.SetLeft(text, x); Canvas.SetTop(text, y); Panel.SetZIndex(text, 2);
        canvas.Children.Add(text);
    }

    private Brush ThemeBrush(string key) => (Brush)FindResource(key);
}
