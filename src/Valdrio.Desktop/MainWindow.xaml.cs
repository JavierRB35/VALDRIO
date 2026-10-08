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
        InitializeGuidance();
        DataContextChanged += OnDataContextChanged;
    }

    public bool RunKeyboardNavigationSmoke()
    {
        workflowStep = 6; UpdateWorkflow();
        DevelopmentTools.IsExpanded = true;
        UpdateLayout();

        Control[] sequence =
        [
            UiLanguageSelector,
            FormatCultureSelector,
            TimeZoneSelector,
            FxQuoteBox,
            FxRateDateBox,
            FxRateSourceBox,
            ApplyFxRateButton,
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

    internal bool RunSectionLayoutSmoke()
    {
        UpdateLayout();
        var root=MainContent;
        var sections=root.Children.OfType<FrameworkElement>()
            .Where(e=>e.Visibility == Visibility.Visible && e.ActualHeight > 0)
            .Select(e=>e.TransformToAncestor(root).TransformBounds(new Rect(0,0,e.ActualWidth,e.ActualHeight)))
            .OrderBy(r=>r.Top).ToArray();
        return sections.Zip(sections.Skip(1),(first,next)=>first.Bottom<=next.Top+0.1).All(x=>x);
    }

    internal void ScrollToElement(FrameworkElement element)
    {
        RevealWorkflowElement(element);
        Keyboard.ClearFocus(); UpdateLayout();
        var offset=RootScrollViewer.VerticalOffset + element.TranslatePoint(new Point(0,0),RootScrollViewer).Y;
        RootScrollViewer.ScrollToVerticalOffset(offset); UpdateLayout();
    }

    private void OnFxQuoteGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) =>
        viewModel?.BeginFxEdit();

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
            BindGuidance();
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
