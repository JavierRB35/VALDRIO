using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows.Data;

namespace Valdrio.Desktop;

public sealed class ParetoChart : UserControl
{
    private sealed record Detail(string Node, string Cost, string Cumulative, decimal Amount, decimal? Percentage);
    public static readonly DependencyProperty SecondaryProperty = DependencyProperty.Register(
        nameof(Secondary), typeof(bool), typeof(ParetoChart), new PropertyMetadata(false, (d, _) => ((ParetoChart)d).QueueDraw()));
    public bool Secondary { get => (bool)GetValue(SecondaryProperty); set => SetValue(SecondaryProperty, value); }
    private AllocationWorkspaceViewModel? model;
    private bool queued;
    public ParetoChart()
    {
        DataContextChanged += (_, _) => Attach();
        Loaded += (_, _) => Attach();
        Unloaded += (_, _) => { if (model is not null) model.PropertyChanged -= Changed; model = null; };
        SizeChanged += (_, e) => { if (e.WidthChanged) QueueDraw(); };
    }
    private void Attach()
    {
        if (model is not null) model.PropertyChanged -= Changed;
        model = DataContext as AllocationWorkspaceViewModel;
        if (model is not null) model.PropertyChanged += Changed;
        QueueDraw();
    }
    private void Changed(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == "Item[]" ||
            e.PropertyName is nameof(AllocationWorkspaceViewModel.PrimaryResultCategoryIndex)
                or nameof(AllocationWorkspaceViewModel.SecondaryResultCategoryIndex))
            QueueDraw();
    }
    private void QueueDraw()
    {
        if (queued) return;
        queued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => { queued = false; Draw(); }));
    }
    private Brush Brush(string key) => (Brush)FindResource(key);
    private void Draw()
    {
        if (model is not { } vm) { Content = null; return; }
        var points = ParetoSeries.Build(Secondary
            ? (vm.SecondaryResultCategoryIndex == 1 ? vm.DirectPrincipalCosts : vm.GeneralPrincipalCosts)
                .Select(x => new ParetoCost(x.Node, x.TotalAmount))
            : (vm.PrimaryResultCategoryIndex == 1 ? vm.DirectPrimaryNodeTotals : vm.GeneralPrimaryNodeTotals)
                .Select(x => new ParetoCost(x.Center + " · " + x.Node, x.NumericAmount)));
        var panel = new StackPanel();
        Content = panel;
        panel.Children.Add(new TextBlock { Text = vm[Secondary ? "ParetoSecondaryTitle" : "ParetoPrimaryTitle"], FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 12, 0, 6), TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new TextBlock { Text = vm[Secondary ? "ParetoSecondaryHelp" : "ParetoPrimaryHelp"], TextWrapping = TextWrapping.Wrap });
        if (points.Count == 0)
        {
            panel.Children.Add(new TextBlock { Text = vm["ParetoEmpty"], Margin = new Thickness(0, 8, 0, 8), TextWrapping = TextWrapping.Wrap });
            return;
        }
        panel.Children.Add(new TextBlock { Text = vm["ParetoLegend"], Margin = new Thickness(0, 8, 0, 8), TextWrapping = TextWrapping.Wrap });
        if (points.Any(x => x.Amount < 0) || points[0].CumulativePercent is null)
            panel.Children.Add(new TextBlock { Text = vm[points[0].CumulativePercent is null ? "ParetoNoPercentage" : "ParetoCredits"], TextWrapping = TextWrapping.Wrap });
        const double left = 112, top = 28, plotHeight = 210, right = 76;
        var width = Math.Max(Math.Max(600, ActualWidth - 20), left + right + points.Count * 116d);
        var plotWidth = width - left - right;
        var cell = plotWidth / points.Count;
        var canvas = new Canvas { Width = width, Height = 338, Background = Brush("Surface.Canvas") };
        var scroll = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = canvas };
        panel.Children.Add(scroll);
        var min = Math.Min(0m, points.Min(x => x.Amount));
        var max = Math.Max(0m, points.Max(x => x.Amount));
        if (max == min) max = min + 1;
        var percentMax = Math.Max(100m, Math.Ceiling(points.Max(x => x.CumulativePercent ?? 0) / 25) * 25);
        double Y(decimal value) => top + (double)((max - value) / (max - min)) * plotHeight;
        double P(decimal value) => top + plotHeight * (1 - (double)(value / percentMax));
        void Label(string text, double x, double y, double w, bool bold = false)
        {
            var label = new TextBlock { Text = text, Width = w, FontSize = 11, TextWrapping = TextWrapping.Wrap, Foreground = Brush("Text.Primary"), FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal };
            if (y > top + plotHeight) { label.MaxHeight = 82; label.TextTrimming = TextTrimming.CharacterEllipsis; label.ToolTip = text; }
            Canvas.SetLeft(label, x); Canvas.SetTop(label, y); canvas.Children.Add(label);
        }
        for (var tick = 0; tick <= 4; tick++)
        {
            var amount = min + (max - min) * tick / 4;
            var y = Y(amount);
            canvas.Children.Add(new Line { X1 = left, X2 = width - right, Y1 = y, Y2 = y, Stroke = Brush("Border.Soft") });
            Label(vm.ParetoMoney(amount), 2, y - 8, left - 8);
            if (points[0].CumulativePercent is not null)
                Label((percentMax * tick / 4).ToString("0.#", vm.ParetoFormat) + " %", width - right + 8, P(percentMax * tick / 4) - 8, right - 8);
        }
        canvas.Children.Add(new Line { X1 = left, X2 = width - right, Y1 = Y(0), Y2 = Y(0), Stroke = Brush("Text.Primary") });
        var line = new Polyline { Stroke = Brush("Brand.Yellow"), StrokeThickness = 3, IsHitTestVisible = false };
        for (var i = 0; i < points.Count; i++)
        {
            var point = points[i];
            var x = left + cell * (i + .5);
            var percent = point.CumulativePercent?.ToString("0.##", vm.ParetoFormat) + " %";
            var description = point.Label + ": " + vm.ParetoMoney(point.Amount) + (point.CumulativePercent is null ? "" : " · " + vm["ParetoCumulative"] + " " + percent);
            var bar = new Border { Width = cell * .62, Height = Math.Max(1, Math.Abs(Y(point.Amount) - Y(0))), Background = Brush("Accent.ActiveGreen"), ToolTip = description, Focusable = true };
            AutomationProperties.SetName(bar, description);
            Canvas.SetLeft(bar, x - bar.Width / 2); Canvas.SetTop(bar, Math.Min(Y(point.Amount), Y(0))); canvas.Children.Add(bar);
            Label(point.Label, x - cell / 2 + 4, top + plotHeight + 12, cell - 8);
            if (point.CumulativePercent is { } cumulative) line.Points.Add(new Point(x, P(cumulative)));
        }
        canvas.Children.Add(line);
        foreach (var point in line.Points)
        {
            var dot = new Ellipse { Width = 7, Height = 7, Fill = Brush("Brand.Yellow"), Stroke = Brush("Brand.DeepGreen"), IsHitTestVisible = false };
            Canvas.SetLeft(dot, point.X - 3.5); Canvas.SetTop(dot, point.Y - 3.5); canvas.Children.Add(dot);
        }
        var details = new DataGrid
        {
            AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false,
            ItemsSource = points.Select(x => new Detail(x.Label, vm.ParetoMoney(x.Amount),
                x.CumulativePercent is { } value ? value.ToString("0.##", vm.ParetoFormat) + " %" : "—",
                x.Amount, x.CumulativePercent)).ToArray()
        };
        ScrollViewer.SetVerticalScrollBarVisibility(details, ScrollBarVisibility.Disabled);
        details.Columns.Add(new DataGridTextColumn { Header = vm["ParetoNode"], Binding = new Binding("Node"), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
        details.Columns.Add(new DataGridTextColumn { Header = vm["PrimaryAmount"], Binding = new Binding("Cost"), SortMemberPath = "Amount", Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        details.Columns.Add(new DataGridTextColumn { Header = vm["ParetoCumulative"], Binding = new Binding("Cumulative"), SortMemberPath = "Percentage", Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        AutomationProperties.SetName(details, vm["ParetoDetails"]);
        panel.Children.Add(new Expander { Header = vm["ParetoDetails"], Content = details, Margin = new Thickness(0, 8, 0, 8) });
    }
}
