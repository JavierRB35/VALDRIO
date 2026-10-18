using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Valdrio.Desktop;

public partial class MainWindow
{
    internal async Task RunCapacityChecks(string directory, string path)
    {
        Directory.CreateDirectory(directory);
        var main = viewModel!;
        await main.Company!.OpenPath(path);
        var studies = main.Studies!;
        studies.StudyChoice = studies.Studies.First();
        await studies.LoadCommand.ExecuteAsync(null);
        var checks = new List<string>();
        void Check(bool condition, string name)
        { if (!condition) throw new InvalidOperationException(name); checks.Add(name); }
        foreach (var language in new[] { "es", "en" })
        {
            main.ApplyPresentationPreset(language, language == "es" ? "es-ES" : "en-US", "UTC", false, false);
            var host = new PhysicalActivityEditor(studies);
            var window = new PhysicalCapacityAssistantWindow(host) { Owner = this };
            window.Show();
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            var editor = (PhysicalCapacityAssistantEditor)window.DataContext;
            void Enter(string name, string value)
            {
                var box = (TextBox)window.FindName(name);
                Check(box.Focus(), "keyboard focus " + name + " " + language);
                box.Text = value;
                box.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
            }
            Enter("DaysInput", "20"); Enter("ShiftsPerDayInput", "2");
            Enter("HoursPerShiftInput", "8"); Enter("EquivalentResourcesInput", "3");
            Enter("StoppagesInput", language == "es" ? "0,5" : "0.5");
            Enter("NormalFactorInput", "90"); Enter("FactorReasonInput", "Approved schedule / calendario aprobado");
            Check(editor.CanAdopt && editor.Proposed.StartsWith("810"), "live WPF calculation " + language);
            Check(!host.HasPendingEdits, "preview does not mutate row " + language);
            window.UpdateLayout();
            ((ScrollViewer)window.FindName("FieldsScroll")).ScrollToTop();
            window.UpdateLayout();
            VisualEvidenceCapture.Capture(window, Path.Combine(directory, "calculator-" + language + ".png"));
            Enter("StoppagesInput", "30");
            Check(!editor.CanAdopt && editor.Proposed == "", "invalid downtime clears result " + language);
            Enter("StoppagesInput", language == "es" ? "0,5" : "0.5");
            window.Width = 680; window.Height = 720;
            window.UpdateLayout();
            VisualEvidenceCapture.Capture(window, Path.Combine(directory, "calculator-narrow-" + language + ".png"));
            editor.Adopt();
            Check(decimal.Parse(host.SelectedRow!.NormalText, host.NumericCulture) == 810m, "apply updates actual row " + language);
            window.Close();
        }
        File.WriteAllText(Path.Combine(directory, "checks.json"), JsonSerializer.Serialize(checks));
    }
}
