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
using Valdrio.Infrastructure;
namespace Valdrio.Desktop;
public partial class MainWindow
{
    internal async Task RunAllocationChecks(string directory)
    {
        var output = Path.Combine(directory, "t08-allocations"); Directory.CreateDirectory(output);
        var main = viewModel!; var company = main.Company!; var studies = main.Studies!; var vm = main.Allocations!; var panel = AllocationPanel;
        var checks = new List<string>();
        void Check(bool value, string label) { if (!value) throw new InvalidOperationException("T08 native: " + label + " · " + vm.Status); checks.Add(label); }
        async Task Idle() { await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle); UpdateLayout(); }
        void Text(TextBox box, string value) { box.SetCurrentValue(TextBox.TextProperty, value); box.GetBindingExpression(TextBox.TextProperty)!.UpdateSource(); }
        void Select(ComboBox box, object value) { box.SetCurrentValue(ComboBox.SelectedValueProperty, value); box.GetBindingExpression(ComboBox.SelectedValueProperty)!.UpdateSource(); }
        async Task Click(Button button)
        {
            GuidedUi.Reveal(button); Check(button.IsEnabled, "enabled action " + button.Name);
            ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke(); await Idle();
            if (button.Command is IAsyncRelayCommand c && c.ExecutionTask is { } task) await task.WaitAsync(TimeSpan.FromSeconds(45));
            await Idle();
        }
        main.ApplyPresentationPreset("es", "es-ES", "Europe/Madrid", false, false);
        await NavigateWorkflow(2); await studies.NewCommand.ExecuteAsync(null);
        studies.Name = "T08 · Demostración sintética"; studies.PeriodStart = new(2026,10,1); studies.PeriodEnd = new(2026,10,31);
        studies.ModelChoice = studies.PublishedModels.Single(); studies.Currency = "EUR"; await AdvanceWorkflow();
        var study = studies.Current!;
        var sourcePath = Path.Combine(output, "names.csv"); File.WriteAllText(sourcePath, "Account;Name;Amount\n6005;Papelería;400\n6005;Material de oficina;600");
        using (var sourceSession = await new ImportSourceSessionFactory().OpenAsync(sourcePath))
        {
            var csv = new CsvSourceDialect("utf-8", ';', '"', CsvBomPolicy.Optional);
            var source = await sourceSession.ReadAsync(new(ImportSourceKind.Csv, null, 1, 2, csv));
            var profile = new SourceParseProfileVersion(SourceParseProfileVersion.GroupingSchema, new(Guid.NewGuid()), 1, "T08 synthetic descriptions",
                ImportSourceKind.Csv, null, 1, 2, new(1, description: 2, amount: 3), csv, new(",", null, [], ImportNegativeSyntax.LeadingMinus), "yyyy-MM-dd",
                ImportAmountMode.PeriodAmount, ImportSignConvention.SignedAmount, study.PeriodStart, study.PeriodEnd, new("EUR"), null,
                ImportDuplicatePolicy.AggregateWithProvenance, ImportZeroPolicy.Keep, ImportAccountSelection.All,
                new(ImportProfileApprovalState.ApprovedBusiness, "6005=1000 EUR"), true, grouping: new([new(2,"6005"),new(3,"6005")]));
            var prepared = company.ActiveImports!.PrepareImport(study.CompanyId, study.Id, study.Revision, new(Guid.NewGuid()), source, profile);
            var batch = company.ActiveImports.ConfirmImport(new(study.CompanyId, study.Id, study.Revision, new(Guid.NewGuid()), prepared.Fingerprint, prepared, ImportConfirmMode.AppendBatch, "native-test"));
            await studies.ApplyResourceReceiptAsync(batch.ConfirmedStudy);
        }
        var w = company.ActiveResources!.ReadResources(study.CompanyId, study.Id);
        var principal = w.Model.Structure.Nodes.First(n => n.Role == NodeRole.Principal);
        var auxiliary = w.Model.Structure.Nodes.Single(n => n.Role == NodeRole.Auxiliary);
        var part = w.Resources.Single().Parts.Single();
        Check(part.Routing == ResourceRoutingKind.Pending && part.PoolId is null, "imported account needs no prior pool or classification");
        await NavigateWorkflow(4); await AdvanceWorkflow(); await Idle();
        Check(workflowStep == 5 && panel.IsVisible && vm.CanEdit, "T07 continues to the real allocation editor");
        Check(vm.Accounts.Single().Label.Contains(vm["NameConflict"], StringComparison.Ordinal), "conflicting descriptions are visible with their account code");
        Text(panel.KeyNameBox, "Clave sin uso para eliminar");
        vm.Lines.First().Included = true; vm.Lines.First().Value = "100";
        await Click(panel.SaveKeyButton); vm.SelectedKey = vm.Keys.Single();
        await Click(panel.DeleteKeyButton); Check(vm.Keys.Count == 0, "unused key is deleted through the visible action");
        Text(panel.KeyNameBox, "Consumos de oficina");
        var line = vm.Lines.Single(l => l.Id == principal.Id); line.Included = true; line.Value = "80";
        await Idle(); Check(panel.SaveKeyButton.IsEnabled && !vm.PublishCommand.CanExecute(null) && vm.Validation.Contains("100"),
            "80 percent can be saved as a draft but cannot be published; the editor explains that 100 percent is required");
        Check(vm.Keys.Count == 0 && vm.CanEditKey, "incomplete percentages preserve editing");
        line.Value = "100";
        await Click(panel.SaveKeyButton); var primaryKey = vm.Keys.Single().Value;
        Check(primaryKey.Status == ModelStatus.Published && panel.KeyNameBox.IsEnabled && vm.CanEditComposition, "unused saved key remains fully editable");
        Check(vm.Validation == vm["Complete"], "saved key has a meaningful validation result");
        await Click(panel.NewKeyButton); Text(panel.KeyNameBox, "Otra distribución"); Select(panel.ModeBox, AllocationKeyMode.EqualShares);
        vm.Lines.Single(l => l.Id == auxiliary.Id).Included = true; await Click(panel.SaveKeyButton);
        var otherKey = vm.Keys.Single(k => k.Value.Name == "Otra distribución").Value.Id;
        panel.RuleTabs.SelectedIndex = 0; await Idle(); Select(panel.RuleAccountBox, "6005"); Select(panel.RuleKeyBox, primaryKey.Id);
        await Click(panel.SaveRuleButton); Check(vm.PrimaryRules.Count == 1 && vm.PrimaryResults.Count == 0, "save rule persists without applying");
        await Click(panel.ApplyRuleButton);
        Check(vm.PrimaryResults.Count == 1 && vm.PrimaryResults.Single().Amount.EndsWith(" EUR", StringComparison.Ordinal) &&
            vm.PrimaryResults.Single().Amount.Contains(",00", StringComparison.Ordinal), "apply rule distributes prepared account");
        panel.RuleTabs.SelectedIndex = 2; await Idle(); Select(panel.NameAccountBox, "6005");
        panel.DescriptionBox.SetCurrentValue(ComboBox.SelectedItemProperty, "Papelería"); panel.DescriptionBox.GetBindingExpression(ComboBox.SelectedItemProperty)!.UpdateSource();
        await Click(panel.ChooseDescriptionButton); Check(vm.Accounts.Single().Label == "6005 — Papelería", "chosen description replaces the conflict label without changing the account code");
        Check(!vm.IsDirty, "description save leaves no pending form: " + vm.KeyState + " / rule=" + vm.HasUnappliedRule);
        await NavigateWorkflow(4); Check(main.Resources!.Rows.Single().DisplayName == "6005 — Papelería", "chosen name is also visible in T07");
        await NavigateWorkflow(5); await Idle();
        panel.RuleTabs.SelectedIndex = 1; await Idle(); Select(panel.PartBox, part.Id); Select(panel.PartKeyBox, primaryKey.Id);
        await Click(panel.AssignButton);
        Check(company.ActiveAllocations!.ReadAllocations(study.CompanyId, study.Id).Configuration.ExplicitAssignments.Single().PartId == part.Id, "manual part override persists");
        await Click(panel.AutomaticButton);
        Check(company.ActiveAllocations.ReadAllocations(study.CompanyId, study.Id).Configuration.ExplicitAssignments.Count == 0, "automatic rules can be restored explicitly");
        ScrollToElement(panel.CoverageGrid); await Idle();
        var coverageRow = (DataGridRow)panel.CoverageGrid.ItemContainerGenerator.ContainerFromIndex(0);
        ComboBox? FindCombo(DependencyObject parent)
        {
            for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is ComboBox combo) return combo;
                if (FindCombo(child) is { } nested) return nested;
            }
            return null;
        }
        var directCombo = FindCombo(coverageRow)!; directCombo.Focus();
        System.Windows.Input.Keyboard.Focus(directCombo);
        directCombo.SetCurrentValue(ComboBox.SelectedValueProperty, otherKey);
        await panel.CoverageAssignmentTask; await Idle();
        Check(vm.Coverage.Single().SelectedKeyId == otherKey, "table combo persists a direct assignment");
        VisualEvidenceCapture.Capture(this, Path.Combine(output, "es-direct-assignment.png"));
        vm.Part = part.Id; await vm.AutomaticCommand.ExecuteAsync(null); await Idle();
        Check(vm.Coverage.Single().SelectedKeyId == primaryKey.Id, "removing manual choice restores the rule");
        var current = studies.Current!; var snapshot = company.ActiveStudies!.Capture(new(current.CompanyId,current.Id,current.Revision,new(Guid.NewGuid()),"native-test"));
        Check(snapshot.Input is AllocationStudySnapshotInput a && a.AppliedKeys.Count == 1, "snapshot freezes actual primary key");
        vm.SelectedKey = vm.Keys.Single(k => k.Value.Id == primaryKey.Id);
        GuidedUi.Reveal(panel.KeyGrid); panel.KeyGrid.ScrollIntoView(vm.SelectedKey); await Idle();
        var keyRow = (DataGridRow)panel.KeyGrid.ItemContainerGenerator.ContainerFromItem(vm.SelectedKey);
        panel.KeyGrid.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left)
            { RoutedEvent = Control.MouseDoubleClickEvent, Source = keyRow });
        if (vm.LoadKeyCommand.ExecutionTask is { } openTask) await openTask;
        await Idle(); Check(vm.Name == primaryKey.Name, "double click opens the selected library key");
        Check(panel.KeyNameBox.IsEnabled && !vm.CanEditComposition, "used key allows name editing only");
        Text(panel.KeyNameBox, "Consumos de oficina renombrados"); await Click(panel.SaveKeyButton);
        Check(company.ActiveAllocations.ReadAllocations(current.CompanyId,current.Id).Coverage.Single(c => c.SourceId == part.Id).KeyId == primaryKey.Id, "renaming retains assignments");
        foreach (var language in new[] { "es", "en" })
        {
            main.ApplyPresentationPreset(language, language == "es" ? "es-ES" : "en-US", "UTC", false, false); await Idle();
            panel.RuleTabs.SelectedIndex = 0; ResetScrollForCapture(); VisualEvidenceCapture.Capture(this, Path.Combine(output, language + "-keys.png"));
            ScrollToElement(panel.RuleTabs); VisualEvidenceCapture.Capture(this, Path.Combine(output, language + "-rules.png"));
            Check(panel.KeyGrid.Columns[0].Header!.ToString() == vm["Name"] && vm.Accounts.Single().Label == "6005 — Papelería", language + " labels localize while account identity stays unchanged");
        }
        main.ApplyPresentationPreset("en", "es-ES", "Europe/Madrid", true, true); await Idle(); ResetScrollForCapture();
        VisualEvidenceCapture.Capture(this, Path.Combine(output,"pseudo-rtl.png"));
        main.ApplyPresentationPreset("es", "es-ES", "Europe/Madrid", false, false);
        var path = company.ActivePath!; await company.CloseCommand.ExecuteAsync(null); File.Delete(sourcePath); await company.OpenPath(path);
        studies.StudyChoice = studies.Studies.Single(s => s.Value.Id == current.Id); await studies.LoadCommand.ExecuteAsync(null); await NavigateWorkflow(5); await Idle();
        var read = company.ActiveAllocations.ReadAllocations(current.CompanyId,current.Id);
        Check(read.Coverage.Where(c => c.SourceId == part.Id).All(c => c.Diagnostic is null) && read.Resources.Origins.Single().Descriptions.Count == 2, "reopen without source file preserves original names, saved choice, keys and full coverage");
        Check(company.ActiveStudies.ReadSnapshots(current.CompanyId,current.Id).Single(s => s.Id == snapshot.Id).CanonicalJson == snapshot.CanonicalJson, "historical allocation snapshot stays byte-identical after a new key version and reopen");
        File.WriteAllText(Path.Combine(output,"snapshot-receipt.json"),CanonicalSnapshotJson.Serialize(snapshot));
        File.WriteAllText(Path.Combine(output,"interaction.json"),JsonSerializer.Serialize(new { Status="passed", PassedChecks=checks, snapshot.EconomicInputHash, snapshot.FullSnapshotHash },new JsonSerializerOptions { WriteIndented=true }));
    }
}
