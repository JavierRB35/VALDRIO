using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Data.Sqlite;
using Valdrio.Application;
using Valdrio.Domain;
using Valdrio.Infrastructure;
using Valdrio.Infrastructure.CompanyFiles;

namespace Valdrio.Desktop;

public partial class StudyWorkspacePanel
{
    internal async Task RunStudyInteractionChecks(string directory, QueuedCompanyFileDialogs files,
        QueuedModelEditorDialogs pending, MainWindowViewModel main)
    {
        var vm = (StudyWorkspaceViewModel)DataContext; var company = main.Company!;
        var output = Path.Combine(directory, "studies"); Directory.CreateDirectory(output);
        var window = (MainWindow)Window.GetWindow(this); var companyPanel = window.CompanyPanel;
        var passed = new List<string>();
        void Check(bool value, string label) { if (!value) throw new InvalidOperationException("Study WPF: " + label); passed.Add(label); }
        void Text(TextBox box, string value) { box.SetCurrentValue(TextBox.TextProperty, value); box.GetBindingExpression(TextBox.TextProperty)!.UpdateSource(); }
        void Select<T>(ComboBox box, T value) { box.SetCurrentValue(ComboBox.SelectedItemProperty, value); box.GetBindingExpression(ComboBox.SelectedItemProperty)!.UpdateSource(); }
        void Date(DatePicker picker, DateTime value) { picker.SetCurrentValue(DatePicker.SelectedDateProperty, value); picker.GetBindingExpression(DatePicker.SelectedDateProperty)!.UpdateSource(); }
        async Task Click(Button button)
        {
            Check(button.IsEnabled, "enabled: " + button.Name);
            ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            if (button.Command is IAsyncRelayCommand command && command.ExecutionTask is { } task) await task.WaitAsync(TimeSpan.FromSeconds(45));
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle); UpdateLayout();
        }
        static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
        static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i); if (child is T match) yield return match;
                foreach (var nested in Descendants<T>(child)) yield return nested;
            }
        }
        var path = Path.Combine(directory, "models", "example.valdrio");
        files.Paths.Enqueue(path); await Click(companyPanel.OpenButton);
        await Click(RefreshStudyButton);
        Check(vm.PublishedModels.Count == 1 && vm.Studies.Count == 0, "only published models offered");
        Check(company.Snapshot?.DefaultCurrency.ToString() == "USD", "company USD preserved");
        main.ApplyPresentationPreset("en", "es-ES", "Europe/Madrid", false, false);
        Text(StudyNameBox, "Octubre 工場");
        Select(StudyModelBox, vm.PublishedModels.Single()); Select(StudyCurrencyBox, "EUR");
        Select(ScenarioBox, vm.Scenarios.Single(s => s.Value == StudyScenario.Actual));
        Date(StartPicker, new(2026, 10, 1)); Date(EndPicker, new(2026, 9, 30));
        await Click(CreateStudyButton);
        Check(vm.Status.StartsWith("STUDY_PERIOD_INVALID", StringComparison.Ordinal) && vm.Current is null && vm.IsDirty, "inverted period rejected without losing edits");
        Date(EndPicker, new(2026, 10, 31)); await Click(CreateStudyButton);
        Check(vm.Current?.Revision == 0 && vm.Current.Currency.FunctionalCurrencyCode.ToString() == "EUR", "EUR context created by button");
        Check(vm.Current?.PeriodStart.ToString() == "2026-10-01" && vm.Current.PeriodEnd.ToString() == "2026-10-31", "period persisted as business dates");
        Check(!vm.IsDirty && !SaveStudyButton.IsEnabled && CaptureStudyButton.IsEnabled, "saved study ready for context capture");
        Check(Descendants<DatePickerTextBox>(StartPicker).Single().IsReadOnly && Descendants<DatePickerTextBox>(EndPicker).Single().IsReadOnly, "calendar-only dates prevent ambiguous pending text");
        var studyId = vm.Current!.Id; var companyId = vm.Current.CompanyId;
        await Click(CaptureStudyButton); var first = vm.SnapshotChoice!.Value;
        Check(vm.Snapshots.Count == 1 && vm.Current.Revision == 0 && vm.Current.CurrentRunId is null, "capture is a photo, not a revision or execution");
        Check(!StudyPreparationService.Evaluate(first.Input).ReadyForCalculation && StudyPreparationService.Evaluate(first.Input).Diagnostics.Count == 4, "pending policies and missing inputs explained");
        Text(StudyNameBox, "Octubre controller"); Check(!CaptureStudyButton.IsEnabled, "dirty input cannot be captured");
        await Click(SaveStudyButton); await Click(CaptureStudyButton); var named = vm.SnapshotChoice!.Value;
        Check(vm.Current!.Revision == 1 && named.EconomicInputHash == first.EconomicInputHash && named.FullSnapshotHash != first.FullSnapshotHash, "administrative rename changes full photo only");
        Date(StartPicker, new(2026, 10, 2)); await Click(SaveStudyButton); await Click(CaptureStudyButton); var period = vm.SnapshotChoice!.Value;
        Check(vm.Current!.Revision == 2 && period.EconomicInputHash != first.EconomicInputHash, "period change has economic identity");
        Select(SnapshotBox, vm.Snapshots.Single(s => s.Value.Id == first.Id));
        Check(!vm.SnapshotChoice!.Value.IsCurrentFor(vm.Current) && vm.SnapshotChoice.Value.CanonicalJson == first.CanonicalJson, "historical snapshot selected unchanged");
        Select(SnapshotBox, vm.Snapshots.Single(s => s.Value.Id == period.Id));
        var sha = Hash(path); var canonical = vm.SnapshotChoice!.Value.CanonicalJson;
        foreach (var preset in new[] { ("en", "es-ES", false, false, "en-esES.png"), ("es", "en-US", false, false, "es-enUS.png"),
            ("en", "de-DE", false, false, "en-deDE.png"), ("es", "en-GB", true, true, "pseudo-rtl.png") })
        {
            main.ApplyPresentationPreset(preset.Item1, preset.Item2, "America/Los_Angeles", preset.Item3, preset.Item4);
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle); UpdateLayout();
            Check(Hash(path) == sha && vm.SnapshotChoice!.Value.CanonicalJson == canonical, "preferences preserve disk and snapshot: " + preset.Item2);
            Check(vm.PeriodStart?.Date == new DateTime(2026, 10, 2) && vm.PeriodEnd?.Date == new DateTime(2026, 10, 31), "calendar identity retained: " + preset.Item2);
            window.ScrollToElement(this);
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            window.UpdateLayout();
            Check(TranslatePoint(new Point(0, 0), window.RootScrollViewer).Y >= -0.1
                && TranslatePoint(new Point(0, 0), window.RootScrollViewer).Y < window.RootScrollViewer.ViewportHeight,
                "study context is in visible viewport: " + preset.Item2);
            VisualEvidenceCapture.Capture(window, Path.Combine(output, preset.Item5));
            window.ScrollToElement(SnapshotHeader);
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            window.UpdateLayout();
            Check(SnapshotHeader.TranslatePoint(new Point(0, 0), window.RootScrollViewer).Y >= -0.1
                && SnapshotHeader.TranslatePoint(new Point(0, 0), window.RootScrollViewer).Y < window.RootScrollViewer.ViewportHeight,
                "snapshot history is in visible viewport: " + preset.Item2);
            VisualEvidenceCapture.Capture(window, Path.Combine(output, Path.GetFileNameWithoutExtension(preset.Item5) + "-history.png"));
        }
        main.ApplyPresentationPreset("en", "es-ES", "Europe/Madrid", false, false);
        Text(StudyNameBox, "unconfirmed name"); pending.Choices.Enqueue(PendingEditChoice.Cancel); await Click(companyPanel.CloseButton);
        Check(company.ActivePath == path && vm.IsDirty && vm.Name == "unconfirmed name", "cancel protects pending study and active file");
        pending.Choices.Enqueue(PendingEditChoice.Discard); await Click(NewStudyButton);
        Check(vm.Current is null && !vm.IsDirty, "discard opens clean new context");
        Select(StudyBox, vm.Studies.Single(s => s.Value.Id == studyId)); await Click(LoadStudyButton);
        Check(vm.Current?.Revision == 2 && vm.Snapshots.Count == 3, "load restores context and snapshot history");
        Text(StudyNameBox, "saved before closing"); pending.Choices.Enqueue(PendingEditChoice.Save); await Click(companyPanel.CloseButton);
        Check(company.Snapshot is null && vm.Current is null && !vm.IsDirty, "save-before-close commits and clears UI");
        files.Paths.Enqueue(path); await Click(companyPanel.OpenButton); Select(StudyBox, vm.Studies.Single()); await Click(LoadStudyButton);
        Check(vm.Current?.Revision == 3 && vm.Current.Name == "saved before closing" && vm.Snapshots.Count == 3, "reopen preserves administrative change and photos");
        await Click(CaptureStudyButton); await Click(ToggleStudyButton);
        Check(vm.Current?.State == StudyState.Closed && !vm.CanEdit && !SaveStudyButton.IsEnabled, "closed context is read-only");
        await Click(ToggleStudyButton); await Click(CaptureStudyButton);
        Check(vm.Current?.Revision == 5 && vm.Current.State == StudyState.Open && vm.Snapshots.Count == 5, "explicit reopen keeps history");
        var latest = vm.SnapshotChoice!.Value; latest.EnsureValid();
        var snapshotPath = Path.Combine(output, "snapshot.json"); File.WriteAllText(snapshotPath, CanonicalSnapshotJson.Serialize(latest));
        Check(window.RunSectionLayoutSmoke(), "root sections do not overlap");
        var backup = Path.Combine(output, "study.valdrio-backup"); files.Paths.Enqueue(backup); await Click(companyPanel.BackupButton);
        var restored = Path.Combine(output, "restored.valdrio"); files.Paths.Enqueue(backup); files.Paths.Enqueue(restored); await Click(companyPanel.RestoreButton);
        Select(StudyBox, vm.Studies.Single()); await Click(LoadStudyButton);
        Check(vm.Current?.Id == studyId && vm.Current.Revision == 5 && vm.Snapshots.Count == 5, "restore to new path retains identity and history");
        Check(vm.Snapshots.Single(s => s.Value.Id == latest.Id).Value.CanonicalJson == latest.CanonicalJson, "restored photo retains both hashes and definitions");
        var catalog = new JsonCurrencyCatalog(Path.Combine(AppContext.BaseDirectory, "reference", "currency-catalog-example.json"));
        using (var read = new SqliteCompanyFileService(catalog, new SystemClock()).Open(restored, CompanyFileOpenMode.ReadOnly))
        {
            Check(read.Studies.ReadStudies().Single().Id == studyId && read.Studies.ReadSnapshots(companyId, studyId).Count == 5, "independent production reader validates SQLite study history");
        }
        using (var c = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = restored, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString()))
        {
            c.Open(); using var command = c.CreateCommand(); command.CommandText = "SELECT count(*) FROM sqlite_master WHERE name IN('CalculationRuns','TerminalCostFacts')";
            Check(Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) == 0, "no fake execution or cost tables");
        }
        await Click(companyPanel.CloseButton);
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "reference", "t04-v2.json")));
        var source = Path.Combine(output, "original-v2.valdrio");
        using (var gzip = new GZipStream(new MemoryStream(Convert.FromBase64String(fixture.RootElement.GetProperty("GzipBase64").GetString()!)), CompressionMode.Decompress))
        using (var target = File.Create(source)) gzip.CopyTo(target);
        var before = Hash(source); Check(before == fixture.RootElement.GetProperty("Sha256").GetString(), "real T04 fixture hash verified");
        var upgraded = Path.Combine(output, "upgraded-v3.valdrio"); files.Paths.Enqueue(source); files.Paths.Enqueue(upgraded); await Click(companyPanel.UpgradeButton);
        Check(company.Snapshot?.SchemaVersion == 3 && Hash(source) == before && vm.PublishedModels.Count == 1, "v2 upgrade button preserves original publication and bytes");
        File.WriteAllText(Path.Combine(output, "interaction.json"), JsonSerializer.Serialize(new { Status = "passed", PassedChecks = passed }, new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(Path.Combine(output, "manifest.json"), JsonSerializer.Serialize(new { Format = SqliteCompanyFileService.FormatId, SchemaVersion = 3,
            CompanyId = companyId.ToString(), StudyId = studyId.ToString(), latest.EconomicInputHash, latest.FullSnapshotHash,
            Scope = "Context-only T05; no calculation", Files = new[] { path, backup, restored, source, upgraded, snapshotPath }.Select(p => new { Name = Path.GetFileName(p), Sha256 = Hash(p) }) }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
