using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Valdrio.Application;
using Valdrio.Infrastructure;
using Valdrio.Infrastructure.CompanyFiles;
using Valdrio.Localization;

namespace Valdrio.Desktop;

public partial class App : System.Windows.Application
{
    private static void SelectEditableText(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox { IsEnabled: true, IsReadOnly: false } box && box.IsKeyboardFocusWithin)
            box.SelectAll();
    }
    private static void FocusEditableTextOnFirstClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is TextBox { IsEnabled: true, IsReadOnly: false } box && !box.IsKeyboardFocusWithin)
        {
            e.Handled = true;
            box.Focus();
            box.SelectAll();
        }
    }
    private CompanyFileViewModel? company;
    private StudyWorkspaceViewModel? studies;
    private ModelWorkspaceViewModel? models;
    private ImportColumnMappingViewModel? importColumns;
    private ResourceWorkspaceViewModel? resources;
    private AllocationWorkspaceViewModel? allocations;
    protected override void OnExit(ExitEventArgs e) { allocations?.Dispose(); resources?.Dispose(); importColumns?.Dispose(); studies?.Dispose(); models?.Dispose(); company?.Dispose(); base.OnExit(e); }
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Focused editable fields select all on both keyboard and initial mouse focus.
        // Read-only/disabled fields are excluded; subsequent clicks keep caret editing.
        EventManager.RegisterClassHandler(typeof(TextBox), Keyboard.GotKeyboardFocusEvent,
            new KeyboardFocusChangedEventHandler(SelectEditableText), true);
        EventManager.RegisterClassHandler(typeof(TextBox), UIElement.PreviewMouseLeftButtonDownEvent,
            new MouseButtonEventHandler(FocusEditableTextOnFirstClick), true);

        var captureDirectory =
            ReadOption(e.Args, "--capture-dir=");
        if (captureDirectory is not null)
        {
            DispatcherUnhandledException += (_, args) =>
            {
                Directory.CreateDirectory(captureDirectory);
                File.WriteAllText(Path.Combine(captureDirectory,"company-failure.txt"),args.Exception.ToString());
                args.Handled=true; Shutdown(2);
            };
        }
        var languages = new LanguageCatalog();
        var texts =
            new UiTextCatalog(
                new TraceLocalizationFallbackSink());
        var displayTimeZones =
            new DisplayTimeZoneResolver();

        var preferencesPath =
            ReadOption(
                e.Args,
                "--preferences-path=")
            ?? Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "VALDRIO",
                "presentation-preferences.json");

        var store =
            new JsonUserPresentationPreferencesStore(
                preferencesPath);

        var validator =
            new PresentationPreferencesValidator(
                languages,
                displayTimeZones);

        var loader =
            new PresentationPreferencesLoader(
                store,
                validator,
                new TracePresentationPreferencesRecoverySink());

        var baseline = loader.LoadOrDefault(
            allowStoredPreferences:
                captureDirectory is null);

        var preferences =
            new PresentationPreferencesService(
                validator,
                BuildInitialPreferences(
                    e.Args,
                    baseline));

        var preferencesPersistence = captureDirectory is null
            ? new PresentationPreferencesPersistence(store) : null;

        var formatter =
            new PresentationFormatter(
                displayTimeZones);
        var roundingExampleService =
            new T02RoundingExampleService();
        var currencyCatalog =
            new JsonCurrencyCatalog(
                Path.Combine(
                    AppContext.BaseDirectory,
                    "reference",
                    "currency-catalog-example.json"));
        var currencyMetadataExampleService =
            new CurrencyMetadataExampleService(
                currencyCatalog);
        var temporalContractExampleService =
            new TemporalContractExampleService(
                new TimeZoneResolver());
        var fxConversionExampleService =
            new FxConversionExampleService();

        var requestedPath =
            e.Args.FirstOrDefault(
                argument =>
                    !argument.StartsWith(
                        "--",
                        StringComparison.Ordinal));

        var samplePath =
            (requestedPath?.EndsWith(".valdrio", StringComparison.OrdinalIgnoreCase) == true ? null : requestedPath)
            ?? Path.Combine(
                AppContext.BaseDirectory,
                "samples",
                "t01-smoke.valdrio-dev.json");

        var companyDialogs = captureDirectory is null
            ? (ICompanyFileDialogs)new CompanyFileDialogs()
            : new QueuedCompanyFileDialogs();
        company = new CompanyFileViewModel(new SqliteCompanyFileService(currencyCatalog, new SystemClock()),
            companyDialogs, currencyCatalog, texts, preferences, formatter);
        if (captureDirectory is null)
        {
            company.OpenFailure = message =>
            {
                var legacy = company.DiagnosticCode == "FILE_UPGRADE_REQUIRED";
                MessageBox.Show(message,
                    texts.Get(legacy ? "CompanyUpgradeNoticeTitle" : "CompanyOpenFailureTitle",
                        CultureInfo.GetCultureInfo(preferences.Current.UiLanguageTag)),
                    MessageBoxButton.OK, legacy ? MessageBoxImage.Information : MessageBoxImage.Warning);
            };
        }
        var modelDialogs = captureDirectory is null ? (IModelEditorDialogs)new ModelEditorDialogs() : new QueuedModelEditorDialogs();
        models = new ModelWorkspaceViewModel(company, texts, preferences, modelDialogs, formatter);
        var importDialogs = captureDirectory is null ? (IImportSourceDialogs)new ImportSourceDialogs() : new QueuedImportSourceDialogs();
        importColumns = new ImportColumnMappingViewModel(new ImportSourceSessionFactory(), importDialogs, texts, preferences, currencyCatalog);
        studies = new StudyWorkspaceViewModel(company, currencyCatalog, texts, preferences, formatter, modelDialogs)
            { ImportColumns = importColumns };
        resources = new ResourceWorkspaceViewModel(company, studies, currencyCatalog, texts, preferences, modelDialogs);
        allocations = new AllocationWorkspaceViewModel(company, studies, texts, preferences, modelDialogs);
        studies.AfterStudyLoadedAsync = () => studies.HasConfirmedImports
            ? allocations.EnterAsync(true) : Task.CompletedTask;
        models.AttachStudyNodes(studies);
        allocations.AfterPrimaryStateChanged = () => resources.EnterAsync(true);
        var viewModel =
            new MainWindowViewModel(
                texts,
                languages,
                preferences,
                formatter,
                roundingExampleService,
                currencyMetadataExampleService,
                temporalContractExampleService,
                fxConversionExampleService,
                preferencesPersistence) { Company = company, Models = models, Studies = studies, Resources = resources, Allocations = allocations, CostFlows = new CostFlowViewModel(company, studies, texts, preferences) };

        if (!viewModel.TryLoadDevelopmentSample(
                samplePath,
                out var diagnosticCode))
        {
            var uiCulture =
                CultureInfo.GetCultureInfo(
                    preferences.Current.UiLanguageTag);

            MessageBox.Show(
                texts.Format(
                    "ErrorSampleOpenBody",
                    uiCulture,
                    diagnosticCode
                    ?? "T01_SAMPLE_OPEN_FAILED"),
                texts.Get(
                    "ErrorSampleOpenTitle",
                    uiCulture),
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(2);
            return;
        }

        var window = new MainWindow
        {
            DataContext = viewModel
        };

        MainWindow = window;
        allocations.AfterPrimaryStateChanged = async () => { await resources.EnterAsync(true); window.UpdateWorkflow(); };
        window.Show();
        bool allowClose = false;
        window.Closing += async (_, args) =>
        {
            if (!company.IsIdle || models.IsBusy || studies.IsBusy || resources.IsBusy || allocations.IsBusy || viewModel.CostFlows?.CalculateCommand.IsRunning == true)
            {
                args.Cancel = true; company.CancelCommand.Execute(null); models.CancelCommand.Execute(null); studies.CancelCommand.Execute(null); resources.CancelCommand.Execute(null); viewModel.CostFlows?.CancelCommand.Execute(null); return;
            }
            if (!allowClose && (models.IsDirty || studies.IsDirty || resources.IsDirty || allocations.IsDirty))
            {
                args.Cancel = true; if (await company.BeforeChangeAsync!()) { allowClose = true; window.Close(); }
            }
        };

        if (captureDirectory is null && requestedPath?.EndsWith(".valdrio", StringComparison.OrdinalIgnoreCase) == true)
            Dispatcher.BeginInvoke(new Action(async () => await company.OpenPath(requestedPath)));

        if (captureDirectory is not null)
        {
            Dispatcher.BeginInvoke(
                DispatcherPriority.ApplicationIdle,
                new Action(
                    async () =>
                    {
                        try
                        {
                        if (e.Args.Contains("--adjustments-only", StringComparer.Ordinal))
                        {
                            await window.RunGuidedWorkflowChecks(captureDirectory, (QueuedCompanyFileDialogs)companyDialogs, (QueuedImportSourceDialogs)importDialogs);
                            await window.RunAdjustmentChecks(captureDirectory, (QueuedModelEditorDialogs)modelDialogs);
                            Shutdown(0); return;
                        }
                        if (e.Args.Contains("--cost-flow-only", StringComparer.Ordinal))
                        {
                            if (requestedPath is null) throw new InvalidOperationException("Cost flow smoke requires persisted fixture.");
                            await company.OpenPath(requestedPath);
                            await window.RunCostFlowChecks(captureDirectory);
                            Shutdown(0); return;
                        }
                        if (e.Args.Contains("--t13-service-flow-only", StringComparer.Ordinal)
                            || e.Args.Contains("--t13-industrial-flow-only", StringComparer.Ordinal))
                        {
                            if (requestedPath is null) throw new InvalidOperationException("T13 flow smoke requires a generated v10 company.");
                            await company.OpenPath(requestedPath);
                            if (company.Snapshot is null) throw new InvalidOperationException("Cannot open T13 company: " + company.Status);
                            if (e.Args.Contains("--t13-service-flow-only", StringComparer.Ordinal))
                                await window.RunT13CostFlowChecks(captureDirectory, "September 2026 - Service operations", 17500m, 2);
                            else
                                await window.RunT13CostFlowChecks(captureDirectory, "September 2026 - Production cost", 36000m, 3);
                            Shutdown(0); return;
                        }
                        if (e.Args.Contains("--results-scroll-only", StringComparer.Ordinal))
                        {
                            if (requestedPath is null) throw new InvalidOperationException("Results checks require a disposable demo file.");
                            await company.OpenPath(requestedPath);
                            await window.RunResultsScrollChecks(captureDirectory);
                            Shutdown(0); return;
                        }
                        if (e.Args.Contains("--allocation-only", StringComparer.Ordinal) || e.Args.Contains("--workspace-fixes-only", StringComparer.Ordinal))
                        {
                            if (requestedPath is null) throw new InvalidOperationException("Allocation capture requires a disposable project copy.");
                            await company.OpenPath(requestedPath);
                            if (company.Snapshot is null) throw new InvalidOperationException("Cannot open synthetic project: " + company.Status);
                            if (e.Args.Contains("--workspace-fixes-only", StringComparer.Ordinal))
                                await window.RunResourceChecks(captureDirectory);
                            await window.RunAllocationChecks(captureDirectory);
                            if (e.Args.Contains("--methodology", StringComparer.Ordinal)) await window.RunMethodologyChecks(captureDirectory);
                            if (e.Args.Contains("--workspace-fixes-only", StringComparer.Ordinal))
                                await window.RunWorkspaceEditingChecks(captureDirectory, (QueuedModelEditorDialogs)modelDialogs);
                            Shutdown(0); return;
                        }
                        Directory.CreateDirectory(captureDirectory);
                        VisualEvidenceCapture.Capture(window, Path.Combine(captureDirectory, "startup.png"));
                        await window.CompanyPanel.RunCompanyInteractionChecks(captureDirectory,
                            (QueuedCompanyFileDialogs)companyDialogs, viewModel);
                        await window.ModelPanel.RunModelInteractionChecks(captureDirectory,
                            (QueuedCompanyFileDialogs)companyDialogs, (QueuedModelEditorDialogs)modelDialogs, viewModel);
                        await window.StudyPanel.RunStudyInteractionChecks(captureDirectory,
                            (QueuedCompanyFileDialogs)companyDialogs, (QueuedModelEditorDialogs)modelDialogs, viewModel);
                        var importWindow = new ImportColumnMappingWindow { Owner = window, DataContext = importColumns };
                        importWindow.Show();
                        await importWindow.RunImportMappingChecks(captureDirectory, (QueuedImportSourceDialogs)importDialogs, viewModel);
                        await importWindow.RunImportConfirmationChecks(captureDirectory, (QueuedImportSourceDialogs)importDialogs,
                            (QueuedCompanyFileDialogs)companyDialogs, viewModel);
                        await importWindow.RunDebitCreditChecks(captureDirectory, (QueuedImportSourceDialogs)importDialogs,
                            (QueuedCompanyFileDialogs)companyDialogs, viewModel);
                        await importWindow.RunImportGroupingChecks(captureDirectory, (QueuedImportSourceDialogs)importDialogs, viewModel);
                        await importWindow.RunImportCompletionChecks(captureDirectory, (QueuedImportSourceDialogs)importDialogs, viewModel);
                        await importWindow.RunInternationalImportChecks(captureDirectory, (QueuedImportSourceDialogs)importDialogs, viewModel);
                        importWindow.Close();
                        await window.RunGuidedWorkflowChecks(captureDirectory, (QueuedCompanyFileDialogs)companyDialogs, (QueuedImportSourceDialogs)importDialogs);
                        await window.RunResourceChecks(captureDirectory);
                        await window.RunAllocationChecks(captureDirectory);
                            if (e.Args.Contains("--methodology", StringComparer.Ordinal)) await window.RunMethodologyChecks(captureDirectory);
                        await window.RunVisualShellChecks(captureDirectory);
                        if (e.Args.Contains("--adjustments", StringComparer.Ordinal))
                        {
                            await window.RunGuidedWorkflowChecks(Path.Combine(captureDirectory, "adjustment-fixture"), (QueuedCompanyFileDialogs)companyDialogs, (QueuedImportSourceDialogs)importDialogs);
                            await window.RunAdjustmentChecks(captureDirectory, (QueuedModelEditorDialogs)modelDialogs);
                        }
                        CaptureVisualEvidence(
                            window,
                            viewModel,
                            captureDirectory);
                        Shutdown(0);
                        }
                        catch (Exception ex)
                        {
                            Directory.CreateDirectory(captureDirectory);
                            File.WriteAllText(Path.Combine(captureDirectory, "company-failure.txt"), ex.ToString());
                            Shutdown(2);
                        }
                    }));
        }
    }

    private static UserPresentationPreferences BuildInitialPreferences(
        string[] args,
        UserPresentationPreferences baseline) =>
        baseline with
        {
            UiLanguageTag =
                ReadOption(args, "--ui=")
                ?? baseline.UiLanguageTag,
            FormatCultureTag =
                ReadOption(args, "--format=")
                ?? baseline.FormatCultureTag,
            DisplayTimeZoneId =
                ReadOption(args, "--zone=")
                ?? baseline.DisplayTimeZoneId
        };

    private static string? ReadOption(
        IEnumerable<string> args,
        string prefix)
    {
        var match =
            args.FirstOrDefault(
                argument =>
                    argument.StartsWith(
                        prefix,
                        StringComparison.Ordinal));

        return match is null
            ? null
            : match[prefix.Length..];
    }

    private static void CaptureVisualEvidence(
        MainWindow window,
        MainWindowViewModel viewModel,
        string directory)
    {
        if (!window.RunKeyboardNavigationSmoke())
        {
            throw new InvalidOperationException(
                "T01A keyboard navigation smoke failed.");
        }

        window.RunFxEditorInteractionChecks(directory);

        var presets = new[]
        {
            new VisualPreset(
                "01-en-esES-ltr.png",
                "en",
                "es-ES",
                "Europe/Madrid",
                false,
                false),
            new VisualPreset(
                "02-es-esES-ltr.png",
                "es",
                "es-ES",
                "Europe/Madrid",
                false,
                false),
            new VisualPreset(
                "03-es-enUS-ltr.png",
                "es",
                "en-US",
                "UTC",
                false,
                false),
            new VisualPreset(
                "04-pseudo-rtl.png",
                "en",
                "es-ES",
                "Europe/Madrid",
                true,
                true)
        };

        foreach (var preset in presets)
        {
            viewModel.ApplyPresentationPreset(
                preset.UiLanguage,
                preset.FormatCulture,
                preset.TimeZone,
                preset.Pseudo,
                preset.ForceRtl);

            window.ResetScrollForCapture();

            VisualEvidenceCapture.Capture(
                window,
                Path.Combine(
                    directory,
                    preset.FileName));
        }
    }

    private sealed record VisualPreset(
        string FileName,
        string UiLanguage,
        string FormatCulture,
        string TimeZone,
        bool Pseudo,
        bool ForceRtl);
}
