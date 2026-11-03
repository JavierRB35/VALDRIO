using System.Collections;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Valdrio.Infrastructure;
using Valdrio.Infrastructure.CompanyFiles;
namespace Valdrio.Desktop;
public partial class MainWindow
{
    private static IEnumerable<T> ScrollDescendants<T>(DependencyObject root) where T:DependencyObject
    {
        if(root is T found) yield return found;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)
            foreach(var child in ScrollDescendants<T>(VisualTreeHelper.GetChild(root,i))) yield return child;
    }
    internal async Task RunSingleScrollChecks(string directory,string source,bool extrasOnly=false)
    {
        Directory.CreateDirectory(directory);
        var checks=new List<string>();
        void Check(bool ok,string label)
        {
            if(!ok) throw new InvalidOperationException(label);
            checks.Add(label);File.WriteAllText(Path.Combine(directory,"progress.json"),JsonSerializer.Serialize(checks));
        }
        async Task Idle(){await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);UpdateLayout();}
        var path=Path.Combine(directory,"disposable.valdrio");
        var service=new SqliteCompanyFileService(new JsonCurrencyCatalog(Path.Combine(AppContext.BaseDirectory,"reference","currency-catalog-example.json")),new SystemClock());
        using(var original=service.Open(source,Valdrio.Application.CompanyFileOpenMode.ReadOnly)) original.Backup(path);
        var main=viewModel!;var studies=main.Studies!;
        await main.Company!.OpenPath(path);studies.StudyChoice=studies.Studies.First();await studies.LoadCommand.ExecuteAsync(null);
        async Task Verify(FrameworkElement area,ScrollViewer page,string label)
        {
            await Idle();
            foreach(var grid in ScrollDescendants<DataGrid>(area).Where(g=>g.IsVisible).ToArray())
            {
                Check(ScrollViewer.GetVerticalScrollBarVisibility(grid)==ScrollBarVisibility.Disabled,label+" natural table "+grid.Name);
                Check(double.IsNaN(grid.Height)&&double.IsPositiveInfinity(grid.MaxHeight),label+" unbounded table "+grid.Name);
            }
            var table=ScrollDescendants<DataGrid>(area).FirstOrDefault(g=>g.IsVisible&&g.Items.Count>0);
            if(table is not null)
            {
                var binding=BindingOperations.GetBindingBase(table,ItemsControl.ItemsSourceProperty);
                var original=table.ItemsSource;
                var item=table.Items.Cast<object>().First();
                table.ItemsSource=Enumerable.Repeat(item,60).ToArray();await Idle();
                Check(table.ActualHeight>1000,label+" table grows for 60 rows");
                page.ScrollToTop();await Idle();table.Focus();page.ScrollToTop();await Idle();
                var y=page.VerticalOffset;
                table.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice,Environment.TickCount,-120){RoutedEvent=PreviewMouseWheelEvent,Source=table});await Idle();
                Check(page.VerticalOffset>y,label+" focused table wheel scrolls page");
                if(binding is not null) BindingOperations.SetBinding(table,ItemsControl.ItemsSourceProperty,binding);else table.ItemsSource=original;
                await Idle();
            }
            bool InScroll(DependencyObject node)
            {
                for(var parent=VisualTreeHelper.GetParent(node);parent is not null;parent=VisualTreeHelper.GetParent(parent))
                    if(parent is ScrollViewer viewer && viewer.VerticalScrollBarVisibility!=ScrollBarVisibility.Disabled) return true;
                return false;
            }
            var buttons=ScrollDescendants<Button>(ActionFooter).Concat(ScrollDescendants<Button>(area).Where(b=>!InScroll(b)))
                .Where(b=>b.IsVisible&&b.Content is not null).Distinct().ToArray();
            page.ScrollToTop();await Idle();var positions=buttons.Select(b=>b.TranslatePoint(new Point(),this).Y).ToArray();
            page.ScrollToBottom();await Idle();
            for(int i=0;i<buttons.Length;i++)
            {
                var b=buttons[i];var y=b.TranslatePoint(new Point(),this).Y;
                Check(Math.Abs(y-positions[i])<1&&y>=0&&y+b.ActualHeight<ActualHeight,label+" fixed action "+b.Content);
                var x=b.TranslatePoint(new Point(),this).X;
                Check(x>=0&&x+b.ActualWidth<=ActualWidth,label+" action fits width "+b.Content);
            }
            page.ScrollToTop();await Idle();
        }
        foreach(var language in new[]{"es","en"})
        {
            main.ApplyPresentationPreset(language,language=="es"?"es-ES":"en-US","UTC",false,false);
            foreach(var width in new[]{1536d,1000d})
            {
                Width=width;Height=900;await Idle();
                foreach(var step in (extrasOnly?Array.Empty<int>():new[]{1,2,4,5,7,8}))
                {
                    await NavigateWorkflow(step);await Idle();
                    FrameworkElement area=step switch {1=>ModelPanel,2=>StudyPanel,4=>ResourcePanel,5=>AllocationPanel,7=>SecondaryPanel,_=>CostFlowsPanel};
                    var tabs=ScrollDescendants<TabControl>(area).FirstOrDefault();
                    var count=tabs?.Items.Count??1;
                    for(int index=0;index<count;index++)
                    {
                        if(tabs is not null){tabs.SelectedIndex=index;await Idle();}
                        var label=language+"-"+width+"-step"+step+"-tab"+index;
                        await Verify(area,RootScrollViewer,label);
                        if(step is 5 or 7) VisualEvidenceCapture.Capture(this,Path.Combine(directory,label+".png"));
                    }
                }
                foreach(var kind in (extrasOnly?new[]{"import","reuse","capacity","activity","physical-import"}:new[]{"catalogue","activity","rates","orders","physical-import"}))
                {
                    IntegratedWorkspaceWindow page=kind switch {"catalogue"=>new CompositionWindow(studies),"activity"=>new PhysicalActivityWindow(studies),"rates"=>new PrincipalRatesWindow(studies),"physical-import"=>new PhysicalImportWindow(new PhysicalActivityEditor(studies)),
                        "import"=>new ImportColumnMappingWindow { DataContext=studies.ImportColumns },
                        "reuse"=>new AllocationReuseWindow(new AllocationReuseEditor(main.Company.ActiveAllocations!,main.Company.ActiveStudies!,studies.Current!,System.Globalization.CultureInfo.GetCultureInfo(language))),
                        "capacity"=>new PhysicalCapacityAssistantWindow(new PhysicalActivityEditor(studies)),_=>new EstimateWindow(studies)};
                    page.Owner=this;page.ShowIntegrated();if(page is EstimateWindow estimate) await estimate.InitializeAsync();await Idle();
                    var tabs=ScrollDescendants<TabControl>(page.Surface!).FirstOrDefault();
                    for(int index=0;index<(tabs?.Items.Count??1);index++)
                    {
                        if(tabs is not null){tabs.SelectedIndex=index;await Idle();}
                        var scroll=ScrollDescendants<ScrollViewer>(page.Surface!).First(s=>s.IsVisible&&s.VerticalScrollBarVisibility==ScrollBarVisibility.Auto);
                        await Verify(page.Surface!,scroll,language+"-"+width+"-"+kind+"-"+index);
                        VisualEvidenceCapture.Capture(this,Path.Combine(directory,language+"-"+width+"-"+kind+"-"+index+".png"));
                    }
                    if(extrasOnly && width==1000)
                    {
                        page.Surface!.LayoutTransform=new ScaleTransform(1.5,1.5);await Idle();
                        var scaled=ScrollDescendants<ScrollViewer>(page.Surface).First(s=>s.IsVisible&&s.VerticalScrollBarVisibility==ScrollBarVisibility.Auto);
                        await Verify(page.Surface,scaled,language+"-scaled-"+kind);
                        Check(scaled.ActualHeight>100,"scaled page remains usable "+kind);
                        VisualEvidenceCapture.Capture(this,Path.Combine(directory,language+"-scale150-"+kind+".png"));
                        page.Surface.LayoutTransform=null;
                    }
                    page.Close();await Idle();
                    Check(!FixedPageActionsHost.Children.Cast<FrameworkElement>().Any(c=>c.IsVisible),"departed page has no orphan fixed actions "+kind);
                }
            }
        }
        File.WriteAllText(Path.Combine(directory,"checks.json"),JsonSerializer.Serialize(checks,new JsonSerializerOptions{WriteIndented=true}));
    }
}
