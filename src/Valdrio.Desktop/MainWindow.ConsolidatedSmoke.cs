using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Valdrio.Domain;
using Valdrio.Infrastructure;
using Valdrio.Infrastructure.CompanyFiles;
namespace Valdrio.Desktop;
public partial class MainWindow
{
    private static T? ConsolidatedFind<T>(DependencyObject root) where T:DependencyObject
    {
        if(root is T match)return match;
        for(int i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);i++)
            if(ConsolidatedFind<T>(System.Windows.Media.VisualTreeHelper.GetChild(root,i)) is { } found)return found;
        return null;
    }
    internal async Task RunConsolidatedChecks(string directory, string source)
    {
        Directory.CreateDirectory(directory);
        var checks = new List<string>();
        void Check(bool ok, string label) { if (!ok) throw new InvalidOperationException(label); checks.Add(label); File.WriteAllText(Path.Combine(directory,"progress.json"),JsonSerializer.Serialize(checks)); }
        var path=Path.Combine(directory,"disposable.valdrio");
        var service=new SqliteCompanyFileService(new JsonCurrencyCatalog(Path.Combine(AppContext.BaseDirectory,"reference","currency-catalog-example.json")),new SystemClock());
        using (var original = service.Open(source, Valdrio.Application.CompanyFileOpenMode.ReadOnly)) original.Backup(path);
        var main=viewModel!; var company=main.Company!; var studies=main.Studies!;
        await company.OpenPath(path);
        Check(company.Snapshot is not null,"Disposable project opens: "+company.Status);
        studies.StudyChoice=studies.Studies.First(); await studies.LoadCommand.ExecuteAsync(null);
        Check(studies.CanOpenPhysical,"Selected analysis is usable");
        foreach(var language in new[]{"es","en"})
        {
            main.ApplyPresentationPreset(language,language=="es"?"es-ES":"en-US","UTC",false,false);
            var page=new CompositionWindow(studies){Owner=this};page.ShowIntegrated(); await page.InitializeAsync();
            var editor=page.Editor;
            editor.AddItem();var item=editor.Items.Last();item.Code="CHECK-"+language;item.Name="Producto de prueba "+language;item.ReferenceUnitCost=12.5m;
            Check(await editor.SaveAsync(false),"Manual reference cost saves "+language+": "+editor.Feedback);
            var saved=editor.Items.Single(i=>i.Id==item.Id);Check(saved.ReferenceUnitCost==12.5m,"Cost survives reload "+language);
            saved.ReferenceUnitCost=13.75m;Check(await editor.SaveAsync(false),"Existing reference cost edits "+language);
            Check(editor.OpenProduct(item.Id),"Product ID opens composition "+language);
            Check(editor.Editable && editor.Product==item.Id,"New draft belongs to selected product "+language);
            Check(await editor.SaveAsync(false),"Draft persists "+language);
            Check(editor.OpenProduct(item.Id) && editor.Editable,"Existing draft loads automatically "+language);
            page.RefreshView();
            var grid=(DataGrid)page.FindName("CatalogGrid");
            Check(!grid.Columns[5].IsReadOnly,"Cost cell is editable "+language);
            var priceRow=editor.Items.Single(i=>i.Id==item.Id);
            grid.ScrollIntoView(priceRow,grid.Columns[5]);grid.CurrentCell=new DataGridCellInfo(priceRow,grid.Columns[5]);
            grid.Focus();Check(grid.BeginEdit(),"Existing price begins cell edit "+language);
            if(grid.Columns[5].GetCellContent(priceRow) is TextBox priceBox)
            {
                priceBox.Text=language=="es"?"14,25":"14.25";
                priceBox.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
            }
            Check(grid.CommitEdit(DataGridEditingUnit.Cell,true) && grid.CommitEdit(DataGridEditingUnit.Row,true),"Price cell commits "+language);
            Check(priceRow.ReferenceUnitCost==14.25m && await editor.SaveAsync(false),"Localized price edit persists "+language);
            var csv=Path.Combine(directory,"catalog-"+language+".csv");
            File.WriteAllText(csv,"Referencia;Nombre;Unidad de medida;Coste unitario\nIMPORT-"+language+";Importado;"+company.Snapshot!.Units.First(u=>u.Id==item.Unit).Code+";"+(language=="es"?"2,50":"2.50"));
            editor.ImportProducts(csv);Check(editor.Items.Any(i=>i.Code=="IMPORT-"+language && i.ReferenceUnitCost==2.5m),"Import preview "+language+": "+editor.Feedback);
            Check(await editor.SaveAsync(false),"Imported catalogue persists "+language);
            foreach(var width in new[]{1536d,1000d})
            {
                Width=width;Height=840;((TabControl)page.FindName("ProductTabs")).SelectedIndex=0;
                UpdateLayout();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
                Check(grid.ActualWidth>400,"Catalogue usable width "+language+width);
                VisualEvidenceCapture.Capture(this,Path.Combine(directory,"catalog-"+language+"-"+width+".png"));
                ((TabControl)page.FindName("ProductTabs")).SelectedIndex=1;UpdateLayout();
                VisualEvidenceCapture.Capture(this,Path.Combine(directory,"composition-"+language+"-"+width+".png"));
            }
            page.Close();
            var rates=new PrincipalRatesWindow(studies){Owner=this};rates.ShowIntegrated(); await rates.InitializeAsync();UpdateLayout();
            Check(rates.Editor.Report is not null,"Rates load without manual refresh "+language+": "+rates.Editor.Feedback);
            VisualEvidenceCapture.Capture(this,Path.Combine(directory,"rates-"+language+".png"));rates.Close();
            await NavigateWorkflow(8);await main.CostFlows!.EnterAsync();main.CostFlows.ShowAuxiliaryReport=true;UpdateLayout();
            var reportPanel=ConsolidatedFind<CostFlowPanel>(this);
            if(reportPanel?.FindName("AuxiliaryReportGrid") is DataGrid reportGrid) reportGrid.BringIntoView();
            await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
            Check(!AuxiliaryStep.IsVisible,"Auxiliary menu removed "+language);
            VisualEvidenceCapture.Capture(this,Path.Combine(directory,"auxiliary-report-"+language+".png"));
            foreach (var kind in new[]{"activity","capacity","orders"})
            {
                IntegratedWorkspaceWindow area=kind switch { "activity"=>new PhysicalActivityWindow(studies),
                    "capacity"=>new PhysicalCapacityAssistantWindow(new PhysicalActivityEditor(studies)), _=>new EstimateWindow(studies) };
                area.Owner=this;area.ShowIntegrated();await area.InitializeAsync();
                if(area is EstimateWindow estimate) await estimate.InitializeAsync();
                foreach(var width in new[]{1536d,1000d})
                {
                    Width=width;Height=840;UpdateLayout();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
                    Check(area.Surface!.ActualHeight>300,"Usable work area "+kind+language+width);
                    VisualEvidenceCapture.Capture(this,Path.Combine(directory,kind+"-"+language+"-"+width+".png"));
                }
                area.Surface!.LayoutTransform=new System.Windows.Media.ScaleTransform(1.5,1.5);UpdateLayout();
                await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
                VisualEvidenceCapture.Capture(this,Path.Combine(directory,kind+"-"+language+"-scale150.png"));
                area.Surface.LayoutTransform=null;area.Close();
            }
            foreach(var step in new[]{5,7})
            {
                await NavigateWorkflow(step);
                var allocation=step==5?AllocationPanel:(AllocationWorkspacePanel)SecondaryPanel.FindName("Workspace");
                var tabs=ConsolidatedFind<TabControl>(allocation)!;tabs.SelectedIndex=1;
                ((Expander)allocation.FindName("LibrarySection")).IsExpanded=true;
                ((Expander)allocation.FindName("DefinitionSection")).IsExpanded=true;
                foreach(var width in new[]{1536d,1000d})
                {
                    Width=width;UpdateLayout();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
                    var destinations=(DataGrid)allocation.FindName("LinesGrid");
                    Check(destinations.ActualHeight>=200,"Destination editor grows "+step+language+width);
                    destinations.BringIntoView();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
                    VisualEvidenceCapture.Capture(this,Path.Combine(directory,"keys-"+step+"-"+language+"-"+width+".png"));
                }
            }

        }
        await company.CloseCommand.ExecuteAsync(null);await company.OpenPath(path);
        Check(company.ActiveCompositions!.ReadCompositions().Items.Count(i=>i.Code.StartsWith("CHECK-")||i.Code.StartsWith("IMPORT-"))==4,"Catalogue and import survive closing the file");
        File.WriteAllText(Path.Combine(directory,"checks.json"),JsonSerializer.Serialize(checks,new JsonSerializerOptions{WriteIndented=true}));
    }
}
