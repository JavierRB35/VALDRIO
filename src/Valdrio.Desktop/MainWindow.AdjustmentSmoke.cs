using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;

namespace Valdrio.Desktop;
public partial class MainWindow
{
    internal async Task RunAdjustmentChecks(string directory)
    {
        var output = Path.Combine(directory, "reusable-adjustments"); Directory.CreateDirectory(output);
        var vm = viewModel!.Resources!;
        var checks = new List<string>();
        void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message + " / " + vm.Status); checks.Add(message); }
        async Task Idle() { await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle); UpdateLayout(); }
        IEnumerable<T> Descendants<T>(DependencyObject node) where T : DependencyObject
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            {
                var child = VisualTreeHelper.GetChild(node, i);
                if (child is T match) yield return match;
                foreach (var nested in Descendants<T>(child)) yield return nested;
            }
        }
        async Task Click(Button button)
        {
            button.BringIntoView(); await Idle(); Check(button.IsEnabled, "enabled: " + button.Content);
            ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke(); await Idle();
            if (button.Command is IAsyncRelayCommand c && c.ExecutionTask is { } task) await task;
            await Idle();
        }
        viewModel.ApplyPresentationPreset("es", "es-ES", "Europe/Madrid", false, false);
        await NavigateWorkflow(4); await Idle();
        var panel = Descendants<ResourceWorkspacePanel>(this).First(p => p.IsVisible);
        panel.ReusableAdjustmentsSection.IsExpanded = true; await Idle();
        await Click(panel.AddAdjustmentRuleButton);
        var card = vm.RuleCards.Single();
        void Input(string field, string text)
        {
            var box = Descendants<TextBox>(panel.ReusableAdjustmentsSection).Single(b => b.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path.Path == field);
            box.SetCurrentValue(TextBox.TextProperty, text); box.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
        }
        Input("Name", "Amortización técnica"); Input("Pattern", vm.Rows.First().Code); Input("Code", "AJ-AMORT-TEC");
        Input("Value", "500"); Input("Reason", "Estimación técnica del periodo");
        await Idle();
        Check(Descendants<TextBlock>(panel.ReusableAdjustmentsSection).Any(t =>
            t.IsVisible && t.Text == card.SelectionSummary && t.Text.Contains("EUR")), "imported balance visible while card is unsaved");
        Button Action(IRelayCommand command) => Descendants<Button>(panel.ReusableAdjustmentsSection).Single(b => ReferenceEquals(b.Command, command));
        await Click(Action(card.SaveCommand)); card = vm.RuleCards.Single();
        Check(!card.IsDirty, "card saved through native controls");
        await Click(Action(card.ApplyCommand)); Check(card.HasPreview, "economic preview displayed before writing");
        Action(card.ConfirmCommand).BringIntoView(); await Idle();
        VisualEvidenceCapture.Capture(this, Path.Combine(output, "01-es-preview.png"));
        await Click(Action(card.ConfirmCommand));
        Check(vm.Rows.Any(r => r.Code == "AJ-AMORT-TEC" && r.Effective == "500 EUR"), "analytical cost visible with expected amount");
        panel.ReusableAdjustmentsSection.IsExpanded = false; panel.ResourceGrid.BringIntoView(); await Idle();
        VisualEvidenceCapture.Capture(this, Path.Combine(output, "02-es-applied.png"));
        panel.ReusableAdjustmentsSection.IsExpanded = true; await Idle();
        await Click(Action(card.UndoCommand)); await Click(Action(card.ConfirmCommand));
        Check(vm.Rows.Single(r => r.Code == "AJ-AMORT-TEC").Effective == "0 EUR", "undo neutralizes generated cost without removing history");
        viewModel.ApplyPresentationPreset("en", "en-US", "UTC", false, false); await Idle();
        panel.ReusableAdjustmentsSection.BringIntoView(); await Idle();
        VisualEvidenceCapture.Capture(this, Path.Combine(output, "03-en-undone.png"));
        viewModel.ApplyPresentationPreset("es", "es-ES", "Europe/Madrid", false, false); await Idle();
        card = vm.RuleCards.Single();
        Input("Pattern", ""); await Click(Action(card.SaveCommand)); card = vm.RuleCards.Single();
        await Click(Action(card.ApplyCommand));
        Check(card.HasPreview && !string.IsNullOrWhiteSpace(card.Feedback), "source-free preview has explicit feedback");
        await Click(Action(card.ConfirmCommand));
        Check(vm.Rows.Single(r => r.Code == "AJ-AMORT-TEC").Effective == "500 EUR", "additional analytical row created without source accounts");
        Check(Descendants<TextBlock>(panel.ReusableAdjustmentsSection).Any(t => t.Text == card.Feedback && t.IsVisible), "application feedback visible next to card");
        Action(card.UndoCommand).BringIntoView(); await Idle();
        VisualEvidenceCapture.Capture(this, Path.Combine(output, "04-es-additional-feedback.png"));
        await File.WriteAllTextAsync(Path.Combine(output, "interaction.json"), JsonSerializer.Serialize(new { Status = "passed", PassedChecks = checks }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
