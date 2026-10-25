using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
namespace Valdrio.Desktop;
public partial class EstimateWindow : IntegratedWorkspaceWindow
{
    public EstimateEditor Editor { get; }
    internal Func<bool>? DiscardDecision { get; set; }
    public EstimateWindow(StudyWorkspaceViewModel host)
    {
        Editor = new(host); InitializeComponent(); DataContext = Editor;
        Column(AnswersGrid,"Question","Question",250); Column(AnswersGrid,"Answer","Answer",new(1,DataGridLengthUnitType.Star));
        Column(DetailsGrid,"Component","Name",230); Column(DetailsGrid,"Quantity","Quantity",120);
        Column(DetailsGrid,"Rate","Rate",160); Column(DetailsGrid,"Total","Amount",150); Column(DetailsGrid,"Sources","Source",230);
        Closing += (_, e) => { if (!Resolve()) e.Cancel = true; };
    }
    private Task? initialization;
    internal Task InitializeAsync() => initialization ??= Run(Editor.LoadAsync);
    protected override async void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e); await InitializeAsync();
    }
    private void Column(DataGrid grid,string key,string path,DataGridLength width)
    {
        var style=new Style(typeof(TextBlock)); style.Setters.Add(new Setter(TextBlock.TextWrappingProperty,TextWrapping.Wrap));
        grid.Columns.Add(new DataGridTextColumn{ Header=Editor[key],Binding=new Binding(path),Width=width,ElementStyle=style });
    }
    private bool Resolve()
    {
        if (Editor.Busy) return false;
        if (!Editor.Dirty) return true;
        if (DiscardDecision is not null) return DiscardDecision();
        return new EconomicConfirmationWindow(Editor["Title"],Editor["Leave"],Editor.Action("Discard"),Editor.Action("KeepEditing"))
            { Owner=WorkspaceHost ?? (Window)this }.ShowDialog()==true;
    }
    private async Task Run(Func<Task> action)
    {
        var surface=Surface ?? (FrameworkElement)Content; surface.IsEnabled=false;
        try { await action(); } finally { surface.IsEnabled=true; }
    }
    private void New(object sender,RoutedEventArgs e) { if(Resolve()) Editor.New(); }
    private void Revise(object sender,RoutedEventArgs e) { if(Resolve()) Editor.Revise(); }
    private async void Preview(object sender,RoutedEventArgs e) => await Run(Editor.PreviewAsync);
    private async void Accept(object sender,RoutedEventArgs e) => await Run(async () => await Editor.AcceptAsync());
    private void ShowHistory(object sender,RoutedEventArgs e) => Editor.ShowHistory();
    private void Compare(object sender,RoutedEventArgs e) => Editor.Compare();
    private void ClosePage(object sender,RoutedEventArgs e) => Close();
}
