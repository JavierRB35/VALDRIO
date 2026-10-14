using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Data.Sqlite;
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
            GuidedUi.Reveal(button);
            Check(button.IsEnabled, "enabled: " + button.Name);
            ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            if (button.Command is IAsyncRelayCommand command && command.ExecutionTask is { } task) await task.WaitAsync(TimeSpan.FromSeconds(30));
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            UpdateLayout();
        }
        Check(CompanyFileDialogs.SuggestedFileName("Café / Taller: Norte") == "Café - Taller- Norte",
            "new project dialog suggests a Windows-safe filename from project name");
        Check(CompanyFileDialogs.SuggestedFileName("CON") == "_CON",
            "reserved Windows device names receive a safe prefix");
        main.ApplyPresentationPreset("en", "es-ES", "UTC", false, false);
        var template = Path.Combine(AppContext.BaseDirectory, "examples", "services.valdrio");
        var templateHash = SHA256.HashData(File.ReadAllBytes(template));
        dialogs.Paths.Enqueue(null); await Click(ExampleButton);
        Check(vm.Snapshot is null, "cancel example destination leaves project closed");
        var examplePath = Path.Combine(output, "learning-copy.valdrio");
        dialogs.Paths.Enqueue(examplePath); await Click(ExampleButton);
        Check(vm.Snapshot is not null && vm.ActivePath == examplePath && main.Studies!.Studies.Count > 0
              && vm.Snapshot.SchemaVersion == SqliteCompanyFileService.SchemaVersion && vm.CanWrite,
            "example opens a real editable current-format copy with saved analyses"
            + " (diagnostic=" + (vm.DiagnosticCode ?? "none") + ", status=" + vm.Status + ")");
        var exampleId = vm.Snapshot!.Id;
        var sentinel = Path.Combine(output, "do-not-overwrite.valdrio");
        File.WriteAllText(sentinel, "existing destination");
        dialogs.Paths.Enqueue(sentinel);
        await vm.OpenExampleCommand.ExecuteAsync(null);
        Check(File.ReadAllText(sentinel) == "existing destination" && vm.Snapshot?.Id == exampleId,
            "example never overwrites a destination or replaces the active project on failure");
        Check(templateHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(template))), "shipped example remains byte-for-byte unchanged");
        await vm.CloseCommand.ExecuteAsync(null);
        await Click(ImportStartButton);
        Check(NameBox.IsKeyboardFocusWithin, "import entry focuses the existing project setup instead of bypassing prerequisites");
        SetText(NameBox, "VALDRIO synthetic factory 日本");
        CurrencyBox.SetCurrentValue(ComboBox.SelectedItemProperty, "USD"); CurrencyBox.GetBindingExpression(ComboBox.SelectedItemProperty)!.UpdateSource();
        dialogs.Paths.Enqueue(path); await Click(CreateButton);
        Check(vm.Snapshot?.DefaultCurrency.ToString() == "USD", "EN/es-ES/USD creation");
        Check(ActiveCompanyNameText.Text == "VALDRIO synthetic factory 日本", "active company name shown after create");
        SetText(NameBox, "Draft for another company");
        Check(ActiveCompanyNameText.Text == "VALDRIO synthetic factory 日本", "new company draft does not rename active company");
        var id = vm.Snapshot!.Id;
        Check(vm.Snapshot.Units.Count == DefaultActivityUnitCatalog.All.Count &&
              vm.Snapshot.Units.Any(u => u.Code == "machine_hour"), "new project contains standard units");
        SetText(UnitCodeBox, "00123"); await Click(AddUnitButton);
        Check(vm.Snapshot.Units.Any(u => u.Code == "00123"), "bound custom unit saved");
        dialogs.Paths.Enqueue(backup); await Click(BackupButton);
        Check(File.Exists(backup), "live backup created");
        await Click(CloseButton); Check(vm.Snapshot is null, "close removes active session");
        Check(ActiveCompanyNameText.Text == vm.ActiveCompanyName && !ActiveCompanyNameText.Text.Contains("synthetic factory", StringComparison.Ordinal), "closing clears active company label");
        dialogs.Paths.Enqueue(path); await Click(OpenButton);
        Check(ActiveCompanyNameText.Text == "VALDRIO synthetic factory 日本", "reopening shows persisted company name");
        Check(vm.Snapshot?.Id == id && vm.Snapshot.Units.Count == DefaultActivityUnitCatalog.All.Count + 1,
            "real reopen retains identity and standard plus custom units");
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        var bad = Path.Combine(output, "bad.fixture"); File.WriteAllText(bad, "corrupt");
        dialogs.Paths.Enqueue(bad); await Click(OpenButton);
        Check(vm.Snapshot?.Id == id && vm.DiagnosticCode?.StartsWith("FILE_", StringComparison.Ordinal) == true, "failed open preserves active company");
        // Simulate a previous-format file on a disposable copy. Schema rejection must
        // not crash WPF, mutate the source or replace the active company session.
        var older = Path.Combine(output, "older-schema.valdrio");
        File.Copy(path, older);
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
               { DataSource = older, Pooling = false }.ToString()))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE FileMetadata SET SchemaVersion=8";
            Check(command.ExecuteNonQuery() == 1, "previous format test fixture prepared");
        }
        var oldHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(older)));
        string? alert = null;
        vm.OpenFailure = message => alert = message;
        dialogs.Paths.Enqueue(older); await Click(OpenButton);
        Check(vm.DiagnosticCode == "FILE_UPGRADE_REQUIRED" && !string.IsNullOrWhiteSpace(alert),
            "previous format raises an actionable warning without ending the WPF session");
        Check(vm.UpgradeSuggested && UpgradeSuggestionButton.IsVisible,
            "upgrade action is visible for a previous-format file without reselecting its source");
        Check(vm.Snapshot?.Id == id && vm.ActivePath == path &&
              Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(older))) == oldHash,
            "previous format leaves active project and source file untouched");
        vm.OpenFailure = null;
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
            Check(independent.Read().Id == id && independent.Read().Units.Any(u => u.Code == "00123")
                && independent.Read().Units.Count == DefaultActivityUnitCatalog.All.Count + 1,
                "independent read of UI-created SQLite");
        var window = (MainWindow)System.Windows.Window.GetWindow(this);
        window.ResetScrollForCapture();
        VisualEvidenceCapture.Capture(window, Path.Combine(output, "company-panel.png"));
        await Click(CloseButton);
        File.WriteAllText(Path.Combine(output, "interaction.json"), JsonSerializer.Serialize(new { Status = "passed", PassedChecks = passed }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
