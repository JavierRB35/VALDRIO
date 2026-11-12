using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Data.Sqlite;
using Valdrio.Domain;

namespace Valdrio.Desktop;

public partial class MainWindow
{
    internal async Task RunActivityFeedbackChecks(string directory, string path, QueuedCompanyFileDialogs files)
    {
        Directory.CreateDirectory(directory);
        var main = viewModel!;
        var company = main.Company!;
        await company.OpenPath(path);
        if (company.DiagnosticCode == "FILE_UPGRADE_REQUIRED")
        {
            path = Path.Combine(directory, "upgraded.valdrio");
            files.Paths.Enqueue(path);
            await company.UpgradeCommand.ExecuteAsync(null);
        }
        if (company.Snapshot is null) throw new InvalidOperationException(company.Status);
        var studies = main.Studies!;
        studies.StudyChoice = studies.Studies.First(s => s.Value.State != StudyState.Closed);
        await studies.LoadCommand.ExecuteAsync(null);
        var checks = new List<string>();
        void Check(bool value, string name)
        { if (!value) throw new InvalidOperationException(name); checks.Add(name); }
        async Task Render() => await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        foreach (var language in new[] { "es", "en" })
        {
            main.ApplyPresentationPreset(language, language == "es" ? "es-ES" : "en-US", "UTC", false, false);
            var activity = new PhysicalActivityWindow(studies) { Owner = this };
            activity.ShowIntegrated();
            await activity.InitializeAsync();
            var host = (PhysicalActivityEditor)activity.DataContext;
            Check(host.Rows.Count > 0, "activity loaded " + language);
            var calculator = new PhysicalCapacityAssistantWindow(host) { Owner = this };
            calculator.ShowIntegrated();
            await Render();
            var editor = (PhysicalCapacityAssistantEditor)calculator.DataContext;
            void Enter(string name, string value)
            {
                var box = (TextBox)calculator.FindName(name);
                box.Focus(); box.Text = value;
                box.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
            }
            Enter("NormalInput", "-1"); Enter("RealInput", "80");
            await calculator.SaveActivityAsync(true);
            Check(workspacePages.Last() == calculator && host.HasPendingEdits && !company.IsProcessing,
                "invalid confirmation stays editable " + language);
            Enter("NormalInput", "100");
            await calculator.SaveActivityAsync(false);
            Check(workspacePages.Last() == calculator && !host.HasPendingEdits,
                "draft remains in calculator " + language);
            Enter("RealInput", "81");
            var before = host.SelectedRow!.Version;
            using (var blocker = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString()))
            {
                blocker.Open();
                using var transaction = blocker.BeginTransaction();
                var save = calculator.SaveActivityAsync(true);
                try
                {
                    await Task.Delay(200);
                    await Render();
                    Check(!save.IsCompleted && host.IsBusy && !editor.CanConfirm && !editor.CanSave,
                        "slow save disables repeated actions " + language);
                    Check(company.IsProcessing && GlobalActivityBar.IsVisible && GlobalActivityBar.IsIndeterminate,
                        "global animated activity shown " + language);
                    Check(!calculator.CanLeave(), "cannot leave during persistence " + language);
                    await calculator.SaveActivityAsync(true);
                    VisualEvidenceCapture.Capture(this, Path.Combine(directory, "saving-" + language + "-1.png"));
                    await Task.Delay(300);
                    await Render();
                    Check(!save.IsCompleted, "dispatcher remains responsive during write " + language);
                    VisualEvidenceCapture.Capture(this, Path.Combine(directory, "saving-" + language + "-2.png"));
                }
                finally { transaction.Rollback(); }
                await save;
            }
            await Render();
            Check(workspacePages.Last() == activity, "successful confirmation returns to activity " + language);
            Check(host.SelectedRow.Version == before + 1 && !host.HasPendingEdits,
                "exactly one version committed " + language);
            Check(host.SelectedRow.NormalText == "100" && host.SelectedRow.RealText == "81",
                "confirmed values shown in table " + language);
            Check(!company.IsProcessing && !GlobalActivityBar.IsVisible, "indicator cleared " + language);
            VisualEvidenceCapture.Capture(this, Path.Combine(directory, "returned-" + language + ".png"));
            await host.ReloadAsync();
            Check(host.SelectedRow!.NormalText == "100" && host.SelectedRow.RealText == "81",
                "values survive reload " + language);
            activity.Close();
        }
        var composition = new CompositionWindow(studies) { Owner = this };
        composition.ShowIntegrated();
        await composition.InitializeAsync();
        composition.Editor.AddItem();
        var item = composition.Editor.Items.Last();
        item.Code = "ACTIVITY-CHECK"; item.Name = "Synthetic activity feedback check";
        composition.LeaveDecision = () => MessageBoxResult.Yes;
        var leaving = composition.CanLeaveAsync();
        Check(company.IsProcessing, "save pending composition keeps global activity active");
        Check(await leaving, "pending composition saved asynchronously before leaving");
        Check(!composition.Editor.Dirty && !company.IsProcessing, "pending save clears activity and dirty state");
        composition.Close();
        Check(!workspacePages.Contains(composition), "composition leaves after successful save");

        var rates = new PrincipalRatesWindow(studies) { Owner = this };
        rates.ShowIntegrated(); await rates.InitializeAsync();
        Check(rates.Editor.Report is not null && !company.IsProcessing, "rates load completes and clears activity");
        rates.Close();
        var auxiliary = new AuxiliaryCapacityWindow(studies) { Owner = this };
        auxiliary.ShowIntegrated(); await auxiliary.InitializeAsync();
        Check(auxiliary.Editor.Rows.Count > 0 && !company.IsProcessing, "auxiliary load completes and clears activity");
        auxiliary.Editor.Dirty = false; auxiliary.Close();
        await CheckActivityReportOutput(directory);
        Check(!company.IsProcessing, "asynchronous report output completes");
        var first = company.BeginActivity();
        var second = company.BeginActivity();
        first.Dispose(); first.Dispose();
        Check(company.IsProcessing, "overlapping scope keeps activity on; disposal is idempotent");
        second.Dispose();
        Check(!company.IsProcessing, "last scope clears activity");
        try { using var scope = company.BeginActivity(); throw new OperationCanceledException(); }
        catch (OperationCanceledException) { }
        Check(!company.IsProcessing, "cancellation releases activity");
        File.WriteAllText(Path.Combine(directory, "activity-checks.json"), JsonSerializer.Serialize(checks));
    }
    internal async Task CheckActivityReportOutput(string directory)
    {
        Directory.CreateDirectory(directory);
        var company = viewModel!.Company!;
        var report = viewModel.CostFlows!;
        await report.EnterAsync();
        var reportPath = Path.Combine(directory, "report.xps");
        using (var activityScope = company.BeginActivity())
        using (var document = new System.Windows.Xps.Packaging.XpsDocument(reportPath, FileAccess.Write))
        {
            var writer = System.Windows.Xps.Packaging.XpsDocument.CreateXpsDocumentWriter(document);
            await CostFlowPanel.WriteReportAsync(writer, CostFlowPanel.BuildPrintableReport(report, 800, 1100));
        }
        using var reopened = new System.Windows.Xps.Packaging.XpsDocument(reportPath, FileAccess.Read);
        if (reopened.GetFixedDocumentSequence().References.Count == 0 || company.IsProcessing)
            throw new InvalidOperationException("Report output did not complete or activity remained active.");
        File.WriteAllText(Path.Combine(directory, "report-check.txt"), "PASS: asynchronous XPS output reopened; global activity cleared.");
    }

}
