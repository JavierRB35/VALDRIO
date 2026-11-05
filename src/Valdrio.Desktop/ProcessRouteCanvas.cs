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
    private readonly Border selectionPanel = new();
    private readonly StackPanel selectionContent = new();
    private readonly ScrollViewer destinationScroll;
    private EntityId? lastRenderedOrigin;
    private readonly ScaleTransform zoomTransform = new(1, 1);
    private ModelWorkspaceViewModel? model;
    private EntityId? firstNode;
    private EntityId? selectedOrigin;
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
        // A stable left-hand master/detail panel is the primary way to define
        // destinations. The original click-to-connect interaction remains
        // available behind the advanced controls.
        selectionPanel.Width = 270;
        selectionPanel.Margin = new Thickness(0, 0, 14, 0);
        selectionPanel.Padding = new Thickness(12);
        selectionPanel.BorderThickness = new Thickness(1);
        selectionPanel.CornerRadius = new CornerRadius(7);
        destinationScroll = new ScrollViewer
        {
            Content = selectionContent,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MaxHeight = 580
        };
        selectionPanel.Child = destinationScroll;
        var layout = new Grid();
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(288) });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(selectionPanel, 0);
        Grid.SetColumn(scroll, 1);
        layout.Children.Add(selectionPanel);
        layout.Children.Add(scroll);
        Content = layout;
        DataContextChanged += (_, _) => Attach();
        Loaded += (_, _) => Attach();
        Unloaded += (_, _) => Detach();
    }

    private Brush ResourceBrush(string key) => (Brush)FindResource(key);
    private void Detach()
    {
        if (model is not null) model.PropertyChanged -= ModelChanged;
        model = null;
        selectedOrigin = null;
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
        RenderDestinations(vm);
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
            var selfService = link.Source == link.Destination &&
                link.Kind == ProcessRouteKind.AuxiliaryService;
            // Draw auxiliary self service as a clear, small loop to the
            // right of its own card instead of an ambiguous line across it.
            var p0 = selfService
                ? new Point(a.X + CardWidth + 4, a.Y + 13)
                : new Point(a.X + CardWidth - 4, a.Y + CardHeight / 2);
            var p3 = selfService
                ? new Point(a.X + CardWidth + 4, a.Y + CardHeight - 13)
                : new Point(b.X + 4, b.Y + CardHeight / 2);
            if (!selfService && p3.X <= p0.X + 25)
                p0 = new Point(a.X + CardWidth / 2, a.Y + CardHeight);
            var c1 = selfService
                ? new Point(p0.X + 24, p0.Y - 10)
                : new Point(p0.X + Math.Max(45, Math.Abs(p3.X - p0.X) * .4), p0.Y);
            var c2 = selfService
                ? new Point(p3.X + 24, p3.Y + 10)
                : new Point(p3.X - Math.Max(45, Math.Abs(p3.X - p0.X) * .4), p3.Y);
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
                BorderBrush = ResourceBrush(n.Id == firstNode || n.Id == selectedOrigin
                    ? "Brand.Yellow" : "Accent.ActiveGreen"),
                Background = ResourceBrush("Brand.White"), Padding = new Thickness(9, 5, 8, 4),
                ToolTip = n.Code + Environment.NewLine + description + Environment.NewLine +
                          vm.Text("Model.Function." + NodeCostClassification.Function(n)) +
                          " · " + vm.Text(n.Role == NodeRole.Principal ? "Model.Principal" : "Model.Auxiliary"),
                Child = new StackPanel { Children = { title, detail, unit } },
                Cursor = vm.CanEditProcessMap ? Cursors.Hand : Cursors.Arrow,
                Focusable = true
            };
            AutomationProperties.SetName(border, n.Code + " · " + description);
            AutomationProperties.SetHelpText(border,
                vm.Text("Model.MapKeyboardHelp"));
            Canvas.SetLeft(border, p.X); Canvas.SetTop(border, p.Y);
            border.MouseLeftButtonDown += (_, e) => StartDrag(n.Id, border, e);
            border.MouseMove += (_, e) => ContinueDrag(border, e);
            border.MouseLeftButtonUp += (_, e) => EndDrag(n.Id, border, e);
            border.KeyDown += (_, e) =>
            {
                if (e.Key is Key.Enter or Key.Space)
                {
                    HandleNodeClick(n.Id);
                    Draw();
                    e.Handled = true;
                }
                else if (vm.CanEditProcessMap && !ConnectMode &&
                         e.Key is Key.Left or Key.Right or Key.Up or Key.Down)
                {
                    var p = nodePositions[n.Id];
                    var x = e.Key == Key.Left ? -10 : e.Key == Key.Right ? 10 : 0;
                    var y = e.Key == Key.Up ? -10 : e.Key == Key.Down ? 10 : 0;
                    vm.SetProcessPosition(n.Id, new ProcessRoutePoint((int)p.X + x, (int)p.Y + y));
                    Draw();
                    e.Handled = true;
                }
            };
            canvas.Children.Add(border);
        }
    }

    private void RenderDestinations(ModelWorkspaceViewModel vm)
    {
        var previousOffset = selectedOrigin == lastRenderedOrigin
            ? destinationScroll.VerticalOffset : 0;
        selectionContent.Children.Clear();
        selectionPanel.Background = ResourceBrush("Brand.White");
        selectionPanel.BorderBrush = ResourceBrush("Border.Soft");
        var nodes = vm.ProcessMapNodes.Where(n => n.IsActive)
            .OrderBy(n => n.Code, StringComparer.CurrentCultureIgnoreCase).ToArray();
        if (selectedOrigin is not null && nodes.All(n => n.Id != selectedOrigin))
            selectedOrigin = null;
        void Heading(string text, int size = 14)
        {
            selectionContent.Children.Add(new TextBlock
            {
                Text = text,
                FontSize = size,
                FontWeight = FontWeights.SemiBold,
                Foreground = ResourceBrush("Brand.DeepGreen"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            });
        }
        void Help(string text)
        {
            selectionContent.Children.Add(new TextBlock
            {
                Text = text,
                Foreground = ResourceBrush("Text.Secondary"),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 10)
            });
        }
        Heading(vm.Text("Model.MapChooseOrigin"));
        var origins = new ComboBox
        {
            ItemsSource = nodes,
            DisplayMemberPath = nameof(CalculationNodeDefinition.Code),
            IsEnabled = nodes.Length > 0,
            Margin = new Thickness(0, 0, 0, 10),
            MinHeight = 30
        };
        AutomationProperties.SetName(origins, vm.Text("Model.MapChooseOrigin"));
        origins.SelectedItem = nodes.FirstOrDefault(n => n.Id == selectedOrigin);
        origins.SelectionChanged += (_, _) =>
        {
            if (origins.SelectedItem is CalculationNodeDefinition chosen)
            {
                selectedOrigin = chosen.Id;
                firstNode = null;
                Draw();
            }
        };
        selectionContent.Children.Add(origins);
        if (selectedOrigin is null)
        {
            Help(vm.Text("Model.MapSelectOriginHelp"));
            return;
        }
        lastRenderedOrigin = selectedOrigin;
        var source = nodes.Single(n => n.Id == selectedOrigin);
        Heading(source.Code, 13);
        Help(vm.Text("Model.Function." + NodeCostClassification.Function(source)) +
             " · " + vm.Text(source.Role == NodeRole.Principal
                ? "Model.Principal" : "Model.Auxiliary"));
        Heading(vm.Text(source.Role == NodeRole.Principal
            ? "Model.MapNextNodes" : "Model.MapServiceDestinations"), 13);
        Help(vm.Text(source.Role == NodeRole.Principal
            ? "Model.MapSequenceCheckHelp" : "Model.MapServiceCheckHelp"));
        if (vm.Current?.Status == ModelStatus.Published)
            Help(vm.Text("Model.MapReadOnly"));

        var kind = source.Role == NodeRole.Principal
            ? ProcessRouteKind.Sequence : ProcessRouteKind.AuxiliaryService;
        var links = vm.ProcessMapLinks.ToHashSet();
        foreach (var target in nodes)
        {
            var link = new ProcessRouteLink(source.Id, target.Id, kind);
            var alreadyLinked = links.Contains(link);
            var restriction = ProcessRouteGraph.Restriction(nodes, link);
            var canChange = vm.CanEditProcessMap &&
                (alreadyLinked || restriction == ProcessRouteRestriction.None);
            var label = target.Code +
                " · " + vm.Text(target.Role == NodeRole.Principal
                    ? "Model.Principal" : "Model.Auxiliary");
            if (source.Id == target.Id && source.Role == NodeRole.Auxiliary)
                label += " · " + vm.Text("Model.MapSelfService");
            var check = new CheckBox
            {
                Content = label,
                IsChecked = alreadyLinked,
                IsEnabled = canChange,
                Margin = new Thickness(0, 4, 0, 4),
                VerticalContentAlignment = VerticalAlignment.Center,
                ToolTip = restriction == ProcessRouteRestriction.None
                    ? vm.Text("Model.MapCheckDestinationHelp")
                    : vm.Text("Model.MapRestriction." + restriction)
            };
            AutomationProperties.SetName(check, label);
            check.Checked += (_, _) => UpdateDestination(vm, link, true);
            check.Unchecked += (_, _) => UpdateDestination(vm, link, false);
            selectionContent.Children.Add(check);
        }
        if (source.Role == NodeRole.Auxiliary)
            Help(vm.Text("Model.MapSelfServiceHelp"));
        Help(vm.Text("Model.MapChangesRequireValidation"));
        // Rebuilding checkboxes after an edit must not jump a long target list
        // back to the top and disrupt the analyst's work.
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,
            new Action(() => destinationScroll.ScrollToVerticalOffset(previousOffset)));
    }

    private void UpdateDestination(ModelWorkspaceViewModel vm, ProcessRouteLink link, bool enabled)
    {
        // Never turn a UI checkbox into an economic allocation. This only edits
        // a versioned route; the secondary key proposal remains a separate act.
        if (!vm.TrySetProcessLink(link, enabled))
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(Draw));
            return;
        }
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(Draw));
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
        var maxPrincipals = Functions.Max(f => model?.ProcessMapNodes.Count(n =>
            n.IsActive && n.Role == NodeRole.Principal &&
            NodeCostClassification.Function(n) == f) ?? 0);
        var auxiliaryTop = 75d + Math.Max(1, maxPrincipals) * RowHeight + 55d;
        var minTop = current.Role == NodeRole.Principal ? 75d : auxiliaryTop;
        var maxTop = current.Role == NodeRole.Principal
            ? auxiliaryTop - CardHeight - 12 : canvas.Height - CardHeight - 20;
        Canvas.SetTop(card, Math.Clamp(cardStart.Y + offset.Y, minTop, maxTop));
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
        // Selecting a card always displays its destinations, including in
        // published models where the map is intentionally read-only.
        selectedOrigin = id;
        if (model?.CanEditProcessMap != true || !ConnectMode)
        {
            Draw();
            return;
        }
        if (firstNode is null) { firstNode = id; Draw(); return; }
        if (firstNode == id && ConnectionKind == ProcessRouteKind.Sequence)
        { firstNode = null; return; }
        model.TrySetProcessLink(new ProcessRouteLink(firstNode.Value, id, ConnectionKind), !RemoveMode);
        firstNode = null;
        Draw();
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
