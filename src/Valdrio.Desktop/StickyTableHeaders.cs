using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Valdrio.Desktop;

// The page owns vertical scrolling. A table's column headings follow that
// page while there are rows below them, then leave with the final row.
// This is visual-only: the DataGrid, its sort handlers, data and selection
// remain the originals (including keyboard access and horizontal scrolling).
public static class StickyTableHeaders
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(StickyTableHeaders), new PropertyMetadata(false, Changed));
    private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached(
        "State", typeof(Behavior), typeof(StickyTableHeaders));

    public static bool GetEnabled(DependencyObject element) => (bool)element.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject element, bool enabled) => element.SetValue(EnabledProperty, enabled);

    private static void Changed(DependencyObject element, DependencyPropertyChangedEventArgs args)
    {
        if (element is not ScrollViewer viewer) return;
        if (viewer.GetValue(StateProperty) is Behavior previous)
        {
            previous.Dispose();
            viewer.ClearValue(StateProperty);
        }
        if ((bool)args.NewValue) viewer.SetValue(StateProperty, new Behavior(viewer));
    }

    private sealed class Behavior : IDisposable
    {
        private readonly ScrollViewer viewer;
        private AdornerLayer? layer;
        private HeaderAdorner? adorner;
        private bool queued;

        public Behavior(ScrollViewer viewer)
        {
            this.viewer = viewer;
            viewer.Loaded += Loaded;
            viewer.Unloaded += Unloaded;
            viewer.ScrollChanged += Scrolled;
            viewer.SizeChanged += Resized;
            if (viewer.IsLoaded) Loaded(viewer, new RoutedEventArgs());
        }

        private void Loaded(object sender, RoutedEventArgs args)
        {
            // A table's internal ScrollViewer is not a second page.
            var vertical = ScrollViewer.GetVerticalScrollBarVisibility(viewer);
            if (vertical is ScrollBarVisibility.Hidden or ScrollBarVisibility.Disabled ||
                ParentIsDataGrid(viewer)) return;
            layer = AdornerLayer.GetAdornerLayer(viewer);
            if (layer is null || adorner is not null) return;
            adorner = new HeaderAdorner(viewer);
            layer.Add(adorner);
            Queue();
        }

        private static bool ParentIsDataGrid(DependencyObject element)
        {
            for (var current = VisualTreeHelper.GetParent(element); current is not null;
                 current = VisualTreeHelper.GetParent(current))
                if (current is DataGrid) return true;
            return false;
        }

        private void Unloaded(object sender, RoutedEventArgs args)
        {
            if (adorner is not null) layer?.Remove(adorner);
            adorner = null;
            layer = null;
        }

        private void Scrolled(object sender, ScrollChangedEventArgs args) => Queue();
        private void Resized(object sender, SizeChangedEventArgs args) => Queue();
        private void Queue()
        {
            if (queued || adorner is null) return;
            queued = true;
            viewer.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                queued = false;
                if (adorner is not null) Update();
            }));
        }

        private void Update()
        {
            if (!viewer.IsVisible || viewer.ViewportHeight <= 0)
            {
                adorner!.Hide();
                return;
            }
            foreach (var grid in Descendants<DataGrid>(viewer))
            {
                if (!grid.IsVisible || grid.Items.Count == 0 || grid.ActualHeight <= 0) continue;
                var header = Descendants<DataGridColumnHeadersPresenter>(grid).FirstOrDefault();
                if (header is null || !header.IsVisible || header.ActualHeight <= 0) continue;
                try
                {
                    var origin = header.TranslatePoint(new Point(), viewer);
                    var bottom = grid.TranslatePoint(new Point(0, grid.ActualHeight), viewer).Y;
                    if (origin.Y >= 0 || bottom <= 0) continue;
                    // Once the final record passes behind the heading, the
                    // heading scrolls off with it instead of remaining fixed.
                    adorner!.Show(header, origin.X, Math.Min(0, bottom - header.ActualHeight),
                        header.ActualWidth, header.ActualHeight);
                    return;
                }
                catch (InvalidOperationException) { /* element was reparented while scrolling */ }
            }
            adorner!.Hide();
        }

        private static IEnumerable<T> Descendants<T>(DependencyObject element) where T : DependencyObject
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            {
                var child = VisualTreeHelper.GetChild(element, i);
                if (child is T match) yield return match;
                foreach (var descendant in Descendants<T>(child)) yield return descendant;
            }
        }

        public void Dispose()
        {
            viewer.Loaded -= Loaded;
            viewer.Unloaded -= Unloaded;
            viewer.ScrollChanged -= Scrolled;
            viewer.SizeChanged -= Resized;
            Unloaded(viewer, new RoutedEventArgs());
        }
    }

    private sealed class HeaderAdorner : Adorner
    {
        private readonly ScrollViewer viewer;
        private readonly VisualCollection visuals;
        private readonly Border overlay;
        private readonly Rectangle image;
        private DataGridColumnHeadersPresenter? current;
        private double left, top, width, height;

        public HeaderAdorner(ScrollViewer viewer) : base(viewer)
        {
            this.viewer = viewer;
            image = new Rectangle { Stretch = Stretch.Fill };
            overlay = new Border
            {
                Child = image,
                Background = Application.Current?.TryFindResource("Surface.Subtle") as Brush ?? Brushes.White,
                Visibility = Visibility.Collapsed
            };
            visuals = new VisualCollection(this) { overlay };
            overlay.MouseLeftButtonUp += ClickHeader;
            overlay.PreviewMouseWheel += Wheel;
            IsHitTestVisible = true;
        }

        public void Hide()
        {
            if (overlay.Visibility != Visibility.Visible) return;
            overlay.Visibility = Visibility.Collapsed;
            current = null;
            image.Fill = null;
            InvalidateArrange();
        }

        public void Show(DataGridColumnHeadersPresenter header, double x, double y, double w, double h)
        {
            if (!ReferenceEquals(current, header))
            {
                current = header;
                image.Fill = new VisualBrush(header)
                {
                    Stretch = Stretch.Fill,
                    AlignmentX = AlignmentX.Left,
                    AlignmentY = AlignmentY.Top
                };
            }
            left = x; top = y; width = w; height = h;
            overlay.Visibility = Visibility.Visible;
            Clip = new RectangleGeometry(new Rect(0, 0, viewer.ActualWidth, viewer.ActualHeight));
            InvalidateArrange();
        }

        private void ClickHeader(object sender, MouseButtonEventArgs e)
        {
            if (current is null) return;
            var x = e.GetPosition(overlay).X;
            foreach (var header in Headers(current))
            {
                var position = header.TranslatePoint(new Point(), current).X;
                if (x < position || x > position + header.ActualWidth) continue;
                if (UIElementAutomationPeer.CreatePeerForElement(header)?.GetPattern(PatternInterface.Invoke) is IInvokeProvider action)
                    action.Invoke(); // preserve the DataGrid's original sort operation
                e.Handled = true;
                return;
            }
        }

        private static IEnumerable<DataGridColumnHeader> Headers(DependencyObject parent)
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is DataGridColumnHeader header) yield return header;
                foreach (var nested in Headers(child)) yield return nested;
            }
        }

        private void Wheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0) viewer.LineUp();
            else if (e.Delta < 0) viewer.LineDown();
            e.Handled = true;
        }

        protected override int VisualChildrenCount => visuals.Count;
        protected override Visual GetVisualChild(int index) => visuals[index];
        protected override Size MeasureOverride(Size constraint)
        {
            overlay.Measure(new Size(Math.Max(0, width), Math.Max(0, height)));
            return AdornedElement.RenderSize;
        }
        protected override Size ArrangeOverride(Size finalSize)
        {
            overlay.Arrange(new Rect(left, top, Math.Max(0, width), Math.Max(0, height)));
            return finalSize;
        }
    }
}
