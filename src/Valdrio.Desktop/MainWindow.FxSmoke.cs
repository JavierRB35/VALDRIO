using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Valdrio.Desktop;

// Exercises the bound controls and the real button command in the Windows CI process.
public partial class MainWindow
{
    public void RunFxEditorInteractionChecks(string directory)
    {
        var vm = (MainWindowViewModel)DataContext;
        var passed = new List<string>();
        void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("FX WPF check failed: " + name);
            passed.Add(name);
        }
        void Text(TextBox control, string value)
        {
            control.SetCurrentValue(TextBox.TextProperty, value);
            control.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
        }
        void Culture(string culture)
        {
            FormatCultureSelector.SetCurrentValue(ComboBox.SelectedItemProperty, culture);
            FormatCultureSelector.GetBindingExpression(ComboBox.SelectedItemProperty)!.UpdateSource();
            UpdateLayout();
        }
        void Apply()
        {
            var peer = new ButtonAutomationPeer(ApplyFxRateButton);
            ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke();
            var frame = new DispatcherFrame();
            Dispatcher.BeginInvoke(DispatcherPriority.Background,
                new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
            UpdateLayout();
        }

        vm.ApplyPresentationPreset("es", "es-ES", "UTC", false, false);
        Text(FxQuoteBox, "0,90");
        Text(FxRateDateBox, "2026-10-05");
        Text(FxRateSourceBox, "wpf-audit");
        Apply();
        Check(vm.FxFunctional == "90,00 EUR", "es-ES regional input and output");

        Culture("en-US");
        Text(FxQuoteBox, "0.90");
        Apply();
        Check(vm.FxFunctional == "90.00 EUR", "en-US regional input and output");

        Culture("es-ES");
        Text(FxQuoteBox, "0,95");
        Check(vm.FxCapturedCulture == "es-ES", "capture edit culture");
        Culture("en-US");
        Check(vm.FxQuoteText == "0,95", "keep dirty text across region change");
        Apply();
        Check(vm.FxFunctional == "95.00 EUR", "commit using captured culture");

        Text(FxQuoteBox, "1,2,3");
        Apply();
        Check(vm.FxStatus.StartsWith("FX_QUOTE_TEXT_INVALID", StringComparison.Ordinal),
            "concrete malformed-number diagnostic");
        Check(vm.FxFunctional == "95.00 EUR", "error preserves last valid result");

        Text(FxQuoteBox, "0.9");
        Text(FxRateDateBox, "01/02/2026");
        Apply();
        Check(vm.FxStatus.StartsWith("FX_DATE_INVALID", StringComparison.Ordinal),
            "explicit ISO date diagnostic");

        Text(FxRateDateBox, "2026-10-05");
        Text(FxRateSourceBox, "");
        Apply();
        Check(vm.FxStatus.StartsWith("FX_SOURCE_REQUIRED", StringComparison.Ordinal),
            "source diagnostic");

        Text(FxRateSourceBox, "wpf-audit");
        Text(FxQuoteBox, "-0.9");
        Apply();
        Check(vm.FxStatus.StartsWith("FX_RATE_INVALID", StringComparison.Ordinal),
            "domain quote diagnostic");

        Text(FxQuoteBox, "0.9");
        Apply(); // Clears the failed edit before changing region.
        Culture("de-DE");
        Text(FxQuoteBox, "0,90");
        Apply();
        Check(vm.FxFunctional == "90,00 EUR", "de-DE input and output");
        vm.SelectedUiLanguageTag = "en";
        Check(vm.FxFunctional == "90,00 EUR", "language does not alter number region");

        // Restore the development fixture before the visual captures.
        Text(FxQuoteBox, "0,9");
        Text(FxRateDateBox, "2026-10-04");
        Text(FxRateSourceBox, "manual-demo");
        Apply();
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "fx-editor-interaction.json"),
            JsonSerializer.Serialize(new { Status = "passed", PassedChecks = passed },
                new JsonSerializerOptions { WriteIndented = true }));
    }
}
