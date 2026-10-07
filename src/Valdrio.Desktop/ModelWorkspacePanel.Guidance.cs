using System.Windows;
using System.Windows.Controls;

namespace Valdrio.Desktop;

public partial class ModelWorkspacePanel
{
    private void InitializeGuidance()
    {
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is ModelWorkspaceViewModel old) old.PropertyChanged -= GuidanceChanged;
            if (e.NewValue is ModelWorkspaceViewModel vm) vm.PropertyChanged += GuidanceChanged;
            RefreshGuidance();
        };
    }
    private void GuidanceChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => RefreshGuidance();
    private void RefreshGuidance()
    {
        if (DataContext is not ModelWorkspaceViewModel vm) return;
        var keys = new[] { "DraftTab", "MastersTab", "NodesTab", "PublishTab" };
        for (var i = 0; i < 4; i++) ((TabItem)ModelSteps.Items[i]).Header = vm.Guide(keys[i]);
        MastersHelp.Text = vm.Guide("MasterHelp"); NodesHelp.Text = vm.Guide("NodesHelp");
        ModelBack.Content = vm.Guide("Back"); ModelBack.IsEnabled = ModelSteps.SelectedIndex > 0;
        ModelNext.Content = vm.Guide("Next"); ModelNext.Visibility = ModelSteps.SelectedIndex < 3 ? Visibility.Visible : Visibility.Collapsed;
        PublishGuidedButton.Content = vm.Guide("PublishNext");
        PublishGuidedButton.IsEnabled = vm.CanEdit;
    }
    private void ModelStepChanged(object sender, SelectionChangedEventArgs e) { if (e.Source == ModelSteps) RefreshGuidance(); }
    private void ModelPrevious(object sender, RoutedEventArgs e) => ModelSteps.SelectedIndex = Math.Max(0, ModelSteps.SelectedIndex - 1);
    private void ModelContinue(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ModelWorkspaceViewModel vm) return;
        if (ModelSteps.SelectedIndex == 0 && vm.Current is null) { DescriptionBox.Focus(); return; }
        if (ModelSteps.SelectedIndex == 1 && (vm.Centers.Count == 0 || vm.Units.Count == 0)) return;
        ModelSteps.SelectedIndex = Math.Min(3, ModelSteps.SelectedIndex + 1);
    }
    internal void ShowNextRequiredStep()
    {
        if (DataContext is not ModelWorkspaceViewModel vm) return;
        ModelSteps.SelectedIndex = vm.Current is null ? 0 : vm.Centers.Count == 0 || vm.Units.Count == 0 ? 1 : vm.Nodes.Count == 0 ? 2 : 3;
    }
    internal void RevealElement(FrameworkElement element)
    {
        foreach (TabItem tab in ModelSteps.Items)
            if (GuidedUi.Contains(tab, element)) { ModelSteps.SelectedItem = tab; break; }
    }
    private async void SaveValidatePublish(object sender, RoutedEventArgs e)
        => await (PublicationTask = PublishForStudy());
    internal Task? PublicationTask { get; private set; }
    internal async Task PublishForStudy()
    {
        if (DataContext is not ModelWorkspaceViewModel vm || !vm.CanEdit) return;
        if (vm.SaveCommand.CanExecute(null)) await vm.SaveCommand.ExecuteAsync(null);
        if (vm.IsDirty) return;
        if (vm.ValidateCommand.CanExecute(null)) await vm.ValidateCommand.ExecuteAsync(null);
        if (!string.IsNullOrWhiteSpace(vm.ValidationText)) return;
        if (vm.PublishCommand.CanExecute(null)) await vm.PublishCommand.ExecuteAsync(null);
    }
}
