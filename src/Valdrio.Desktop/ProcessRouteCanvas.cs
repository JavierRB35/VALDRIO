using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Valdrio.Domain;

namespace Valdrio.Desktop;

/// <summary>
/// A purely WPF view over the optional model process routes. Never performs
/// allocations. Sequence and auxiliary service use distinct line styles.
/// </summary>
public sealed class ProcessRouteCanvas : UserControl
{
    private const double ColumnWidth = 300;
    private const double CardWidth = 250;
    private const double CardHeight = 76;
    private const double RowHeight = 100;
    private readonly Canvas canvas = new();
    private readonly ScrollViewer scroll;
    private readonly ScaleTransform zoomTransform = new(1, 1);
    private ModelWorkspaceViewModel? model;
    private EntityId? firstNode;
    private Border? draggedCard;
    private EntityId? draggedId;
    private Point mouseStart;
    private Point cardStart;
    private bool dragged;
    private double zoom = 1;
    private readonly Dictionary<EntityId, Point> nodePositions = [];

    public ProcessRouteKind ConnectionKind { get; set; } = ProcessRouteKind.Sequence;
    public bool ConnectMode { get; set; }
    public bool RemoveMode { get; set; }

    public double Zoom
    {
        get => zoom;
        set
        {
            zoom = Math.Clamp(value, .5, 2.0);
            zoomTransform.ScaleX = zoomTransform.ScaleY = zoom;
        }
    }

