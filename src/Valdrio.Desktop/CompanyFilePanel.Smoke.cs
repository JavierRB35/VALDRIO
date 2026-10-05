using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using Valdrio.Application;
using Valdrio.Infrastructure;
using Valdrio.Infrastructure.CompanyFiles;

namespace Valdrio.Desktop;

public partial class CompanyFilePanel
{
    internal async Task RunCompanyInteractionChecks(string directory, QueuedCompanyFileDialogs dialogs, MainWindowViewModel main)
    {
        var vm = (CompanyFileViewModel)DataContext;
        var output = Path.Combine(directory, "company-file"); Directory.CreateDirectory(output);
        var path = Path.Combine(output, "example.valdrio"); var backup = Path.Combine(output, "example.valdrio-backup");
        var restored = Path.Combine(output, "restored.valdrio");
        var passed = new List<string>();
        void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException("Company WPF: " + message); passed.Add(message); }
        void SetText(TextBox control, string value) { control.SetCurrentValue(TextBox.TextProperty, value); control.GetBindingExpression(TextBox.TextProperty)!.UpdateSource(); }
        async Task Click(Button button)
        {
            Check(button.IsEnabled, "enabled: " + button.Name);
            ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            if (button.Command is IAsyncRelayCommand command && command.ExecutionTask is { } task) await task.WaitAsync(TimeSpan.FromSeconds(30));
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            UpdateLayout();
        }
        main.ApplyPresentationPreset("en", "es-ES", "UTC", false, false);
        SetText(NameBox, "VALDRIO synthetic factory 日本");
        CurrencyBox.SetCurrentValue(ComboBox.SelectedItemProperty, "USD"); CurrencyBox.GetBindingExpression(ComboBox.SelectedItemProperty)!.UpdateSource();
        dialogs.Paths.Enqueue(path); await Click(CreateButton);
        Check(vm.Snapshot?.DefaultCurrency.ToString() == "USD", "EN/es-ES/USD creation");
        var id = vm.Snapshot!.Id;
        SetText(UnitCodeBox, "00123"); await Click(AddUnitButton);
        Check(vm.Snapshot.Units.Single().Code == "00123", "bound unit saved");
        dialogs.Paths.Enqueue(backup); await Click(BackupButton);
        Check(File.Exists(backup), "live backup created");
        await Click(CloseButton); Check(vm.Snapshot is null, "close removes active session");
        dialogs.Paths.Enqueue(path); await Click(OpenButton);
        Check(vm.Snapshot?.Id == id && vm.Snapshot.Units.Count == 1, "real reopen retains identity and unit");
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        var bad = Path.Combine(output, "bad.fixture"); File.WriteAllText(bad, "corrupt");
        dialogs.Paths.Enqueue(bad); await Click(OpenButton);
        Check(vm.Snapshot?.Id == id && vm.Status.StartsWith("FILE_", StringComparison.Ordinal), "failed open preserves active company");
        dialogs.Paths.Enqueue(path); await Click(CreateButton);
        Check(vm.Snapshot?.Id == id && Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) == hash,
            "failed creation never overwrites");
        main.ApplyPresentationPreset("es", "en-US", "Europe/Madrid", false, false);
        Check(vm.Snapshot?.DefaultCurrency.ToString() == "USD" && Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) == hash,
            "language region and zone do not write company");
        dialogs.Paths.Enqueue(backup); dialogs.Paths.Enqueue(restored); await Click(RestoreButton);
        Check(vm.Snapshot?.Id == id && vm.ActivePath == restored, "restore switches to validated new file");
        var invalidTarget = Path.Combine(output, "invalid-restore.valdrio");
        dialogs.Paths.Enqueue(bad); dialogs.Paths.Enqueue(invalidTarget); await Click(RestoreButton);
        Check(vm.ActivePath == restored && !File.Exists(invalidTarget), "failed restore preserves active company");
        var euro = Path.Combine(output, "euro.valdrio");
        CurrencyBox.SetCurrentValue(ComboBox.SelectedItemProperty, "EUR"); CurrencyBox.GetBindingExpression(ComboBox.SelectedItemProperty)!.UpdateSource();
        dialogs.Paths.Enqueue(euro); await Click(CreateButton);
        Check(vm.Snapshot?.DefaultCurrency.ToString() == "EUR", "ES/en-US/EUR creation");
        main.ApplyPresentationPreset("en", "de-DE", "UTC", false, false);
        Check(vm.Snapshot?.DefaultCurrency.ToString() == "EUR", "de-DE preserves currency");
        // Query independently using the same public storage port; UI text alone is not persistence evidence.
        var catalog = new JsonCurrencyCatalog(Path.Combine(AppContext.BaseDirectory, "reference", "currency-catalog-example.json"));
        using (var independent = new SqliteCompanyFileService(catalog, new SystemClock()).Open(path, CompanyFileOpenMode.ReadOnly))
            Check(independent.Read().Id == id && independent.Read().Units.Single().Code == "00123", "independent read of UI-created SQLite");
        VisualEvidenceCapture.CaptureContent(this, Path.Combine(output, "company-panel.png"));
        await Click(CloseButton);
        File.WriteAllText(Path.Combine(output, "interaction.json"), JsonSerializer.Serialize(new { Status = "passed", PassedChecks = passed }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
