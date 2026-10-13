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
            GuidedUi.Reveal(button);
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
        Check(company.Snapshot?.SchemaVersion == SqliteCompanyFileService.SchemaVersion, "current schema created from company button");
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
        Check(vm.Units.Count == DefaultActivityUnitCatalog.All.Count + 2,
            "standard and custom unit selections loaded through port");
        Text(CenterCodeBox, "PRENSA-001");
        Text(CenterNameBox, "Prensa 日本");
        await Click(SaveCenterButton);
        Text(CenterCodeBox, "MANT-001");
        Text(CenterNameBox, "Mantenimiento");
        await Click(SaveCenterButton);
        Check(vm.Centers.Count == 2 && CenterGrid.Items.Count == 2, "physical masters saved and visibly listed");
        Check(vm.Master is null && string.IsNullOrEmpty(vm.CenterCode) && string.IsNullOrEmpty(vm.CenterName), "center form clears after save");
        Text(CenterCodeBox, "MANT-001");
        Text(CenterNameBox, "Duplicate");
        await Click(SaveCenterButton);
        Check(vm.Status.StartsWith("MODEL_DUPLICATE", StringComparison.Ordinal) && vm.Centers.Count == 2, "duplicate diagnostic shown without extra master");
        Text(CenterCodeBox, "");
        Text(CenterNameBox, "");
        Text(DescriptionBox, "Industrial setup and process");
        await Click(NewDraftButton);
        Check(vm.Current?.Status == ModelStatus.Draft, "draft created");
        Check(vm.Status == vm.Text("Model.StructureCreated"), "new structure has explicit saved feedback");
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
        Check(!vm.IsDirty && vm.Nodes.Count == 3 && NodeSummary.Items.Count == 3, "three nodes saved immediately and visibly listed");
        Check(string.IsNullOrEmpty(vm.NodeCode) && vm.SelectedNode is null, "node form clears after save");
        await Click(ValidateModelButton);
        Check(string.IsNullOrEmpty(vm.ValidationText), "structure validates through persistence");
        await Click(PublishModelButton);
        Check(vm.Current!.Status == ModelStatus.Published && !SaveModelButton.IsEnabled && !NodeEditor.IsEnabled && DescriptionBox.IsReadOnly, "published UI rejects edits");
        Check(NodeSummary.Items.Count == 3, "published nodes visibly listed");
        Check(NodeSummary.Columns[0].Header.ToString() == vm.Text("Model.ColumnCenter") &&
            vm.Nodes.Select(n => n.Node.Code).SequenceEqual(new[] { "MANT", "PROCESO", "SETUP" }), "physical centre first with deterministic centre and node ordering");
        ModelSteps.SelectedIndex = 3; UpdateLayout();
        Check(StructureReviewTree.Items.Count == 2 && vm.ReviewTree.Sum(c => c.Children.Count) == 3 &&
            !vm.Details.Contains(vm.Current!.StructuralHash!, StringComparison.Ordinal), "review tree shows centres and all nodes without technical hash");
        VisualEvidenceCapture.Capture(window, Path.Combine(output, "review-tree.png"));
        Check(window.RunSectionLayoutSmoke(), "main sections do not overlap");
        var original = vm.Current;
        var originalJson = original.PublishedCanonicalJson;
        Check(original.Publication!.DriverUnits.Count == 2 && original.Structure.MethodPolicy.Resolution == PolicyResolution.Pending, "publication freezes two units and pending policies");
        window.ScrollToElement(this);
        VisualEvidenceCapture.Capture(window, Path.Combine(output, "published-en-esES.png"));
        Select(MasterBox, vm.Centers.Single(x => x.Value.Code == "PRENSA-001"));
        Text(CenterNameBox, "Renamed live master");
        await Click(SaveCenterButton);
        Check(vm.Master is null && string.IsNullOrEmpty(vm.CenterCode), "edited center form clears after save");
        Select(MasterBox, vm.Centers.Single(x => x.Value.Code == "PRENSA-001"));
        await Click(ToggleCenterButton);
        Check(vm.Current.PublishedCanonicalJson == originalJson && vm.HistoricalDefinitions.Contains("Prensa 日本", StringComparison.Ordinal), "master rename and deactivation preserve historical names");
        ModelSteps.SelectedIndex = 2;
        UpdateLayout();
        Check(vm.IsPublished && CloneToEditNodesButton.IsVisible && CloneToEditNodesButton.IsEnabled &&
            !RoleBox.IsEnabled && !PoolBox.IsEnabled,
            "published node editor explains lock and offers a visible editable copy in the same tab");
        await Click(CloneToEditNodesButton);
        Check(vm.Current!.Status == ModelStatus.Draft && vm.Current.Structure.ParentVersionId == original.Structure.Id, "duplicate is editable with lineage");
        Check(!vm.Current.Structure.Nodes.Any(n => original.Structure.Nodes.Any(old => old.Id == n.Id)) && vm.Current.Structure.Pools.All(p => vm.Current.Structure.Nodes.Any(n => n.Id == p.NodeId)), "clone node IDs remapped");
        Check(RoleBox.IsEnabled && PoolBox.IsEnabled && !CloneToEditNodesButton.IsVisible,
            "draft clone unlocks both principal/auxiliary and pool description");
        // Reactivate the master; its current name remains distinct from frozen publication.
        Select(MasterBox, vm.Centers.Single(x => x.Value.Code == "PRENSA-001"));
        await Click(ToggleCenterButton);
        var clonedMaintenance = vm.Nodes.Single(n => n.Node.Code == "MANT");
        NodeSummary.SetCurrentValue(DataGrid.SelectedItemProperty, clonedMaintenance);
        NodeSummary.GetBindingExpression(DataGrid.SelectedItemProperty)!.UpdateSource();
        Check(vm.Role?.Value == NodeRole.Auxiliary && PoolBox.Text == "MANT driver h",
            "selected existing node loads role and pool description");
        Select(RoleBox, vm.Roles.Single(r => r.Value == NodeRole.Principal));
        Text(PoolBox, "Shared service cost pool");
        Check(vm.Role?.Value == NodeRole.Principal &&
            vm.PoolDescription == "Shared service cost pool",
            "principal/auxiliary and pool description can both be edited");
        await Click(ApplyNodeButton);
        Check(vm.Current!.Structure.Nodes.Single(n => n.Code == "MANT").Role == NodeRole.Principal &&
            vm.Current.Structure.Pools.Single(p =>
                p.NodeId == vm.Current.Structure.Nodes.Single(n => n.Code == "MANT").Id).Description == "Shared service cost pool",
            "role and pool description persist in editable model without changing published version");
        Check(original.Structure.Nodes.Single(n => n.Code == "MANT").Role == NodeRole.Auxiliary &&
            original.Structure.Pools.Single(p =>
                p.NodeId == original.Structure.Nodes.Single(n => n.Code == "MANT").Id).Description == "MANT driver h",
            "original published role and pool description remain frozen");
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
        Check(vm.Current!.Structure.Description == "Clone editable" && vm.Current.Revision >= 2 &&
            vm.Current.Structure.Nodes.Single(n => n.Code == "MANT").Role == NodeRole.Principal &&
            vm.Current.Structure.Pools.Single(p =>
                p.NodeId == vm.Current.Structure.Nodes.Single(n => n.Code == "MANT").Id).Description == "Shared service cost pool",
            "reopened clone persists both role and pool description after native UI editing");
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
        Check(vm.Models.Count == 2, "current backup restore retains published and clone");
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
        var upgraded = Path.Combine(output, "upgraded-current.valdrio");
        files.Paths.Enqueue(source);
        files.Paths.Enqueue(upgraded);
        await Click(companyPanel.UpgradeButton);
        Check(company.Snapshot?.SchemaVersion == SqliteCompanyFileService.SchemaVersion && Hash(source) == before, "Upgrade button preserves original v1 bytes");
        await Click(companyPanel.CloseButton);
        File.WriteAllText(Path.Combine(output, "manifest.json"), JsonSerializer.Serialize(new { Format = SqliteCompanyFileService.FormatId, SchemaVersion = SqliteCompanyFileService.SchemaVersion, CompanyId = companyId.ToString(), PublishedModelId = original.Structure.Id.ToString(), original.StructuralHash, Source = "synthetic WPF interaction", Files = new[] { path, backup, restored, source, upgraded }.Select(p => new { Name = Path.GetFileName(p), Sha256 = Hash(p) }) }, new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(Path.Combine(output, "interaction.json"), JsonSerializer.Serialize(new { Status = "passed", PassedChecks = passed }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
}
