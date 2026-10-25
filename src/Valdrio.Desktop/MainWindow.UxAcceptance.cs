using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Valdrio.Desktop;

public partial class MainWindow
{
    internal async Task RunUxAcceptance(string directory, QueuedCompanyFileDialogs files, QueuedImportSourceDialogs sources)
    {
        Directory.CreateDirectory(directory);
        await RunGuidedWorkflowChecks(directory, files, sources);
        var checks = new List<string>();
        void Check(bool condition, string label)
        { if (!condition) throw new InvalidOperationException("UX: " + label); checks.Add(label); }
        static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typed) yield return typed;
                foreach (var nested in Descendants<T>(child)) yield return nested;
            }
        }
        var vm = viewModel!;
        foreach (var language in new[] { "es", "en" })
        {
            vm.ApplyPresentationPreset(language, language == "es" ? "es-ES" : "en-US", "UTC", false, false);
            await NavigateWorkflow(4);
            RootScrollViewer.ScrollToTop(); UpdateLayout();
            Check(ProjectContextText.Text.Contains("EUR", StringComparison.Ordinal), "visible currency " + language);
            Check(ProjectContextText.TextWrapping == TextWrapping.Wrap, "context wraps instead of truncating " + language);
            Check(!string.IsNullOrWhiteSpace(ReadFieldHelpButton.Content?.ToString()), "field help reader localized " + language);
            foreach (var width in new[] { 1280d, 1366d, 1024d, 760d })
            {
                Width = width; Height = width == 760 ? 620 : width == 1366 ? 768 : 720; UpdateLayout();
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                Check(WorkflowNext.IsVisible && WorkflowBack.IsVisible && ReadFieldHelpButton.IsVisible, "navigation and help visible " + language + width);
                VisualEvidenceCapture.Capture(this, Path.Combine(directory, $"accounts-{language}-{width}.png"));
            }
            var texts = new Valdrio.Localization.UiTextCatalog();
            var culture = System.Globalization.CultureInfo.GetCultureInfo(language);
            string L(string key) => texts.Get("UX." + key, culture);
            foreach (var expected in new[] { PendingEditChoice.Cancel, PendingEditChoice.Save, PendingEditChoice.Discard })
            {
                var dialog = new PendingChangesWindow("VALDRIO", vm.Studies!.RunHistory, L("SaveContinue"), L("Discard"), L("KeepEditing"));
                dialog.Loaded += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
                {
                    Check(dialog.KeepEditingButton.IsKeyboardFocused, "safe initial focus " + language + expected);
                    VisualEvidenceCapture.Capture(dialog, Path.Combine(directory, $"pending-{language}-{expected}.png"));
                    if (expected == PendingEditChoice.Cancel) dialog.Close();
                    else Descendants<Button>(dialog).Single(b => Equals(b.Content, L(expected == PendingEditChoice.Save ? "SaveContinue" : "Discard")))
                        .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }));
                dialog.ShowDialog();
                Check(dialog.Choice == expected, "explicit pending outcome " + language + expected);
            }
        }
        vm.ApplyPresentationPreset("es", "es-ES", "UTC", false, false);
        Width = 1280; Height = 860; UpdateLayout();
        await NavigateWorkflow(0); UpdateWorkflow();
        Check(ProjectTaskPanel.IsVisible, "project resume panel is visible");
        Check(ResumeStructureCard.IsVisible && ResumeStudyCard.IsVisible && ResumeImportCard.IsVisible && ResumeReviewCard.IsVisible,
            "four real project stages are visible together");
        Check(new[] { ResumeStructureState.Text, ResumeStudyState.Text, ResumeImportState.Text, ResumeReviewState.Text,
                ResumeStructureDetail.Text, ResumeStudyDetail.Text, ResumeImportDetail.Text, ResumeReviewDetail.Text }
                .All(text => !string.IsNullOrWhiteSpace(text)),
            "all resume stages explain actual state and prerequisites");
        Check(TaskImportButton.IsEnabled == vm.Studies!.HasImportDestination, "import action follows saved analysis");
        Check(TaskContinueButton.IsEnabled == vm.Studies.HasPublishedModels, "continue action requires a published cost structure");
        VisualEvidenceCapture.Capture(this, Path.Combine(directory, "project-resume.png"));
        if (vm.Studies.HasPublishedModels)
        {
            await ContinueProjectWorkAsync();
            Check(workflowStep == (vm.Studies.HasConfirmedImports ? 4 : vm.Studies.HasImportDestination ? 3 : 2),
                "continue action opens the next real task for the saved project");
            await NavigateWorkflow(0);
        }
        await NavigateWorkflow(2);
        var study = vm.Studies!;
        var firstFocus = StudyPanel.StudyNameBox.Focus();
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        Check(firstFocus && StudyPanel.StudyNameBox.IsKeyboardFocusWithin && FieldHelpText.Text == study.Guide("Help.Name"),
            "field focus shows its contextual help immediately");
        VisualEvidenceCapture.Capture(this, Path.Combine(directory, "field-help-focused.png"));
        var nextFocus = StudyPanel.StudyCurrencyBox.Focus();
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        Check(nextFocus && StudyPanel.StudyCurrencyBox.IsKeyboardFocusWithin && FieldHelpText.Text == study.Guide("Help.Currency"),
            "moving focus to the next field updates its help");
        await NavigateWorkflow(1);
        var model = vm.Models!;
        ModelPanel.ModelSteps.SelectedIndex = 1;
        model.Master = model.Centers.First(); UpdateLayout();
        var originalCode = model.Master.Value.Code;
        ModelPanel.CenterGrid.CurrentCell = new DataGridCellInfo(model.Master, ModelPanel.CenterGrid.Columns[1]);
        ModelPanel.CenterGrid.ScrollIntoView(model.Master); ModelPanel.CenterGrid.Focus();
        Check(ModelPanel.CenterGrid.BeginEdit(), "edit centre name inside table"); UpdateLayout();
        var input = Descendants<TextBox>(ModelPanel.CenterGrid).Single(t => t.IsVisible);
        input.Text = "Centro editado en tabla";
        input.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
        ModelPanel.CenterGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        ModelPanel.CenterGrid.CommitEdit(DataGridEditingUnit.Row, true);
        Check(model.SaveCenterFormCommand.CanExecute(null), "table edits enable existing save");
        await model.SaveCenterFormCommand.ExecuteAsync(null);
        Check(model.Centers.Any(c => c.Value.Code == originalCode && c.Value.Name == "Centro editado en tabla"), "table save preserves code and persists name");
        VisualEvidenceCapture.Capture(this, Path.Combine(directory, "centres-table.png"));
        await model.CloneCommand.ExecuteAsync(null);
        ModelPanel.ModelSteps.SelectedIndex = 2;
        model.SelectedNode = model.Nodes.First(); UpdateLayout();
        var nodeId = model.SelectedNode.Node.Id;
        var nodeCode = model.SelectedNode.Node.Code;
        Check(ModelPanel.NodeCodeBox.Visibility != Visibility.Visible || !ModelPanel.NodeCodeBox.IsVisible, "existing node has no duplicate form");
        ModelPanel.NodeSummary.CurrentCell = new DataGridCellInfo(model.SelectedNode, ModelPanel.NodeSummary.Columns[4]);
        ModelPanel.NodeSummary.ScrollIntoView(model.SelectedNode); ModelPanel.NodeSummary.Focus();
        Check(ModelPanel.NodeSummary.BeginEdit(), "edit node cost group in table"); UpdateLayout();
        var nodeInput = Descendants<TextBox>(ModelPanel.NodeSummary).Single(t => t.IsVisible);
        nodeInput.Text = "Costes revisados en tabla";
        nodeInput.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
        ModelPanel.NodeSummary.CommitEdit(DataGridEditingUnit.Cell, true);
        ModelPanel.NodeSummary.CommitEdit(DataGridEditingUnit.Row, true);
        await model.SaveNodeCommand.ExecuteAsync(null);
        Check(model.Nodes.Any(n => n.Node.Id == nodeId && n.Node.Code == nodeCode && n.Pool?.Description == "Costes revisados en tabla"), "node table saves existing identity and description");
        VisualEvidenceCapture.Capture(this, Path.Combine(directory, "nodes-table.png"));
        await NavigateWorkflow(6);
        Width = 1280; Height = 720; UpdateLayout();
        Check(FormatCultureSelector.SelectedItem?.ToString() == "es-ES", "friendly preferences retain persisted codes");
        Check(Descendants<TextBlock>(FormatCultureSelector).Any(t => t.Text.Contains("España", StringComparison.Ordinal)), "friendly region name and format example");
        VisualEvidenceCapture.Capture(this, Path.Combine(directory, "preferences.png"));
        ReadFieldHelpButton.Focus();
        ReadFieldHelp(ReadFieldHelpButton, new RoutedEventArgs());
        var helpReader = System.Windows.Application.Current.Windows.OfType<ContextHelpWindow>().Single();
        Check(helpReader.IsVisible && ReferenceEquals(helpReader.Owner, this),
            "full explanation opens the contextual guide for this screen");
        var helpContent = Descendants<FlowDocumentScrollViewer>(helpReader)
            .Select(view => view.Document is null ? "" : new System.Windows.Documents.TextRange(
                view.Document.ContentStart, view.Document.ContentEnd).Text)
            .FirstOrDefault() ?? "";
        Check(helpContent.Contains("Recommended workflow", StringComparison.Ordinal) ||
              helpContent.Contains("Recorrido recomendado", StringComparison.Ordinal),
            "complete workflow is rendered, including content after technical references");
        helpReader.Close();
        Check(ReadFieldHelpButton.IsKeyboardFocused, "help reader restores keyboard focus");
        var physical = new PhysicalActivityEditor(vm.Studies!);
        var import = new PhysicalImportEditor(physical);
        var revision = physical.Study.Revision;
        Check(import.CreateTemplate().Contains("node;unit;period_start;period_end", StringComparison.Ordinal), "physical template contract");
        Check(physical.Study.Revision == revision && !physical.HasPendingEdits, "template has no mutations");
        var page = new PhysicalActivityWindow(vm.Studies!) { Owner = this }; page.ShowIntegrated();
        UpdateLayout(); await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        VisualEvidenceCapture.Capture(this, Path.Combine(directory, "activity-integrated.png"));
        page.Close();
        File.WriteAllText(Path.Combine(directory, "ux-acceptance.json"), JsonSerializer.Serialize(checks, new JsonSerializerOptions { WriteIndented = true }));
    }
}
