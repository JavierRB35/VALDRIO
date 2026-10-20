using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
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
    internal async Task RunCompositionChecks(string directory,string source)
    {
        Directory.CreateDirectory(directory);
        var checks=new List<string>(); var times=new List<object>();
        void Check(bool ok,string label) { if(!ok)throw new InvalidOperationException(label);checks.Add(label);File.WriteAllText(Path.Combine(directory,"progress.txt"),string.Join(Environment.NewLine,checks)); }
        var original=SHA256.HashData(File.ReadAllBytes(source));
        var path=Path.Combine(directory,"aurora-t17.valdrio");
        var service=new SqliteCompanyFileService(new JsonCurrencyCatalog(Path.Combine(AppContext.BaseDirectory,"reference","currency-catalog-example.json")),new SystemClock());
        var legacy=Path.Combine(directory,"aurora-legacy-copy.valdrio");
        if(File.Exists(source+"-wal") || File.Exists(source+"-journal")) throw new InvalidOperationException("Source has an active journal; use a consistent backup.");
        File.Copy(source,legacy);
        Check(original.SequenceEqual(SHA256.HashData(File.ReadAllBytes(legacy))) && original.SequenceEqual(SHA256.HashData(File.ReadAllBytes(source))),"Stable byte-for-byte source copy");
        using(var file=service.Upgrade(legacy,path,"t17-local-check"))
        {
            var snapshot=file.Read();
            file.AddUnits([new(new(new(Guid.NewGuid())),"t17_kg","mass","kg",12,false),new(new(new(Guid.NewGuid())),"t17_piece","count","ud",0,true)],snapshot.Revision,"t17-local-check");
        }
        Check(original.SequenceEqual(SHA256.HashData(File.ReadAllBytes(source))),"AURORA original unchanged after copy upgrade");
        var main=viewModel!; var studies=main.Studies!;
        await main.Company!.OpenPath(path);
        studies.StudyChoice=studies.Studies.Single(s=>s.Value.Name.StartsWith("01 ",StringComparison.Ordinal));
        await studies.LoadCommand.ExecuteAsync(null);
        var kg=studies.PhysicalCompany!.Units.Single(u=>u.Code=="t17_kg").Id;
        var piece=studies.PhysicalCompany.Units.Single(u=>u.Code=="t17_piece").Id;
        foreach(var language in new[]{"es","en"})
        {
            main.ApplyPresentationPreset(language,language=="es"?"es-ES":"en-US","UTC",false,false);
            var clock=Stopwatch.StartNew();var page=new CompositionWindow(studies){Owner=this};page.ShowIntegrated();
            await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
            times.Add(new {language,milliseconds=clock.Elapsed.TotalMilliseconds});
            var editor=page.Editor;
            if(language=="es")
            {
                editor.AddItem();var product=editor.Items.Last(); product.Code="T17-P";product.Name="Conjunto industrial";product.Kind=CompositionItemKind.Product;product.Unit=piece;
                editor.AddItem();var material=editor.Items.Last();material.Code="T17-M";material.Name="Acero para conjunto";material.Kind=CompositionItemKind.Material;material.Unit=kg;
                var saving=editor.SaveAsync(false);
                Check(editor.Busy && !main.Company.WorkspaceIdle && !page.CanLeave(),"Save keeps file and page protected while UI remains responsive");
                await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
                Check(await saving,"Catalogue saves: "+editor.Feedback);
                editor.Product=product.Id;editor.NewVersion();editor.BatchSize="50";
                editor.AddLine();var line=editor.Lines.Last();line.Target=editor.Targets.Single(t=>t.Id==material.Id);line.Quantity="2";line.Loss=CompositionLossKind.ExtraInput;line.Percent="10";
                editor.AddLine();line=editor.Lines.Last();line.Target=editor.Targets.First(t=>t.Kind==2);line.Quantity="2";line.Basis=ConsumptionBasis.PerStartedBatch;
                page.RefreshView();editor.OutputQuantity="120";editor.Preview();
                Check(editor.Results.Count==2 && editor.Results[0].Required=="264" && editor.Results[1].Required=="6","120 units: 264 kg and 6 setup units");
                Check(await editor.SaveAsync(true),"Publication saves: "+editor.Feedback);
                Check(!editor.Editable,"Published version is read-only");
                editor.NewVersion();editor.Lines[0].Quantity="3";Check(editor.Dirty && editor.Results.Count==0,"Editing invalidates previous answers");
                Check(await editor.SaveAsync(false),"Second draft saves");
                editor.Lines[0].Quantity="";page.LeaveDecision=()=>MessageBoxResult.Yes;
                Check(!page.CanLeave() && editor.Dirty,"Invalid save blocks leaving");
                page.LeaveDecision=()=>MessageBoxResult.Cancel; Check(!page.CanLeave() && editor.Lines[0].Quantity=="","Cancel retains draft and page");
                page.LeaveDecision=()=>MessageBoxResult.No;Check(page.CanLeave() && !editor.Dirty,"Explicit discard reloads saved data");page.LeaveDecision=null;
                editor.Version=editor.Versions.First().Value;editor.Open();Check(editor.Lines[0].Quantity=="2","Published v1 unchanged by v2 edit");
                editor.AddItem();var svc=editor.Items.Last();svc.Code="T17-S";svc.Name="Servicio de mantenimiento";svc.Kind=CompositionItemKind.Service;svc.Unit=piece;
                Check(await editor.SaveAsync(false),"Service catalogue item saves");editor.Product=svc.Id;editor.NewVersion();
                editor.AddLine();line=editor.Lines.Last();line.Target=editor.Targets.First(t=>t.Kind==2);line.Quantity="2";line.Basis=ConsumptionBasis.PerOrder;
                editor.AddLine();line=editor.Lines.Last();line.Target=editor.Targets.First(t=>t.Kind==2);line.Quantity="0,5";line.Basis=ConsumptionBasis.PerUnit;
                editor.OutputQuantity="4";editor.Preview();Check(editor.Results.Count==2 && editor.Results.All(r=>r.Required=="2"),"Service: 4 interventions require 2 per order + 2 variable activity units");
                Check(await editor.SaveAsync(true),"Service version publishes");
                editor.Product=product.Id;editor.Version=editor.Versions.First().Value;editor.Open();
            }
            else { editor.Product=editor.Products.Single(p=>p.Label.StartsWith("T17-P",StringComparison.Ordinal)).Value;editor.Version=editor.Versions.First().Value;editor.Open(); }
            page.RefreshView();editor.OutputQuantity="120";editor.Preview();
            var tabs=((Grid)page.Surface!).Children.OfType<TabControl>().Single();tabs.SelectedIndex=1;
            await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
            Check(CompositionStep.IsEnabled && CompositionStep.Tag as string=="Active","Integrated menu active "+language);
            Check(editor.Answers.Count==7 && editor.Results[0].Required=="264","Questions from persisted version "+language);
            page.RefreshView();UpdateLayout();
            Check(((DataGrid)page.FindName("LinesGrid")).Columns.OfType<DataGridComboBoxColumn>().First().ItemsSource.Cast<CompositionTarget>().Any(t=>t.Label.Contains("Acero",StringComparison.Ordinal)),"Component selector contains saved material "+language);
            VisualEvidenceCapture.Capture(this,Path.Combine(directory,"composition-"+language+".png"));
            ((DataGrid)page.FindName("AnswersGrid")).BringIntoView();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
            VisualEvidenceCapture.Capture(this,Path.Combine(directory,"answers-"+language+".png"));
            foreach(var scale in new[]{1.25,1.5,2.0})
            {
                page.Surface.LayoutTransform=new System.Windows.Media.ScaleTransform(scale,scale);UpdateLayout();
                ((DataGrid)page.FindName("AnswersGrid")).BringIntoView();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
                Check(((DataGrid)page.FindName("AnswersGrid")).ActualWidth>250,"Answers reachable at scale "+scale+" "+language);
            }
            page.Surface.LayoutTransform=null;editor.OutputQuantity=language=="es"?"1.5":"1,5";editor.Preview();
            Check(editor.Results.Count==0 && editor.Feedback==editor["Number"],"Reject foreign decimal separator "+language);
            page.Close();Check(workspacePages.Count==0,"Integrated back "+language);
        }
        Check(original.SequenceEqual(SHA256.HashData(File.ReadAllBytes(source))),"Original preserved after all UI checks");
        File.WriteAllText(Path.Combine(directory,"composition-checks.json"),JsonSerializer.Serialize(new{checks,times},new JsonSerializerOptions{WriteIndented=true}));
    }
}