    public ProcessRouteCanvas()
    {
        canvas.Background = Brushes.Transparent;
        canvas.LayoutTransform = zoomTransform;
        scroll = new ScrollViewer
        {
            Content = canvas,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        Content = scroll;
        DataContextChanged += (_, _) => Attach();
        Loaded += (_, _) => Attach();
        Unloaded += (_, _) => Detach();
    }

    private Brush ResourceBrush(string key) => (Brush)FindResource(key);
    private void Detach()
    {
        if (model is not null) model.PropertyChanged -= ModelChanged;
        model = null;
    }
    private void Attach()
    {
        Detach();
        model = DataContext as ModelWorkspaceViewModel;
        if (model is not null) model.PropertyChanged += ModelChanged;
        Draw();
    }
    private void ModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) ||
            e.PropertyName is nameof(ModelWorkspaceViewModel.IsBusy))
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(Draw));
    }
    private static NodeEconomicFunction[] Functions =>
        [NodeEconomicFunction.Structure, NodeEconomicFunction.Production,
         NodeEconomicFunction.Commercial, NodeEconomicFunction.Unspecified];

    private void Draw()
    {
        if (model is not { } vm || draggedCard is not null) return;
        canvas.Children.Clear();
        nodePositions.Clear();
        var all = vm.ProcessMapNodes.Where(n => n.IsActive).ToArray();
        var maxPrincipal = Functions.Max(f => all.Count(n => n.Role == NodeRole.Principal &&
            NodeCostClassification.Function(n) == f));
        var maxAux = Functions.Max(f => all.Count(n => n.Role == NodeRole.Auxiliary &&
            NodeCostClassification.Function(n) == f));
        var principalTop = 75d;
        var auxiliaryTop = principalTop + Math.Max(1, maxPrincipal) * RowHeight + 55d;
        canvas.Width = Functions.Length * ColumnWidth + 30d;
        canvas.Height = auxiliaryTop + Math.Max(1, maxAux) * RowHeight + 60d;

        for (var fi = 0; fi < Functions.Length; fi++)
        {
            var function = Functions[fi];
            var colLeft = 12d + fi * ColumnWidth;
            var zone = new Border
            {
                Width = ColumnWidth - 10, Height = canvas.Height - 14,
                CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1),
                BorderBrush = ResourceBrush("Border.Soft"),
                Background = ResourceBrush("Surface.Canvas")
            };
            Canvas.SetLeft(zone, colLeft);
            Canvas.SetTop(zone, 8);
            canvas.Children.Add(zone);
            Text(vm.Text("Model.Function." + function), colLeft + 12, 18, 260, true);
            Text(vm.Text("Model.Principal"), colLeft + 12, 52, 255, false);
            Text(vm.Text("Model.Auxiliary"), colLeft + 12, auxiliaryTop - 26, 255, false);
            foreach (var role in new[] { NodeRole.Principal, NodeRole.Auxiliary })
            {
                var group = all.Where(n => NodeCostClassification.Function(n) == function &&
                                           n.Role == role).OrderBy(n => n.Code, StringComparer.Ordinal).ToArray();
                for (var j = 0; j < group.Length; j++)
                {
                    var n = group[j];
                    var pos = ProcessRouteGraph.ReadPosition(n);
                    var left = Math.Clamp((double?)pos?.X ?? colLeft + 18,
                        colLeft + 10, colLeft + ColumnWidth - CardWidth - 18);
                    var topDefault = (role == NodeRole.Principal ? principalTop : auxiliaryTop) + j * RowHeight;
                    var top = Math.Clamp((double?)pos?.Y ?? topDefault,
                        role == NodeRole.Principal ? principalTop : auxiliaryTop,
                        role == NodeRole.Principal ? auxiliaryTop - CardHeight - 12 : canvas.Height - CardHeight - 20);
                    nodePositions[n.Id] = new Point(left, top);
                }
            }
        }

        foreach (var link in vm.ProcessMapLinks)
        {
            if (!nodePositions.TryGetValue(link.Source, out var a) ||
                !nodePositions.TryGetValue(link.Destination, out var b)) continue;
            var p0 = new Point(a.X + CardWidth - 4, a.Y + CardHeight / 2);
            var p3 = new Point(b.X + 4, b.Y + CardHeight / 2);
            if (p3.X <= p0.X + 25)
                p0 = new Point(a.X + CardWidth / 2, a.Y + CardHeight);
            var c1 = new Point(p0.X + Math.Max(45, Math.Abs(p3.X - p0.X) * .4), p0.Y);
            var c2 = new Point(p3.X - Math.Max(45, Math.Abs(p3.X - p0.X) * .4), p3.Y);
            var figure = new PathFigure { StartPoint = p0 };
            figure.Segments.Add(new BezierSegment(c1, c2, p3, true));
            var geom = new PathGeometry();
            geom.Figures.Add(figure);
            var path = new Path
            {
                Data = geom, Stroke = ResourceBrush(link.Kind == ProcessRouteKind.Sequence
                    ? "Accent.ActiveGreen" : "Brand.Yellow"),
                StrokeThickness = 2.4, IsHitTestVisible = false
            };
            if (link.Kind == ProcessRouteKind.AuxiliaryService)
                path.StrokeDashArray = [4, 3];
            canvas.Children.Add(path);
            var orientation = p3 - c2;
            var length = Math.Max(1, orientation.Length);
            var u = orientation / length;
            var perpendicular = new Vector(-u.Y, u.X);
            var arrow = new Polygon
            {
                Points = [
                    p3,
                    p3 - u * 11 + perpendicular * 5,
                    p3 - u * 11 - perpendicular * 5
                ],
                Fill = path.Stroke, Stroke = path.Stroke,
                IsHitTestVisible = false
            };
            canvas.Children.Add(arrow);
        }

        foreach (var n in all.OrderBy(n => n.Code, StringComparer.Ordinal))
        {
            if (!nodePositions.TryGetValue(n.Id, out var p)) continue;
            var center = vm.Centers.FirstOrDefault(c => c.Value.Id == n.PhysicalCenterId)?.Value.Code ?? "—";
            var title = new TextBlock
            {
                Text = n.Code, FontSize = 13, FontWeight = FontWeights.SemiBold,
                Foreground = ResourceBrush("Brand.DeepGreen"),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            var description = vm.Current?.Structure.Pools.FirstOrDefault(x => x.NodeId == n.Id)?.Description ?? "";
            var detail = new TextBlock
            {
                Text = center + " · " + description,
                FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = ResourceBrush("Text.Secondary")
            };
            var unit = new TextBlock
            {
                Text = vm.Text("Model.Purpose." + NodeCostClassification.Purpose(n)),
                FontSize = 10, TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = ResourceBrush("Text.Secondary")
            };
            var border = new Border
            {
                Width = CardWidth, Height = CardHeight, CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(n.Id == firstNode ? 2 : 1),
                BorderBrush = ResourceBrush(n.Id == firstNode ? "Brand.Yellow" : "Accent.ActiveGreen"),
                Background = ResourceBrush("Brand.White"), Padding = new Thickness(9, 5, 8, 4),
                ToolTip = n.Code + Environment.NewLine + description + Environment.NewLine +
                          vm.Text("Model.Function." + NodeCostClassification.Function(n)) +
                          " · " + vm.Text(n.Role == NodeRole.Principal ? "Model.Principal" : "Model.Auxiliary"),
                Child = new StackPanel { Children = { title, detail, unit } },
                Cursor = vm.CanEditProcessMap ? Cursors.Hand : Cursors.Arrow,
                Focusable = true
            };
            AutomationProperties.SetName(border, n.Code + " · " + description);
            Canvas.SetLeft(border, p.X); Canvas.SetTop(border, p.Y);
            border.MouseLeftButtonDown += (_, e) => StartDrag(n.Id, border, e);
            border.MouseMove += (_, e) => ContinueDrag(border, e);
            border.MouseLeftButtonUp += (_, e) => EndDrag(n.Id, border, e);
            canvas.Children.Add(border);
        }
    }

    private void Text(string value, double left, double top, double width, bool strong)
    {
        var t = new TextBlock
        {
            Text = value, Width = width,
            FontSize = strong ? 12 : 11,
            FontWeight = strong ? FontWeights.SemiBold : FontWeights.Normal,
            Foreground = ResourceBrush("Text.Primary"),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Canvas.SetLeft(t, left); Canvas.SetTop(t, top);
        canvas.Children.Add(t);
    }

    private void StartDrag(EntityId id, Border card, MouseButtonEventArgs e)
    {
        if (model?.CanEditProcessMap != true) return;
        draggedCard = card;
        draggedId = id;
        mouseStart = e.GetPosition(canvas);
        cardStart = new Point(Canvas.GetLeft(card), Canvas.GetTop(card));
        dragged = false;
        Mouse.Capture(card);
        e.Handled = true;
    }
    private void ContinueDrag(Border card, MouseEventArgs e)
    {
        if (draggedCard != card || draggedId is null || ConnectMode) return;
        var offset = e.GetPosition(canvas) - mouseStart;
        if (offset.Length < 4 && !dragged) return;
        dragged = true;
        var current = model?.ProcessMapNodes.FirstOrDefault(n => n.Id == draggedId);
        if (current is null) return;
        var group = Array.IndexOf(Functions, NodeCostClassification.Function(current));
        var colX = 12d + group * ColumnWidth;
        Canvas.SetLeft(card, Math.Clamp(cardStart.X + offset.X,
            colX + 10, colX + ColumnWidth - CardWidth - 18));
        Canvas.SetTop(card, Math.Clamp(cardStart.Y + offset.Y, 75, canvas.Height - CardHeight - 15));
    }
    private void EndDrag(EntityId id, Border card, MouseButtonEventArgs e)
    {
        if (draggedCard != card) return;
        Mouse.Capture(null);
        draggedCard = null;
        draggedId = null;
        if (dragged && !ConnectMode)
        {
            model?.SetProcessPosition(id, new ProcessRoutePoint(
                (int)Canvas.GetLeft(card), (int)Canvas.GetTop(card)));
        }
        else
            HandleNodeClick(id);
        dragged = false;
        Draw();
        e.Handled = true;
    }

    private void HandleNodeClick(EntityId id)
    {
        if (model?.CanEditProcessMap != true || !ConnectMode) return;
        if (firstNode is null) { firstNode = id; return; }
        if (firstNode == id && ConnectionKind == ProcessRouteKind.Sequence)
        { firstNode = null; return; }
        model.TrySetProcessLink(new ProcessRouteLink(firstNode.Value, id, ConnectionKind), !RemoveMode);
        firstNode = null;
    }

    public void ClearConnection() { firstNode = null; Draw(); }
    public void AutoArrange() { model?.AutoArrangeProcessMap(); Draw(); }

    public void ExportPng()
    {
        if (canvas.Children.Count == 0) return;
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "PNG (*.png)|*.png", DefaultExt = ".png",
            FileName = "valdrio-proceso.png"
        };
        if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;
        var original = Zoom;
        try
        {
            Zoom = 1;
            canvas.Measure(new Size(canvas.Width, canvas.Height));
            canvas.Arrange(new Rect(new Size(canvas.Width, canvas.Height)));
            canvas.UpdateLayout();
            var bitmap = new RenderTargetBitmap(
                Math.Max(1, (int)Math.Ceiling(canvas.Width)),
                Math.Max(1, (int)Math.Ceiling(canvas.Height)),
                96, 96, PixelFormats.Pbgra32);
            bitmap.Render(canvas);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = System.IO.File.Create(dlg.FileName);
            encoder.Save(file);
        }
        finally { Zoom = original; }
    }
}
