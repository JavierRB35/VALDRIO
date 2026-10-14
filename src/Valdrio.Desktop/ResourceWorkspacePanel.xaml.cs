using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
namespace Valdrio.Desktop;
public partial class ResourceWorkspacePanel : UserControl
{
    public static readonly DependencyProperty ShowDistributionToolsProperty = DependencyProperty.Register(
        nameof(ShowDistributionTools), typeof(bool), typeof(ResourceWorkspacePanel), new PropertyMetadata(true));
    public bool ShowDistributionTools
    {
        get => (bool)GetValue(ShowDistributionToolsProperty);
        set => SetValue(ShowDistributionToolsProperty, value);
    }
    private ResourceWorkspaceViewModel? vm;
    private CultureInfo? correctionCulture;
    public Task<bool>? CorrectionSaveTask { get; private set; }
    public ResourceWorkspacePanel()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Bind(); Loaded += (_, _) => Bind();
        Unloaded += (_, _) =>
        {
            if (vm is not null)
            {
                vm.PropertyChanged -= Changed;
                vm.RowsRebuilding -= BeforeRowsRebuilding;
            }
        };
    }
    private void Bind()
    {
        if (vm is not null)
        {
            vm.PropertyChanged -= Changed;
            vm.RowsRebuilding -= BeforeRowsRebuilding;
        }
        vm = DataContext as ResourceWorkspaceViewModel;
        if (vm is not null)
        {
            vm.PropertyChanged += Changed;
            vm.RowsRebuilding += BeforeRowsRebuilding;
        }
        RefreshHeaders();
    }
    private void BeforeRowsRebuilding(object? sender, EventArgs e)
    {
        // SelectedCells/CurrentCell belong to concrete row instances, not to
        // the persisted resource/part IDs. Discard those visual references before
        // Rows.Clear()/Parts.Clear() causes DataGrid's virtualized generator to
        // raise automation selection events for cells with null items.
        // The ViewModel retains IDs and restores ResourceChoice/PartChoice once
        // the new rows have been populated.
        foreach (var grid in new[] { ResourceGrid, PartGrid, ReviewGrid })
        {
            grid.UnselectAllCells();
            grid.CurrentCell = default;
        }
    }

    private void Changed(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == "Item[]") RefreshHeaders();
        else if (e.PropertyName == nameof(ResourceWorkspaceViewModel.CorrectionsReadOnly) && vm is not null)
            CorrectionsColumn.IsReadOnly = vm.CorrectionsReadOnly;
    }
    private static T? Ancestor<T>(DependencyObject? source) where T : DependencyObject
    {
        while (source is not null)
        {
            if (source is T found) return found;
            source = source is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D ?
                VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
        }
        return null;
    }
    private void SelectCorrectionCell(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        var cell = Ancestor<DataGridCell>(e.OriginalSource as DependencyObject);
        if (cell?.Column != CorrectionsColumn || cell.IsEditing || vm?.CanEditCorrections != true) return;
        var row = Ancestor<DataGridRow>(cell);
        if (row is null) return;
        ResourceGrid.SetCurrentValue(DataGrid.SelectedItemProperty, row.Item);
        ResourceGrid.CurrentCell = new(row.Item, CorrectionsColumn);
        cell.Focus(); ResourceGrid.BeginEdit();
        e.Handled = true;
    }
    private void PrepareCorrection(object sender, DataGridPreparingCellForEditEventArgs e)
    {
        if (e.Column != CorrectionsColumn) return;
        var box = FindTextBox(e.EditingElement);
        if (box is null) return;
        box.Focus(); box.SelectAll();
    }
    private void TypeCorrection(object sender, TextCompositionEventArgs e)
    {
        if (ResourceGrid.CurrentColumn != CorrectionsColumn || vm?.CanEditCorrections != true ||
            e.OriginalSource is TextBox) return;
        if (!ResourceGrid.BeginEdit()) return;
        if (ResourceGrid.CurrentItem is not ResourceGridRow row) return;
        var box = FindTextBox(CorrectionsColumn.GetCellContent(row));
        if (box is null) return;
        box.SelectAll(); box.SelectedText = e.Text; box.CaretIndex = box.Text.Length; e.Handled = true;
    }
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
    private static TextBox? FindTextBox(System.Windows.DependencyObject? element)
    {
        if (element is null) return null;
        if (element is TextBox box) return box;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            if (FindTextBox(VisualTreeHelper.GetChild(element, i)) is { } child) return child;
        return null;
    }
    private void RefreshHeaders()
    {
        if (vm is not null) RuleAdjustmentsColumn.Header = vm.RuleText("RuleTotal");
        if (vm is null) return;
        void Headers(DataGrid grid, string[] keys) { for (var i = 0; i < keys.Length; i++) grid.Columns[i].Header = vm[keys[i]]; }
        Headers(ResourceGrid, ["Code", "Originals", "Converted", "Adjustments", "Effective", "PendingAmount", "Difference", "AssignmentStatus"]);
        ResourceGrid.Columns[4].Header = vm["EffectiveBasis"];
        // Preparation only reconciles amounts; allocation status belongs to step 6.
        ResourceGrid.Columns[5].Visibility = ResourceGrid.Columns[7].Visibility = ShowDistributionTools ? Visibility.Visible : Visibility.Collapsed;
        CorrectionsColumn.IsReadOnly = vm.CorrectionsReadOnly;
        CorrectionsColumn.Header = (vm.HasRuleAdjustments ? vm.RuleText("ManualTotal") : vm["Adjustments"]) + " (" + vm.FunctionalCurrency + ")";
        Headers(PartGrid, ["Amount", "Route", "Destination", "Behavior", "Layer"]);
        Headers(ReviewGrid, ["Amount", "Route", "Destination", "Behavior", "Layer"]);
    }
}
