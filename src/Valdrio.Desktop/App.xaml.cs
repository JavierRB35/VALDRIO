using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Valdrio.Application;
using Valdrio.Infrastructure;
using Valdrio.Localization;

namespace Valdrio.Desktop;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var captureDirectory =
            ReadOption(e.Args, "--capture-dir=");
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

        if (captureDirectory is null)
        {
            preferences.Changed +=
                (_, args) =>
                    store.Save(args.Current);
        }

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
            requestedPath
            ?? Path.Combine(
                AppContext.BaseDirectory,
                "samples",
                "t01-smoke.valdrio-dev.json");

        var viewModel =
            new MainWindowViewModel(
                texts,
                languages,
                preferences,
                formatter,
                roundingExampleService,
                currencyMetadataExampleService,
                temporalContractExampleService,
                fxConversionExampleService);

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
        window.Show();

        if (captureDirectory is not null)
        {
            Dispatcher.BeginInvoke(
                DispatcherPriority.ApplicationIdle,
                new Action(
                    () =>
                    {
                        CaptureVisualEvidence(
                            window,
                            viewModel,
                            captureDirectory);
                        Shutdown(0);
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
