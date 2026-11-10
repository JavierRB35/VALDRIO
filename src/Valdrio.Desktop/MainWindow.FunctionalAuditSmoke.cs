using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Valdrio.Application;
using Valdrio.Domain;
using Valdrio.Infrastructure;

namespace Valdrio.Desktop;

public partial class MainWindow
{
    internal async Task RunFunctionalAuditChecks(string directory)
    {
        Directory.CreateDirectory(directory);
        var main = viewModel!; var studies = main.Studies!; var allocation = main.Allocations!;
        var checks = new List<string>();
        void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); checks.Add(message); }
        async Task Idle() { await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle); UpdateLayout(); }
        // A change of the study picker highlights Open until that study becomes active.
        await studies.NewCommand.ExecuteAsync(null);
        studies.StudyChoice = studies.Studies.First();
        await Idle();
        Check(studies.HasPendingStudySelection && StudyPanel.LoadStudyButton.Tag is true,
            "Choosing a different study highlights the Open analysis action");
        await studies.LoadCommand.ExecuteAsync(null);
        await Idle();
        Check(!studies.HasPendingStudySelection && StudyPanel.LoadStudyButton.Tag is false,
            "Opening the selected analysis clears the pending action highlight");
        foreach (var language in new[] { "es", "en" })
        {
            main.ApplyPresentationPreset(language, language == "es" ? "es-ES" : "en-US", "Europe/Madrid", false, false);
            await NavigateWorkflow(4); ResourcePanel.PreparationTabs.SelectedItem = ResourcePanel.ClassificationTab;
            await RefreshClassification(); await Idle();
            var classification = ((StackPanel)ResourcePanel.ClassificationTab.Content).Children.OfType<ClassificationReviewPanel>().Single();
            Check(ReferenceEquals(classification.DataContext, allocation), "Classification shares actual allocation state " + language);
            Check(classification.CompositionGrid.Items.Count > 0 && classification.CompositionGrid.IsVisible, "Classification accounts visible in preparation " + language);
            Check(!ResourcePanel.ClassificationTab.Header.ToString()!.StartsWith("Resources."), "Preparation task label localized " + language);
            VisualEvidenceCapture.Capture(this, Path.Combine(directory, language + "-classification.png"));
            await NavigateWorkflow(5); await Idle();
            var before = CanonicalSnapshotJson.Serialize(main.Company!.ActiveAllocations!.ReadAllocations(studies.Current!.CompanyId, studies.Current.Id).Configuration);
            await allocation.GenerateDirectKeysCommand.ExecuteAsync(null);
            var ids = allocation.Keys.Select(k => k.Value.Id).OrderBy(id => id.ToString()).ToArray();
            await allocation.GenerateDirectKeysCommand.ExecuteAsync(null);
            Check(ids.SequenceEqual(allocation.Keys.Select(k => k.Value.Id).OrderBy(id => id.ToString())), "Direct key generation is idempotent " + language);
            Check(before == CanonicalSnapshotJson.Serialize(main.Company.ActiveAllocations.ReadAllocations(studies.Current.CompanyId, studies.Current.Id).Configuration), "Direct keys do not reassign accounts " + language);
            AllocationPanel.ShowKeys(); await Idle(); VisualEvidenceCapture.Capture(this, Path.Combine(directory, language + "-direct-keys.png"));
            await NavigateWorkflow(8); var flows = main.CostFlows!; flows.UseCurrentPrimary = true; await Idle();
            Check(flows.Nodes.Count > 0 && flows.ExecutiveSummary.Length > 0 && ReportStatusBanner.IsVisible, "Current primary and persistent banner visible " + language);
            Check(flows.Runs.Count == 0, "Current primary query creates no saved execution " + language);
            VisualEvidenceCapture.Capture(this, Path.Combine(directory, language + "-primary.png"));
            var physical = new PhysicalActivityEditor(studies);
            var editor = new PhysicalCapacityAssistantEditor(physical) { Method = PhysicalCapacityMethod.Declared, DeclaredQuantity = "200", DeclaredSource = "", DeclaredDescription = "" };
            Check(editor.CanAdopt, "Declared numeric proposal needs no narrative " + language);
            editor.Adopt(); Check(decimal.Parse(physical.SelectedRow!.NormalText, physical.NumericCulture) == 200m, "Adoption retains declared value " + language);
        }
        main.ApplyPresentationPreset("es", "es-ES", "Europe/Madrid", false, false);
        await NavigateWorkflow(8);
        var report = main.CostFlows!;
        await report.CalculateCommand.ExecuteAsync(null);
        Check(report.SelectedRun?.Run.Status == CalculationRunStatus.Completed, "Example calculation completes: " + report.Status);
        report.SecondaryPhase = true;
        Check(report.NodeBalances.Sum(r => r.FinalValue) == 17500m, "Example final cost is 17500 without double counting");
        Check(report.NodeBalances.Any(r => r.Label.Contains("DELIVERY") && decimal.Round(r.FinalValue, 2) == 11478.57m), "DELIVERY final cost is 11478.57");
        Check(report.NodeBalances.Any(r => r.Label.Contains("CUSTOMER_SUCCESS") && decimal.Round(r.FinalValue, 2) == 6021.43m), "CUSTOMER_SUCCESS final cost is 6021.43");
        var savedRun = report.SelectedRun!.Run.Id;
        var company = main.Company!;
        var current = studies.Current!;
        var workspace = company.ActiveAllocations!.ReadAllocations(current.CompanyId, current.Id);
        if (workspace.Configuration.SecondaryClosed)
        {
            var receipt = company.ActiveAllocations.ChangeAllocations(new(current.CompanyId, current.Id, current.Revision, new(Guid.NewGuid()), "native-test", workspace.Configuration.WithSecondaryClosed(false)));
            studies.AcceptExternalReceipt(receipt.ConfirmedStudy); current = studies.Current!;
        }
        workspace = company.ActiveAllocations.ReadAllocations(current.CompanyId, current.Id);
        if (workspace.Configuration.PrimaryClosed)
        {
            var receipt = company.ActiveAllocations.ChangeAllocations(new(current.CompanyId, current.Id, current.Revision, new(Guid.NewGuid()), "native-test", workspace.Configuration.WithPrimaryClosed(false)));
            studies.AcceptExternalReceipt(receipt.ConfirmedStudy); current = studies.Current!;
        }
        var sourcePath = Path.Combine(directory, "acceptance-import.csv");
        File.WriteAllText(sourcePath, "Account;Name;Amount\n629900;Pendiente positivo;1000\n629902;Mantenimiento directo;600\n629901;Abono pendiente;-100");
        using (var sourceSession = await new ImportSourceSessionFactory().OpenAsync(sourcePath))
        {
            var csv = new CsvSourceDialect("utf-8", ';', '"', CsvBomPolicy.Optional);
            var source = await sourceSession.ReadAsync(new(ImportSourceKind.Csv, null, 1, 2, csv));
            var profile = new SourceParseProfileVersion(SourceParseProfileVersion.GroupingSchema, new(Guid.NewGuid()), 1, "Audit signed import", ImportSourceKind.Csv, null, 1, 2,
                new(1, description: 2, amount: 3), csv, new(",", null, [], ImportNegativeSyntax.LeadingMinus), "yyyy-MM-dd", ImportAmountMode.PeriodAmount, ImportSignConvention.SignedAmount,
                current.PeriodStart, current.PeriodEnd, new("EUR"), null, ImportDuplicatePolicy.AggregateWithProvenance, ImportZeroPolicy.Keep, ImportAccountSelection.All,
                new(ImportProfileApprovalState.ApprovedBusiness, "signed acceptance fixture"), true, grouping: new([new(2,"629900"),new(3,"629902"),new(4,"629901")]));
            var prepared = company.ActiveImports!.PrepareImport(current.CompanyId, current.Id, current.Revision, new(Guid.NewGuid()), source, profile);
            Check(prepared.Reconciliation.NetAmount == 1500m, "Signed import adds net 1500");
            var receipt = company.ActiveImports.ConfirmImport(new(current.CompanyId, current.Id, current.Revision, new(Guid.NewGuid()), prepared.Fingerprint, prepared, ImportConfirmMode.AppendBatch, "native-test"));
            await studies.ApplyResourceReceiptAsync(receipt.ConfirmedStudy);
        }
        await NavigateWorkflow(5); await allocation.EnterAsync();
        var admin = company.ActiveAllocations.ReadAllocations(studies.Current!.CompanyId, studies.Current.Id).Resources.Model.Structure.Nodes.Single(n => n.Code == "ADMIN");
        allocation.NewRuleCommand.Execute(null); allocation.RuleAccount = "629902";
        allocation.RuleKey = allocation.Keys.First(k => k.Value.Stage == AllocationStage.Primary && k.Value.Status == ModelStatus.Published && k.Value.Lines.Count == 1 && k.Value.Lines[0].DestinationNodeId == admin.Id).Value.Id;
        await allocation.ApplyRuleCommand.ExecuteAsync(null);
        await NavigateWorkflow(8); report.UseCurrentPrimary = true;
        Check(report.Concentration.Sum(r => r.Amount) == 18100m, "Primary assigns 18100 after maintenance");
        Check(report.Diagnostics.Contains("629900") && report.Diagnostics.Contains("629901"), "Partial report names both pending accounts");
        Check(report.ExecutiveSummary.Contains("19.000") && report.ExecutiveSummary.Contains("1.100") && report.ExecutiveSummary.Contains("900"), "Summary separates total, absolute and net pending amounts");
        await report.CalculateCommand.ExecuteAsync(null);
        Check(!report.HasError && report.CurrentState.Contains(report["Partial"]), "Missing data remains partial, not a technical failure");
        report.SelectedRun = report.Runs.Single(r => r.Run.Id == savedRun); report.SecondaryPhase = true;
        Check(report.Status != report["Failed"], "Selecting a valid history clears incomplete-run feedback");
        Check(report.CurrentState.Contains(report["Historical"]), "Saved 17500 result is historical after import");
        report.ReportFilter = "DELIVERY";
        Check(report.Concentration.Count == 1 && decimal.Round(report.Concentration[0].Amount, 2) == 11478.57m, "Filtered historical result retains frozen amount");
        File.WriteAllLines(Path.Combine(directory, "historical-filtered.csv"), CostFlowPanel.BuildCsv(report), new System.Text.UTF8Encoding(true));
        var document = CostFlowPanel.BuildPrintableReport(report, 793, 1122);
        Check(document.Pages.Count > 0 && document.Pages.All(p => ((StackPanel)p.Child.Children[0]).Children.OfType<TextBlock>().First().Text.Contains(report.CurrentState)), "Every printed page retains historical status");
        for (var page = 0; page < document.Pages.Count; page++)
        {
            var visual = document.Pages[page].Child;
            visual.Measure(new Size(793, 1122)); visual.Arrange(new Rect(0, 0, 793, 1122)); visual.UpdateLayout();
            VisualEvidenceCapture.Capture(visual, Path.Combine(directory, $"report-page-{page + 1}.png"));
        }
        await Idle(); VisualEvidenceCapture.Capture(this, Path.Combine(directory, "historical-filtered.png"));
        var activity = new PhysicalActivityEditor(studies);
        var capacity = new PhysicalCapacityAssistantEditor(activity) { Method = PhysicalCapacityMethod.Declared, DeclaredQuantity = "1000", DeclaredSource = "Plan aprobado", DeclaredDescription = "" };
        capacity.Adopt(); activity.SelectedRow!.RealText = "800"; activity.SelectedRow.RealSource = "Parte de actividad";
        activity.Save(true);
        Check(activity.SelectedRow.Utilization.Contains("80") && !activity.HasPendingEdits, "Capacity 1000 / activity 800 confirms with source and empty narrative: " + activity.Feedback);
        var model = company.ActiveAllocations.ReadAllocations(studies.Current!.CompanyId, studies.Current.Id).Resources.Model.Structure;
        var delivery = model.Nodes.Single(n => n.Code == "DELIVERY"); var pool = model.Pools.Single(p => p.NodeId == delivery.Id);
        var product = new CompositionItem(new(Guid.NewGuid()), "AUDIT-P", "Servicio de prueba", pool.DriverUnitId, CompositionItemKind.Product, true);
        var recipe = new CompositionVersion(new(Guid.NewGuid()), product.Id, 1, model.Id, 1, CompositionState.Published, "", [
            new(new(Guid.NewGuid()), null, null, pool.Id, 2m, pool.DriverUnitId, 1m, ConsumptionBasis.PerUnit, CompositionLossKind.None, 0m)]);
        var catalog = studies.ActiveCompositions!.ReadCompositions();
        studies.ActiveCompositions.SaveCompositions(catalog with { Items = catalog.Items.Append(product).ToArray(), Versions = catalog.Versions.Append(recipe).ToArray() }, catalog.Revision, "native-test");
        var estimate = new EstimateEditor(studies); await estimate.LoadAsync(); estimate.Code = "AUDIT-10"; estimate.Quantity = "10"; estimate.Recipe = recipe.Id; estimate.Run = savedRun;
        await estimate.PreviewAsync();
        Check(!estimate.CanAccept && estimate.Details.Single().Quantity.StartsWith("20"), "Incomplete estimate keeps 20 physical hours and blocks acceptance: " + estimate.Feedback);
        Check(estimate.Details.Single().Amount == "—", "Missing rate is not a zero monetary cost");
        File.WriteAllText(Path.Combine(directory, "checks.json"), JsonSerializer.Serialize(checks, new JsonSerializerOptions { WriteIndented = true }));
    }
}

