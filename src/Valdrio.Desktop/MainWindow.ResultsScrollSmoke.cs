using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Valdrio.Application;
using Valdrio.Domain;

namespace Valdrio.Desktop;

public partial class MainWindow
{
    internal async Task RunResultsScrollChecks(string directory)
    {
        Directory.CreateDirectory(directory);
        var main = viewModel!; var company = main.Company!; var studies = main.Studies!; var vm = main.Allocations!;
        var checks = new List<string>();
        void Check(bool value, string message)
        { if (!value) throw new InvalidOperationException("Results/scroll: " + message); checks.Add(message); }
        async Task Idle() { await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle); UpdateLayout(); }
        IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T match) yield return match;
                foreach (var nested in Descendants<T>(child)) yield return nested;
            }
        }
        void Click(UIElement element) => element.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,
            Environment.TickCount, MouseButton.Left) { RoutedEvent = Mouse.PreviewMouseDownEvent });
        void Wheel(UIElement element, int delta) => element.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice,
            Environment.TickCount, delta) { RoutedEvent = Mouse.PreviewMouseWheelEvent });
        await NavigateWorkflow(2);
        studies.StudyChoice = studies.Studies.Single(s => s.Value.Name.StartsWith("02 -", StringComparison.Ordinal));
        await studies.LoadCommand.ExecuteAsync(null);
        foreach (var language in new[] { "es", "en" })
        {
            main.ApplyPresentationPreset(language, language == "es" ? "es-ES" : "en-US", "UTC", false, false);
            Width = 1600; Height = 1000;
            await NavigateWorkflow(5); await Idle();
            Check(vm.IsPrimaryClosed && vm.PrimaryResults.All(r => !string.IsNullOrWhiteSpace(r.Behavior)), "closed primary shows behavior " + language);
            vm.SelectedKey = vm.Keys.First(); await vm.LoadKeyCommand.ExecuteAsync(null); await Idle();
            Check(AllocationPanel.LinesGrid.IsEnabled && !vm.CanEditComposition, "closed primary key remains navigable " + language);
            Check(Descendants<TextBox>(AllocationPanel.LinesGrid).All(b => b.IsReadOnly), "primary weights are read-only " + language);
            ScrollToElement(AllocationPanel.DefinitionSection); await Idle();
            VisualEvidenceCapture.Capture(this, Path.Combine(directory, language + "-locked-key.png"));
            ScrollToElement(AllocationPanel.PrimaryResultsGrid); await Idle();
            VisualEvidenceCapture.Capture(this, Path.Combine(directory, language + "-primary.png"));
            await NavigateWorkflow(7); await Idle();
            var panel = SecondaryPanel.Workspace;
            Check(vm.IsSecondaryClosed && vm.SecondaryResults.Count > 0, "publication loaded without calculating " + language);
            var snapshot = company.ActiveAllocations!.ReadAllocations(studies.Current!.CompanyId, studies.Current.Id);
            var publication = snapshot.SecondaryPublication!;
            Check(vm.PrincipalCosts.Sum(r => r.TotalAmount) == publication.Facts.Sum(f => f.PostedAmount.Amount), "principal totals equal posted ledger " + language);
            Check(vm.PrincipalCosts.All(r => r.PrimaryAmount + r.AuxiliaryAmount == r.TotalAmount), "primary plus auxiliary reconciles per principal " + language);
            Check(vm.SecondaryResults.Sum(r => r.PostedAmount) == vm.PrincipalCosts.Sum(r => r.TotalAmount), "contributions reconcile with principal totals " + language);
            foreach (var factGroup in publication.Facts.GroupBy(f => (f.TerminalNodeId, f.Behavior)))
                Check(vm.SecondaryResults.Where(r => r.TerminalNodeId == factGroup.Key.TerminalNodeId && r.SourceBehavior == factGroup.Key.Behavior).Sum(r => r.PostedAmount)
                    == factGroup.Sum(f => f.PostedAmount.Amount), "behavior reconciles per destination " + language);
            vm.SelectedKey = vm.Keys.First(); await vm.LoadKeyCommand.ExecuteAsync(null); await Idle();
            Check(panel.LinesGrid.IsEnabled && !vm.CanEditComposition, "closed secondary key remains navigable " + language);
            Check(Descendants<TextBox>(panel.LinesGrid).All(b => b.IsReadOnly), "secondary weights are read-only " + language);
            // Wheel over an unselected table belongs to the page, including small touchpad deltas.
            ScrollToElement(panel.LinesGrid); await Idle(); Click(panel.ActionStatus);
            var inside = Descendants<ScrollViewer>(panel.LinesGrid).First();
            inside.ScrollToTop(); RootScrollViewer.ScrollToTop(); await Idle();
            var innerBefore = inside.VerticalOffset;
            Wheel(panel.LinesGrid, -30); await Idle();
            Check(RootScrollViewer.VerticalOffset > 0 && inside.VerticalOffset == innerBefore, "unselected table wheel scrolls page " + language);
            Click(panel.LinesGrid); var outerBefore = RootScrollViewer.VerticalOffset;
            Wheel(panel.LinesGrid, -120); await Idle();
            Check(inside.VerticalOffset > innerBefore && RootScrollViewer.VerticalOffset == outerBefore, "entered table wheel scrolls only table " + language);
            inside.ScrollToBottom(); await Idle(); Wheel(panel.LinesGrid, -120); await Idle();
            Check(RootScrollViewer.VerticalOffset == outerBefore, "table bottom does not transfer wheel to page " + language);
            inside.ScrollToTop(); await Idle(); Wheel(panel.LinesGrid, 120); await Idle();
            Check(RootScrollViewer.VerticalOffset == outerBefore, "table top does not transfer wheel to page " + language);
            Click(panel.ActionStatus); Wheel(panel.LinesGrid, -120); await Idle();
            Check(RootScrollViewer.VerticalOffset > outerBefore, "click outside returns wheel to page " + language);
            var editorScroll = Descendants<ScrollViewer>(panel.KeyNameBox).Single();
            outerBefore = RootScrollViewer.VerticalOffset;
            Wheel(editorScroll, -30); await Idle();
            Check(RootScrollViewer.VerticalOffset > outerBefore, "text field cannot capture page wheel " + language);
            var height = RootScrollViewer.ViewportHeight; var offset = RootScrollViewer.VerticalOffset;
            ShowFieldHelp(string.Join(" ", Enumerable.Repeat("Ayuda contextual extensa", 50))); await Idle();
            Check(RootScrollViewer.ViewportHeight == height && RootScrollViewer.VerticalOffset == offset, "hover help cannot resize viewport or move page " + language);
            vm.CheckReciprocalCommand.Execute(null); await Idle();
            Check(vm.ReciprocalChecks.All(r => !string.IsNullOrWhiteSpace(r.TechnicalDetail) && !r.Detail.Contains("1,111111")), "feasibility has readable meaning and technical detail " + language);
            var selected = vm.SecondaryResults.First(r => r.PrimaryNodeId != r.TerminalNodeId);
            panel.SecondaryResultsGrid.SelectedItem = selected; await Idle();
            Check(vm.SelectedSecondaryResultRow == selected && vm.SecondaryExplanationDetails.Count > 0 && vm.SecondaryExplanation.Contains(selected.Destination), "selection automatically explains correct cost " + language);
            panel.SecondaryExplanationGrid.Focus(); await Idle();
            Check(ReferenceEquals(panel.SecondaryResultsGrid.SelectedItem, selected), "reading explanation retains selected cost " + language);
            var following = vm.SecondaryResults.First(r => r != selected);
            panel.SecondaryResultsGrid.SelectedItem = following; await Idle();
            Check(vm.SelectedSecondaryResultRow == following && vm.SecondaryExplanation.Contains(following.Amount), "changing row refreshes explanation " + language);
            panel.SecondaryResultsGrid.SelectedItem = selected; await Idle();
            foreach (var width in new[] { 960d, 1600d })
            {
                Width = width; await Idle(); ScrollToElement(panel.SecondaryResultsSection); await Idle();
                VisualEvidenceCapture.Capture(this, Path.Combine(directory, language + "-secondary-" + width + ".png"));
            }
            ScrollToElement(panel.SecondaryExplanationGrid); await Idle();
            VisualEvidenceCapture.Capture(this, Path.Combine(directory, language + "-explanation.png"));
            GuidedUi.Reveal(panel.SecondaryBridgesGrid); ScrollToElement(panel.SecondaryBridgesGrid); await Idle();
            Check(panel.SecondaryBridgesGrid.Columns.Skip(1).All(c =>
                ((DataGridTextColumn)c).HeaderStyle.Setters.OfType<Setter>().Any(s => s.Property == Control.HorizontalContentAlignmentProperty && Equals(s.Value, HorizontalAlignment.Right))), "numeric headers align with amounts " + language);
            VisualEvidenceCapture.Capture(this, Path.Combine(directory, language + "-reconciliation.png"));
            GuidedUi.Reveal(panel.ReciprocalGrid); ScrollToElement(panel.ReciprocalGrid); await Idle();
            VisualEvidenceCapture.Capture(this, Path.Combine(directory, language + "-feasibility.png"));
        }
        var closedTotals = vm.PrincipalCosts.Select(r => (r.PrimaryAmount, r.AuxiliaryAmount, r.TotalAmount)).ToArray();
        foreach (var scale in new[] { 1.25, 1.5, 2d })
        {
            MainContent.LayoutTransform = new ScaleTransform(scale, scale); await Idle();
            ScrollToElement(SecondaryPanel.Workspace.SecondaryResultsSection); await Idle();
            Check(RootScrollViewer.ViewportHeight > 80 && SecondaryPanel.Workspace.PrincipalCostsGrid.IsVisible, "results accessible at layout scale " + scale);
            VisualEvidenceCapture.Capture(this, Path.Combine(directory, "scale-" + scale.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".png"));
        }
        MainContent.LayoutTransform = Transform.Identity;
        var path = company.ActivePath!;
        await company.CloseCommand.ExecuteAsync(null); await company.OpenPath(path); await NavigateWorkflow(2);
        studies.StudyChoice = studies.Studies.Single(s => s.Value.Name.StartsWith("01 -", StringComparison.Ordinal));
        await studies.LoadCommand.ExecuteAsync(null); await NavigateWorkflow(7);
        await vm.CalculateSecondaryCommand.ExecuteAsync(null); await Idle();
        Check(vm.PrincipalCosts.Count > 0 && vm.PrincipalCosts.Sum(r => r.TotalAmount) == 146900m, "open calculation uses same reconciled presentation as publication");
        Check(closedTotals.SequenceEqual(vm.PrincipalCosts.Select(r => (r.PrimaryAmount, r.AuxiliaryAmount, r.TotalAmount))), "preview and reopened publication have identical principal breakdown");
        File.WriteAllText(Path.Combine(directory, "interaction.json"), JsonSerializer.Serialize(new { Status = "passed", PassedChecks = checks }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
