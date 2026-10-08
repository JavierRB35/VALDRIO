using System.ComponentModel;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
namespace Valdrio.Desktop;
public partial class ResourceWorkspacePanel : UserControl
{
    private ResourceWorkspaceViewModel? vm;
    private CultureInfo? correctionCulture;
    public Task<bool>? CorrectionSaveTask { get; private set; }
    public ResourceWorkspacePanel()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Bind(); Loaded += (_, _) => Bind();
        Unloaded += (_, _) => { if (vm is not null) vm.PropertyChanged -= Changed; };
    }
    private void Bind()
    {
        if (vm is not null) vm.PropertyChanged -= Changed;
        vm = DataContext as ResourceWorkspaceViewModel;
        if (vm is not null) vm.PropertyChanged += Changed;
        RefreshHeaders();
    }
    private void Changed(object? sender, PropertyChangedEventArgs e) => RefreshHeaders();
    private void BeginCorrection(object sender, DataGridBeginningEditEventArgs e)
    {
        if (e.Column != CorrectionsColumn || vm?.CanEditCorrections != true || e.Row.Item is not ResourceGridRow row)
        { e.Cancel = true; return; }
        correctionCulture = CultureInfo.GetCultureInfo(row.FormatCulture);
    }
    private async void EndCorrection(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit || e.Column != CorrectionsColumn ||
            e.Row.Item is not ResourceGridRow row || vm is null) return;
        var box = FindTextBox(e.EditingElement);
        if (box is null) return;
        var text = box.Text;
        var culture = correctionCulture ?? CultureInfo.GetCultureInfo(row.FormatCulture);
        var editor = vm;
        // End the grid edit before a receipt rebuilds its items. No binding mutates saved rows.
        e.Cancel = true;
        if (!editor.ReserveCorrectionCommit()) return;
        await Dispatcher.InvokeAsync(() => ResourceGrid.CancelEdit(), DispatcherPriority.DataBind);
        CorrectionSaveTask = editor.SaveCorrectionAsync(row, text, culture);
        await CorrectionSaveTask;
    }
    private static TextBox? FindTextBox(System.Windows.DependencyObject element)
    {
        if (element is TextBox box) return box;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            if (FindTextBox(VisualTreeHelper.GetChild(element, i)) is { } child) return child;
        return null;
    }
    private void RefreshHeaders()
    {
        if (vm is null) return;
        void Headers(DataGrid grid, string[] keys) { for (var i = 0; i < keys.Length; i++) grid.Columns[i].Header = vm[keys[i]]; }
        Headers(ResourceGrid, ["Code", "Originals", "Converted", "Adjustments", "Effective", "PendingAmount", "Difference"]);
        CorrectionsColumn.IsReadOnly = vm.CorrectionsReadOnly;
        CorrectionsColumn.Header = vm["Adjustments"] + " (" + vm.FunctionalCurrency + ")";
        Headers(PartGrid, ["Amount", "Route", "Destination", "Behavior", "Layer"]);
        Headers(ReviewGrid, ["Amount", "Route", "Destination", "Behavior", "Layer"]);
    }
}
