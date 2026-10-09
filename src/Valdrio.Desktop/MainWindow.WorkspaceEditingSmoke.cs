using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using Valdrio.Application;
using Valdrio.Domain;
using Valdrio.Infrastructure;

namespace Valdrio.Desktop;

public partial class MainWindow
{
    internal async Task RunWorkspaceEditingChecks(string directory, QueuedModelEditorDialogs dialogs)
    {
        var output = Path.Combine(directory, "workspace-editing"); Directory.CreateDirectory(output);
        var main = viewModel!; var company = main.Company!; var studies = main.Studies!;
        var resources = main.Resources!; var allocations = main.Allocations!;
        var checks = new List<string>();
        void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("Workspace editing: " + message + " · " + resources.Status + " · " + allocations.Status); checks.Add(message); }
        async Task Idle() { await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle); UpdateLayout(); }
        static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T match) yield return match;
                foreach (var nested in Descendants<T>(child)) yield return nested;
            }
        }
        void CheckGrid(DataGrid grid)
        {
            Check(grid.Columns.Where(c => c.Visibility == Visibility.Visible).All(c => c.ActualWidth >= c.MinWidth - 1), grid.Name + " retains readable column widths");
            var headers = Descendants<DataGridColumnHeader>(grid).Where(h => h.IsVisible).ToArray();
            Check(headers.Length > 0 && headers.All(h => h.ActualHeight <= 72), grid.Name + " headers do not consume the table body");
            Check(Descendants<DataGridRow>(grid).Any(r => r.IsVisible), grid.Name + " shows data rows");
        }
        main.ApplyPresentationPreset("es", "es-ES", "Europe/Madrid", false, false);
        await NavigateWorkflow(2); await studies.NewCommand.ExecuteAsync(null);
        studies.Name = "19 cuentas · prueba sintética";
        studies.PeriodStart = new(2026, 10, 1); studies.PeriodEnd = new(2026, 10, 31);
        studies.ModelChoice = studies.PublishedModels.Single(); studies.Currency = "EUR"; await AdvanceWorkflow();
        var study = studies.Current!;
        var sourcePath = Path.Combine(output, "accounts.csv");
        File.WriteAllText(sourcePath, "Account;Name;Amount\n" + string.Join("\n", Enumerable.Range(0, 19)
            .Select(i => $"{6000 + i};Cuenta de prueba {i + 1};1000")));
        using (var session = await new ImportSourceSessionFactory().OpenAsync(sourcePath))
        {
            var csv = new CsvSourceDialect("utf-8", ';', '"', CsvBomPolicy.Optional);
            var source = await session.ReadAsync(new(ImportSourceKind.Csv, null, 1, 2, csv));
            var profile = new SourceParseProfileVersion(SourceParseProfileVersion.CurrentSchema, new(Guid.NewGuid()), 1, "19 synthetic accounts",
                ImportSourceKind.Csv, null, 1, 2, new(1, description: 2, amount: 3), csv, new(",", null, [], ImportNegativeSyntax.LeadingMinus), "yyyy-MM-dd",
                ImportAmountMode.PeriodAmount, ImportSignConvention.SignedAmount, study.PeriodStart, study.PeriodEnd, new("EUR"), null,
                ImportDuplicatePolicy.Reject, ImportZeroPolicy.Keep, ImportAccountSelection.All,
                new(ImportProfileApprovalState.ApprovedBusiness, "19 x 1000 EUR"), true);
            var prepared = company.ActiveImports!.PrepareImport(study.CompanyId, study.Id, study.Revision, new(Guid.NewGuid()), source, profile);
            var receipt = company.ActiveImports.ConfirmImport(new(study.CompanyId, study.Id, study.Revision,
                new(Guid.NewGuid()), prepared.Fingerprint, prepared, ImportConfirmMode.AppendBatch, "native-test"));
            await studies.ApplyResourceReceiptAsync(receipt.ConfirmedStudy);
        }
        await NavigateWorkflow(4); await Idle();
        Check(resources.Rows.Count == 19 && !resources.IsDirty, "19 accounts open without a false unsaved state");
        Check(!ResourcePanel.OpenSelectedResourceButton.IsVisible && !ResourcePanel.PartGrid.IsVisible, "preparation has no destination or classification form");
        foreach (var language in new[] { "es", "en" })
        foreach (var width in new[] { 960d, 1257d, 1600d })
        {
            main.ApplyPresentationPreset(language, language == "es" ? "es-ES" : "en-US", "UTC", false, false);
            Width = width; await Idle(); ResetScrollForCapture(); CheckGrid(ResourcePanel.ResourceGrid);
            VisualEvidenceCapture.Capture(this, Path.Combine(output, $"{language}-accounts-{width}.png"));
            Check(!resources.IsDirty, "presentation changes do not create edits");
        }
        Width = 1257;
        foreach (var scale in new[] { 1.25, 1.5, 2d })
        {
            MainContent.LayoutTransform = new ScaleTransform(scale, scale); await Idle();
            CheckGrid(ResourcePanel.ResourceGrid);
            VisualEvidenceCapture.Capture(this, Path.Combine(output, $"accounts-scale-{scale.ToString(System.Globalization.CultureInfo.InvariantCulture)}.png"));
        }
        MainContent.LayoutTransform = Transform.Identity;
        main.ApplyPresentationPreset("es", "es-ES", "Europe/Madrid", false, false);
        await NavigateWorkflow(5); await Idle();
        var accounts = allocations.Accounts; var modes = allocations.Modes;
        var timings = new List<double>();
        foreach (var character in "Distribución de las cuentas ajustadas")
        {
            var watch = Stopwatch.StartNew();
            AllocationPanel.KeyNameBox.SetCurrentValue(TextBox.TextProperty, allocations.Name + character);
            AllocationPanel.KeyNameBox.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
            await Idle(); timings.Add(watch.Elapsed.TotalMilliseconds);
        }
        Check(ReferenceEquals(accounts, allocations.Accounts) && ReferenceEquals(modes, allocations.Modes), "typing retains selector item sources");
        var destination = allocations.Lines.First(); destination.Included = true; destination.Value = "100";
        dialogs.Choices.Enqueue(PendingEditChoice.Save);
        await NavigateWorkflow(4); await Idle();
        Check(workflowStep == 4 && !allocations.IsDirty && dialogs.Choices.Count == 0, "Yes saves the key and leaves once");
        await NavigateWorkflow(5); await NavigateWorkflow(4); await NavigateWorkflow(5); await Idle();
        Check(!allocations.IsDirty && !resources.IsDirty, "return and leave again needs no confirmation");
        allocations.SelectedKey = allocations.Keys.Single(k => k.Value.Name == "Distribución de las cuentas ajustadas"); await allocations.LoadKeyCommand.ExecuteAsync(null);
        Check(await allocations.SaveKeyAsync(true), "saved key can publish");
        allocations.SelectorKind = AccountSelectorKind.Prefix; allocations.Prefix = "60";
        allocations.RuleKey = allocations.Keys.Single(k => k.Value.Name == "Distribución de las cuentas ajustadas").Value.Id; await allocations.ApplyRuleCommand.ExecuteAsync(null); await Idle();
        Check(allocations.PrimaryResults.Count == 19, "all prepared accounts distribute without selecting a pool");
        ResetScrollForCapture(); CheckGrid(AllocationPanel.KeyGrid); CheckGrid(AllocationPanel.LinesGrid);
        Check(RunSectionLayoutSmoke(), "allocation sections and account options never overlap");
        VisualEvidenceCapture.Capture(this, Path.Combine(output, "es-keys-19-accounts.png"));
        AllocationPanel.LibrarySection.IsExpanded = false; AllocationPanel.DefinitionSection.IsExpanded = false;
        AllocationPanel.RulesSection.IsExpanded = false; AllocationPanel.ResultsSection.IsExpanded = false;
        Check(!AllocationPanel.KeyGrid.IsVisible && !AllocationPanel.LinesGrid.IsVisible, "key sections collapse independently");
        ScrollToElement(AllocationPanel.CoverageGrid); await Idle();
        var originalRows = allocations.Coverage.ToArray(); var chosenRow = originalRows[14];
        AllocationPanel.CoverageGrid.SelectedItem = chosenRow;
        AllocationPanel.CoverageGrid.ScrollIntoView(chosenRow); await Idle();
        var rowContainer = (DataGridRow)AllocationPanel.CoverageGrid.ItemContainerGenerator.ContainerFromItem(chosenRow);
        var combo = Descendants<ComboBox>(rowContainer).Single();
        var gridScroll = Descendants<ScrollViewer>(AllocationPanel.CoverageGrid).First();
        var previousOffset = gridScroll.VerticalOffset;
        combo.Focus(); System.Windows.Input.Keyboard.Focus(combo);
        combo.SetCurrentValue(ComboBox.SelectedValueProperty, allocations.PublishedPrimary.First(k => k.Value != chosenRow.SelectedKeyId).Value);
        await AllocationPanel.CoverageAssignmentTask; await Idle();
        Check(originalRows.SequenceEqual(allocations.Coverage), "assignment retains row identity and order");
        Check(ReferenceEquals(chosenRow, AllocationPanel.CoverageGrid.SelectedItem) && Math.Abs(previousOffset - gridScroll.VerticalOffset) < 1,
            "assignment retains selected row and scroll position");
        Check(AllocationPanel.CoverageGrid.ActualHeight >= 400, "account table has an expanded viewport");
        VisualEvidenceCapture.Capture(this, Path.Combine(output, "es-stable-account-selection.png"));
        AllocationPanel.ResultsSection.IsExpanded = true;
        ScrollToElement(AllocationPanel.PrimaryResultsGrid); await Idle(); CheckGrid(AllocationPanel.PrimaryResultsGrid);
        VisualEvidenceCapture.Capture(this, Path.Combine(output, "es-results-19-accounts.png"));
        await allocations.TogglePrimaryCommand.ExecuteAsync(null); await Idle();
        Check(allocations.IsPrimaryClosed, "primary allocation closes after reconciliation");
        await NavigateWorkflow(4); await Idle();
        Check(resources.CorrectionsReadOnly && !resources.CanEditCorrections, "closed primary allocation locks corrections in step 5");
        VisualEvidenceCapture.Capture(this, Path.Combine(output, "es-closed-corrections.png"));
        await NavigateWorkflow(5); await Idle();
        var path = company.ActivePath!;
        await company.CloseCommand.ExecuteAsync(null); Check(company.Snapshot is null, "saved work closes without prompting");
        await company.OpenPath(path); studies.StudyChoice = studies.Studies.Single(s => s.Value.Id == study.Id);
        await studies.LoadCommand.ExecuteAsync(null); await NavigateWorkflow(5); await Idle();
        Check(allocations.PrimaryResults.Count == 19 && !allocations.IsDirty, "19 allocations reopen without unsaved state");
        Check(allocations.IsPrimaryClosed, "primary closure survives file reopen");
        await allocations.TogglePrimaryCommand.ExecuteAsync(null); await NavigateWorkflow(4); await Idle();
        Check(!resources.CorrectionsReadOnly, "reopening from step 6 unlocks corrections");
        await NavigateWorkflow(5); await Idle();
        File.WriteAllText(Path.Combine(output, "interaction.json"), JsonSerializer.Serialize(new
        {
            Status = "passed", PassedChecks = checks, Accounts = 19,
            TypingMilliseconds = new { Average = timings.Average(), Maximum = timings.Max(), Samples = timings.Count },
            Scaling = "WPF layout scaling at 125%, 150%, 200%; not a change to system DPI"
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
