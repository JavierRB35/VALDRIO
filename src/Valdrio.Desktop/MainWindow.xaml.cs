using System.ComponentModel;
using System.Windows;

namespace Valdrio.Desktop;

public partial class MainWindow : Window
{
    private MainWindowViewModel? viewModel;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (viewModel is not null)
        {
            viewModel.PresentationChanged -= OnPresentationChanged;
        }

        viewModel = e.NewValue as MainWindowViewModel;

        if (viewModel is not null)
        {
            viewModel.PresentationChanged += OnPresentationChanged;
            ApplyPresentation();
        }
    }

    private void OnPresentationChanged(object? sender, EventArgs e) =>
        ApplyPresentation();

    private void ApplyPresentation()
    {
        if (viewModel is null)
        {
            return;
        }

        WpfPresentationAdapter.Apply(
            this,
            viewModel.CurrentPreferences,
            viewModel.ForceRtlForTest);
    }
}
