using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Valdrio.Domain;
namespace Valdrio.Desktop;
public partial class AllocationWorkspacePanel : UserControl
{
    private AllocationWorkspaceViewModel? viewModel;
    private bool secondaryOnly;
    private bool previouslyClosedPrimary;
    private bool previouslyHadSecondaryResults;
    public AllocationWorkspacePanel()
    {
        InitializeComponent();
        ConfigurePage(false);
        DataContextChanged += (_, e) =>
        {
            if (viewModel is not null)
            {
                viewModel.PropertyChanged -= Changed;
                viewModel.PrincipalOriginsChanged -= PrincipalFlowChanged;
            }
            viewModel = e.NewValue as AllocationWorkspaceViewModel;
            if (viewModel is not null)
            {
                viewModel.PropertyChanged += Changed;
                viewModel.PrincipalOriginsChanged += PrincipalFlowChanged;
            }
            Headers();
            UpdateBatchState();
            RedrawPrincipalCostFlow();
        };
    }
    public bool SecondaryOnly { set => ConfigurePage(value); }
    private void ConfigurePage(bool secondary)
    {
        secondaryOnly = secondary;
        RedrawPrincipalCostFlow();
        // Each page has its own visual tree; shared controls retain the same commands and validation.
        static void Card(FrameworkElement section, bool visible) =>
            ((FrameworkElement)section.Parent).Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        Card(CompositionSection, !secondary);
        Card(AccountsSection, !secondary);
        Card(ResultsSection, !secondary);
        Card(SecondaryResultsSection, secondary);
        SecondaryPhaseBar.Visibility = secondary ? Visibility.Visible : Visibility.Collapsed;
        ((FrameworkElement)((FrameworkElement)TogglePrimaryButton.Parent).Parent).Visibility = secondary ? Visibility.Collapsed : Visibility.Visible;
        for (var i = 0; i < RuleTabs.Items.Count; i++)
            ((TabItem)RuleTabs.Items[i]).Visibility = (i == 1) == secondary ? Visibility.Visible : Visibility.Collapsed;
        RuleTabs.SelectedIndex = secondary ? 1 : 0;
        RulesSection.IsExpanded = true;
    }
    private void Changed(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == "Item[]") Headers();
        UpdateBatchState();
        if (viewModel is not { } vm || !string.IsNullOrEmpty(e.PropertyName)) return;
        if (secondaryOnly)
        {
            var hasResults = vm.SecondaryResults.Count > 0;
            var firstCalculation = !previouslyHadSecondaryResults && hasResults;
            previouslyHadSecondaryResults = hasResults;
            if (firstCalculation && IsVisible) RevealResults(SecondaryResultsSection);
        }
        else
        {
            var closed = vm.IsPrimaryClosed;
            var newClosure = !previouslyClosedPrimary && closed;
            previouslyClosedPrimary = closed;
            if (newClosure && IsVisible) RevealResults(ResultsSection);
        }
    }
    private async void NavigateToSecondaryRepair(object sender, RoutedEventArgs e)
    {
        if (viewModel is not { } vm) return;
        LibrarySection.IsExpanded = true;
        DefinitionSection.IsExpanded = true;
        RulesSection.IsExpanded = true;
        RuleTabs.SelectedIndex = 1;
        if (vm.SelectedSecondary is null) vm.SelectedSecondary = vm.SecondaryRules.FirstOrDefault();
        vm.SelectSecondaryRepairKey();
        if (vm.LoadKeyCommand.CanExecute(null))
            await vm.LoadKeyCommand.ExecuteAsync(null);
        Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
        {
            if (SecondaryRulesGrid.IsVisible && vm.SelectedSecondary is not null)
            {
                SecondaryRulesGrid.SelectedItem = vm.SelectedSecondary;
                SecondaryRulesGrid.ScrollIntoView(vm.SelectedSecondary);
                SecondaryRulesGrid.BringIntoView();
            }
        }));
    }
    private void RevealResults(Expander section)
    {
        section.IsExpanded = true;
        Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
        {
            if (IsVisible) section.BringIntoView();
        }));
    }
    private void Headers()
    {
        if (viewModel is null) return;
        Set(SecondaryNodeAssignmentsGrid, "Auxiliary", "PublishedKey");
        Set(PrimaryNodeTotalsGrid, "NodeCenter", "Node", "PrimaryAmount");
        Set(KeyGrid, "Name", "State"); Set(LinesGrid, "Include", "Destination", "NodeUnit", "Weight"); Set(ReciprocalGrid, "ReciprocalGroup", "ReciprocalStatus", "ReciprocalDetail"); Set(SecondaryRulesGrid, "Auxiliary", "ReciprocalGroup", "AppliedKey"); Set(SecondaryResultsGrid, "SecondarySourceNode", "SecondaryPrincipalDestination", "PrimaryAmount", "Behavior"); Set(SecondaryCirculationGrid, "Auxiliary", "SecondaryGross");  Set(SecondaryGroupAssignmentsGrid, "SecondarySourcePart", "SecondarySelectedGroup");
        Set(PrincipalCostsGrid, "SecondaryPrincipalDestination", "PrincipalPrimary", "PrincipalAuxiliary", "PrincipalTotal");
        Set(SecondaryExplanationGrid, "PrimarySource", "ExplainPrimaryAmount", "ExplainEffectiveShare", "PrimaryAmount", "ExplainKeys");
        Set(PrincipalOriginGrid, "Account", "PrimarySource", "Behavior", "PrimaryAmount", "ExplainKeys");
        CompositionGrid.Columns[0].Header = viewModel["CompositionAccount"];
        CompositionGrid.Columns[1].Header = viewModel["CompositionAmount"];
        CompositionGrid.Columns[2].Header = viewModel["CompositionBehavior"];
        CompositionGrid.Columns[3].Header = viewModel["CompositionFixedWeight"];
        CompositionGrid.Columns[4].Header = viewModel["CompositionVariableWeight"];
        CompositionGrid.Columns[5].Header = viewModel["CompositionReview"];
        Set(CoverageGrid, "Source", "Amount", "AppliedKey", "Result"); Set(PrimaryResultsGrid, "PrimarySource", "PrimaryDestination", "PrimaryAmount", "Behavior"); Set(PrimaryGrid, "Account", "AppliedKey", "State");
        Set(AbsoluteGrid, "Include", "Destination", "Amount");
        void Set(DataGrid grid, params string[] keys) { for (var i = 0; i < keys.Length; i++) grid.Columns[i].Header = viewModel[keys[i]]; }
    }
    internal void FocusKeyLibrary()
    {
        // Only when entering a phase. Preserve editor focus during saving/rebuilding.
        LibrarySection.IsExpanded = true;
        Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(async () =>
        {
            if (!KeyGrid.IsVisible || !KeyGrid.IsEnabled) return;

            if (KeyGrid.Items.Count > 0)
            {
                var first = KeyGrid.Items[0];
                KeyGrid.SelectedItem = first;
                // A saved, closed phase cannot leave a highlighted key next to a
                // blank invalid editor. Load its read-only composition immediately.
                if (viewModel is { } vm && (vm.IsPrimaryClosed || vm.IsSecondaryClosed) &&
                    vm.LoadKeyCommand.CanExecute(null))
                    await vm.LoadKeyCommand.ExecuteAsync(null);
                KeyGrid.CurrentCell = new DataGridCellInfo(first, KeyGrid.Columns[0]);
                KeyGrid.ScrollIntoView(first);
                KeyGrid.UpdateLayout();
                if (KeyGrid.ItemContainerGenerator.ContainerFromItem(first) is DataGridRow row)
                {
                    row.Focus();
                    Keyboard.Focus(row);
                    return;
                }
            }

            KeyGrid.Focus();
            Keyboard.Focus(KeyGrid);
        }));
    }
    internal void RestoreClosedKeyDetails()
    {
        // Do not steal focus or displace the financial results on a closed phase.
        Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(async () =>
        {
            if (viewModel is not { } vm || !KeyGrid.IsVisible || KeyGrid.Items.Count == 0) return;
            var first = KeyGrid.Items[0];
            KeyGrid.SelectedItem = first;
            if (vm.LoadKeyCommand.CanExecute(null))
                await vm.LoadKeyCommand.ExecuteAsync(null);
        }));
    }

    private void FocusNewKey(object sender, RoutedEventArgs e)
    {
        // The key editor is shared by primary and secondary pages.
        Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            if (KeyNameBox.IsEnabled && KeyNameBox.IsVisible)
            {
                KeyNameBox.Focus();
                KeyNameBox.SelectAll();
            }
        }));
    }
    private async void OpenKeySelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.OriginalSource == KeyGrid && KeyGrid.IsKeyboardFocusWithin &&
            viewModel?.LoadKeyCommand.CanExecute(null) == true)
            await viewModel.LoadKeyCommand.ExecuteAsync(null);
    }
    internal Task SecondaryAssignmentTask { get; private set; } = Task.CompletedTask;

    // WPF Popups hold focus outside the ComboBox visual tree. SelectionChanged
    // therefore cannot use IsKeyboardFocusWithin as the sole indication of a
    // mouse selection. Persist pointer choices when the dropdown closes.
    private async void SecondaryAssignmentDropDownClosed(object sender, EventArgs e)
    {
        if (sender is ComboBox combo) await PersistSecondaryAssignment(combo);
    }

    // Keyboard arrows can select a value without opening the popup. A dropdown
    // choice is handled only on DropDownClosed to avoid duplicate commits.
    private async void SecondaryAssignmentChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.OriginalSource is ComboBox combo && combo.IsLoaded &&
            !combo.IsDropDownOpen && combo.IsKeyboardFocusWithin)
            await PersistSecondaryAssignment(combo);
    }

    private async Task PersistSecondaryAssignment(ComboBox combo)
    {
        if (combo.DataContext is not SecondaryNodeAssignmentRow row ||
            combo.SelectedValue is not EntityId keyId || viewModel is not { } vm ||
            !combo.IsLoaded || vm.IsBusy || !vm.CanShowSecondaryEditor) return;

        if (row.PersistedKeyId == keyId)
        {
            row.AssignmentError = "";
            return;
        }

        row.SelectedKeyId = keyId;
        row.AssignmentError = "";
        async Task Save()
        {
            var saved = await vm.AssignSecondaryForNodeAsync(row.NodeId, keyId);
            if (saved)
            {
                // The persisted configuration has already rebuilt the row.
                row.AcceptCommitted(keyId);
            }
            else
            {
                row.RestoreCommitted();
                row.AssignmentError = vm.SecondaryAssignmentFailure;
            }
        }
        SecondaryAssignmentTask = Save();
        await SecondaryAssignmentTask;
    }

    private async void OpenKeyDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ItemsControl.ContainerFromElement(KeyGrid, e.OriginalSource as DependencyObject) is DataGridRow && viewModel?.LoadKeyCommand.CanExecute(null) == true)
            await viewModel.LoadKeyCommand.ExecuteAsync(null);
    }
    private async void OpenKeyKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && viewModel?.LoadKeyCommand.CanExecute(null) == true)
        { e.Handled = true; await viewModel.LoadKeyCommand.ExecuteAsync(null); }
    }
    private void BatchSelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateBatchState();
    private void UpdateBatchState()
    {
        if (BatchApplyButton is null || BatchPreviewText is null || BatchKeyBox is null || CoverageGrid is null) return;
        if (viewModel is not { } vm || BatchKeyBox.SelectedValue is not EntityId keyId)
        {
            BatchApplyButton.IsEnabled = false;
            BatchPreviewText.Text = viewModel?["BulkAssignSelectKey"] ?? "";
            return;
        }
        var ids = CoverageGrid.SelectedItems.OfType<AllocationCoverageRow>().Select(row => row.Id).ToArray();
        var preview = vm.PreviewCoverageBatch(ids, keyId);
        BatchApplyButton.IsEnabled = preview.EligibleCount > 0 && !vm.IsBusy;
        BatchPreviewText.Text = string.Format(System.Globalization.CultureInfo.CurrentCulture,
            vm["BulkAssignPreview"], preview.SelectedCount, preview.EligibleCount,
            preview.SkippedCount, vm.FormatBatchAmount(preview.EligibleAmount));
    }
    internal Task BatchAssignmentTask { get; private set; } = Task.CompletedTask;
    private async void ApplyBatchToSelectedRows(object sender, RoutedEventArgs e)
    {
        if (viewModel is not { } vm || BatchKeyBox.SelectedValue is not EntityId keyId) return;
        var ids = CoverageGrid.SelectedItems.OfType<AllocationCoverageRow>().Select(row => row.Id).ToArray();
        var preview = vm.PreviewCoverageBatch(ids, keyId);
        if (preview.EligibleCount == 0) return;
        var message = string.Format(System.Globalization.CultureInfo.CurrentCulture, vm["BulkAssignConfirm"],
            preview.EligibleCount, vm.FormatBatchAmount(preview.EligibleAmount), preview.SkippedCount);
        if (MessageBox.Show(Window.GetWindow(this), message, vm["BulkAssignTitle"], MessageBoxButton.YesNo,
                MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        try
        {
            var operation = vm.AssignCoverageBatchAsync(ids, keyId);
            BatchAssignmentTask = operation;
            await operation;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError("Primary batch assignment failed: {0}", ex);
            MessageBox.Show(Window.GetWindow(this), vm["BulkAssignError"], vm["BulkAssignTitle"],
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { UpdateBatchState(); }
    }
    internal Task CoverageAssignmentTask { get; private set; } = Task.CompletedTask;
    private async void CoverageKeyChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox { IsKeyboardFocusWithin: true, DataContext: AllocationCoverageRow row, SelectedValue: Valdrio.Domain.EntityId key } box || key == row.SelectedKeyId || viewModel is null) return;
        CoverageAssignmentTask = viewModel.AssignCoverageAsync(row.Id, key);
        await CoverageAssignmentTask;
        box.GetBindingExpression(ComboBox.SelectedValueProperty)?.UpdateTarget();
    }
    private bool restoringTab;
    private int previousTab;
    private void RuleTabChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.OriginalSource != RuleTabs || restoringTab) return;
        if (viewModel?.HasUnappliedRule == true)
        {
            restoringTab = true; RuleTabs.SelectedIndex = previousTab; restoringTab = false;
            viewModel.ExplainPendingRule(); return;
        }
        previousTab = RuleTabs.SelectedIndex;
    }
    private void SelectNumber(object sender, KeyboardFocusChangedEventArgs e) { if (sender is TextBox box) box.SelectAll(); }
}