public partial class MainWindow
{
    internal async Task RunCompositionViewChecks(string directory,string path)
    {
        Directory.CreateDirectory(directory);var checks=new List<string>();
        void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);checks.Add(label);}
        var before=SHA256.HashData(File.ReadAllBytes(path));
        var main=viewModel!;await main.Company!.OpenPath(path);var studies=main.Studies!;
        studies.StudyChoice=studies.Studies.Single(s=>s.Value.Name.StartsWith("01 ",StringComparison.Ordinal));await studies.LoadCommand.ExecuteAsync(null);
        foreach(var language in new[]{"es","en"})
        {
            main.ApplyPresentationPreset(language,language=="es"?"es-ES":"en-US","UTC",false,false);
            var page=new CompositionWindow(studies){Owner=this};page.ShowIntegrated();var e=page.Editor;
            e.Product=e.Products.Single(p=>p.Label.StartsWith("T17-P",StringComparison.Ordinal)).Value;e.Version=e.Versions.First().Value;e.Open();page.RefreshView();
            e.OutputQuantity="120";e.Preview();Check(e.Results.Count==2&&e.Answers.Count==7,"Industrial preview "+language);
            var tabs=((Grid)page.Surface!).Children.OfType<TabControl>().Single();tabs.SelectedIndex=1;
            await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
            Check(((DataGrid)page.FindName("LinesGrid")).IsReadOnly,"Published table read-only "+language);
            VisualEvidenceCapture.Capture(this,Path.Combine(directory,"composition-"+language+".png"));
            ((DataGrid)page.FindName("AnswersGrid")).BringIntoView();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
            VisualEvidenceCapture.Capture(this,Path.Combine(directory,"answers-"+language+".png"));
            e.Version=e.Versions.Last().Value;Check(e.Results.Count==0&&!e.Editable,"Selection invalidates old result "+language);e.Preview();Check(e.Results.Count==0&&e.Feedback==e["ChooseRecipe"],"Unopened selection cannot calculate old recipe "+language);
            e.Open();page.RefreshView();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);Check(!((DataGrid)page.FindName("LinesGrid")).IsReadOnly,"Draft table editable "+language);
            e.Product=e.Products.Single(p=>p.Label.StartsWith("T17-S",StringComparison.Ordinal)).Value;e.Version=e.Versions.First().Value;e.Open();page.RefreshView();e.OutputQuantity="4";e.Preview();
            Check(e.Results.Count==2&&e.Results.All(r=>r.Required=="2"),"Service preview "+language);
            Check(e.Answers.Single(a=>a.Question==e["QBatches"]).Answer==e["NoBatches"],"Service without per-batch consumption does not imply batch preparation "+language);
            ((DataGrid)page.FindName("AnswersGrid")).BringIntoView();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
            VisualEvidenceCapture.Capture(this,Path.Combine(directory,"service-"+language+".png"));
            page.Close();
        }
        Check(before.SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))),"Language and preview leave persisted quantities unchanged");
        File.WriteAllText(Path.Combine(directory,"view-checks.json"),JsonSerializer.Serialize(checks,new JsonSerializerOptions{WriteIndented=true}));
    }
}
