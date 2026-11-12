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
        await NavigateWorkflow(9);
        await NavigateWorkflow(2);
        studies.Name = name + " pending";
        dialogs.Choices.Enqueue(PendingEditChoice.Cancel);
        await GoBackWorkflow();
        Check(workflowStep == 2 && studies.IsDirty && studies.Name == name + " pending",
            "cancelled Back preserves the study and its return route");
        dialogs.Choices.Enqueue(PendingEditChoice.Discard);
        await GoBackWorkflow();
        Check(workflowStep == 9 && !studies.IsDirty && studies.Name == name,
            "Back retries the same destination after an explicit discard");
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
            ((IInvokeProvider)new ButtonAutomationPeer(PhysicalStep).GetPattern(PatternInterface.Invoke)).Invoke();
            for (var attempt = 0; attempt < 200 && workspacePages.LastOrDefault() is not PhysicalActivityWindow; attempt++)
                await Task.Delay(25);
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            Check(workspacePages.LastOrDefault() is PhysicalActivityWindow, "menu opens integrated activity " + language);
            var page = (PhysicalActivityWindow)workspacePages[^1];
            Check(page.DataContext is PhysicalActivityEditor editor && editor.Rows.Count > 0,
                "menu opens populated physical table " + language);
            Check(page.Title == studies.PhysicalOpenLabel, "physical title localized " + language);
            Check(new System.Windows.Interop.WindowInteropHelper(page).Handle == IntPtr.Zero && WorkspaceContent.IsVisible,
                "no independent native window " + language);
            VisualEvidenceCapture.Capture(this, Path.Combine(directory, "activity-" + language + ".png"));
            await GoBackWorkflow();
            Check(workspacePages.Count == 0 && workflowStep == 8 && CostFlowsPanel.IsVisible,
                "one back returns directly to the invoking cost flows " + language);
            VisualEvidenceCapture.Capture(this, Path.Combine(directory, "cost-flows-return-" + language + ".png"));
            Check(AllocationPhase.IsExpanded && (string?)CostFlowStep.Tag == "Active",
                "correct phase and active marker after returning " + language);
            Check(studies.Current!.Revision == revision, "opening physical editor does not change study revision " + language);
        }
        File.WriteAllText(Path.Combine(directory, "checks.json"), JsonSerializer.Serialize(checks, new JsonSerializerOptions { WriteIndented = true }));
    }
}
