using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using Valdrio.Application;
using Valdrio.Domain;
using Valdrio.Infrastructure;
using Valdrio.Infrastructure.CompanyFiles;
namespace Valdrio.Desktop;

public partial class MainWindow
{
    internal async Task RunResourceChecks(string directory)
    {
        var output = Path.Combine(directory, "t07-resources"); Directory.CreateDirectory(output);
        var main = viewModel!; var company = main.Company!; var studies = main.Studies!; var vm = main.Resources!; var panel = ResourcePanel;
        var checks = new List<string>();
        void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException("T07 native: " + message + " · " + vm.Status); checks.Add(message); }
        void Text(TextBox box, string value)
        {
            box.SetCurrentValue(TextBox.TextProperty, value); box.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
        }
        void Select(ComboBox box, object value)
        {
            box.SetCurrentValue(ComboBox.SelectedValueProperty, value); box.GetBindingExpression(ComboBox.SelectedValueProperty)!.UpdateSource();
        }
        async Task Click(Button button)
        {
            GuidedUi.Reveal(button); Check(button.IsEnabled, "enabled action: " + button.Name);
            ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            if (button.Command is IAsyncRelayCommand command && command.ExecutionTask is { } task) await task;
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        }
        main.ApplyPresentationPreset("es", "es-ES", "Europe/Madrid", false, false);
        await NavigateWorkflow(2); await studies.NewCommand.ExecuteAsync(null);
        studies.Name = "T07 · C04 sintético"; studies.PeriodStart = new(2026, 10, 1); studies.PeriodEnd = new(2026, 10, 31);
        studies.ModelChoice = studies.PublishedModels.Single(); studies.Currency = "EUR"; await AdvanceWorkflow();
        var study = studies.Current!;
        var sourcePath = Path.Combine(output, "synthetic.csv"); File.WriteAllText(sourcePath, "Account;Amount\n6000;1000");
        using (var sourceSession = await new ImportSourceSessionFactory().OpenAsync(sourcePath))
        {
            var csv = new CsvSourceDialect("utf-8", ';', '"', CsvBomPolicy.Optional);
            var source = await sourceSession.ReadAsync(new(ImportSourceKind.Csv, null, 1, 2, csv));
            var profile = new SourceParseProfileVersion(SourceParseProfileVersion.CurrentSchema, new(Guid.NewGuid()), 1, "T07 synthetic C04",
                ImportSourceKind.Csv, null, 1, 2, new(1, amount: 2), csv, new(",", null, [], ImportNegativeSyntax.LeadingMinus), "yyyy-MM-dd",
                ImportAmountMode.PeriodAmount, ImportSignConvention.SignedAmount, study.PeriodStart, study.PeriodEnd, new CurrencyCode("EUR"), null,
                ImportDuplicatePolicy.Reject, ImportZeroPolicy.Keep, ImportAccountSelection.All,
                new(ImportProfileApprovalState.ApprovedBusiness, "Synthetic account 6000 = 1000 EUR"), true);
            var prepared = company.ActiveImports!.PrepareImport(study.CompanyId, study.Id, study.Revision, new(Guid.NewGuid()), source, profile);
            var batch = company.ActiveImports.ConfirmImport(new(study.CompanyId, study.Id, study.Revision, new(Guid.NewGuid()), prepared.Fingerprint,
                prepared, ImportConfirmMode.AppendBatch, "native-test"));
            await studies.ApplyResourceReceiptAsync(batch.ConfirmedStudy);
        }
        await NavigateWorkflow(3); await AdvanceWorkflow();
        Check(workflowStep == 4 && vm.Stage == 0 && panel.IsVisible, "saved import advances to resources with one primary action");
        Check(vm.Rows.Count == 1 && vm.Rows.Single().Code == "6000", "account appears with converted original and pending coverage");
        await AdvanceWorkflow(); Check(vm.Stage == 1, "choose account advances to adjustments");
        Text(panel.AdjustmentForm_AmountBox, "-100"); Text(panel.AdjustmentForm_TypeBox, "Corrección"); Text(panel.AdjustmentForm_ReasonBox, "Partida que no representa coste");
        await AdvanceWorkflow(); Check(vm.Stage == 1, "unapplied adjustment prevents silent loss on continue");
        await Click(panel.AddAdjustmentButton); Check(vm.Adjustments.Single().Value.Conversion.OriginalMoney.Amount == -100m, "signed adjustment retains original currency and amount");
        await AdvanceWorkflow(); Check(vm.Stage == 2, "adjustments advance to parts");
        vm.PartChoice = vm.Parts.Single(); await Click(panel.LoadPartButton);
        Text(panel.PartForm_AmountBox, "300"); Select(panel.PartForm_RouteBox, ResourceRoutingKind.DirectObject);
        Text(panel.ObjectForm_CodeBox, "PROD"); Text(panel.ObjectForm_NameBox, "Producto de prueba"); Select(panel.ObjectKindBox, ResourceObjectKind.Product);
        await Click(panel.PutObjectButton);
        Check(vm.PartForm["Amount"] == "300" && vm.Stage == 2, "saving a direct cost object preserves the unfinished part form");
        Select(panel.PartForm_ObjectIdBox, vm.Objects.Single().Value);
        Select(panel.PartForm_BehaviorBox, ResourceBehavior.Fixed); Select(panel.PartForm_UnitIdBox, vm.Units.Single().Value);
        Text(panel.PartForm_MinimumBox, "0"); Text(panel.PartForm_MaximumBox, "100"); Select(panel.PartForm_LayerBox, ResourceCostLayer.Production);
        await Click(panel.ApplyPartButton);
        Text(panel.PartForm_AmountBox, "500"); Select(panel.PartForm_RouteBox, ResourceRoutingKind.AllocationPool); Select(panel.PartForm_PoolIdBox, vm.Pools.First().Value);
        Select(panel.PartForm_BehaviorBox, ResourceBehavior.Variable); Select(panel.PartForm_UnitIdBox, vm.Units.Single().Value);
        Text(panel.PartForm_MinimumBox, "0"); Text(panel.PartForm_MaximumBox, "100"); Select(panel.PartForm_LayerBox, ResourceCostLayer.Production);
        Select(panel.PartForm_CapacityBox, ResourceCapacityTreatment.AssessAtPool); Text(panel.PartForm_NormalBox, "100"); Text(panel.PartForm_ActualBox, "90");
        await Click(panel.ApplyPartButton);
        Text(panel.PartForm_AmountBox, "100"); Select(panel.PartForm_RouteBox, ResourceRoutingKind.Excluded);
        await Click(panel.ApplyPartButton); Check(vm.Parts.Count == 2, "exclusion without a reason is blocked and form retained");
        Text(panel.PartForm_ExclusionBox, "Fuera del perímetro del estudio"); await Click(panel.ApplyPartButton);
        Check(vm.Parts.Count == 3 && vm.Parts.Sum(p => p.Value.Amount.Amount) == 900m, "C04 routes 300 direct plus 500 pool plus 100 excluded");
        Text(panel.PartForm_AmountBox, "100"); await Click(panel.ApplyPartButton);
        await AdvanceWorkflow(); Text(panel.ReasonBox, "Revisión C04 y comprobación de conservación");
        var revision = studies.Current!.Revision; await AdvanceWorkflow();
        Check(vm.Stage == 3 && vm.IsDirty && studies.Current.Revision == revision, "extra 100 cannot be saved and draft stays editable");
        await GoBackWorkflow(); vm.PartChoice = vm.Parts.Single(p => p.Value.Routing == ResourceRoutingKind.Pending); await Click(panel.RemovePartButton);
        await AdvanceWorkflow();
        panel.ReasonBox.Focus(); await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        Check(!string.IsNullOrWhiteSpace(AutomationProperties.GetHelpText(panel.ReasonBox)), "resource field explanation is exposed to keyboard and assistive technology");
        ResetScrollForCapture(); VisualEvidenceCapture.Capture(this, Path.Combine(output, "review-es.png"));
        main.ApplyPresentationPreset("en", "es-ES", "UTC", true, true); ResetScrollForCapture();
        VisualEvidenceCapture.Capture(this, Path.Combine(output, "review-pseudo-rtl.png"));
        main.ApplyPresentationPreset("es", "es-ES", "Europe/Madrid", false, false);
        await AdvanceWorkflow(); Check(!vm.IsDirty && vm.Stage == 0 && vm.Status == vm["Saved"], "primary save persists account with explicit saved confirmation");
        var current = studies.Current!; var snapshot = company.ActiveStudies!.Capture(new(current.CompanyId, current.Id, current.Revision, new(Guid.NewGuid()), "native-test"));
        snapshot.EnsureValid();
        File.WriteAllText(Path.Combine(output, "snapshot-receipt.json"), CanonicalSnapshotJson.Serialize(snapshot));
        var hash = snapshot.EconomicInputHash;
        main.ApplyPresentationPreset("en", "en-US", "UTC", false, false); await vm.EnterAsync(true);
        Check(company.ActiveStudies.ReadSnapshots(current.CompanyId, current.Id).Single().EconomicInputHash == hash, "language region and time zone preserve saved economics");
        ResetScrollForCapture(); VisualEvidenceCapture.Capture(this, Path.Combine(output, "saved-en.png"));
        var path = company.ActivePath!;
        await company.CloseCommand.ExecuteAsync(null); await company.OpenPath(path);
        studies.StudyChoice = studies.Studies.Single(s => s.Value.Id == current.Id); await studies.LoadCommand.ExecuteAsync(null);
        await NavigateWorkflow(4);
        var read = company.ActiveResources!.ReadResources(current.CompanyId, current.Id);
        Check(read.Preparation.IsValid && !read.Preparation.HasPendingParts && read.Preparation.Coverage.Single().Effective.Amount == 900m,
            "close and reopen retain all routes adjustments classification and capacity observations");
        Check(read.Resources.Single().Parts.Single(p => p.Routing == ResourceRoutingKind.AllocationPool).Classification.CapacityOwner!.Actual!.Value.Value == 90m,
            "capacity owner and its typed observation are persisted");
        var receiptJson = File.ReadAllText(Path.Combine(output, "snapshot-receipt.json"));
        Check(company.ActiveStudies.ReadSnapshots(current.CompanyId, current.Id).Single().CanonicalJson == snapshot.CanonicalJson,
            "historical snapshot remains byte-identical after reopen");
        File.WriteAllText(Path.Combine(output, "interaction.json"), JsonSerializer.Serialize(new { Status = "passed", SchemaVersion = company.Snapshot!.SchemaVersion,
            PassedChecks = checks, snapshot.EconomicInputHash, snapshot.FullSnapshotHash,
            ReceiptSha256 = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(receiptJson))).ToLowerInvariant() }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
