using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Valdrio.Desktop;

public partial class MainWindow
{
    // Native acceptance exercises the real navigation handlers and UI tree after T07 persistence checks.
    internal async Task RunVisualShellChecks(string directory)
    {
        var output = Path.Combine(directory, "visual-shell");
        Directory.CreateDirectory(output);
        var checks = new List<string>();
        void Check(bool value, string label)
        {
            if (!value) throw new InvalidOperationException("Visual shell: " + label);
            checks.Add(label);
        }
        bool Inside(FrameworkElement element)
        {
            var bounds = element.TransformToAncestor(this).TransformBounds(new Rect(element.RenderSize));
            return bounds.Left >= -1 && bounds.Top >= -1 && bounds.Right <= ActualWidth + 1 && bounds.Bottom <= ActualHeight + 1;
        }
        async Task Idle() => await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        Button[] navigation = [CompanyStep, ModelStep, StudyStep, ImportStep, ResourcesStep, AllocationsStep, SettingsStep];
        var main = viewModel!;
        var originalWidth = Width;
        var originalHeight = Height;
        try
        {
            foreach (var language in new[] { "es", "en" })
            {
                main.ApplyPresentationPreset(language, "es-ES", "Europe/Madrid", false, false);
                for (var index = 0; index < navigation.Length; index++)
                {
                    var button = navigation[index];
                    Check(button.IsEnabled, language + " navigation enabled with saved study: " + index);
                    ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
                    await Idle();
                    // The real handler refreshes studies asynchronously before navigation finishes.
                    var timer = System.Diagnostics.Stopwatch.StartNew();
                    while ((main.Company?.WorkspaceIdle == false || workflowStep != index) && timer.Elapsed < TimeSpan.FromSeconds(15))
                    { await Task.Delay(25); await Idle(); }
                    UpdateLayout();
                    // The accordion uses an active Tag and marker, not a bold
                    // local FontWeight. Preferences retains its original style.
                    bool activePresentation;
                    if (index == 6)
                    {
                        activePresentation = button.FontWeight == FontWeights.Bold;
                    }
                    else
                    {
                        var marker = index switch
                        {
                            0 => CompanyStepMarker,
                            1 => ModelStepMarker,
                            2 => StudyStepMarker,
                            3 => ImportStepMarker,
                            4 => ResourcesStepMarker,
                            5 => AllocationsStepMarker,
                            _ => throw new InvalidOperationException("Unexpected navigation index")
                        };
                        activePresentation = Equals(button.Tag, "Active") &&
                            marker.Background is SolidColorBrush selectedBrush &&
                            selectedBrush.Color == Colors.White &&
                            NavigationPhases[PhaseForStep(index)].IsExpanded &&
                            NavigationPhases.Count(phase => phase.IsExpanded) == 1;
                    }
                    Check(workflowStep == index && activePresentation,
                        language + " sidebar invokes the original handler and marks selected step: " + index);
                    Check(button.Focus() && button.IsKeyboardFocusWithin, language + " sidebar keyboard focus: " + index);
                    Check(RootScrollViewer.ViewportHeight > 80 && Inside(WorkflowBack), language + " content and back action remain accessible: " + index);
                    if (index == 6)
                    {
                        DevelopmentTools.IsExpanded = true;
                        await Idle(); UpdateLayout();
                        Check(RunDevelopmentToolsLayoutSmoke(), language + " expanded development tools have separate rows");
                    }
                    ResetScrollForCapture();
                    VisualEvidenceCapture.Capture(this, Path.Combine(output, $"{language}-step-{index + 1}.png"));
                }
            }
            Width = 760; Height = 620;
            await NavigateWorkflow(4); await Idle();
            Check(RootScrollViewer.ScrollableWidth > 0 && RootScrollViewer.ViewportHeight > 80 && Inside(WorkflowBack),
                "760x620 keeps navigation and footer visible; wide forms have horizontal scroll");
            VisualEvidenceCapture.Capture(this, Path.Combine(output, "minimum-window.png"));
            Width = originalWidth; Height = originalHeight;
            await NavigateWorkflow(1);
            foreach (var scale in new[] { 1.25, 1.5, 2.0 })
            {
                // Layout scaling probes DIP constraints without changing the user's monitor/DPI settings.
                ShellLayout.LayoutTransform = new ScaleTransform(scale, scale);
                await Idle(); UpdateLayout();
                Check(RootScrollViewer.ViewportHeight > 48 && Inside(WorkflowBack) && Inside(WorkflowNext) && Inside(Sidebar),
                    "scaled WPF layout retains scrollable work area and footer: " + scale);
                VisualEvidenceCapture.Capture(this, Path.Combine(output, $"layout-scale-{scale * 100:0}.png"));
            }
            ShellLayout.LayoutTransform = Transform.Identity;
            await NavigateWorkflow(6); DevelopmentTools.IsExpanded = true;
            foreach (var scale in new[] { 1.0, 1.25, 1.5, 2.0 })
            {
                ShellLayout.LayoutTransform = new ScaleTransform(scale, scale);
                await Idle(); UpdateLayout();
                Check(RunDevelopmentToolsLayoutSmoke(), "expanded development tools layout scale: " + scale);
            }
            ShellLayout.LayoutTransform = Transform.Identity;
            main.ApplyPresentationPreset("en", "es-ES", "Europe/Madrid", true, true);
            await NavigateWorkflow(6); await Idle();
            Check(Inside(Sidebar) && Inside(WorkflowBack) && RootScrollViewer.ViewportHeight > 80,
                "pseudo RTL retains sidebar, footer and work area");
            VisualEvidenceCapture.Capture(this, Path.Combine(output, "pseudo-rtl.png"));
            File.WriteAllText(Path.Combine(output, "interaction.json"), JsonSerializer.Serialize(new
            {
                Status = "passed", PassedChecks = checks,
                MonitorDpi = VisualTreeHelper.GetDpi(this).PixelsPerInchX,
                ScaleMethod = "WPF LayoutTransform at 125/150/200%; monitor settings unchanged"
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally
        {
            ShellLayout.LayoutTransform = Transform.Identity;
            Width = originalWidth; Height = originalHeight;
            main.ApplyPresentationPreset("es", "es-ES", "Europe/Madrid", false, false);
        }
    }
}
