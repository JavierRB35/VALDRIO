using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using Valdrio.Application;
using Valdrio.Domain;

namespace Valdrio.Desktop;
public partial class MainWindow
{
    internal async Task RunMethodologyChecks(string directory)
    {
        var output = Path.Combine(directory, "methodology"); Directory.CreateDirectory(output);
        var main = viewModel!; var company = main.Company!; var studies = main.Studies!;
        var vm = main.Allocations!; var nodes = main.Models!;
        var checks = new List<string>();
        void Check(bool ok, string label) { if (!ok) throw new InvalidOperationException("Methodology: " + label + " / " + vm.Status + " / " + nodes.Status); checks.Add(label); }
        async Task Idle() { await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle); UpdateLayout(); }
        void Text(TextBox box, string value) { box.SetCurrentValue(TextBox.TextProperty, value); box.GetBindingExpression(TextBox.TextProperty)!.UpdateSource(); }
        async Task Click(Button button)
        {
            GuidedUi.Reveal(button); await Idle(); Check(button.IsEnabled, "enabled " + button.Name);
            ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke(); await Idle();
            if (button.Command is IAsyncRelayCommand command && command.ExecutionTask is { } task) await task.WaitAsync(TimeSpan.FromSeconds(60));
            await Idle();
        }
        main.ApplyPresentationPreset("es", "es-ES", "Europe/Madrid", false, false);
        await NavigateWorkflow(5); await Idle();
        Check(AllocationPanel.IsVisible && !SecondaryPanel.IsVisible, "primary has a separate page");
        Check(AllocationPanel.CompositionSection.IsExpanded, "composition is visible by default in step 6");
        var composition = vm.CompositionAccounts.Single();
        AllocationPanel.CompositionGrid.ScrollIntoView(composition); await Idle();
        var gridRow = (DataGridRow)AllocationPanel.CompositionGrid.ItemContainerGenerator.ContainerFromItem(composition);
        IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
        {
            for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T match) yield return match;
                foreach (var nested in Descendants<T>(child)) yield return nested;
            }
        }
        Check(!AllocationPanel.SaveCompositionRowsButton.IsEnabled, "table save starts disabled");
        var behaviorCombo = Descendants<ComboBox>(gridRow).Single();
        Check(Descendants<TextBox>(gridRow).Where(b => b.GetBindingExpression(TextBox.TextProperty) is not null).All(b => !b.IsVisible), "fixed rows hide percentage inputs");
        behaviorCombo.SetCurrentValue(ComboBox.SelectedValueProperty, ResourceBehavior.Mixed); await Idle();
        var weightBoxes = Descendants<TextBox>(gridRow).Where(b => b.GetBindingExpression(TextBox.TextProperty) is not null).ToArray();
        Check(weightBoxes.Length == 2 && weightBoxes.All(b => b.IsVisible && b.IsEnabled), "mixed percentages editable inside row");
        Check(weightBoxes[0].IsReadOnly && !weightBoxes[1].IsReadOnly, "only variable percentage is editable");
        Text(weightBoxes[1], "140");
        await Click(AllocationPanel.SaveCompositionRowsButton);
        Check(vm.HasCompositionChanges, "invalid percentages keep table edits");
        Text(weightBoxes[1], "30"); await Idle(); Check(weightBoxes[0].Text == "70", "fixed percentage automatically complements variable"); await Click(AllocationPanel.SaveCompositionRowsButton);
        Check(!AllocationPanel.SaveCompositionRowsButton.IsEnabled, "table save disables after persistence");
        composition = vm.CompositionAccounts.Single();
        AllocationPanel.CompositionGrid.ScrollIntoView(composition); await Idle();
        gridRow = (DataGridRow)AllocationPanel.CompositionGrid.ItemContainerGenerator.ContainerFromItem(composition);
        behaviorCombo = Descendants<ComboBox>(gridRow).Single();
        behaviorCombo.SetCurrentValue(ComboBox.SelectedValueProperty, ResourceBehavior.Variable); await Idle();
        Check(AllocationPanel.SaveCompositionRowsButton.IsEnabled, "new table edit enables save again");
        Check(Descendants<TextBox>(gridRow).Where(b => b.GetBindingExpression(TextBox.TextProperty) is not null).All(b => !b.IsVisible), "variable rows hide percentage inputs");
        behaviorCombo.SetCurrentValue(ComboBox.SelectedValueProperty, ResourceBehavior.Mixed); await Idle();
        weightBoxes = Descendants<TextBox>(gridRow).Where(b => b.GetBindingExpression(TextBox.TextProperty) is not null).ToArray();
        Text(weightBoxes[1], "30"); await Idle();
        Check(!AllocationPanel.SaveCompositionRowsButton.IsEnabled, "restoring original values clears pending edits");
        Check(vm.CompositionAccounts.Single().Behavior.Contains("70"), "70 fixed and 30 variable saved by visible controls");
        ScrollToElement(AllocationPanel.CompositionSection); await Idle();
        VisualEvidenceCapture.Capture(this, Path.Combine(output, "es-primary-composition.png"));
        await NavigateWorkflow(4); await Idle();
        Check(!string.IsNullOrWhiteSpace(main.Resources!.History), "classification history loads without missing resources");
        await NavigateWorkflow(1); await Idle();
        Check(nodes.CanEditStudyNodes, "study nodes remain editable before primary closure");
        var principal = nodes.Nodes.First(n => n.Node.Role == NodeRole.Principal);
        nodes.SelectedNode = principal;
        ModelPanel.ModelSteps.SelectedIndex = 2;
        Text(ModelPanel.PoolBox, "Producción revisada en el análisis");
        await nodes.SaveNodeCommand.ExecuteAsync(null); await Idle();
        Check(!nodes.IsDirty && !nodes.HasError, "study node edit saved");
        Check(ModelPanel.FindName("LayerBox") is null, "node editor has no fixed or variable selector");
        Check(company.ActiveResources!.ReadResources(studies.Current!.CompanyId, studies.Current.Id)
            .Model.Structure.Pools.Single(p => p.NodeId == principal.Node.Id).Description.Contains("revisada"), "study node change persists");
        ScrollToElement(ModelPanel.NodeSummary); await Idle();
        VisualEvidenceCapture.Capture(this, Path.Combine(output, "es-editable-nodes.png"));
        await NavigateWorkflow(5); await Idle();
        Check(AllocationPanel.KeyGrid.Items.Count == 0 ||
            AllocationPanel.KeyGrid.SelectedIndex == 0 && AllocationPanel.KeyGrid.IsKeyboardFocusWithin,
            $"primary allocation opens with focus on the first saved project key (index={AllocationPanel.KeyGrid.SelectedIndex}, focus={AllocationPanel.KeyGrid.IsKeyboardFocusWithin}, visible={AllocationPanel.KeyGrid.IsVisible}, enabled={AllocationPanel.KeyGrid.IsEnabled}, active={IsActive})");
        await Click(AllocationPanel.TogglePrimaryButton);
        Check(vm.IsPrimaryClosed, "primary closes with weighted composition");
        await NavigateWorkflow(1); await Idle(); Check(!nodes.CanEdit, "primary closure locks analysis nodes");
        await NavigateWorkflow(7); await Idle();
        var secondary = SecondaryPanel.Workspace;
        Check(SecondaryPanel.IsVisible && !AllocationPanel.IsVisible && workflowStep == 7, "secondary is independent step 7");
        Check(!secondary.CompositionSection.IsVisible, "secondary does not offer reclassification");
        await Click(secondary.NewKeyButton);
        Check(vm.Stage == AllocationStage.Secondary, "new key belongs to secondary automatically");
        Text(secondary.KeyNameBox, "Servicios auxiliares a producción");
        vm.Lines.Single(l => l.Id == principal.Node.Id).Included = true;
        vm.Lines.Single(l => l.Id == principal.Node.Id).Value = "80";
        var auxiliarySelf = vm.Auxiliaries.First().Value;
        vm.Lines.Single(l => l.Id == auxiliarySelf).Included = true;
        vm.Lines.Single(l => l.Id == auxiliarySelf).Value = "20";
        await Click(secondary.SaveKeyButton);
        var key = vm.Keys.Single().Value;
        Check(key.SelfAllocation == SelfAllocationPolicy.Allowed,
            "new secondary key automatically permits self-service without a checkbox");
        // Enter both phases again: the first saved key must be selected and keyboard-focused.
        await NavigateWorkflow(5); await Idle();
        await NavigateWorkflow(7); await Idle();
        Check(secondary.KeyGrid.Items.Count > 0 && secondary.KeyGrid.SelectedIndex == 0 &&
            secondary.KeyGrid.IsKeyboardFocusWithin,
            "secondary allocation opens with focus on the first saved project key");
        // Regression: a pointer-driven popup selection must persist when the popup closes,
        // even though the ComboBox itself loses keyboard focus to the popup.
        var firstAuxiliary = vm.SecondaryNodeAssignments.First();
        secondary.SecondaryNodeAssignmentsGrid.ScrollIntoView(firstAuxiliary);
        await Idle();
        var assignmentRow = secondary.SecondaryNodeAssignmentsGrid.ItemContainerGenerator.ContainerFromItem(firstAuxiliary) as DataGridRow
            ?? throw new InvalidOperationException("Auxiliary assignment row is not visible");
        var assignmentCombo = Descendants<ComboBox>(assignmentRow).Single();
        assignmentCombo.Focus();
        System.Windows.Input.Keyboard.Focus(assignmentCombo);
        assignmentCombo.SetCurrentValue(ComboBox.IsDropDownOpenProperty, true);
        await Idle();
        assignmentCombo.SetCurrentValue(ComboBox.SelectedValueProperty, key.Id);
        await Idle();
        Check(firstAuxiliary.PersistedKeyId is null,
            "selection made while the popup is open has not yet been persisted");
        assignmentCombo.SetCurrentValue(ComboBox.IsDropDownOpenProperty, false);
        // Popup closure is dispatched by WPF; let DropDownClosed install the
        // current save task before awaiting it, not the previous completed task.
        await Idle();
        await secondary.SecondaryAssignmentTask; await Idle();
        Check(firstAuxiliary.SelectedKeyId == key.Id && firstAuxiliary.PersistedKeyId == key.Id &&
            !firstAuxiliary.HasAssignmentError,
            "pointer secondary table assignment remains selected after save");
        Check(company.ActiveAllocations!.ReadAllocations(studies.Current!.CompanyId, studies.Current.Id)
            .Configuration.SecondaryRules.Any(rule => rule.IsActive && rule.AuxiliaryNodeId == firstAuxiliary.NodeId &&
                rule.KeyVersionId == key.Id),
            "direct secondary table assignment is persisted in the company file");
        // Read again through the normal UI; this catches optimistic selection that never reached storage.
        await vm.EnterAsync(true); await Idle();
        Check(vm.SecondaryNodeAssignments.Single(row => row.NodeId == firstAuxiliary.NodeId).PersistedKeyId == key.Id,
            "secondary assignment survives a clean re-read of the allocation workspace");
        foreach (var auxiliary in vm.Auxiliaries.ToArray())
        {
            vm.Auxiliary = auxiliary.Value; vm.SecondaryKey = key.Id;
            await Click(secondary.SaveSecondaryButton);
        }
        await Click(secondary.CalculateSecondaryButton);
        Check(vm.SecondaryResults.Count == 2, "secondary preserves both fixed and variable components");
        Check(vm.SecondaryResults.Sum(row => row.PostedAmount) ==
            vm.PrincipalCosts.Sum(row => row.TotalAmount), "secondary contribution footer reconciles with final principal costs");
        Check(vm.PrincipalCosts.All(row => row.PrimaryAmount + row.AuxiliaryAmount == row.TotalAmount),
            "terminal totals keep primary and auxiliary components distinct");
        vm.SelectedSecondaryResultRow = vm.SecondaryResults.First();
        await Idle();
        Check(!string.IsNullOrWhiteSpace(vm.SecondaryExplanation), "component explanation selects the matching fact");
        foreach (var language in new[] { "es", "en" })
        {
            main.ApplyPresentationPreset(language, language == "es" ? "es-ES" : "en-US", "UTC", false, false);
            await Idle(); ScrollToElement(secondary.SecondaryResultsSection); await Idle();
            VisualEvidenceCapture.Capture(this, Path.Combine(output, language + "-secondary.png"));
            Check(SecondaryStep.Content.ToString() == (language == "es"
                ? "Reparto secundario y cierre" : "Secondary allocation and closing"),
                language + " secondary navigation label without duplicated step numbering");
        }
        await Click(secondary.ToggleSecondaryButton); Check(vm.IsSecondaryClosed, "secondary publication closes");
        var studyId = studies.Current!.Id; var path = company.ActivePath!;
        await company.CloseCommand.ExecuteAsync(null); await company.OpenPath(path);
        studies.StudyChoice = studies.Studies.Single(s => s.Value.Id == studyId); await studies.LoadCommand.ExecuteAsync(null);
        await NavigateWorkflow(7); await Idle();
        Check(vm.IsSecondaryClosed && vm.SecondaryResults.Count == 2, "published mixed result survives reopen");
        Check(vm.SecondaryNodeAssignments.Single(row => row.NodeId == firstAuxiliary.NodeId).PersistedKeyId == key.Id,
            "auxiliary key assignment survives closing and reopening the entire project");
        Check(secondary.KeyGrid.Items.Count > 0 && secondary.KeyGrid.SelectedIndex == 0 &&
            secondary.SecondaryResultsSection.IsExpanded,
            "closed secondary restores key details and prioritizes published results after reopening");
        File.WriteAllText(Path.Combine(output, "interaction.json"), JsonSerializer.Serialize(new { Status = "passed", PassedChecks = checks }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
