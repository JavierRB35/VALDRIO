using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using Valdrio.Application;
using Valdrio.Domain;
using Valdrio.Infrastructure;
using Valdrio.Infrastructure.CompanyFiles;

namespace Valdrio.Desktop;
public partial class ModelWorkspacePanel
{
    internal async Task RunModelInteractionChecks(string directory, QueuedCompanyFileDialogs files, QueuedModelEditorDialogs pending, MainWindowViewModel main)
    {
        var vm = (ModelWorkspaceViewModel)DataContext;
        var company = main.Company!;
        var output = Path.Combine(directory, "models");
        Directory.CreateDirectory(output);
        var path = Path.Combine(output, "example.valdrio");
        var passed = new List<string>();
        void Check(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException("Model WPF: " + message);
            passed.Add(message);
        }

        void Text(TextBox box, string value)
        {
            box.SetCurrentValue(TextBox.TextProperty, value);
            box.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
        }

        void Select<T>(ComboBox box, T value)
        {
            box.SetCurrentValue(ComboBox.SelectedItemProperty, value);
            box.GetBindingExpression(ComboBox.SelectedItemProperty)!.UpdateSource();
        }

        async Task Click(Button button)
        {
            Check(button.IsEnabled, "enabled: " + button.Name);
            ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
            await Dispatcher.InvokeAsync(() =>
            {
            }, DispatcherPriority.ApplicationIdle);
            if (button.Command is IAsyncRelayCommand cmd && cmd.ExecutionTask is { } task)
                await task.WaitAsync(TimeSpan.FromSeconds(45));
            await Dispatcher.InvokeAsync(() =>
            {
            }, DispatcherPriority.ApplicationIdle);
            UpdateLayout();
        }

        var window = (MainWindow)Window.GetWindow(this);
        var companyPanel = window.CompanyPanel;
        main.ApplyPresentationPreset("en", "es-ES", "UTC", false, false);
        Text(companyPanel.NameBox, "VALDRIO T04 synthetic factory 日本");
        Select(companyPanel.CurrencyBox, "USD");
        files.Paths.Enqueue(path);
        await Click(companyPanel.CreateButton);
        Check(company.Snapshot?.SchemaVersion == 2, "v2 created from company button");
        var companyId = company.Snapshot!.Id;
        Text(companyPanel.UnitCodeBox, "h");
        Text(companyPanel.DimensionBox, "time");
        Text(companyPanel.SymbolBox, "h");
        Select(companyPanel.ScaleBox, 12);
        companyPanel.DiscreteBox.SetCurrentValue(CheckBox.IsCheckedProperty, false);
        await Click(companyPanel.AddUnitButton);
        Text(companyPanel.UnitCodeBox, "lot");
        Text(companyPanel.DimensionBox, "batch");
        Text(companyPanel.SymbolBox, "lot");
        Select(companyPanel.ScaleBox, 0);
        companyPanel.DiscreteBox.SetCurrentValue(CheckBox.IsCheckedProperty, true);
        await Click(companyPanel.AddUnitButton);
        Check(vm.Units.Count == 2, "unit selections loaded through port");
        Text(CenterCodeBox, "PRENSA-001");
        Text(CenterNameBox, "Prensa 日本");
        await Click(CreateCenterButton);
        Text(CenterCodeBox, "MANT-001");
        Text(CenterNameBox, "Mantenimiento");
        await Click(CreateCenterButton);
        Check(vm.Centers.Count == 2, "physical masters saved");
        Text(CenterCodeBox, "MANT-001");
        Text(CenterNameBox, "Duplicate");
        await Click(CreateCenterButton);
        Check(vm.Status.StartsWith("MODEL_DUPLICATE", StringComparison.Ordinal) && vm.Centers.Count == 2, "duplicate diagnostic shown without extra master");
        Text(CenterNameBox, "Mantenimiento");
        Text(DescriptionBox, "Industrial setup and process");
        await Click(NewDraftButton);
        Check(vm.Current?.Status == ModelStatus.Draft, "draft created");
        async Task AddNode(string code, string physical, string unit, NodeRole role, string functions)
        {
            Text(NodeCodeBox, code);
            Select(PhysicalBox, vm.Centers.Single(x => x.Value.Code == physical));
            Select(UnitBox, vm.Units.Single(x => x.Value.Definition.Code == unit));
            Select(RoleBox, vm.Roles.Single(x => x.Value == role));
            Text(FunctionBox, functions);
            Text(PoolBox, code + " driver " + unit);
            await Click(ApplyNodeButton);
        }

        await AddNode("SETUP", "PRENSA-001", "lot", NodeRole.Principal, "production");
        await AddNode("PROCESO", "PRENSA-001", "h", NodeRole.Principal, "production");
        await AddNode("MANT", "MANT-001", "h", NodeRole.Auxiliary, "maintenance, production");
        Check(vm.IsDirty && vm.Nodes.Count == 3, "three local nodes and homogeneous pools staged");
        await Click(SaveModelButton);
        Check(!vm.IsDirty && vm.Current!.Revision == 1, "draft committed with revision");
        await Click(ValidateModelButton);
        Check(string.IsNullOrEmpty(vm.ValidationText), "structure validates through persistence");
        await Click(PublishModelButton);
        Check(vm.Current!.Status == ModelStatus.Published && !SaveModelButton.IsEnabled && !NodeEditor.IsEnabled && DescriptionBox.IsReadOnly, "published UI rejects edits");
        Check(NodeSummary.Items.Count == 3, "published nodes visibly listed");
        Check(window.RunSectionLayoutSmoke(), "main sections do not overlap");
        var original = vm.Current;
        var originalJson = original.PublishedCanonicalJson;
        Check(original.Publication!.DriverUnits.Count == 2 && original.Structure.MethodPolicy.Resolution == PolicyResolution.Pending, "publication freezes two units and pending policies");
        window.ScrollToElement(this);
        VisualEvidenceCapture.Capture(window, Path.Combine(output, "published-en-esES.png"));
        Select(MasterBox, vm.Centers.Single(x => x.Value.Code == "PRENSA-001"));
        Text(CenterNameBox, "Renamed live master");
        await Click(SaveCenterButton);
        await Click(ToggleCenterButton);
        Check(vm.Current.PublishedCanonicalJson == originalJson && vm.HistoricalDefinitions.Contains("Prensa 日本", StringComparison.Ordinal), "master rename and deactivation preserve historical names");
        await Click(CloneModelButton);
        Check(vm.Current!.Status == ModelStatus.Draft && vm.Current.Structure.ParentVersionId == original.Structure.Id, "duplicate is editable with lineage");
        Check(!vm.Current.Structure.Nodes.Any(n => original.Structure.Nodes.Any(old => old.Id == n.Id)) && vm.Current.Structure.Pools.All(p => vm.Current.Structure.Nodes.Any(n => n.Id == p.NodeId)), "clone node IDs remapped");
        // Reactivate the master; its current name remains distinct from frozen publication.
        await Click(ToggleCenterButton);
        Text(DescriptionBox, "Clone editable");
        pending.Choices.Enqueue(PendingEditChoice.Cancel);
        files.Paths.Enqueue(path);
        await Click(companyPanel.OpenButton);
        Check(vm.IsDirty && company.ActivePath == path, "cancel pending changes keeps company and edits");
        pending.Choices.Enqueue(PendingEditChoice.Save);
        await Click(companyPanel.CloseButton);
        Check(company.Snapshot is null, "save before closing commits clone");
        files.Paths.Enqueue(path);
        await Click(companyPanel.OpenButton);
        Select(ModelBox, vm.Models.Single(x => x.Value.Status == ModelStatus.Draft));
        await Click(LoadModelButton);
        Check(vm.Current!.Structure.Description == "Clone editable" && vm.Current.Revision == 1, "reopened clone contains saved edits");
        Text(DescriptionBox, "Discarded edit");
        pending.Choices.Enqueue(PendingEditChoice.Discard);
        await Click(companyPanel.CloseButton);
        files.Paths.Enqueue(path);
        await Click(companyPanel.OpenButton);
        Check(vm.Models.Single(x => x.Value.Status == ModelStatus.Draft).Value.Structure.Description == "Clone editable", "discard before closing preserves saved description");
        Select(ModelBox, vm.Models.Single(x => x.Value.Status == ModelStatus.Published));
        await Click(LoadModelButton);
        var sha = Hash(path);
        main.ApplyPresentationPreset("es", "en-US", "Europe/Madrid", false, false);
        Check(vm.Current!.StructuralHash == original.StructuralHash && Hash(path) == sha, "Spanish interface US format Madrid do not alter file or hash");
        window.ScrollToElement(this);
        VisualEvidenceCapture.Capture(window, Path.Combine(output, "published-es-enUS.png"));
        main.ApplyPresentationPreset("en", "de-DE", "UTC", true, true);
        window.ScrollToElement(NodeEditor);
        VisualEvidenceCapture.Capture(window, Path.Combine(output, "nodes-pseudo-rtl.png"));
        Check(Hash(path) == sha, "pseudo RTL and de-DE do not write models");
        main.ApplyPresentationPreset("en", "en-GB", "UTC", false, false);
        var backup = Path.Combine(output, "example.valdrio-backup");
        files.Paths.Enqueue(backup);
        await Click(companyPanel.BackupButton);
        var restored = Path.Combine(output, "restored.valdrio");
        files.Paths.Enqueue(backup);
        files.Paths.Enqueue(restored);
        await Click(companyPanel.RestoreButton);
        Check(vm.Models.Count == 2, "v2 backup restore retains published and clone");
        var catalog = new JsonCurrencyCatalog(Path.Combine(AppContext.BaseDirectory, "reference", "currency-catalog-example.json"));
        using (var independent = new SqliteCompanyFileService(catalog, new SystemClock()).Open(path, CompanyFileOpenMode.ReadOnly))
        {
            var values = independent.Models.ReadModels();
            Check(values.Count == 2 && values.Single(x => x.Status == ModelStatus.Published).PublishedCanonicalJson == originalJson, "independent SQLite read verifies UI commits and publication");
        }

        await Click(companyPanel.CloseButton);
        // Upgrade the genuine T03-produced synthetic file using the actual Upgrade button.
        var source = Path.Combine(output, "original-v1.valdrio");
        using (var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "reference", "t03-v1.json"))))
        {
            using var zip = new GZipStream(new MemoryStream(Convert.FromBase64String(doc.RootElement.GetProperty("GzipBase64").GetString()!)), CompressionMode.Decompress);
            using var file = File.Create(source);
            zip.CopyTo(file);
        }

        var before = Hash(source);
        var upgraded = Path.Combine(output, "upgraded-v2.valdrio");
        files.Paths.Enqueue(source);
        files.Paths.Enqueue(upgraded);
        await Click(companyPanel.UpgradeButton);
        Check(company.Snapshot?.SchemaVersion == 2 && Hash(source) == before, "Upgrade button preserves original v1 bytes");
        await Click(companyPanel.CloseButton);
        File.WriteAllText(Path.Combine(output, "manifest.json"), JsonSerializer.Serialize(new { Format = SqliteCompanyFileService.FormatId, SchemaVersion = 2, CompanyId = companyId.ToString(), PublishedModelId = original.Structure.Id.ToString(), original.StructuralHash, Source = "synthetic WPF interaction", Files = new[] { path, backup, restored, source, upgraded }.Select(p => new { Name = Path.GetFileName(p), Sha256 = Hash(p) }) }, new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(Path.Combine(output, "interaction.json"), JsonSerializer.Serialize(new { Status = "passed", PassedChecks = passed }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
}
