using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Polyline = System.Windows.Shapes.Polyline;
using System.Windows.Threading;

namespace Valdrio.Desktop;
public partial class MainWindow
{
    internal async Task RunParetoChecks(string directory)
    {
        Directory.CreateDirectory(directory);
        var main = viewModel!;
        var studies = main.Studies!;
        var vm = main.Allocations!;
        var checks = new List<string>();
        void Check(bool ok, string label) { if (!ok) throw new InvalidOperationException(label); checks.Add(label); }
        async Task Idle() { await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle); UpdateLayout(); }
        IEnumerable<T> Children<T>(DependencyObject parent) where T : DependencyObject
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T value) yield return value;
                foreach (var nested in Children<T>(child)) yield return nested;
            }
        }
        await NavigateWorkflow(2);
        studies.StudyChoice = studies.Studies.Single(s => s.Value.Name.StartsWith("02 ", StringComparison.Ordinal));
        await studies.LoadCommand.ExecuteAsync(null);
        Check(vm.IsPrimaryClosed && vm.PrincipalCosts.Count > 0, "Saved demo loads both allocation results");
        foreach (var language in new[] { "es", "en" })
        {
            main.ApplyPresentationPreset(language, language == "es" ? "es-ES" : "en-US", "UTC", false, false);
            Width = 1280; Height = 860;
            await NavigateWorkflow(5); await Idle();
            foreach (var phase in NavigationPhases) phase.IsExpanded = true;
            await Idle();
            Check(NavigationPhases.All(x => x.IsExpanded), "Independent menu groups " + language);
            Check(NavigationPhases.Length == 5 && CapacityPhase.IsExpanded && ResultsPhase.IsExpanded, "All five workflow groups visible " + language);
            Check(AllocationsStep.Tag as string == "Active", "Active location preserved " + language);
            ScrollToElement(AllocationPanel.PrimaryPareto); await Idle();
            VisualEvidenceCapture.Capture(this, Path.Combine(directory, language + "-primary-page.png"));
            foreach (var secondary in new[] { false, true })
            {
                await NavigateWorkflow(secondary ? 7 : 5); await Idle();
                var chart = secondary ? SecondaryPanel.Workspace.SecondaryPareto : AllocationPanel.PrimaryPareto;
                ScrollToElement(chart); await Idle();
                var chartTop = chart.TranslatePoint(new Point(), RootScrollViewer).Y;
                RootScrollViewer.ScrollToVerticalOffset(RootScrollViewer.VerticalOffset + chartTop); await Idle();
                var expected = secondary ? vm.PrincipalCosts.Count : vm.PrimaryNodeTotals.Count;
                Check(Children<Polyline>(chart).Single().Points.Count == expected, "One cumulative point per node " + language + secondary);
                Check(!Children<TextBlock>(chart).Any(x => x.Text.Contains("AllocationUI.")), "Localized chart " + language + secondary);
                var detail = Children<Expander>(chart).Single();
                detail.IsExpanded = true; await Idle();
                Check(detail.IsExpanded && Children<DataGrid>(chart).Single().Items.Count == expected, "Exact values table remains open " + language + secondary);
                Check(Children<DataGrid>(chart).Single().Focus(), "Exact values table accepts keyboard focus " + language + secondary);
                detail.IsExpanded = false; await Idle();
                VisualEvidenceCapture.Capture(this, Path.Combine(directory, language + (secondary ? "-secondary-page.png" : "-primary-page.png")));
                var preview = new Window { Width = 1200, Height = 560, Content = new ParetoChart { DataContext = vm, Secondary = secondary, Margin = new Thickness(20) }, Owner = this };
                preview.Show(); await Idle();
                VisualEvidenceCapture.Capture(preview, Path.Combine(directory, language + (secondary ? "-secondary-chart.png" : "-primary-chart.png")));
                preview.Close();
            }
            Width = 760; Height = 620; await Idle();
            Check(SecondaryStep.IsEnabled, "Secondary navigation available at minimum size " + language);
            VisualEvidenceCapture.Capture(this, Path.Combine(directory, language + "-compact.png"));
        }
        File.WriteAllText(Path.Combine(directory, "checks.json"), JsonSerializer.Serialize(checks, new JsonSerializerOptions { WriteIndented = true }));
    }
}
