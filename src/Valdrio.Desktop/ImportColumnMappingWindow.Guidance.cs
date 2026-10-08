using System.Windows;
using System.Windows.Controls;

namespace Valdrio.Desktop;

public partial class ImportColumnMappingWindow
{
    private Valdrio.Application.PreparedImport? displayedPreparation;
    private Valdrio.Application.ImportedBatch? displayedConfirmation;
    private string? lastHelpKey;
    internal void ShowFieldHelp(string text, string? key = null) { lastHelpKey = key; ImportFieldHelp.Text = text; }
    private void RefreshGuidance()
    {
        if (viewModel is not { } vm || ImportSteps is null) return;
        if (!ReferenceEquals(displayedPreparation, vm.Preparation))
        {
            displayedPreparation = vm.Preparation;
            if (displayedPreparation is not null) { ImportSteps.SelectedIndex = 4; UpdateLayout(); }
        }
        if (!ReferenceEquals(displayedConfirmation, vm.ConfirmedImport))
        {
            displayedConfirmation = vm.ConfirmedImport;
            if (displayedConfirmation is not null) ImportSteps.SelectedIndex = 4;
        }
        var keys = new[] { "ImportFile", "ImportColumns", "ImportRecords", "ImportMeaning", "ImportReview" };
        for (var i = 0; i < 5; i++) ((TabItem)ImportSteps.Items[i]).Header = vm.Guide(keys[i]);
        var step = Math.Max(0, ImportSteps.SelectedIndex);
        ImportStepHelp.Text = !vm.HasImportDestination ? vm.Guide("PreviewOnly") : vm.Guide(step switch
        {
            0 => "FileHelp", 1 => "ColumnsHelp", 2 => "RecordsHelp", 3 => "MeaningHelp",
            _ => vm.ConfirmedImport is not null ? "SavedImport" : "ReviewHelp"
        });
        ImportBack.Content = vm.Guide("Back"); ImportBack.IsEnabled = step > 0 && vm.IsIdle;
        ImportNext.Content = vm.Guide(step switch
        {
            0 => "ReadNext", 1 => "ValidateNext", 3 => "ReviewNext",
            4 => vm.ConfirmedImport is not null ? "Back" : "SaveImport", _ => "Next"
        });
        if (step == 4 && vm.ConfirmedImport is not null) ImportNext.Content = vm.Guide("ContinueResources");
        if (step == 4 && vm.ConfirmedImport is null && !vm.HasConfirmableReview) ImportNext.Content = vm.Guide("CorrectImport");
        ConfirmImportButton.Visibility = step == 4 && vm.ConfirmedImport is null && vm.HasConfirmableReview
            ? Visibility.Visible : Visibility.Collapsed;
        ImportNext.Visibility =
            step == 0 && !vm.HasFile ? Visibility.Collapsed :
            step == 4 && vm.HasConfirmableReview && vm.ConfirmedImport is null ? Visibility.Collapsed :
            Visibility.Visible;
        // The visible primary action always remains clickable; if a requirement is missing,
        // the click explains exactly what the user needs to do instead of silently doing nothing.
        ImportNext.IsEnabled = vm.IsIdle;
        ImportFieldHelp.Text = vm.Guide(lastHelpKey is null ? "HelpHint" : "Help." + lastHelpKey);
    }
    private async void ImportStepChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source != ImportSteps) return;
        if (ImportSteps.SelectedIndex == 4 && viewModel is { IsIdle: true, CanReviewInGuide: true, Preparation: null } vm)
            await vm.PrepareCommand.ExecuteAsync(null);
        lastHelpKey = null;
        RefreshGuidance();
    }
    private void PreviousImportStep(object sender, RoutedEventArgs e) => ImportSteps.SelectedIndex = Math.Max(0, ImportSteps.SelectedIndex - 1);
    private void AmountMeaningChanged(object sender, SelectionChangedEventArgs e)
    {
        if (viewModel is not null && AmountModeBox.IsKeyboardFocusWithin) viewModel.AcknowledgeAmountMeaning();
    }
    private async void NextImportStep(object sender, RoutedEventArgs e) => await AdvanceImport();
    internal async Task AdvanceImport()
    {
        if (viewModel is not { IsIdle: true } vm) return;
        switch (ImportSteps.SelectedIndex)
        {
            case 0:
                if (!vm.HasFile)
                {
                    ImportStepHelp.Text = vm.FileStepRequirement;
                    return;
                }
                if (!vm.ReadCommand.CanExecute(null))
                {
                    ImportStepHelp.Text = vm.FileStepRequirement;
                    return;
                }
                if (!vm.HasColumns) await vm.ReadCommand.ExecuteAsync(null);
                if (vm.HasColumns) ImportSteps.SelectedIndex = 1;
                else ImportStepHelp.Text = vm.UserStatus;
                break;
            case 1:
                if (!vm.ValidateCommand.CanExecute(null))
                {
                    ImportStepHelp.Text = vm.ColumnStepRequirement;
                    return;
                }
                if (!vm.IsReady) await vm.ValidateCommand.ExecuteAsync(null);
                if (vm.IsReady) ImportSteps.SelectedIndex = 2;
                else ImportStepHelp.Text = vm.FriendlySummary.Length > 0 ? vm.FriendlySummary : vm.UserStatus;
                break;
            case 2:
                if (!vm.IsAccountSelectionReady) { ImportStepHelp.Text = vm.Guide("NeedRecords"); return; }
                ImportSteps.SelectedIndex = 3;
                break;
            case 3:
                if (!vm.CanReviewInGuide) { ImportStepHelp.Text = vm.ReviewRequirement; return; }
                await vm.PrepareCommand.ExecuteAsync(null);
                if (vm.Preparation is not null) ImportSteps.SelectedIndex = 4;
                break;
            case 4:
                if (vm.ConfirmedImport is not null) { Close(); return; }
                if (!vm.ConfirmCommand.CanExecute(null))
                { ImportSteps.SelectedIndex = !vm.IsReady ? 1 : !vm.IsAccountSelectionReady ? 2 : 3; ImportStepHelp.Text = vm.Guide("EditReview"); return; }
                await vm.ConfirmCommand.ExecuteAsync(null);
                break;
        }
        RefreshGuidance();
    }
    internal void RevealImportElement(FrameworkElement element)
    {
        if (element == ConfirmImportButton) ImportSteps.SelectedIndex = 4;
        foreach (TabItem tab in ImportSteps.Items)
            if (GuidedUi.Contains(tab, element)) { ImportSteps.SelectedItem = tab; break; }
        UpdateLayout();
    }
}
