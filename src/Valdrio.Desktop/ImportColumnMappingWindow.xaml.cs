using System.Windows;
using Microsoft.Win32;

namespace Valdrio.Desktop;

public partial class ImportColumnMappingWindow : IntegratedWorkspaceWindow
{
    private ImportColumnMappingViewModel? viewModel;
    public ImportColumnMappingWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (viewModel is not null) viewModel.PropertyChanged -= Changed;
            viewModel = DataContext as ImportColumnMappingViewModel;
            if (viewModel is not null) viewModel.PropertyChanged += Changed;
            RefreshHeaders(); RefreshGuidance();
        };
        Closed += (_, _) => { if (viewModel is not null) viewModel.PropertyChanged -= Changed; };
        Closing += (_, e) =>
        {
            if (DataContext is ImportColumnMappingViewModel { IsBusy: true } vm)
            { e.Cancel = true; vm.CancelCommand.Execute(null); }
        };
    }
    private void CloseMapping(object sender, RoutedEventArgs e) => Close();
    private void Changed(object? sender, System.ComponentModel.PropertyChangedEventArgs e) { RefreshHeaders(); RefreshGuidance(); }
    private void RefreshHeaders()
    {
        if (viewModel is null) return;
        AccountSelectionGrid.Columns[0].Header = viewModel["IncludeAccount"];
        AccountSelectionGrid.Columns[1].Header = viewModel["Row"];
        AccountSelectionGrid.Columns[2].Header = viewModel["SourceCode"];
        AccountSelectionGrid.Columns[3].Header = viewModel["AccountDescriptionHeader"];
        AccountSelectionGrid.Columns[4].Header = viewModel["Parsed"];
        GroupedPreviewGrid.Columns[0].Header = viewModel["SourceCode"];
        GroupedPreviewGrid.Columns[1].Header = viewModel["AccountDescriptionHeader"];
        GroupedPreviewGrid.Columns[2].Header = viewModel["SourceRecords"];
        GroupedPreviewGrid.Columns[3].Header = viewModel["Parsed"];
        SelectionPreviewGrid.Columns[0].Header = viewModel["SourceCode"];
        SelectionPreviewGrid.Columns[1].Header = viewModel["AccountDescriptionHeader"];
        SelectionPreviewGrid.Columns[2].Header = viewModel["SourceRecords"];
        SelectionPreviewGrid.Columns[3].Header = viewModel["Parsed"];
        var comparisonKeys = new[] { "SourceCode", "AccountDescriptionHeader", "EarlierAmount", "CurrentAmount", "DifferenceAmount", "EarlierRecords" };
        for (var i = 0; i < comparisonKeys.Length; i++) ComparisonGrid.Columns[i].Header = viewModel[comparisonKeys[i]];
        var fxKeys = new[] { "Row", "DocumentDateColumn", "Original", "Converted", "RateId", "RateDate", "RateSource", "RoundingAdjustment" };
        for (var i = 0; i < fxKeys.Length; i++) ConversionGrid.Columns[i].Header = viewModel[fxKeys[i]];
        var keys = new[] { "Row", "AccountColumn", "Original", "OriginalDebit", "OriginalCredit", "Parsed", "Issues", "DescriptionColumn" };
        for (var i = 0; i < keys.Length; i++) PreviewGrid.Columns[i].Header = viewModel[keys[i]];
        PreviewGrid.Columns[2].Visibility = viewModel.IsSignedAmount ? Visibility.Visible : Visibility.Collapsed;
        PreviewGrid.Columns[3].Visibility = PreviewGrid.Columns[4].Visibility = viewModel.IsDebitCredit ? Visibility.Visible : Visibility.Collapsed;
    }
}

public sealed class ImportSourceDialogs : IImportSourceDialogs
{
    public string? SelectSource()
    {
        var dialog = new OpenFileDialog { Filter = "CSV / Excel (*.csv;*.xlsx)|*.csv;*.xlsx",
            CheckFileExists = true, Multiselect = false };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}

public sealed class QueuedImportSourceDialogs : IImportSourceDialogs
{
    public Queue<string?> Paths { get; } = new();
    public string? SelectSource() => Paths.Count == 0 ? null : Paths.Dequeue();
}
