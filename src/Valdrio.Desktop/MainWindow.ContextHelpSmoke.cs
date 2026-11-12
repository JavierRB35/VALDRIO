using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;

namespace Valdrio.Desktop;

public partial class MainWindow
{
    // Local acceptance harness: navigate real views using a disposable company,
    // exercise the reader and never save economic inputs or run a calculation.
    internal async Task RunContextHelpChecks(string directory)
    {
        Directory.CreateDirectory(directory);
        var vm = viewModel!;
        var studies = vm.Studies!;
        if (vm.Company?.Snapshot is null) throw new InvalidOperationException(vm.Company?.Status ?? "No project");
        await NavigateWorkflow(2);
        studies.StudyChoice = studies.Studies.First();
        await studies.LoadCommand.ExecuteAsync(null);
        var revision = studies.Current!.Revision;
        var checks = new List<string>();
        var covered = new HashSet<string>();
        void Check(bool valid, string text)
        { if (!valid) throw new InvalidOperationException(text); checks.Add(text); }
        async Task Idle() { await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle); UpdateLayout(); }
        foreach (var language in new[] { "es", "en" })
        {
            vm.ApplyPresentationPreset(language, language == "es" ? "es-ES" : "en-US", "Europe/Madrid", false, false);
            async Task Inspect(string expected)
            {
                await Idle();
                // Use the same routed key event consumed by the actual F1 handler.
                RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(this)!, 0, Key.F1)
                    { RoutedEvent = Keyboard.PreviewKeyDownEvent });
                await Idle();
                Check(contextHelpWindow?.IsVisible == true && shownHelpView == expected && shownHelpLanguage == language,
                    language + ": active help " + expected);
                var root = (DockPanel)contextHelpWindow!.Content;
                var reader = root.Children.OfType<FlowDocumentScrollViewer>().Single();
                var text = new TextRange(reader.Document.ContentStart, reader.Document.ContentEnd).Text;
                Check(text.Contains(language == "es" ? "Recorrido recomendado" : "Recommended workflow"),
                    language + ": complete trailing section " + expected);
                Check(reader.Document.Blocks.OfType<Table>().Any(), language + ": tables " + expected);
                contextHelpWindow.Width = 920; contextHelpWindow.Height = 730;
                contextHelpWindow.UpdateLayout();
                VisualEvidenceCapture.Capture(contextHelpWindow, Path.Combine(directory, language + "-" + expected + ".png"));
                contextHelpWindow.Width = 560; reader.Zoom = 150; contextHelpWindow.UpdateLayout();
                Check(reader.ActualWidth > 0 && reader.ActualHeight > 0, language + ": narrow reader " + expected);
                VisualEvidenceCapture.Capture(contextHelpWindow, Path.Combine(directory, language + "-" + expected + "-narrow.png"));
                reader.Zoom = 100;
                covered.Add(language + ":" + expected);
            }
            for (var step = 0; step <= 10; step++)
            {
                await NavigateWorkflow(step);
                Check(workflowStep == step, "Navigate to stage " + step);
                await Inspect(ContextHelpCatalog.ViewFor(step));
            }
            var physical = new PhysicalActivityEditor(studies);
            var pages = new Func<IntegratedWorkspaceWindow>[]
            {
                () => new ImportColumnMappingWindow { DataContext = studies.ImportColumns },
                () => new PhysicalActivityWindow(studies), () => new PhysicalCapacityAssistantWindow(physical),
                () => new PhysicalImportWindow(physical), () => new AuxiliaryCapacityWindow(studies),
                () => new PrincipalRatesWindow(studies), () => new CompositionWindow(studies), () => new EstimateWindow(studies),
                // The sample has closed allocations: instantiate the read-only
                // reuse preview directly, without reopening or adopting anything.
                () => new AllocationReuseWindow(new AllocationReuseEditor(vm.Company!.ActiveAllocations!,
                    vm.Company.ActiveStudies!, studies.Current!, System.Globalization.CultureInfo.GetCultureInfo(language)))
            };
            foreach (var create in pages)
            {
                var page = create();
                File.AppendAllText(Path.Combine(directory, "progress.txt"), language + ": " + page.GetType().Name + "\n");
                page.Owner = this; page.ShowIntegrated(); await page.InitializeAsync(); await Idle();
                if (page is EstimateWindow estimate) await estimate.InitializeAsync();
                await Inspect(ContextHelpCatalog.ViewFor(workflowStep, page.GetType().Name));
                // The old example has no capacity plan. Opening that editor creates
                // an unsaved default proposal, even without user edits. Discard only
                // that in-memory fixture proposal, never persist it during help checks.
                if (page is AuxiliaryCapacityWindow auxiliary) auxiliary.Editor.Dirty = false;
                page.Close(); await Idle();
                Check(!workspacePages.Contains(page), "Leave " + page.GetType().Name);
            }
            contextHelpWindow!.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,
                PresentationSource.FromVisual(contextHelpWindow)!, 0, Key.Escape)
                { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            Check(contextHelpWindow is null, language + ": Escape closes help");
        }
        Check(covered.Count == 34, "All 17 guides in both languages");
        Check(studies.Current!.Revision == revision, "Help leaves economic revision unchanged");
        File.WriteAllText(Path.Combine(directory, "context-help.json"), JsonSerializer.Serialize(new
            { Guides = covered.Order().ToArray(), Checks = checks, EconomicRevision = revision }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
