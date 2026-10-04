using System.Globalization;
using System.IO;
using System.Windows;
using Valdrio.Localization;

namespace Valdrio.Desktop;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var culture = CultureInfo.GetCultureInfo("en");
        var texts = new UiTextCatalog();

        var requestedPath = e.Args.FirstOrDefault(argument =>
            !argument.StartsWith("--", StringComparison.Ordinal));

        var samplePath = requestedPath
            ?? Path.Combine(AppContext.BaseDirectory, "samples", "t01-smoke.valdrio-dev.json");

        var viewModel = new MainWindowViewModel(texts, culture);

        if (!viewModel.TryLoadDevelopmentSample(samplePath, out var diagnosticCode))
        {
            MessageBox.Show(
                texts.Format("ErrorSampleOpenBody", culture, diagnosticCode ?? "T01_SAMPLE_OPEN_FAILED"),
                texts.Get("ErrorSampleOpenTitle", culture),
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
    }
}
