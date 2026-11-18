using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Media;
using Valdrio.Domain;
namespace Valdrio.Desktop;
public partial class CompositionWindow : IntegratedWorkspaceWindow
{
    public CompositionEditor Editor { get; }
    internal Func<MessageBoxResult>? LeaveDecision { get; set; }
    private readonly Dictionary<DataGridColumn, string> columnHelp = new();
    private DataGridCell? lastHelpCell;
    private int activeTab;
    private bool changingTab;
    private EntityId selectedProduct;
    private bool selectingProduct;
    internal void RefreshView() => Columns();
    public CompositionWindow(StudyWorkspaceViewModel host)
    {
        Editor = new(host, initialize: false); InitializeComponent(); DataContext = Editor;
        ContentRendered += async (_, _) => await InitializeAsync();
        Columns();
        ProductTabs.SelectionChanged += ProductTabChanged;
        ProductPicker.SelectionChanged += ProductPickerChanged;
        CatalogGrid.PreviewMouseMove += GridHelpOnMouseMove;
        LinesGrid.PreviewMouseMove += GridHelpOnMouseMove;
        CatalogGrid.AddHandler(Keyboard.GotKeyboardFocusEvent,
            new KeyboardFocusChangedEventHandler(GridHelpOnFocus), true);
        LinesGrid.AddHandler(Keyboard.GotKeyboardFocusEvent,
            new KeyboardFocusChangedEventHandler(GridHelpOnFocus), true);
        Closing += (_, e) => { if (!Resolve()) e.Cancel = true; };
    }
    private Task? initialization;
    internal override Task InitializeAsync() => initialization ??= RunActivityAsync(async () =>
    {
        selectingProduct = true;
        try { await Editor.ReloadForDisplayAsync(); selectedProduct = Editor.Product; }
        finally { selectingProduct = false; }
        Columns();
    });
    private void Columns()
    {
        CatalogGrid.Columns.Clear(); LinesGrid.Columns.Clear(); AnswersGrid.Columns.Clear(); ResultsGrid.Columns.Clear();
        columnHelp.Clear(); lastHelpCell = null;
        Text(CatalogGrid,"Code","Code",120); Text(CatalogGrid,"Name","Name",250);
        Choice(CatalogGrid,"Kind","Kind",Editor.Kinds,140); Choice(CatalogGrid,"Unit","Unit",Editor.Units,160);
        CatalogGrid.Columns.Add(new DataGridCheckBoxColumn { Header=Editor["Active"],Binding=new Binding("Active"),Width=85 });
        // A string edit buffer preserves an invalid input for correction and
        // parses both decimal comma and numeric-keypad dot, without truncation.
        CatalogGrid.Columns.Add(new DataGridTextColumn { Header=Editor["ReferenceUnitCost"],
            Binding=new Binding("ReferenceUnitCostInput") { UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged },
            IsReadOnly=false, Width=150 });
        CatalogGrid.Columns.Add(new DataGridTextColumn { Header=Editor["ReferenceCostSource"],
            Binding=new Binding("CostOriginDisplay"), IsReadOnly=true, Width=180 });
        var action = new FrameworkElementFactory(typeof(Button));
        action.SetValue(Button.ContentProperty, Editor["EditComposition"]);
        action.SetBinding(Button.IsEnabledProperty, new Binding("CanOpenComposition"));
        action.AddHandler(Button.ClickEvent, new RoutedEventHandler(EditComposition));
        CatalogGrid.Columns.Add(new DataGridTemplateColumn { Header=Editor["Recipe"], CellTemplate=new DataTemplate { VisualTree=action }, Width=180, DisplayIndex=0 });
        LinesGrid.Columns.Add(new DataGridComboBoxColumn { Header=Editor["Component"],SelectedItemBinding=new Binding("Target") { UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged }, ItemsSource=Editor.Targets,DisplayMemberPath="Label",Width=270 });
        Text(LinesGrid,"Quantity","Quantity",90); Choice(LinesGrid,"Unit","Unit",Editor.Units,140); Text(LinesGrid,"Factor","Factor",90);
        Choice(LinesGrid,"Basis","Basis",Editor.Bases,140); Choice(LinesGrid,"Loss","Loss",Editor.Losses,140); Text(LinesGrid,"Percent","Percent",80);
        var factorColumn = new DataGridTextColumn { Header=Editor["LossFactor"],
            Binding=new Binding("LossFactorDisplay"), IsReadOnly=true, Width=120 };
        var factorStyle = new Style(typeof(TextBlock));
        factorStyle.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, new Binding("LossFormulaText")));
        factorColumn.ElementStyle = factorStyle; LinesGrid.Columns.Add(factorColumn);
        LinesGrid.Columns.Add(new DataGridTextColumn { Header = Editor["AdjustedInput"], Binding = new Binding("AdjustedQuantityDisplay"), IsReadOnly = true, Width = 140 });
        Text(AnswersGrid,"Question","Question",250); Text(AnswersGrid,"Answer","Answer",new DataGridLength(1,DataGridLengthUnitType.Star));
        Text(ResultsGrid,"Path","Path",180); Text(ResultsGrid,"Component","Name",230); Text(ResultsGrid,"Kind","Kind",100);
        Text(ResultsGrid,"Count","Count",90); Text(ResultsGrid,"InputUnit","InputUnit",100);
        Text(ResultsGrid,"Converted","Converted",110); Text(ResultsGrid,"Base","Base",100); Text(ResultsGrid,"Adjusted","Adjusted",100); Text(ResultsGrid,"Required","Required",100); Text(ResultsGrid,"Unit","Unit",70);
        CatalogGrid.IsReadOnly=!Editor.Writable;
        string[] catalogKeys = ["CatalogCode", "CatalogName", "CatalogKind", "CatalogUnit",
            "CatalogActive", "CatalogCost", "CatalogOrigin", "OpenProduct"];
        for (var i = 0; i < catalogKeys.Length; i++)
            columnHelp[CatalogGrid.Columns[i]] = catalogKeys[i];
        string[] lineKeys = ["Component", "Quantity", "LineUnit", "ConversionFactor",
            "Basis", "Loss", "Percent", "LossFactor", "AdjustedInput"];
        for (var i = 0; i < lineKeys.Length; i++)
            columnHelp[LinesGrid.Columns[i]] = lineKeys[i];
    }
    private void GridHelpOnMouseMove(object sender, MouseEventArgs e) =>
        ShowGridHelp(e.OriginalSource as DependencyObject);
    private void GridHelpOnFocus(object sender, KeyboardFocusChangedEventArgs e) =>
        ShowGridHelp(e.OriginalSource as DependencyObject);
    private void ShowGridHelp(DependencyObject? source)
    {
        while (source is not null && source is not DataGridCell)
            source = VisualTreeHelper.GetParent(source);
        if (source is not DataGridCell cell || ReferenceEquals(cell, lastHelpCell) ||
            !columnHelp.TryGetValue(cell.Column, out var key)) return;
        lastHelpCell = cell;
        var explanation = Editor["Help." + key];
        cell.ToolTip = explanation;
        AutomationProperties.SetHelpText(cell, explanation);
        (WorkspaceHost ?? Owner as MainWindow ?? System.Windows.Application.Current.MainWindow as MainWindow)
            ?.ShowFieldHelp(explanation, "Composition." + key);
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
        if (valid) foreach (var item in Editor.Items) item.NormalizeCostInput();
        return valid;
    }
    private bool Resolve()
    {
        if (changingTab || selectingProduct || Editor.Busy) return false;
        if (!Commit()) return false;
        if (!Editor.Dirty) return true;
        var answer = LeaveDecision?.Invoke() ?? ResolvePending();
        if(answer==MessageBoxResult.Cancel) return false;
        if(answer==MessageBoxResult.Yes) return Editor.SaveChanges();
        Editor.Discard(); return true;
    }
    internal override Task<bool> CanLeaveAsync() => ResolveAsync();
    private async Task<bool> ResolveAsync(bool ownedByTabChange = false, bool ownedByProductChange = false)
    {
        // A pending tab/product change already owns the "unsaved changes"
        // dialog; a concurrent close must not open a second one or save twice.
        if (!ownedByTabChange && changingTab) return false;
        if (!ownedByProductChange && selectingProduct) return false;
        if (OperationRunning || Editor.Busy || !Commit()) return false;
        if (!Editor.Dirty) return true;
        var answer = LeaveDecision?.Invoke() ?? ResolvePending();
        if (answer == MessageBoxResult.Cancel) return false;
        var resolved = false;
        await RunActivityAsync(async () =>
        {
            if (answer == MessageBoxResult.Yes) resolved = await Editor.SaveChangesAsync();
            else { await Editor.DiscardAsync(); resolved = true; }
        });
        return resolved;
    }
    private async void ProductPickerChanged(object sender, SelectionChangedEventArgs e)
    {
        if (selectingProduct || changingTab || e.OriginalSource != ProductPicker) return;
        var requested = Editor.Product;
        if (requested == selectedProduct) return;
        selectingProduct = true;
        try
        {
            // Keep the loaded document selected until its changes are resolved:
            // SaveChanges must see Editable=true for the document being saved.
            ProductPicker.SelectedValue = selectedProduct;
            if (!await ResolveAsync(ownedByProductChange: true)) return;
            selectedProduct = requested;
            ProductPicker.SelectedValue = requested;
        }
        finally { selectingProduct = false; }
    }
    private bool OpenProduct(EntityId id)
    {
        selectingProduct = true;
        try
        {
            var opened = Editor.OpenProduct(id);
            selectedProduct = Editor.Product;
            return opened;
        }
        finally { selectingProduct = false; }
    }
    private async void ProductTabChanged(object sender, SelectionChangedEventArgs e)
    {
        if (changingTab || e.OriginalSource != ProductTabs) return;
        var requested = ProductTabs.SelectedIndex;
        if (requested == activeTab) return;
        if (selectingProduct) { ProductTabs.SelectedIndex = activeTab; return; }
        changingTab = true;
        try
        {
            ProductTabs.SelectedIndex = activeTab;
            if (!await ResolveAsync(ownedByTabChange: true)) return;
            activeTab = requested;
            ProductTabs.SelectedIndex = requested;
            Columns();
        }
        finally { changingTab = false; }
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
    private async void InstallUnits(object sender, RoutedEventArgs e)
    {
        if (!await ResolveAsync()) return;
        if (await Editor.InstallDefaultUnitsAsync()) Columns();
    }
    private async void ImportProducts(object sender,RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = Editor["ImportFileFilter"], CheckFileExists = true };
        if (Commit() && dialog.ShowDialog(WorkspaceHost ?? (Window)this) == true) await RunActivityAsync(() => Editor.ImportProductsAsync(dialog.FileName));
    }
    private async void EditComposition(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: CompositionItemRow row } || !row.CanOpenComposition) return;
        var id = row.Id;
        if (!await ResolveAsync()) return;
        var saved = Editor.Items.FirstOrDefault(i => i.Id == id);
        if (saved is null || !saved.CanOpenComposition)
        { Editor.Feedback = Editor[saved is { IsCostInputValid: false } ? "InvalidItem" : "ChooseRecipe"]; return; }
        if (saved.Active && saved.ReferenceUnitCost is > 0 &&
            !saved.ReferenceCostSource.StartsWith("Composition · ", StringComparison.Ordinal))
        {
            var warning = new EconomicConfirmationWindow(Editor["CostOverrideTitle"],
                Editor["CostOverrideHelp"], Editor["CostOverrideContinue"], Editor.ActionLabel("KeepEditing"))
                { Owner = WorkspaceHost ?? (Window)this };
            if (warning.ShowDialog() != true) return;
        }
        if (OpenProduct(id)) { ProductTabs.SelectedIndex=1; Columns(); }
        else Editor.Feedback=Editor["ChooseRecipe"];
    }
    private void RemoveItem(object sender,RoutedEventArgs e) { if (Commit()) Editor.RemoveItem(CatalogGrid.SelectedItem as CompositionItemRow); }
    private async Task SavePage(bool publish)
    {
        await RunActivityAsync(async () =>
        {
            var product = Editor.Product;
            if (await Editor.SaveAsync(publish) && publish) OpenProduct(product);
            Columns();
        });
    }
    private async void Save(object sender,RoutedEventArgs e) { if(Commit()) await SavePage(Editor.HasCompositionChanges); }
    private async void SaveComposition(object sender,RoutedEventArgs e) { if(Commit()) await SavePage(true); }
    private async void Publish(object sender,RoutedEventArgs e) { if(Commit() && new EconomicConfirmationWindow(Editor["Publish"], Editor.State + "\n\n" + Editor["PublishHelp"], Editor["Publish"], Editor.ActionLabel("KeepEditing")) { Owner = WorkspaceHost ?? (Window)this }.ShowDialog()==true) await SavePage(true); }
    private async void Open(object sender,RoutedEventArgs e)
    {
        var product=Editor.Product;
        if(await ResolveAsync() && ConfirmCompositionCost(product) && OpenProduct(product)) Columns();
    }
    private bool ConfirmCompositionCost(EntityId product)
    {
        var saved = Editor.Items.FirstOrDefault(i => i.Id == product);
        if (saved is null || !saved.CanOpenComposition)
        {
            Editor.Feedback = Editor[saved is { IsCostInputValid: false } ? "InvalidItem" : "ChooseRecipe"];
            return false;
        }
        if (saved.ReferenceUnitCost is not > 0 || !saved.Active ||
            saved.ReferenceCostSource.StartsWith("Composition · ", StringComparison.Ordinal)) return true;
        return new EconomicConfirmationWindow(Editor["CostOverrideTitle"], Editor["CostOverrideHelp"],
            Editor["CostOverrideContinue"], Editor.ActionLabel("KeepEditing"))
            { Owner = WorkspaceHost ?? (Window)this }.ShowDialog() == true;
    }
    private async void NewVersion(object sender,RoutedEventArgs e) { var product=Editor.Product; if(await ResolveAsync()) { Editor.Product=product; Editor.NewVersion(); Columns(); } }
    private void AddLine(object sender,RoutedEventArgs e) => Editor.AddLine();
    private void RemoveLine(object sender,RoutedEventArgs e) { if(Commit()) Editor.RemoveLine(LinesGrid.SelectedItem as CompositionLineRow); }
    private async void Preview(object sender,RoutedEventArgs e) { if(Commit()) await RunActivityAsync(Editor.PreviewAsync); }
    private async void Reload(object sender,RoutedEventArgs e) { if(await ResolveAsync()) await RunActivityAsync(async () => { await Editor.ReloadForDisplayAsync(); Columns(); }); }
    private void ClosePage(object sender,RoutedEventArgs e) => Close();
}
