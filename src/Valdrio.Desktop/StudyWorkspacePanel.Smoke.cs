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
            GuidedUi.Reveal(button);
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
        var modelSource = Path.Combine(directory, "models", "example.valdrio");
        var modelSourceHash = Hash(modelSource);
        var path = Path.Combine(output, "example.valdrio");
        // Restore through the production UI to an isolated study file. The model
        // stage's artifact and manifest must remain valid after all later smokes.
        files.Paths.Enqueue(modelSource); files.Paths.Enqueue(path); await Click(companyPanel.RestoreButton);
        // COR-06 removed the manual Refresh button: opening the study
        // selector must refresh the list without loading a different study.
        Check(StudyBox.IsEnabled, "saved-study selector is enabled for on-demand refresh");
        StudyBox.SetCurrentValue(ComboBox.IsDropDownOpenProperty, true);
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        if (vm.RefreshCommand.ExecutionTask is { } refresh)
            await refresh.WaitAsync(TimeSpan.FromSeconds(45));
        StudyBox.SetCurrentValue(ComboBox.IsDropDownOpenProperty, false);
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        Check(vm.PublishedModels.Count == 1 && vm.Studies.Count == 0, "only published models offered");
        Check(company.Snapshot?.DefaultCurrency.ToString() == "USD", "company USD preserved");
        main.ApplyPresentationPreset("en", "es-ES", "Europe/Madrid", false, false);
        Text(StudyNameBox, "Octubre 工場");
        Select(StudyModelBox, vm.PublishedModels.Single()); Select(StudyCurrencyBox, "EUR");
        Select(ScenarioBox, vm.Scenarios.Single(s => s.Value == StudyScenario.Actual));
        Date(StartPicker, new(2026, 10, 1)); Date(EndPicker, new(2026, 9, 30));
        await Click(CreateStudyButton);
        Check(vm.DiagnosticCode == "STUDY_PERIOD_INVALID" && vm.Current is null && vm.IsDirty, "inverted period rejected without losing edits");
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
        main.ApplyPresentationPreset("es", "en-US", "America/Los_Angeles", false, false);
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle); UpdateLayout();
        Check(vm.IsDirty && vm.Name == "Octubre controller" && vm.PeriodStart?.Date == new DateTime(2026, 10, 1)
            && StartPicker.Text == new DateTime(2026, 10, 1).ToString(StartPicker.SelectedDateFormat == DatePickerFormat.Short ? "d" : "D",
                System.Globalization.CultureInfo.GetCultureInfo("en-US")), "region switch preserves pending edits and formats typed date");
        main.ApplyPresentationPreset("en", "es-ES", "Europe/Madrid", false, false);
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
            var culture = System.Globalization.CultureInfo.GetCultureInfo(preset.Item2);
            Check(StartPicker.Text == new DateTime(2026, 10, 2).ToString(StartPicker.SelectedDateFormat == DatePickerFormat.Short ? "d" : "D", culture)
                && EndPicker.Text == new DateTime(2026, 10, 31).ToString(EndPicker.SelectedDateFormat == DatePickerFormat.Short ? "d" : "D", culture)
                && !vm.IsDirty, "visible calendar text follows region without input mutation: " + preset.Item2);
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
        var previousModelId = vm.Current!.SelectedModelVersionId;
        var previousCurrency = vm.Current.Currency.FunctionalCurrencyCode.ToString();
        var previousScenario = vm.Current.ScenarioKind;
        pending.Choices.Enqueue(PendingEditChoice.Discard); await Click(NewStudyButton);
        Check(vm.Current is null && !vm.IsDirty, "discard opens clean new context");
        Check(vm.ModelChoice?.Value.Structure.Id == previousModelId && vm.Currency == previousCurrency &&
            vm.Scenario?.Value == previousScenario && vm.PeriodStart is null && vm.PeriodEnd is null && vm.Name == "",
            "new period reuses structure currency and scenario without copying the previous dates or name");
        var beforeInvalidCreate = Hash(path);
        await Click(CreateStudyButton);
        Check(vm.DiagnosticCode == "STUDY_NAME_REQUIRED" && vm.HasStatusError && vm.Current is null,
            "empty new period displays a validation message and keeps the form open");
        Check(Hash(path) == beforeInvalidCreate, "empty new period does not write to the company file");
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
            c.Open(); using var command = c.CreateCommand(); command.CommandText = "SELECT count(*) FROM CalculationRuns";
            Check(Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) == 0, "no fake execution or cost tables");
        }
        await Click(companyPanel.CloseButton);
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "reference", "t04-v2.json")));
        var source = Path.Combine(output, "original-v2.valdrio");
        using (var gzip = new GZipStream(new MemoryStream(Convert.FromBase64String(fixture.RootElement.GetProperty("GzipBase64").GetString()!)), CompressionMode.Decompress))
        using (var target = File.Create(source)) gzip.CopyTo(target);
        var before = Hash(source); Check(before == fixture.RootElement.GetProperty("Sha256").GetString(), "real T04 fixture hash verified");
        var upgraded = Path.Combine(output, "upgraded-v4.valdrio"); files.Paths.Enqueue(source); files.Paths.Enqueue(upgraded); await Click(companyPanel.UpgradeButton);
        Check(company.Snapshot?.SchemaVersion == SqliteCompanyFileService.SchemaVersion && Hash(source) == before && vm.PublishedModels.Count == 1, "v2 upgrade button preserves original publication and bytes");
        Check(Hash(modelSource) == modelSourceHash, "study workflow preserves the model-stage artifact and manifest");
        await RunPostCommitRecoveryRegression(output, modelSource);
        File.WriteAllText(Path.Combine(output, "interaction.json"), JsonSerializer.Serialize(new { Status = "passed", PassedChecks = passed }, new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(Path.Combine(output, "manifest.json"), JsonSerializer.Serialize(new { Format = SqliteCompanyFileService.FormatId, SchemaVersion = SqliteCompanyFileService.SchemaVersion,
            CompanyId = companyId.ToString(), StudyId = studyId.ToString(), latest.EconomicInputHash, latest.FullSnapshotHash,
            Scope = "Context-only T05; no calculation", Files = new[] { path, backup, restored, source, upgraded, snapshotPath }.Select(p => new { Name = Path.GetFileName(p), Sha256 = Hash(p) }) }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static async Task RunPostCommitRecoveryRegression(string output, string modelSource)
    {
        var recoveryDirectory = Path.Combine(output, "recovery");
        Directory.CreateDirectory(recoveryDirectory);
        var recoveryPath = Path.Combine(recoveryDirectory, "post-commit.valdrio");
        var catalog = new JsonCurrencyCatalog(Path.Combine(AppContext.BaseDirectory, "reference", "currency-catalog-example.json"));
        var baseService = new SqliteCompanyFileService(catalog, new SystemClock());
        using (baseService.Restore(modelSource, recoveryPath))
        {
        }

        var faultState = new PostCommitReadFailureState();
        var service = new PostCommitReadFailureCompanyFileService(baseService, faultState);
        var preferences = new Valdrio.Localization.PresentationPreferencesService(
            new Valdrio.Localization.LanguageCatalog(),
            Valdrio.Localization.UserPresentationPreferences.Default);
        var texts = new Valdrio.Localization.UiTextCatalog();
        var formatter = new Valdrio.Localization.PresentationFormatter(
            new Valdrio.Localization.DisplayTimeZoneResolver());
        using var company = new CompanyFileViewModel(
            service,
            new QueuedCompanyFileDialogs(),
            catalog,
            texts,
            preferences,
            formatter);
        using var studies = new StudyWorkspaceViewModel(
            company,
            catalog,
            texts,
            preferences,
            formatter,
            new QueuedModelEditorDialogs());

        var passed = new List<string>();
        void CheckRecovery(bool value, string label)
        {
            if (!value) throw new InvalidOperationException("Study recovery regression: " + label);
            passed.Add(label);
        }

        await company.OpenPath(recoveryPath);
        studies.Name = "Recovery study";
        studies.PeriodStart = new DateTime(2026, 10, 1);
        studies.PeriodEnd = new DateTime(2026, 10, 31);
        studies.ModelChoice = studies.PublishedModels.Single();
        studies.Currency = "EUR";
        studies.Scenario = studies.Scenarios.Single(item => item.Value == StudyScenario.Actual);

        await studies.CreateCommand.ExecuteAsync(null);
        var current = studies.Current ?? throw new InvalidOperationException("Recovery study was not created.");
        var studyId = current.Id;
        var innerStudies = service.ActiveInnerStudies ??
            throw new InvalidOperationException("Recovery inner study workspace was not available.");
        CheckRecovery(
            current.Revision == 0 &&
            innerStudies.ReadStudies().Single(item => item.Id == studyId).Revision == 0 &&
            !studies.IsDirty,
            "confirmed create survives refresh failure");

        studies.Name = "Recovery saved once";
        await studies.SaveCommand.ExecuteAsync(null);
        current = studies.Current!;
        CheckRecovery(
            current.Revision == 1 &&
            innerStudies.ReadStudies().Single(item => item.Id == studyId).Revision == 1,
            "confirmed save survives refresh failure");

        studies.Name = "Recovery saved twice";
        await studies.SaveCommand.ExecuteAsync(null);
        current = studies.Current!;
        CheckRecovery(
            current.Revision == 2 &&
            innerStudies.ReadStudies().Single(item => item.Id == studyId).Revision == 2,
            "next save uses confirmed revision");

        await studies.ToggleStateCommand.ExecuteAsync(null);
        current = studies.Current!;
        CheckRecovery(
            current.Revision == 3 &&
            current.State == StudyState.Closed &&
            innerStudies.ReadStudies().Single(item => item.Id == studyId).State == StudyState.Closed,
            "confirmed close survives refresh failure");

        await studies.ToggleStateCommand.ExecuteAsync(null);
        current = studies.Current!;
        CheckRecovery(
            current.Revision == 4 &&
            current.State == StudyState.Open &&
            innerStudies.ReadStudies().Single(item => item.Id == studyId).State == StudyState.Open,
            "confirmed reopen survives refresh failure");

        await studies.CaptureCommand.ExecuteAsync(null);
        var capturedId = studies.SnapshotChoice?.Value.Id ??
            throw new InvalidOperationException("Confirmed snapshot was not selected.");
        CheckRecovery(
            innerStudies.ReadSnapshots(current.CompanyId, studyId).Any(item => item.Id == capturedId),
            "confirmed snapshot survives refresh failure");

        await studies.RefreshCommand.ExecuteAsync(null);
        CheckRecovery(
            studies.SnapshotChoice?.Value.Id == capturedId,
            "manual refresh retains confirmed snapshot selection");

        var persisted = innerStudies.ReadStudies().Single(item => item.Id == studyId);
        var externallyUpdated = innerStudies.UpdateContext(
            RecoveryUpdate(persisted, "External revision"));
        await studies.RefreshCommand.ExecuteAsync(null);
        CheckRecovery(
            studies.Current?.Revision == externallyUpdated.Revision &&
            studies.Current.Name == "External revision" &&
            !studies.IsDirty,
            "clean refresh adopts newer persisted revision");

        studies.Name = "Pending editor text";
        var editorRevision = studies.Current!.Revision;
        var newer = innerStudies.UpdateContext(
            RecoveryUpdate(externallyUpdated, "Newer persisted revision"));
        await studies.RefreshCommand.ExecuteAsync(null);
        CheckRecovery(
            studies.IsDirty &&
            studies.Name == "Pending editor text" &&
            studies.Current.Revision == editorRevision &&
            studies.Studies.Single(item => item.Value.Id == studyId).Value.Revision == newer.Revision,
            "dirty refresh preserves editor while list advances");

        studies.DiscardCommand.Execute(null);
        await studies.RefreshCommand.ExecuteAsync(null);
        CheckRecovery(
            !studies.IsDirty &&
            studies.Current?.Revision == newer.Revision &&
            studies.Current.Name == "Newer persisted revision",
            "discard then refresh reconciles current revision");

        File.WriteAllText(
            Path.Combine(output, "recovery.json"),
            JsonSerializer.Serialize(
                new { Status = "passed", PassedChecks = passed },
                new JsonSerializerOptions { WriteIndented = true }));
    }

    private static UpdateStudyContext RecoveryUpdate(StudyDefinition study, string name) =>
        new(
            study.CompanyId,
            study.Id,
            study.Revision,
            name,
            study.PeriodStart,
            study.PeriodEnd,
            study.Purpose,
            study.ScenarioKind,
            study.SelectedModelVersionId,
            study.Currency.FunctionalCurrencyCode,
            "recovery-regression");

    private sealed class PostCommitReadFailureState
    {
        private int failNextModelRead;

        public void Arm() => Interlocked.Exchange(ref failNextModelRead, 1);

        public bool Consume() => Interlocked.Exchange(ref failNextModelRead, 0) == 1;
    }

    private sealed class PostCommitReadFailureCompanyFileService(
        ICompanyFileService inner,
        PostCommitReadFailureState state)
        : ICompanyFileService
    {
        public IStudyWorkspace? ActiveInnerStudies { get; private set; }

        private ICompanyFileSession Wrap(ICompanyFileSession session)
        {
            ActiveInnerStudies = session.Studies;
            return new PostCommitReadFailureSession(session, state);
        }

        public ICompanyFileSession Upgrade(
            string source,
            string destination,
            string actor,
            CancellationToken cancellationToken = default) =>
            Wrap(inner.Upgrade(source, destination, actor, cancellationToken));

        public ICompanyFileSession Create(
            CreateCompanyFile command,
            CancellationToken cancellationToken = default) =>
            Wrap(inner.Create(command, cancellationToken));

        public ICompanyFileSession Open(
            string path,
            CompanyFileOpenMode mode = CompanyFileOpenMode.Edit,
            CancellationToken cancellationToken = default) =>
            Wrap(inner.Open(path, mode, cancellationToken));

        public ICompanyFileSession Restore(
            string source,
            string destination,
            CancellationToken cancellationToken = default) =>
            Wrap(inner.Restore(source, destination, cancellationToken));
    }

    private sealed class PostCommitReadFailureSession(
        ICompanyFileSession inner,
        PostCommitReadFailureState state)
        : ICompanyFileSession
    {
        public IResourceWorkspace Resources => inner.Resources;
        public IImportWorkspace Imports => inner.Imports;
        public IStudyWorkspace Studies { get; } =
            new PostCommitReadFailureStudyWorkspace(inner.Studies, state);
        public IModelWorkspace Models { get; } =
            new PostCommitReadFailureModelWorkspace(inner.Models, state);
        public string Path => inner.Path;
        public CompanyFileOpenMode Mode => inner.Mode;

        public CompanyFileSnapshot Read(CancellationToken cancellationToken = default) =>
            inner.Read(cancellationToken);

        public CompanyFileSnapshot AddUnits(
            IEnumerable<UnitDefinition> units,
            long expectedRevision,
            string actor,
            CancellationToken cancellationToken = default) =>
            inner.AddUnits(units, expectedRevision, actor, cancellationToken);

        public void Backup(string destination, CancellationToken cancellationToken = default) =>
            inner.Backup(destination, cancellationToken);

        public void Dispose() => inner.Dispose();
    }

    private sealed class PostCommitReadFailureModelWorkspace(
        IModelWorkspace inner,
        PostCommitReadFailureState state)
        : IModelWorkspace
    {
        public IReadOnlyList<PhysicalCenter> ReadCenters(CancellationToken token = default) =>
            inner.ReadCenters(token);

        public IReadOnlyList<DriverUnitState> ReadDriverUnits(CancellationToken token = default) =>
            inner.ReadDriverUnits(token);

        public IReadOnlyList<ModelVersionSnapshot> ReadModels(CancellationToken token = default)
        {
            if (state.Consume())
                throw new IOException("Injected post-commit model read failure.");
            return inner.ReadModels(token);
        }

        public PhysicalCenter CreateCenter(CreatePhysicalCenter command, CancellationToken token = default) =>
            inner.CreateCenter(command, token);

        public PhysicalCenter ChangeCenter(ChangePhysicalCenter command, CancellationToken token = default) =>
            inner.ChangeCenter(command, token);

        public DriverUnitState ChangeUnitState(ChangeDriverUnitState command, CancellationToken token = default) =>
            inner.ChangeUnitState(command, token);

        public ModelVersionSnapshot CreateDraft(CreateModelDraft command, CancellationToken token = default) =>
            inner.CreateDraft(command, token);

        public ModelVersionSnapshot SaveDraft(SaveModelDraft command, CancellationToken token = default) =>
            inner.SaveDraft(command, token);

        public IReadOnlyList<DomainDiagnostic> ValidateModel(
            EntityId companyId,
            EntityId modelId,
            CancellationToken token = default) =>
            inner.ValidateModel(companyId, modelId, token);

        public ModelVersionSnapshot Publish(PublishModel command, CancellationToken token = default) =>
            inner.Publish(command, token);

        public ModelVersionSnapshot Clone(CloneModel command, CancellationToken token = default) =>
            inner.Clone(command, token);
    }

    private sealed class PostCommitReadFailureStudyWorkspace(
        IStudyWorkspace inner,
        PostCommitReadFailureState state)
        : IStudyWorkspace
    {
        public IReadOnlyList<StudyDefinition> ReadStudies(CancellationToken token = default) =>
            inner.ReadStudies(token);

        public StudyDefinition Create(CreateStudy command, CancellationToken token = default)
        {
            var result = inner.Create(command, token);
            state.Arm();
            return result;
        }

        public StudyDefinition UpdateContext(UpdateStudyContext command, CancellationToken token = default)
        {
            var result = inner.UpdateContext(command, token);
            state.Arm();
            return result;
        }

        public StudyDefinition ChangeState(ChangeStudyState command, CancellationToken token = default)
        {
            var result = inner.ChangeState(command, token);
            state.Arm();
            return result;
        }

        public StudySnapshotReceipt Capture(CaptureStudySnapshot command, CancellationToken token = default)
        {
            var result = inner.Capture(command, token);
            state.Arm();
            return result;
        }

        public IReadOnlyList<StudySnapshotReceipt> ReadSnapshots(
            EntityId companyId,
            EntityId studyId,
            CancellationToken token = default) =>
            inner.ReadSnapshots(companyId, studyId, token);
    }

}
