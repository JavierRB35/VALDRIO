using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;

namespace Valdrio.Desktop;

public partial class MainWindow
{
    internal async Task RunAllocationLayoutChecks(string directory, bool baseline)
    {
        var main = viewModel!;
        var vm = main.Allocations!;
        var studies = main.Studies!;
        var checks = new List<string>();
        void Check(bool value, string message)
        { if (!value) throw new InvalidOperationException(message + " / " + vm.Status); checks.Add(message); }
        async Task Idle() { await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle); UpdateLayout(); }
        async Task Invoke(Button button)
        {
            Check(button.IsEnabled, button.Name + " enabled");
            ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
            await Idle();
            if (button.Command is IAsyncRelayCommand command && command.ExecutionTask is { } task) await task;
            await Idle();
        }
        await NavigateWorkflow(2);
        studies.StudyChoice = studies.Studies.Single(s => s.Value.Name.StartsWith("T12 DEMO", StringComparison.Ordinal));
        await studies.LoadCommand.ExecuteAsync(null);
        foreach (var language in new[] { "es", "en" })
        {
            main.ApplyPresentationPreset(language, language == "es" ? "es-ES" : "en-US", "UTC", false, false);
            Width = 1600; Height = 1000;
            await NavigateWorkflow(5); await Idle();
            RootScrollViewer.ScrollToTop(); await Idle();
            VisualEvidenceCapture.Capture(this, Path.Combine(directory, language + "-primary-edit.png"));
            await Invoke(AllocationPanel.TogglePrimaryButton);
            Check(vm.IsPrimaryClosed && AllocationPanel.LinesGrid.IsEnabled, "primary closes and tables remain selectable " + language);
            RootScrollViewer.ScrollToBottom(); await Idle();
            VisualEvidenceCapture.Capture(this, Path.Combine(directory, language + "-primary-results.png"));
            await VerifyFooter(AllocationPanel.TogglePrimaryButton, language + " primary");
            await NavigateWorkflow(7); await Idle();
            var panel = SecondaryPanel.Workspace;
            RootScrollViewer.ScrollToTop(); await Idle();
            VisualEvidenceCapture.Capture(this, Path.Combine(directory, language + "-secondary-edit.png"));
            await Invoke(panel.CalculateSecondaryButton);
            Check(vm.PrincipalCosts.Sum(r => r.TotalAmount) == 6500m, "secondary still calculates 6500 " + language);
            await Invoke(panel.ToggleSecondaryButton);
            Check(vm.IsSecondaryClosed, "secondary closes " + language);
            ScrollToElement(panel.PrincipalCostsGrid); await Idle();
            VisualEvidenceCapture.Capture(this, Path.Combine(directory, language + "-secondary-results.png"));
            await VerifyFooter(panel.ToggleSecondaryButton, language + " secondary");
            Width = 960; await Idle();
            ScrollToElement(panel.PrincipalCostsGrid); await Idle();
            VisualEvidenceCapture.Capture(this, Path.Combine(directory, language + "-secondary-narrow.png"));
            if (!baseline)
            {
                Check(panel.ToggleSecondaryButton.TranslatePoint(new Point(), this).Y < ActualHeight - 20,
                    "narrow footer remains in viewport " + language);
                Check(panel.PrincipalCostsGrid.ActualWidth <= RootScrollViewer.ViewportWidth,
                    "principal summary fits narrow viewport " + language);
            }
            await Invoke(panel.ToggleSecondaryButton);
            Check(!vm.IsSecondaryClosed, "secondary reopens " + language);
            await NavigateWorkflow(5); await Idle();
            await Invoke(AllocationPanel.TogglePrimaryButton);
            Check(!vm.IsPrimaryClosed, "primary reopens " + language);
        }
        File.WriteAllText(Path.Combine(directory, "checks.json"), JsonSerializer.Serialize(new { Baseline = baseline, Checks = checks }, new JsonSerializerOptions { WriteIndented = true }));

        async Task VerifyFooter(Button button, string label)
        {
            if (baseline) return;
            RootScrollViewer.ScrollToTop(); await Idle();
            var top = button.TranslatePoint(new Point(), this);
            RootScrollViewer.ScrollToBottom(); await Idle();
            var bottom = button.TranslatePoint(new Point(), this);
            Check(Math.Abs(top.Y - bottom.Y) < 1 && bottom.Y > ActualHeight / 2 && bottom.Y + button.ActualHeight < ActualHeight,
                "closure remains fixed at bottom " + label);
            Check(button.Focus() && button.IsKeyboardFocusWithin, "footer keyboard focus " + label);
        }
    }
}
