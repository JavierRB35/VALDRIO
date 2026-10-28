using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Input;
namespace Valdrio.Desktop;
public partial class ClassificationReviewPanel : UserControl
{
    private AllocationWorkspaceViewModel? model;
    public ClassificationReviewPanel()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => {
            if (model is not null) model.PropertyChanged -= Changed;
            model = DataContext as AllocationWorkspaceViewModel;
            if (model is not null) model.PropertyChanged += Changed;
            Headers();
        };
    }
    private void Changed(object? sender, PropertyChangedEventArgs e)
    { if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == "Item[]") Headers(); }
    private void Headers()
    {
        if (model is null) return;
        string[] keys = ["CompositionAccount", "CompositionAmount", "CompositionBehavior", "CompositionFixedWeight", "CompositionVariableWeight", "CompositionReview"];
        for (int i=0;i<keys.Length;i++) CompositionGrid.Columns[i].Header=model[keys[i]];
    }
    private void SelectNumber(object sender, KeyboardFocusChangedEventArgs e) { if (sender is TextBox box) box.SelectAll(); }
}
