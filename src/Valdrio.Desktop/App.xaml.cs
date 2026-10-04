using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Valdrio.Localization;

namespace Valdrio.Desktop;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var languages = new LanguageCatalog();
        var texts = new UiTextCatalog(new TraceLocalizationFallbackSink());
        var preferences = new PresentationPreferencesService(
            languages,
            BuildInitialPreferences(e.Args));
        var formatter = new PresentationFormatter(new DisplayTimeZoneResolver());

        var requestedPath = e.Args.FirstOrDefault(argument =>
            !argument.StartsWith("--", StringComparison.Ordinal));

        var samplePath = requestedPath
            ?? Path.Combine(AppContext.BaseDirectory, "samples", "t01-smoke.valdrio-dev.json");

        var viewModel = new MainWindowViewModel(
            texts,
            languages,
            preferences,
            formatter);

        if (!viewModel.TryLoadDevelopmentSample(samplePath, out var diagnosticCode))
        {
            var uiCulture = CultureInfo.GetCultureInfo(preferences.Current.UiLanguageTag);
            MessageBox.Show(
                texts.Format(
                    "ErrorSampleOpenBody",
                    uiCulture,
                    diagnosticCode ?? "T01_SAMPLE_OPEN_FAILED"),
                texts.Get("ErrorSampleOpenTitle", uiCulture),
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

        var captureDirectory = ReadOption(e.Args, "--capture-dir=");
        if (captureDirectory is not null)
        {
            Dispatcher.BeginInvoke(
                DispatcherPriority.ApplicationIdle,
                new Action(() =>
                {
                    CaptureVisualEvidence(window, viewModel, captureDirectory);
                    Shutdown(0);
                }));
        }
    }

    private static UserPresentationPreferences BuildInitialPreferences(string[] args)
    {
        var defaults = UserPresentationPreferences.Default;

        return defaults with
        {
            UiLanguageTag = ReadOption(args, "--ui=") ?? defaults.UiLanguageTag,
            FormatCultureTag = ReadOption(args, "--format=") ?? defaults.FormatCultureTag,
            DisplayTimeZoneId = ReadOption(args, "--zone=") ?? defaults.DisplayTimeZoneId
        };
    }

    private static string? ReadOption(IEnumerable<string> args, string prefix)
    {
        var match = args.FirstOrDefault(argument =>
            argument.StartsWith(prefix, StringComparison.Ordinal));

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
            new VisualPreset("01-en-esES-ltr.png", "en", "es-ES", "Europe/Madrid", false, false),
            new VisualPreset("02-es-esES-ltr.png", "es", "es-ES", "Europe/Madrid", false, false),
            new VisualPreset("03-es-enUS-ltr.png", "es", "en-US", "UTC", false, false),
            new VisualPreset("04-pseudo-rtl.png", "en", "es-ES", "Europe/Madrid", true, true)
        };

        foreach (var preset in presets)
        {
            viewModel.ApplyPresentationPreset(
                preset.UiLanguage,
                preset.FormatCulture,
                preset.TimeZone,
                preset.Pseudo,
                preset.ForceRtl);

            window.UpdateLayout();
            VisualEvidenceCapture.Capture(
                window,
                Path.Combine(directory, preset.FileName));
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
