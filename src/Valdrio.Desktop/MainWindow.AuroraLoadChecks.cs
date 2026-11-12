using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Valdrio.Application;
using Valdrio.Domain;

namespace Valdrio.Desktop;

public partial class MainWindow
{
    // Opt-in local measurement harness. Always pass a disposable copy of AURORA.
    internal async Task RunAuroraLoadChecks(string directory, string path)
    {
        Directory.CreateDirectory(directory);
        viewModel!.ApplyPresentationPreset("es", "es-ES", "UTC", false, false);
        var samples = new List<object>();
        async Task Measure(string operation, int iteration, Func<Task> action)
        {
            File.WriteAllText(Path.Combine(directory, "progress.txt"), $"{operation} {iteration}");
            var clock = Stopwatch.StartNew();
            await action();
            samples.Add(new { operation, iteration, milliseconds = clock.Elapsed.TotalMilliseconds });
            File.WriteAllText(Path.Combine(directory, "timings.json"),
                JsonSerializer.Serialize(samples, new JsonSerializerOptions { WriteIndented = true }));
        }
        async Task Idle() => await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        await Measure("company-open", 0, () => viewModel.Company!.OpenPath(path));
        var studies = viewModel.Studies!;
        studies.StudyChoice = studies.Studies.Single(s => s.Value.Name.StartsWith("01 ", StringComparison.Ordinal));
        await Measure("study-load", 0, () => studies.LoadCommand.ExecuteAsync(null));
        if (studies.Current?.Id != studies.StudyChoice.Value.Id)
            throw new InvalidOperationException("Requested AURORA study was not loaded.");
        PhysicalActivityWindow? activity = null;
        for (var i = 0; i < 5; i++)
        {
            foreach (var step in new[] { 1, 2, 4, 5, 7, 8 })
            {
                await Measure("navigation-prepare-" + step, i, () => NavigateWorkflow(step));
                await Measure("navigation-render-" + step, i, Idle);
                if (i == 0) VisualEvidenceCapture.Capture(this, Path.Combine(directory, $"screen-{step}.png"));
            }
            await Measure("activity-prepare", i, () =>
            {
                activity = new PhysicalActivityWindow(studies) { Owner = this };
                return activity.InitializeAsync();
            });
            var editor = (PhysicalActivityEditor)activity!.DataContext;
            if (editor.Rows.Count != 5 || editor.Structure.Nodes.Count(n => n.Role == NodeRole.Principal) != 3 ||
                editor.Structure.Nodes.Count(n => n.Role == NodeRole.Auxiliary) != 2)
                throw new InvalidOperationException("Expected AURORA structure with three principal and two auxiliary centers.");
            File.WriteAllText(Path.Combine(directory, "context.json"), JsonSerializer.Serialize(new
            {
                editor.ProjectName, editor.StudyName, editor.StudyPeriod,
                nodes = editor.Structure.Nodes.Select(n => new { n.Code, n.Role }),
                rows = editor.Rows.Select(r => new { r.Node, r.NormalText, r.RealText }),
                effective = viewModel.CostFlows?.EffectiveTotal, final = viewModel.CostFlows?.FinalTotal,
                reconciliation = viewModel.CostFlows?.ReconciliationState
            }, new JsonSerializerOptions { WriteIndented = true }));
            await Measure("activity-render", i, async () => { activity.ShowIntegrated(); await Idle(); });
            if (i == 0) VisualEvidenceCapture.Capture(this, Path.Combine(directory, "activity.png"));
            for (var j = 0; j < 4; j++)
            {
                var row = editor.Rows[(i * 4 + j) % editor.Rows.Count];
                activity.ActivityTable.ScrollIntoView(row); UpdateLayout();
                var button = CalculatorButtons(activity.ActivityTable).First(b => ReferenceEquals(b.DataContext, row));
                editor.SelectedRow = editor.Rows.Last();
                await Measure("calculator-click-to-ready", i * 4 + j, async () =>
                {
                    button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                    await Idle();
                });
                if (workspacePages.LastOrDefault() is not PhysicalCapacityAssistantWindow calculator ||
                    calculator.DataContext is not PhysicalCapacityAssistantEditor opened ||
                    !opened.Context.StartsWith(row.Node, StringComparison.Ordinal) ||
                    opened.DeclaredQuantity != row.NormalText ||
                    new System.Windows.Interop.WindowInteropHelper(calculator).Handle != IntPtr.Zero)
                    throw new InvalidOperationException("Calculator did not open embedded with the clicked row.");
                if (i == 0 && j == 0) VisualEvidenceCapture.Capture(this, Path.Combine(directory, "calculator.png"));
                calculator.Close();
            }
            activity.Close();
            foreach (var kind in new[] { "rates", "auxiliary" })
            {
                IntegratedWorkspaceWindow? page = null;
                await Measure(kind + "-prepare", i, () =>
                {
                    page = kind == "rates" ? new PrincipalRatesWindow(studies) : new AuxiliaryCapacityWindow(studies);
                    page.Owner = this;
                    return page.InitializeAsync();
                });
                await Measure(kind + "-render", i, async () => { page!.ShowIntegrated(); await Idle(); });
                if (new System.Windows.Interop.WindowInteropHelper(page!).Handle != IntPtr.Zero)
                    throw new InvalidOperationException("Working page created a native window.");
                if (page is PrincipalRatesWindow rates)
                {
                    if (rates.Editor.Report is not { } report || report.Rows.Count != 3 ||
                        report.Rows.Any(r => r.Result is null) || report.Rows.Sum(r => r.Cost) != 3250m)
                        throw new InvalidOperationException("AURORA rates failed to load all three centers.");
                    File.WriteAllText(Path.Combine(directory, "rates-report.json"), CanonicalSnapshotJson.Serialize(report));
                }
                if (page is AuxiliaryCapacityWindow auxiliaryPage && auxiliaryPage.Editor.Rows.Count != 2)
                    throw new InvalidOperationException("AURORA auxiliary centers failed to load.");
                if (i == 0) VisualEvidenceCapture.Capture(this, Path.Combine(directory, kind + ".png"));
                // AURORA predates T16: its unsaved default proposal triggers a confirmation
                // even without edits. Test cleanup discards only this untouched test surface.
                if (page is AuxiliaryCapacityWindow auxiliary && auxiliary.Editor.Dirty)
                {
                    File.WriteAllText(Path.Combine(directory, "auxiliary-default.txt"), auxiliary.Editor.Feedback);
                    WorkspaceContent.Content = null;
                    workspacePages.Remove(page);
                    page.Release();
                    WorkspaceContent.Visibility = Visibility.Collapsed;
                    RootScrollViewer.Visibility = Visibility.Visible;
                    UpdateWorkflow();
                }
                else page!.Close();
            }
        }
        var cancelPage = new PhysicalCapacityAssistantWindow((PhysicalActivityEditor)activity!.DataContext) { Owner = this };
        cancelPage.ShowIntegrated();
        await Idle();
        var attempts = 0;
        System.ComponentModel.CancelEventHandler refuseClose = (_, args) => { attempts++; args.Cancel = true; };
        cancelPage.Closing += refuseClose;
        var cancelEditor = (PhysicalCapacityAssistantEditor)cancelPage.DataContext;
        CalculatorButtons(cancelPage.Surface!).Single(b => Equals(b.Content, cancelEditor.CancelLabel))
            .RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        if (attempts != 1 || workspacePages.LastOrDefault() != cancelPage)
            throw new InvalidOperationException("Cancel must ask once and retain a page whose close is refused.");
        cancelPage.Closing -= refuseClose;
        cancelPage.Close();
        await NavigateWorkflow(8);
        var flows = viewModel.CostFlows!;
        if (flows.SelectedRun?.Run.Output?.Secondary is not { } result ||
            result.Reconciliations.Any(r => r.Difference.Amount != 0) ||
            !flows.Arrows.Any(a => a.Internal))
            throw new InvalidOperationException("Expected reconciled reciprocal AURORA result.");
        var savedRun = CanonicalSnapshotJson.Serialize(flows.SelectedRun.Run);
        CostFlowsPanel.ExpandedAnalysis.IsChecked = true;
        CostFlowsPanel.ExpandedAnalysis.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        CostFlowsPanel.AnalysisTabs.SelectedIndex = 1;
        CostFlowsPanel.FlowZoom.Value = 1.25;
        await Idle();
        if (CostFlowsPanel.ExecutionCard.Visibility != Visibility.Collapsed || !CostFlowsPanel.TotalsPanel.IsVisible)
            throw new InvalidOperationException("Expanded reciprocal graph lost the totals.");
        VisualEvidenceCapture.Capture(this, Path.Combine(directory, "reciprocal-expanded.png"));
        if (savedRun != CanonicalSnapshotJson.Serialize(flows.SelectedRun.Run))
            throw new InvalidOperationException("Presentation changed the saved run.");
        if (workspacePages.Count != 0 || WorkspaceContent.Content is not null ||
            System.Windows.Application.Current.Windows.Count != 1)
            throw new InvalidOperationException("Workspace retained a closed surface.");
        File.WriteAllText(Path.Combine(directory, "completed.txt"), "AURORA: 5 screen passes; 20 real calculator clicks; no writes requested.");
    }
}
