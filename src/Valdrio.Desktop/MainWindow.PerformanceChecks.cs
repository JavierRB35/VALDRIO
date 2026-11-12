using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows.Threading;

namespace Valdrio.Desktop;

public partial class MainWindow
{
    internal async Task RunPerformanceChecks(string directory, string path)
    {
        Directory.CreateDirectory(directory);
        viewModel!.ApplyPresentationPreset("en", "en-US", "UTC", false, false);
        var samples = new List<object>();
        void Record(string operation, int iteration, Stopwatch clock)
        {
            samples.Add(new { operation, iteration, milliseconds = clock.Elapsed.TotalMilliseconds });
            File.WriteAllText(Path.Combine(directory, "timings.json"),
                JsonSerializer.Serialize(samples, new JsonSerializerOptions { WriteIndented = true }));
        }
        var timer = Stopwatch.StartNew();
        await viewModel!.Company!.OpenPath(path);
        var studies = viewModel.Studies!;
        studies.StudyChoice = studies.Studies.FirstOrDefault(s =>
            s.Value.Name.StartsWith("T16", StringComparison.Ordinal)) ?? studies.Studies.First();
        await studies.LoadCommand.ExecuteAsync(null);
        Record("project-and-study", 0, timer);
        timer.Restart();
        var host = new PhysicalActivityEditor(studies);
        Record("activity-editor", 0, timer);
        for (var i = 0; i < 20; i++)
        {
            host.SelectedRow = host.Rows[i % host.Rows.Count];
            timer.Restart();
            _ = host.Structure;
            Record("structure-read", i, timer);
            timer.Restart();
            _ = host.Activity.ReadReferences(host.Study.CompanyId, host.Study.Id);
            Record("history-read", i, timer);
            timer.Restart();
            var window = new PhysicalCapacityAssistantWindow(host) { Owner = this };
            var calculator = (PhysicalCapacityAssistantEditor)window.DataContext;
            if (calculator.DeclaredQuantity != host.SelectedRow.NormalText ||
                !calculator.Context.StartsWith(host.SelectedRow.Node, StringComparison.Ordinal))
                throw new InvalidOperationException("Calculator did not receive the selected row.");
            Record("calculator-constructor", i, timer);
            timer.Restart();
            window.ShowIntegrated();
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            Record("calculator-render", i, timer);
            if (new System.Windows.Interop.WindowInteropHelper(window).Handle != IntPtr.Zero)
                throw new InvalidOperationException("Calculator created a native window.");
            if (i == 0) VisualEvidenceCapture.Capture(this, Path.Combine(directory, "calculator-integrated.png"));
            window.Close();
        }
        foreach (var language in new[] { "es", "en" })
        {
            viewModel.ApplyPresentationPreset(language, language == "es" ? "es-ES" : "en-US", "UTC", false, false);
            var pages = new IntegratedWorkspaceWindow[]
            {
                new PhysicalActivityWindow(studies) { Owner = this },
                new PrincipalRatesWindow(studies) { Owner = this },
                new AuxiliaryCapacityWindow(studies) { Owner = this }
            };
            foreach (var page in pages)
            {
                page.ShowIntegrated(); await page.InitializeAsync();
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                if (new System.Windows.Interop.WindowInteropHelper(page).Handle != IntPtr.Zero ||
                    !ReferenceEquals(WorkspaceContent.Content, page.Surface))
                    throw new InvalidOperationException("Page was not embedded: " + page.GetType().Name);
                if (page is PhysicalActivityWindow activityPage)
                {
                    var activityEditor = (PhysicalActivityEditor)page.DataContext;
                    foreach (var row in activityEditor.Rows.Take(2))
                    {
                        activityPage.ActivityTable.ScrollIntoView(row);
                        UpdateLayout();
                        var button = CalculatorButtons(activityPage.ActivityTable)
                            .First(b => ReferenceEquals(b.DataContext, row));
                        activityEditor.SelectedRow = activityEditor.Rows.Last();
                        button.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                        if (workspacePages.LastOrDefault() is not PhysicalCapacityAssistantWindow assistant ||
                            assistant.DataContext is not PhysicalCapacityAssistantEditor opened ||
                            !opened.Context.StartsWith(row.Node, StringComparison.Ordinal) ||
                            opened.DeclaredQuantity != row.NormalText)
                            throw new InvalidOperationException("Row icon opened another record.");
                        assistant.Close();
                    }
                }
                VisualEvidenceCapture.Capture(this, Path.Combine(directory, page.GetType().Name + "-" + language + ".png"));
                page.Close();
            }
        }
        if (workspacePages.Count != 0 || WorkspaceContent.Content is not null ||
            System.Windows.Application.Current.Windows.Count != 1)
            throw new InvalidOperationException("Workspace retained a closed surface.");
        File.WriteAllText(Path.Combine(directory, "integrated-checks.txt"),
            "Calculator and T14/T15/T16 rendered in the main content; no native window handles; stack released.");
    }
    private static IEnumerable<System.Windows.Controls.Button> CalculatorButtons(System.Windows.DependencyObject root)
    {
        if (root is System.Windows.Controls.Button button) yield return button;
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in CalculatorButtons(System.Windows.Media.VisualTreeHelper.GetChild(root, i)))
                yield return child;
    }
}
