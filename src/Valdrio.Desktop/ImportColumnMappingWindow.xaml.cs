using System.Windows;
using Microsoft.Win32;

namespace Valdrio.Desktop;

public partial class ImportColumnMappingWindow : Window
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
            RefreshHeaders();
        };
        Closed += (_, _) => { if (viewModel is not null) viewModel.PropertyChanged -= Changed; };
        Closing += (_, e) =>
        {
            if (DataContext is ImportColumnMappingViewModel { IsBusy: true } vm)
            { e.Cancel = true; vm.CancelCommand.Execute(null); }
        };
    }
    private void CloseMapping(object sender, RoutedEventArgs e) => Close();
    private void Changed(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => RefreshHeaders();
    private void RefreshHeaders()
    {
        if (viewModel is null) return;
        var keys = new[] { "Row", "AccountColumn", "Original", "Parsed", "Issues" };
        for (var i = 0; i < keys.Length; i++) PreviewGrid.Columns[i].Header = viewModel[keys[i]];
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
