using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Valdrio.Desktop;

public partial class MainWindow : Window
{
    private MainWindowViewModel? viewModel;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    public bool RunKeyboardNavigationSmoke()
    {
        UpdateLayout();

        Control[] sequence =
        [
            UiLanguageSelector,
            FormatCultureSelector,
            TimeZoneSelector,
            PendingEditBox,
            StartEditButton,
            CommitEditButton
        ];

        if (sequence.Any(control =>
            !control.IsVisible
            || !control.IsEnabled
            || !control.Focusable))
        {
            return false;
        }

        for (var index = 0; index < sequence.Length - 1; index++)
        {
            var current = sequence[index];
            var expectedNext = sequence[index + 1];

            if (!current.Focus() || !current.IsKeyboardFocusWithin)
            {
                return false;
            }

            if (!current.MoveFocus(
                new TraversalRequest(FocusNavigationDirection.Next)))
            {
                return false;
            }

            if (!expectedNext.IsKeyboardFocusWithin)
            {
                return false;
            }
        }

        return true;
    }

    public void ResetScrollForCapture()
    {
        Keyboard.ClearFocus();
        RootScrollViewer.ScrollToTop();
        UpdateLayout();
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
