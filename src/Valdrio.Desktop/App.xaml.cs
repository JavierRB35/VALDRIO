using System.IO;
using System.Windows;

namespace Valdrio.Desktop;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var requestedPath = e.Args.FirstOrDefault(argument =>
            !argument.StartsWith("--", StringComparison.Ordinal));

        var samplePath = requestedPath
            ?? Path.Combine(AppContext.BaseDirectory, "samples", "t01-smoke.valdrio-dev.json");

        var viewModel = new MainWindowViewModel();

        if (!viewModel.TryLoadDevelopmentSample(samplePath, out var error))
        {
            MessageBox.Show(
                $"The T01 development sample could not be opened.\n\n{error}",
                "VALDRIO development shell",
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
