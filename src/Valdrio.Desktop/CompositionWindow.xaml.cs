using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Valdrio.Domain;
namespace Valdrio.Desktop;
public partial class CompositionWindow : IntegratedWorkspaceWindow
{
    public CompositionEditor Editor { get; }
    internal Func<MessageBoxResult>? LeaveDecision { get; set; }
    internal void RefreshView() => Columns();
    public CompositionWindow(StudyWorkspaceViewModel host)
    {
        Editor = new(host, initialize: false); InitializeComponent(); DataContext = Editor;
        ContentRendered += async (_, _) => await InitializeAsync();
        Columns(); Closing += (_, e) => { if (!Resolve()) e.Cancel = true; };
    }
    private Task? initialization;
    internal override Task InitializeAsync() => initialization ??= RunActivityAsync(async () => { await Editor.ReloadForDisplayAsync(); Columns(); });
    private void Columns()
    {
        CatalogGrid.Columns.Clear(); LinesGrid.Columns.Clear(); AnswersGrid.Columns.Clear(); ResultsGrid.Columns.Clear();
        Text(CatalogGrid,"Code","Code",120); Text(CatalogGrid,"Name","Name",250);
        Choice(CatalogGrid,"Kind","Kind",Editor.Kinds,140); Choice(CatalogGrid,"Unit","Unit",Editor.Units,160);
        CatalogGrid.Columns.Add(new DataGridCheckBoxColumn { Header=Editor["Active"],Binding=new Binding("Active"),Width=85 });
        CatalogGrid.Columns.Add(new DataGridTextColumn { Header=Editor["ReferenceUnitCost"], Binding=new Binding("ReferenceUnitCost") { UpdateSourceTrigger=UpdateSourceTrigger.LostFocus, ValidatesOnExceptions=true, ConverterCulture=System.Globalization.CultureInfo.GetCultureInfo(Editor.NumericCulture) }, IsReadOnly=false, Width=130 });
        CatalogGrid.Columns.Add(new DataGridTextColumn { Header=Editor["ReferenceCostSource"], Binding=new Binding("ReferenceCostSource"), IsReadOnly=true, Width=220 });
        var action = new FrameworkElementFactory(typeof(Button));
        action.SetValue(Button.ContentProperty, Editor["EditComposition"]);
        action.AddHandler(Button.ClickEvent, new RoutedEventHandler(EditComposition));
        CatalogGrid.Columns.Add(new DataGridTemplateColumn { Header=Editor["Recipe"], CellTemplate=new DataTemplate { VisualTree=action }, Width=180, DisplayIndex=0 });
        LinesGrid.Columns.Add(new DataGridComboBoxColumn { Header=Editor["Component"],SelectedItemBinding=new Binding("Target") { UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged }, ItemsSource=Editor.Targets,DisplayMemberPath="Label",Width=270 });
        Text(LinesGrid,"Quantity","Quantity",90); Choice(LinesGrid,"Unit","Unit",Editor.Units,140); Text(LinesGrid,"Factor","Factor",90);
        Choice(LinesGrid,"Basis","Basis",Editor.Bases,140); Choice(LinesGrid,"Loss","Loss",Editor.Losses,140); Text(LinesGrid,"Percent","Percent",80);
        Text(AnswersGrid,"Question","Question",250); Text(AnswersGrid,"Answer","Answer",new DataGridLength(1,DataGridLengthUnitType.Star));
        Text(ResultsGrid,"Path","Path",180); Text(ResultsGrid,"Component","Name",230); Text(ResultsGrid,"Kind","Kind",100);
        Text(ResultsGrid,"Count","Count",90); Text(ResultsGrid,"InputUnit","InputUnit",100);
        Text(ResultsGrid,"Converted","Converted",110); Text(ResultsGrid,"Base","Base",100); Text(ResultsGrid,"Adjusted","Adjusted",100); Text(ResultsGrid,"Required","Required",100); Text(ResultsGrid,"Unit","Unit",70);
        CatalogGrid.IsReadOnly=!Editor.Writable;
    }
    private void Text(DataGrid grid,string label,string path,DataGridLength width)
    {
        var style = new Style(typeof(TextBlock)); style.Setters.Add(new Setter(TextBlock.TextWrappingProperty,TextWrapping.Wrap));
        grid.Columns.Add(new DataGridTextColumn { Header=Editor[label],Binding=new Binding(path) { UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged },Width=width,ElementStyle=style });
    }
    private void Choice(DataGrid grid,string label,string path,System.Collections.IEnumerable options,double width) => grid.Columns.Add(new DataGridComboBoxColumn { Header=Editor[label],SelectedValueBinding=new Binding(path) { UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged },SelectedValuePath="Value",DisplayMemberPath="Label",ItemsSource=options,Width=width });
    private bool Commit()
    {
        var valid = CatalogGrid.CommitEdit(DataGridEditingUnit.Cell,true) && CatalogGrid.CommitEdit(DataGridEditingUnit.Row,true) && LinesGrid.CommitEdit(DataGridEditingUnit.Cell,true) && LinesGrid.CommitEdit(DataGridEditingUnit.Row,true);
        if (!valid) Editor.Feedback = Editor["InvalidCell"];
        return valid;
    }
    private bool Resolve()
    {
        if (Editor.Busy) return false;
        if (!Commit()) return false;
        if (!Editor.Dirty) return true;
        var answer = LeaveDecision?.Invoke() ?? ResolvePending();
        if(answer==MessageBoxResult.Cancel) return false;
        if(answer==MessageBoxResult.Yes) return Editor.Save(false);
        Editor.Reload(); return true;
    }
    internal override Task<bool> CanLeaveAsync() => ResolveAsync();
    private async Task<bool> ResolveAsync()
    {
        if (OperationRunning || Editor.Busy || !Commit()) return false;
        if (!Editor.Dirty) return true;
        var answer = LeaveDecision?.Invoke() ?? ResolvePending();
        if (answer == MessageBoxResult.Cancel) return false;
        var resolved = false;
        await RunActivityAsync(async () =>
        {
            if (answer == MessageBoxResult.Yes) resolved = await Editor.SaveAsync(false);
            else { await Editor.ReloadAsync(); resolved = true; }
        });
        return resolved;
    }
    private MessageBoxResult ResolvePending()
    {
        var dialog = new PendingChangesWindow(Editor["Title"], Editor["Leave"], Editor.ActionLabel("SaveContinue"),
            Editor.ActionLabel("Discard"), Editor.ActionLabel("KeepEditing")) { Owner = WorkspaceHost ?? (Window)this };
        dialog.ShowDialog();
        return dialog.Choice switch { PendingEditChoice.Save => MessageBoxResult.Yes,
            PendingEditChoice.Discard => MessageBoxResult.No, _ => MessageBoxResult.Cancel };
    }
    private void CatalogBeginningEdit(object? sender,DataGridBeginningEditEventArgs e)
    { if(e.Row.Item is CompositionItemRow { Persisted:true } && CatalogGrid.Columns.IndexOf(e.Column) is not (1 or 4 or 5)) e.Cancel=true; }
    private void AddItem(object sender,RoutedEventArgs e) { Editor.AddItem(); CatalogGrid.ScrollIntoView(Editor.Items.Last()); }
    private async void ImportProducts(object sender,RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "CSV/TSV (*.csv;*.tsv;*.txt)|*.csv;*.tsv;*.txt|Todos los archivos (*.*)|*.*", CheckFileExists = true };
        if (Commit() && dialog.ShowDialog(WorkspaceHost ?? (Window)this) == true) await RunActivityAsync(() => Editor.ImportProductsAsync(dialog.FileName));
    }
    private async void EditComposition(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: CompositionItemRow row }) return;
        var id = row.Id;
        if (!await ResolveAsync()) return;
        if (Editor.OpenProduct(id)) { ProductTabs.SelectedIndex=1; Columns(); }
        else Editor.Feedback=Editor["ChooseRecipe"];
    }
    private void RemoveItem(object sender,RoutedEventArgs e) { if (Commit()) Editor.RemoveItem(CatalogGrid.SelectedItem as CompositionItemRow); }
    private async Task SavePage(bool publish)
    {
        await RunActivityAsync(async () => { await Editor.SaveAsync(publish); Columns(); });
    }
    private async void Save(object sender,RoutedEventArgs e) { if(Commit()) await SavePage(false); }
    private async void Publish(object sender,RoutedEventArgs e) { if(Commit() && new EconomicConfirmationWindow(Editor["Publish"], Editor.State + "\n\n" + Editor["PublishHelp"], Editor["Publish"], Editor.ActionLabel("KeepEditing")) { Owner = WorkspaceHost ?? (Window)this }.ShowDialog()==true) await SavePage(true); }
    private async void Open(object sender,RoutedEventArgs e) { var product=Editor.Product; var version=Editor.Version; if(await ResolveAsync()) { Editor.Product=product; Editor.Version=version; Editor.Open(); Columns(); } }
    private async void NewVersion(object sender,RoutedEventArgs e) { var product=Editor.Product; if(await ResolveAsync()) { Editor.Product=product; Editor.NewVersion(); Columns(); } }
    private void AddLine(object sender,RoutedEventArgs e) => Editor.AddLine();
    private void RemoveLine(object sender,RoutedEventArgs e) { if(Commit()) Editor.RemoveLine(LinesGrid.SelectedItem as CompositionLineRow); }
    private async void Preview(object sender,RoutedEventArgs e) { if(Commit()) await RunActivityAsync(Editor.PreviewAsync); }
    private async void Reload(object sender,RoutedEventArgs e) { if(await ResolveAsync()) await RunActivityAsync(async () => { await Editor.ReloadForDisplayAsync(); Columns(); }); }
    private void ClosePage(object sender,RoutedEventArgs e) => Close();
}
