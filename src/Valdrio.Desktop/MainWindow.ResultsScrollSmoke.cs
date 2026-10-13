using System.IO;
using System.Text.Json;
using System.Windows;
using System.ComponentModel;
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
        Check(vm.IsPrimaryClosed && SecondaryStep.IsEnabled,
            "saved study exposes secondary navigation without opening primary first");
        foreach (var language in new[] { "es", "en" })
        {
            main.ApplyPresentationPreset(language, language == "es" ? "es-ES" : "en-US", "UTC", false, false);
            Width = 1600; Height = 1000;
            await NavigateWorkflow(5); await Idle();
            Check(RootScrollViewer.VerticalOffset > 0 && AllocationPanel.ResultsSection.IsExpanded,
                "closed primary opens at saved results " + language);
            RootScrollViewer.ScrollToTop(); await Idle();
            var primaryToggleY = AllocationPanel.TogglePrimaryButton.TranslatePoint(new Point(), this).Y;
            Check(AllocationPanel.LinesGrid.ActualHeight > 300, "primary destinations use available height " + language);
            Check(vm.IsPrimaryClosed && vm.PrimaryResults.All(r => !string.IsNullOrWhiteSpace(r.Behavior)), "closed primary shows behavior " + language);
            Check(vm.PrimaryAccounts.Count > 0 && vm.PrimaryAccounts.Sum(row => row.PresentedAssigned) == vm.PrimaryNodeTotals.Sum(row => row.PresentedAmount),
                "primary grouped-account assignments reconcile to the primary node totals " + language);
            Check(vm.PrimaryAccounts.All(row => row.PresentedBase - row.PresentedAssigned == row.PresentedPending),
                "primary account-level pending values reconcile exactly " + language);
            Check(vm.SelectedPrimaryAccount is not null && AllocationPanel.PrimaryFlowCanvas.Children.Count > 0,
                "primary account selection draws original ledger source-to-node flow " + language);
            ScrollToElement(AllocationPanel.PrimaryFlowSection); await Idle();
            VisualEvidenceCapture.Capture(this, Path.Combine(directory, language + "-primary-account-flow.png"));
            vm.SelectedKey = vm.Keys.First(); await vm.LoadKeyCommand.ExecuteAsync(null); await Idle();
            Check(AllocationPanel.LinesGrid.IsEnabled && !vm.CanEditComposition, "closed primary key remains navigable " + language);
            Check(Descendants<TextBox>(AllocationPanel.LinesGrid).All(b => b.IsReadOnly), "primary weights are read-only " + language);
            ScrollToElement(AllocationPanel.DefinitionSection); await Idle();
            VisualEvidenceCapture.Capture(this, Path.Combine(directory, language + "-locked-key.png"));
            ScrollToElement(AllocationPanel.PrimaryResultsGrid); await Idle();
            VisualEvidenceCapture.Capture(this, Path.Combine(directory, language + "-primary.png"));
            ScrollToElement(AllocationPanel.CoverageGrid); await Idle();
            foreach (var column in AllocationPanel.CoverageGrid.Columns)
            {
                var header = Descendants<DataGridColumnHeader>(AllocationPanel.CoverageGrid).Single(h => h.Column == column);
                var clickHeader = typeof(DataGridColumnHeader).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
                clickHeader.Invoke(header, null); await Idle();
                Check(column.SortDirection == ListSortDirection.Ascending, "ascending coverage sort " + column.SortMemberPath + " " + language);
                var ascending = AllocationPanel.CoverageGrid.Items.Cast<AllocationCoverageRow>().ToArray();
                if (column.SortMemberPath == "NumericAmount")
                    Check(ascending.Select(r => r.NumericAmount).SequenceEqual(ascending.Select(r => r.NumericAmount).Order()), "amount sort is numeric including credits " + language);
                clickHeader.Invoke(header, null); await Idle();
                Check(column.SortDirection == ListSortDirection.Descending, "descending coverage sort " + column.SortMemberPath + " " + language);
            }
            await NavigateWorkflow(7); await Idle();
            var panel = SecondaryPanel.Workspace;
            Check(RootScrollViewer.VerticalOffset > 0 && panel.SecondaryResultsSection.IsExpanded,
                "closed secondary opens at saved results " + language);
            RootScrollViewer.ScrollToTop(); await Idle();
            Check(Math.Abs(panel.ToggleSecondaryButton.TranslatePoint(new Point(), this).Y - primaryToggleY) < 2,
                "secondary reopen occupies same top position as primary " + language);
            Check(panel.ToggleSecondaryButton.IsEnabled && panel.LinesGrid.ActualHeight > 300,
                "secondary reopen available and destinations expanded " + language);
            Check(vm.IsSecondaryClosed && vm.SecondaryResults.Count > 0, "publication loaded without calculating " + language);
            var snapshot = company.ActiveAllocations!.ReadAllocations(studies.Current!.CompanyId, studies.Current.Id);
            var publication = snapshot.SecondaryPublication!;
            Check(vm.PrincipalCosts.Sum(r => r.TotalAmount) == publication.Facts.Sum(f => f.PostedAmount.Amount), "principal totals equal posted ledger " + language);
            Check(vm.AuxiliarySettlements.Count > 0 && vm.AuxiliarySettlements.All(row => row.InitialAmount + row.IncomingAmount == row.GrossAmount),
                "auxiliary liquidation displays signed mathematically reconciled primary and reciprocal inputs " + language);
            ScrollToElement(panel.AuxiliarySettlementSection); await Idle();
            VisualEvidenceCapture.Capture(this, Path.Combine(directory, language + "-auxiliary-settlement.png"));
            Check(vm.PrincipalCosts.All(r => r.PrimaryAmount + r.AuxiliaryAmount == r.TotalAmount), "primary plus auxiliary reconciles per principal " + language);
            Check(vm.SecondaryResults.Sum(r => r.PostedAmount) == vm.PrincipalCosts.Sum(r => r.TotalAmount), "contributions reconcile with principal totals " + language);
            Check(vm.SelectedPrincipalCostRow is not null && panel.PrincipalOriginGrid.Items.Count > 0,
                "first principal cost centre selected with origin breakdown " + language);
            Check(vm.PrincipalOriginRows.Sum(r => r.NumericAmount) == vm.SelectedPrincipalCostRow!.PresentedTotalAmount,
                "selected centre origins reconcile at displayed currency precision " + language);
            Check(vm.SecondaryFlowArcs.Count > 0 &&
                vm.SecondaryFlowArcs.Any(arc => arc.Stage == SecondaryFlowStage.Primary) &&
                vm.SecondaryFlowArcs.Any(arc => arc.Stage == SecondaryFlowStage.Final),
                "published secondary result projects complete primary-node and auxiliary-to-principal stages " + language);
            var costGraphCards = panel.PrincipalFlowCanvas.Children.OfType<Border>()
                .Where(card => card.Tag is string tag && (
                    tag.StartsWith("primary:", StringComparison.Ordinal) ||
                    tag.StartsWith("auxiliary:", StringComparison.Ordinal) ||
                    tag.StartsWith("principal:", StringComparison.Ordinal))).ToArray();
            Check(costGraphCards.Length == vm.SecondaryFlowNodes.Count * 2 &&
                costGraphCards.Select(card => (string)card.Tag!).Distinct(StringComparer.Ordinal).Count() ==
                    costGraphCards.Length,
                "the three-column secondary graph renders one card per active node per economic stage " + language);
            var auxiliaryColour = (Brush)panel.FindResource("CostGraph.AuxiliaryBackground");
            var principalColour = (Brush)panel.FindResource("CostGraph.PrincipalBackground");
            Check(!Equals(auxiliaryColour, principalColour) &&
                costGraphCards.All(card =>
                    card.Tag is string tag &&
                    (tag.StartsWith("auxiliary:", StringComparison.Ordinal) ||
                     tag.StartsWith("primary:", StringComparison.Ordinal) &&
                        vm.SecondaryFlowNodes.Any(n => n.IsAuxiliary &&
                            tag == "primary:" + n.Id)
                        ? Equals(card.Background, auxiliaryColour)
                        : Equals(card.Background, principalColour))),
                "auxiliary nodes have blue cards and principal nodes green cards in all stages " + language);
            Check(panel.PrincipalFlowCanvas.Children.OfType<System.Windows.Shapes.Path>()
                    .Any(path => path.Data is PathGeometry geometry &&
                        geometry.Figures.Any(fig => fig.Segments.OfType<BezierSegment>().Any())),
                "secondary node graph has real Bezier connectors rather than row-by-row straight lines " + language);
            Check(panel.PrincipalFlowCanvas.Height > 410,
                "complete secondary graph has full content-driven page height " + language);
            Check(panel.PrincipalFlowCanvas.Width == 1320d &&
                panel.PrincipalFlowViewport.Stretch == Stretch.Uniform &&
                panel.PrincipalFlowViewport.StretchDirection == StretchDirection.DownOnly,
                "three-column graph scales to panel width instead of forcing horizontal scrolling " + language);
            var graphArrows = panel.PrincipalFlowCanvas.Children
                .OfType<System.Windows.Shapes.Polygon>().ToArray();
            Check(graphArrows.Length > 0 &&
                graphArrows.All(arrow => arrow.Opacity >= .98d &&
                    arrow.Points.Count == 3 && arrow.Points[0].Y != arrow.Points[2].Y),
                "direction arrows stay large, filled and visually prominent " + language);
            Check(panel.PrincipalFlowCanvas.Children.OfType<System.Windows.Shapes.Path>()
                .All(line => line.StrokeThickness <= 4.01d),
                "cost links use bounded thin strokes rather than large overlapping bands " + language);
            var priorCentre = vm.SelectedPrincipalCostRow;
            var globalArcCount = vm.SecondaryFlowArcs.Count;
            vm.SelectedPrincipalCostRow = null; await Idle();
            Check(vm.SecondaryFlowArcs.Count == globalArcCount && panel.PrincipalFlowCanvas.Children.Count > 0,
                "secondary flow remains visible without selecting any account or principal centre " + language);
            vm.SelectedPrincipalCostRow = priorCentre; await Idle();
            var nextCentre = vm.PrincipalCosts.Skip(1).FirstOrDefault();
            if (nextCentre is not null)
            {
                panel.PrincipalCostsGrid.SelectedItem = nextCentre; await Idle();
                Check(vm.SelectedPrincipalCostRow == nextCentre && vm.PrincipalOriginRows.Sum(r => r.NumericAmount) == nextCentre.PresentedTotalAmount,
                    "changing principal centre updates the adjacent cost-origin table " + language);
            }
            ScrollToElement(panel.PrincipalCostFlowSection); await Idle();
            VisualEvidenceCapture.Capture(this, Path.Combine(directory, language + "-principal-flow.png"));
            foreach (var factGroup in publication.Facts.GroupBy(f => (f.TerminalNodeId, f.Behavior)))
                Check(vm.SecondaryResults.Where(r => r.TerminalNodeId == factGroup.Key.TerminalNodeId && r.SourceBehavior == factGroup.Key.Behavior).Sum(r => r.PostedAmount)
                    == factGroup.Sum(f => f.PostedAmount.Amount), "behavior reconciles per destination " + language);
            vm.SelectedKey = vm.Keys.First(); await vm.LoadKeyCommand.ExecuteAsync(null); await Idle();
            Check(panel.LinesGrid.IsEnabled && !vm.CanEditComposition, "closed secondary key remains navigable " + language);
            Check(Descendants<TextBox>(panel.LinesGrid).All(b => b.IsReadOnly), "secondary weights are read-only " + language);
            ScrollToElement(panel.DefinitionSection); await Idle();
            VisualEvidenceCapture.Capture(this, Path.Combine(directory, language + "-secondary-key.png"));
            panel.LinesGrid.Height = 200; await Idle();
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
            // Regression: the previously activated table must not steal the wheel
            // when the pointer is on another part of the page or another table.
            RootScrollViewer.ScrollToTop(); await Idle();
            var tableAtPointerExit = inside.VerticalOffset;
            Wheel(panel.ActionStatus, -30); await Idle();
            Check(RootScrollViewer.VerticalOffset > 0 && inside.VerticalOffset == tableAtPointerExit,
                "activated table cannot hijack wheel outside its bounds " + language);
            RootScrollViewer.ScrollToTop(); await Idle();
            Wheel(panel.PrincipalCostsGrid, -30); await Idle();
            Check(RootScrollViewer.VerticalOffset > 0 && inside.VerticalOffset == tableAtPointerExit,
                "activated table cannot hijack wheel hovering over another grid " + language);
            // The results graph allows horizontal scrolling only. Its vertical wheel
            // must reach the document, even while a table still owns keyboard focus.
            RootScrollViewer.ScrollToTop(); await Idle();
            var graphScroll = Descendants<ScrollViewer>(panel.PrincipalCostFlowSection).Single();
            var graphVertical = graphScroll.VerticalOffset;
            Wheel(panel.PrincipalFlowCanvas, -30); await Idle();
            Check(RootScrollViewer.VerticalOffset > 0 && graphScroll.VerticalOffset == graphVertical,
                "vertical wheel over horizontal-only results graph scrolls the page " + language);
            RootScrollViewer.ScrollToTop(); await Idle();
            Click(panel.ActionStatus); Wheel(panel.LinesGrid, -120); await Idle();
            Check(RootScrollViewer.VerticalOffset > outerBefore, "click outside returns wheel to page " + language);
            var editorScroll = Descendants<ScrollViewer>(panel.KeyNameBox).Single();
            outerBefore = RootScrollViewer.VerticalOffset;
            Wheel(editorScroll, -30); await Idle();
            Check(RootScrollViewer.VerticalOffset > outerBefore, "text field cannot capture page wheel " + language);
            var height = RootScrollViewer.ViewportHeight; var offset = RootScrollViewer.VerticalOffset;
            ShowFieldHelp(string.Join(" ", Enumerable.Repeat("Ayuda contextual extensa", 50))); await Idle();
            Check(RootScrollViewer.ViewportHeight == height && RootScrollViewer.VerticalOffset == offset, "hover help cannot resize viewport or move page " + language);
            panel.LinesGrid.ClearValue(HeightProperty); await Idle();
            await Idle();
            Check(vm.ReciprocalChecks.Count > 0 && !vm.ReciprocalHasError && vm.ReciprocalChecks.All(r => !string.IsNullOrWhiteSpace(r.TechnicalDetail) && !r.Detail.Contains("1,111111")), "published feasibility appears automatically " + language);
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
            Check(panel.FindName("SecondaryBridgesGrid") is null, "resource reconciliation removed from secondary " + language);
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
        await vm.ToggleSecondaryCommand.ExecuteAsync(null); await Idle();
        Check(!vm.IsSecondaryClosed && vm.CanEditSecondaryConfiguration && vm.ReciprocalChecks.Count == 0,
            "top reopen command unlocks editing and invalidates prior feasibility");
        await vm.CalculateSecondaryCommand.ExecuteAsync(null); await vm.ToggleSecondaryCommand.ExecuteAsync(null); await Idle();
        Check(vm.IsSecondaryClosed && vm.ReciprocalChecks.Count > 0, "recalculation and closure restore automatic viability");
        var path = company.ActivePath!;
        await company.CloseCommand.ExecuteAsync(null); await company.OpenPath(path); await NavigateWorkflow(2);
        studies.StudyChoice = studies.Studies.Single(s => s.Value.Name.StartsWith("01 -", StringComparison.Ordinal));
        await studies.LoadCommand.ExecuteAsync(null); await NavigateWorkflow(7);
        await vm.CalculateSecondaryCommand.ExecuteAsync(null); await Idle();
        Check(vm.PrincipalCosts.Count > 0 && vm.PrincipalCosts.Sum(r => r.TotalAmount) == 146900m, "open calculation uses same reconciled presentation as publication");
        Check(closedTotals.SequenceEqual(vm.PrincipalCosts.Select(r => (r.PrimaryAmount, r.AuxiliaryAmount, r.TotalAmount))), "preview and reopened publication have identical principal breakdown");
        Check(vm.ReciprocalChecks.Count > 0 && !vm.ReciprocalHasError, "successful calculation presents viability without a button");
        // An actual closed auxiliary loop must fail in the real solver, then be
        // visible in red without invoking any separate feasibility command.
        var auxiliaries = vm.Auxiliaries.ToArray();
        for (var i = 0; i < auxiliaries.Length; i++)
        {
            await vm.NewKeyCommand.ExecuteAsync(null); vm.Name = "Closed loop " + i;
            var destination = vm.Lines.Single(l => l.Id == auxiliaries[(i + 1) % auxiliaries.Length].Value);
            destination.Included = true; destination.Value = "100";
            await vm.SaveKeyCommand.ExecuteAsync(null);
            var keyId = vm.Keys.Single(k => k.Value.Name == "Closed loop " + i).Value.Id;
            vm.SelectedSecondary = vm.SecondaryRules.Single(r => r.Source == auxiliaries[i].Label);
            vm.LoadSecondaryCommand.Execute(null); vm.SecondaryKey = keyId;
            await vm.SaveSecondaryCommand.ExecuteAsync(null);
        }
        Check(vm.CanCalculateSecondary, "closed graph has complete assignments and can report calculation error");
        await vm.CalculateSecondaryCommand.ExecuteAsync(null); await Idle();
        Check(vm.ReciprocalHasError && vm.ReciprocalChecks.Single().HasError && vm.SecondaryResults.Count == 0,
            "failed reciprocal calculation automatically reports error and removes stale results");
        Check(vm.SecondaryCalculationFailed && vm.SelectedSecondary is not null &&
            SecondaryPanel.Workspace.RemoveSecondaryButton.IsEnabled,
            "impossible graph exposes selected repairable route without altering the saved key");
        Check(vm.PrincipalOriginRows.Count == 0 && vm.SecondaryFlowArcs.Count == 0 && vm.AuxiliarySettlements.Count == 0,
            "failed calculation shows no stale or invented reciprocal links or auxiliary liquidation");
        ScrollToElement(SecondaryPanel.Workspace.ReciprocalGrid); await Idle();
        var errorText = Descendants<TextBlock>(SecondaryPanel.Workspace.ReciprocalGrid)
            .Where(t => t.DataContext is ReciprocalCheckRow { HasError: true }).ToArray();
        var red = ((SolidColorBrush)FindResource("Brand.Red")).Color;
        Check(errorText.Length > 0 && errorText.All(t => t.Foreground is SolidColorBrush brush && brush.Color == red), "calculation error renders in red");
        VisualEvidenceCapture.Capture(this, Path.Combine(directory, "automatic-error.png"));
        File.WriteAllText(Path.Combine(directory, "interaction.json"), JsonSerializer.Serialize(new { Status = "passed", PassedChecks = checks }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
