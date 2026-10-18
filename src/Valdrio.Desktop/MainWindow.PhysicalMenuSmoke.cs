using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Threading;

namespace Valdrio.Desktop;

public partial class MainWindow
{
    internal async Task RunPhysicalMenuChecks(string directory, string path, QueuedModelEditorDialogs dialogs)
    {
        Directory.CreateDirectory(directory);
        var main = viewModel!;
        var studies = main.Studies!;
        var checks = new List<string>();
        void Check(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); checks.Add(message); }
        UpdateWorkflow();
        Check(!PhysicalStep.IsEnabled, "no project: physical menu disabled");
        await main.Company!.OpenPath(path);
        await NavigateWorkflow(9);
        Check(PhysicalHome.IsVisible && !OpenPhysicalButton.IsEnabled, "no selected analysis: guidance without opening an editor");
        await NavigateWorkflow(2);
        studies.StudyChoice = studies.Studies.Single(s => s.Value.Name.StartsWith("T12 DEMO", StringComparison.Ordinal));
        await studies.LoadCommand.ExecuteAsync(null);
        var revision = studies.Current!.Revision;
        var name = studies.Name;
        studies.Name = name + " pending";
        dialogs.Choices.Enqueue(PendingEditChoice.Cancel);
        await NavigateWorkflow(9);
        Check(workflowStep == 2 && studies.IsDirty && studies.Name == name + " pending", "cancel preserves pending study edit and current page");
        dialogs.Choices.Enqueue(PendingEditChoice.Discard);
        await NavigateWorkflow(9);
        Check(workflowStep == 9 && !studies.IsDirty && studies.Name == name, "explicit discard permits physical navigation");
        foreach (var language in new[] { "es", "en" })
        {
            main.ApplyPresentationPreset(language, language == "es" ? "es-ES" : "en-US", "UTC", false, false);
            await NavigateWorkflow(8);
            Width = language == "es" ? 1280 : 960;
            Height = 860;
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            UpdateLayout();
            Check(PhysicalStep.IsVisible && PhysicalStep.IsEnabled && PhysicalStep.Focus(), "menu visible and keyboard focusable " + language);
            VisualEvidenceCapture.Capture(this, Path.Combine(directory, "menu-" + language + ".png"));
            Exception? failure = null;
            bool opened = false;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            timer.Tick += (_, _) =>
            {
                var window = OwnedWindows.OfType<PhysicalActivityWindow>().FirstOrDefault();
                if (window is null) return;
                timer.Stop();
                try
                {
                    opened = true;
                    Check(window.DataContext is PhysicalActivityEditor editor && editor.Rows.Count > 0,
                        "menu opens populated physical table " + language);
                    Check(window.Title == studies.PhysicalOpenLabel, "physical title localized " + language);
                    VisualEvidenceCapture.Capture(window, Path.Combine(directory, "activity-" + language + ".png"));
                }
                catch (Exception error) { failure = error; }
                finally { window.Close(); }
            };
            timer.Start();
            ((IInvokeProvider)new ButtonAutomationPeer(PhysicalStep).GetPattern(PatternInterface.Invoke)).Invoke();
            // The real Click handler enters a modal dispatcher loop; pumping the dispatcher lets it finish.
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            timer.Stop();
            if (failure is not null) throw failure;
            Check(opened && workflowStep == 9 && PhysicalHome.IsVisible, "menu opens editor and returns to physical phase " + language);
            VisualEvidenceCapture.Capture(this, Path.Combine(directory, "physical-home-" + language + ".png"));
            Check(AllocationPhase.IsExpanded && (string?)PhysicalStep.Tag == "Active", "correct phase and active marker " + language);
            Check(studies.Current!.Revision == revision, "opening physical editor does not change study revision " + language);
            await GoBackWorkflow();
            Check(workflowStep == 8 && CostFlowsPanel.IsVisible, "back returns to cost flows " + language);
        }
        File.WriteAllText(Path.Combine(directory, "checks.json"), JsonSerializer.Serialize(checks, new JsonSerializerOptions { WriteIndented = true }));
    }
}
