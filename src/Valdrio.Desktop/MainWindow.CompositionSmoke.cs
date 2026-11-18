using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Valdrio.Application;
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
        async Task Settle(Func<bool>? done=null) => await CompositionSettle(Dispatcher,done);
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
            var clock=Stopwatch.StartNew();var page=new CompositionWindow(studies){Owner=this};page.ShowIntegrated(); await page.InitializeAsync();
            await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
            times.Add(new {language,milliseconds=clock.Elapsed.TotalMilliseconds});
            var editor=page.Editor;
            var catalogGrid=(DataGrid)page.FindName("CatalogGrid")!;
            var linesGrid=(DataGrid)page.FindName("LinesGrid")!;
            var answersGrid=(DataGrid)page.FindName("AnswersGrid")!;
            var tabControl=(TabControl)page.FindName("ProductTabs")!;
            async Task SelectTab(int index){ tabControl.SelectedIndex=index; await Settle(()=>tabControl.SelectedIndex==index); }
            Check(ReferenceEquals(catalogGrid.ItemsSource,editor.Items) && ReferenceEquals(linesGrid.ItemsSource,editor.Lines) &&
                  ReferenceEquals(answersGrid.ItemsSource,editor.Answers),"Grids bound to live editor collections "+language);
            Check(BindingOperations.GetBinding(linesGrid,DataGrid.IsReadOnlyProperty)?.Path.Path=="LinesReadOnly",
                "Line grid read-only state follows the editor "+language);
            Check(CompositionButtonByKey(page.Surface!,"AddItem").IsEnabled &&
                  CompositionButtonByKey(page.Surface!,"AddLine") is not null &&
                  CompositionButtonByKey(page.Surface!,"SaveCatalog") is not null,"Editable actions and help keys present "+language);
            if(language=="es")
            {
                Check(editor.CanInstallUnitPreset,"Standard unit preset can be installed from the real copy es");
                CompositionClick(CompositionButtonByKey(page.Surface!,"InstallUnits"));
                await Settle(()=>editor.HasUnitPreset);
                Check(editor.HasUnitPreset && studies.PhysicalCompany!.Units.Any(u=>u.Code=="unit" && u.Dimension=="count" && u.IsDiscrete),
                    "Install standard units creates a discrete Unit es");
                var addItem=CompositionButtonByKey(page.Surface!,"AddItem");
                CompositionClick(addItem);
                Check(editor.Items.Count==1,"AddItem button inserts a catalogue row es");
                var product=editor.Items.Last(); product.Code="T17-P";product.Name="Conjunto industrial";product.Kind=CompositionItemKind.Product;product.Unit=piece;
                editor.AddItem();var material=editor.Items.Last();material.Code="T17-M";material.Name="Acero para conjunto";material.Kind=CompositionItemKind.Material;material.Unit=kg;
                product.ReferenceUnitCostInput="99,25"; material.ReferenceUnitCostInput="2,5";
                Check(product.CostOriginDisplay==editor["OriginManual"] &&
                      !string.IsNullOrEmpty(editor["CostOverrideTitle"]) &&
                      !string.IsNullOrEmpty(editor["CostOverrideHelp"]),"Manual origin and override warning available es");
                var saving=editor.SaveAsync(false);
                Check(editor.Busy && !main.Company.WorkspaceIdle && !page.CanLeave(),"Save keeps file and page protected while UI remains responsive");
                await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
                Check(await saving,"Catalogue saves: "+editor.Feedback);
                Check(editor.Items.Single(i=>i.Id==product.Id).CostOriginDisplay==editor["OriginManual"],"Manual cost stays manual until a complete composition is published es");
                editor.Product=product.Id;
                await SelectTab(1);
                editor.NewVersion();editor.BatchSize="50";
                var addLine=CompositionButtonByKey(page.Surface!,"AddLine");
                CompositionEnter(addLine);
                Check(editor.Lines.Count==1,"Enter on AddLine creates the first component row es");
                var line=editor.Lines.Last();line.Target=editor.Targets.Single(t=>t.Id==material.Id);line.Quantity="2";line.Loss=CompositionLossKind.ExtraInput;line.Percent="10";
                editor.AddLine();line=editor.Lines.Last();line.Target=editor.Targets.First(t=>t.Kind==2);line.Quantity="2";line.Basis=ConsumptionBasis.PerStartedBatch;
                page.RefreshView();editor.OutputQuantity="120";editor.Preview();
                Check(editor.Results.Count==2 && editor.Results[0].Required=="264" && editor.Results[1].Required=="6","120 units: 264 kg and 6 setup units");
                Check(editor.Answers.Count==6,"Six analyst answers after preview es");
                Check(await editor.SaveAsync(true),"Publication saves: "+editor.Feedback);
                Check(!editor.Editable && linesGrid.IsReadOnly && !CompositionButtonByKey(page.Surface!,"AddLine").IsEnabled,"Published version is read-only in the grid es");
                Check(editor.Items.Single(i=>i.Id==product.Id).CostOriginDisplay==editor["OriginComposition"],
                    "Complete composition takes precedence over the manual cost es");
                var firstVersionId=editor.Version;
                var publishedSnapshot=CanonicalSnapshotJson.Serialize(studies.ActiveCompositions!.ReadCompositions().Versions.Single(v=>v.Id==firstVersionId));
                tabControl.SelectedIndex=1; await Settle(()=>tabControl.SelectedIndex==1);
                Check(tabControl.SelectedIndex==1 && !editor.Dirty,"Clean tab change succeeds with changingTab guard es");
                tabControl.SelectedIndex=0; await Settle(()=>tabControl.SelectedIndex==0);
                editor.NewVersion();editor.Lines[0].Quantity="3";Check(editor.Dirty && editor.Results.Count==0,"Editing invalidates previous answers");
                page.LeaveDecision=()=>MessageBoxResult.Cancel;
                tabControl.SelectedIndex=1; await Settle(()=>tabControl.SelectedIndex==0);
                Check(tabControl.SelectedIndex==0 && editor.Dirty,"Cancelling a tab change keeps unsaved edits es");
                page.LeaveDecision=()=>MessageBoxResult.No;
                tabControl.SelectedIndex=1; await Settle(()=>tabControl.SelectedIndex==1);
                Check(tabControl.SelectedIndex==1 && !editor.Dirty && editor.Lines[0].Quantity=="2","Discarding on tab change reloads saved data es");
                tabControl.SelectedIndex=0; await Settle(()=>tabControl.SelectedIndex==0);
                page.LeaveDecision=null;
                Check(await editor.SaveAsync(false),"Second draft saves");
                var draftAId=editor.Version;
                editor.Lines[0].Quantity="4";page.LeaveDecision=()=>MessageBoxResult.Yes;
                tabControl.SelectedIndex=1; await Settle(()=>tabControl.SelectedIndex==1);
                Check(tabControl.SelectedIndex==1 && !editor.Dirty && editor.Lines[0].Quantity=="4",
                    "Saving on tab change persists and navigates es");
                Check(studies.ActiveCompositions!.ReadCompositions().Versions.Single(v=>v.Id==draftAId).Lines.Any(l=>l.Quantity==4m),
                    "Tab-change save reaches SQLite es");
                page.LeaveDecision=null;
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
                var serviceVersionId=editor.Version;
                var serviceBefore=CanonicalSnapshotJson.Serialize(studies.ActiveCompositions!.ReadCompositions().Versions.Single(v=>v.Id==serviceVersionId));
                var serviceItemBefore=CanonicalSnapshotJson.Serialize(studies.ActiveCompositions.ReadCompositions().Items.Single(i=>i.Id==svc.Id));
                editor.Product=product.Id;editor.Version=editor.Versions.First().Value;editor.Open();
                var csvPath=Path.Combine(directory,"catalogo-es.csv");
                var kgCode=studies.PhysicalCompany.Units.Single(u=>u.Id==kg).Code;
                File.WriteAllText(csvPath,"Reference;Name;Unit;Unit cost\r\n0012;Acero;"+kgCode+";12,5\r\n",System.Text.Encoding.UTF8);
                await editor.ImportProductsAsync(csvPath);
                var imported=editor.Items.Single(i=>i.Code=="0012");
                Check(imported.ReferenceUnitCost==12.5m && imported.CostOriginDisplay==editor["OriginManual"],"CSV keeps textual code and manual origin es");
                Check(await editor.SaveAsync(false),"Imported catalogue saves es");
                Check(studies.ActiveCompositions.ReadCompositions().Items.Single(i=>i.Code=="0012").ReferenceUnitCost==12.5m,"Imported cost persists es");
                File.WriteAllText(csvPath,"Reference;Name;Unit;Unit cost\r\n0013;Bad;NO-SUCH-UNIT;1\r\n",System.Text.Encoding.UTF8);
                await editor.ImportProductsAsync(csvPath);
                Check(editor.Feedback==string.Format(studies.PhysicalUiCulture,editor["ImportUnknownUnit"],"2"),"Invalid import diagnostic es");

                // Supervised regression: A dirty -> select B without opening -> Save+Open B.
                // HasCompositionChanges used Editable, which becomes false when the
                // pending product selection moves to B; saving must still commit A.
                editor.Product=product.Id; editor.Version=draftAId; editor.Open();
                Check(editor.Editable && !editor.Dirty,"A draft selected cleanly before edit es");
                CompositionClick(CompositionButtonByKey(page.Surface!,"OpenProduct"));
                await Settle();
                Check(editor.Editable && editor.Dirty && editor.Product==product.Id && editor.Version==draftAId,
                    "OpenProduct syncs the window to the loaded A and exposes its draft as pending es");
                editor.Lines[0].Quantity="7";
                Check(editor.Dirty && editor.HasCompositionChanges,"A carries unsaved composition changes before selecting B es");
                var picker=CompositionElementByKey<ComboBox>(page.Surface!,"Product");
                Check(picker.SelectedValue is EntityId aSelected && aSelected==product.Id,
                    "Product picker starts on the loaded A es");
                page.LeaveDecision=()=>MessageBoxResult.Yes;
                picker.SelectedValue=svc.Id;
                await Settle(()=>editor.Product==svc.Id && !editor.Dirty && !editor.Busy);
                var savedDraft=studies.ActiveCompositions!.ReadCompositions().Versions.Single(v=>v.Id==draftAId);
                Check(savedDraft.Lines.Any(l=>l.Quantity==7m),
                    "Selecting B with Save makes A's composition change effective es");
                Check(CanonicalSnapshotJson.Serialize(studies.ActiveCompositions.ReadCompositions().Versions.Single(v=>v.Id==serviceVersionId))==serviceBefore,
                    "Saving A never modifies B's published version es");
                Check(CanonicalSnapshotJson.Serialize(studies.ActiveCompositions.ReadCompositions().Items.Single(i=>i.Id==svc.Id))==serviceItemBefore,
                    "Saving A never modifies B's catalogue cost es");
                CompositionClick(CompositionButtonByKey(page.Surface!,"OpenProduct"));
                await Settle();
                Check(editor.Product==svc.Id && editor.Lines.Count==2 && editor.Results.Count==0,
                    "B opens after the save with its own composition es");
                Check(CanonicalSnapshotJson.Serialize(studies.ActiveCompositions.ReadCompositions().Versions.Single(v=>v.Id==serviceVersionId))==serviceBefore,
                    "Opening B leaves its published version immutable es");
                Check(!studies.ActiveCompositions.ReadCompositions().Versions.Any(v=>v.ProductId==svc.Id && v.State==CompositionState.Draft),
                    "Selecting and opening B does not persist a B draft es");
                page.LeaveDecision=null;

                // Discard while selecting B: A reverts to its persisted draft and B stays intact.
                editor.Product=product.Id; editor.Version=draftAId; editor.Open();
                CompositionClick(CompositionButtonByKey(page.Surface!,"OpenProduct")); await Settle();
                editor.Lines[0].Quantity="9";
                page.LeaveDecision=()=>MessageBoxResult.No;
                picker.SelectedValue=svc.Id;
                await Settle(()=>editor.Product==svc.Id && !editor.Dirty && !editor.Busy);
                Check(studies.ActiveCompositions.ReadCompositions().Versions.Single(v=>v.Id==draftAId).Lines.Any(l=>l.Quantity==7m),
                    "Discarding during product change leaves A's persisted draft untouched es");
                Check(CanonicalSnapshotJson.Serialize(studies.ActiveCompositions.ReadCompositions().Versions.Single(v=>v.Id==serviceVersionId))==serviceBefore,
                    "Discarding during product change never modifies B es");
                page.LeaveDecision=null;

                // Cancel while selecting B: the picker returns to A and unsaved edits survive.
                editor.Product=product.Id; editor.Version=draftAId; editor.Open();
                CompositionClick(CompositionButtonByKey(page.Surface!,"OpenProduct")); await Settle();
                editor.Lines[0].Quantity="10";
                page.LeaveDecision=()=>MessageBoxResult.Cancel;
                picker.SelectedValue=svc.Id;
                await Settle(()=>picker.SelectedValue is EntityId c && c==product.Id);
                Check(picker.SelectedValue is EntityId stillA && stillA==product.Id && editor.Product==product.Id &&
                      editor.Dirty && editor.Lines[0].Quantity=="10",
                    "Cancelling product change keeps A selected with its unsaved edit es");
                Check(studies.ActiveCompositions.ReadCompositions().Versions.Single(v=>v.Id==draftAId).Lines.Any(l=>l.Quantity==7m),
                    "Cancelling product change writes nothing es");
                page.LeaveDecision=null;
                editor.Reload();
                editor.Product=product.Id; editor.Version=editor.Versions.First().Value; editor.Open(); page.RefreshView();

                Check(CanonicalSnapshotJson.Serialize(studies.ActiveCompositions.ReadCompositions().Versions.Single(v=>v.Id==firstVersionId))==publishedSnapshot,
                    "Published snapshot remains immutable after UI edits es");
            }
            else { editor.Product=editor.Products.Single(p=>p.Label.StartsWith("T17-P",StringComparison.Ordinal)).Value;editor.Version=editor.Versions.First().Value;editor.Open(); }
            page.RefreshView();editor.OutputQuantity="120";editor.Preview();
            tabControl.SelectedIndex=1;
            await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
            Check(CompositionStep.IsEnabled && CompositionStep.Tag as string=="Active","Integrated menu active "+language);
            Check(editor.Answers.Count==6 && editor.Results[0].Required=="264","Six answers from persisted version "+language);
            var expectedQuestions=new[]{editor["QNeeds"],editor["QBatches"],editor["QVersion"],editor["QLoss"],editor["QCost"],editor["QActual"]};
            Check(expectedQuestions.All(q=>editor.Answers.Any(a=>a.Question==q)),"Required analyst questions present "+language);
            page.RefreshView();UpdateLayout();
            Check(linesGrid.Columns.OfType<DataGridComboBoxColumn>().First().ItemsSource.Cast<CompositionTarget>().Any(t=>t.Label.Contains("Acero",StringComparison.Ordinal)),"Component selector contains saved material "+language);
            VisualEvidenceCapture.Capture(this,Path.Combine(directory,"composition-"+language+".png"));
            answersGrid.BringIntoView();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
            VisualEvidenceCapture.Capture(this,Path.Combine(directory,"answers-"+language+".png"));
            foreach(var scale in new[]{1.25,1.5,2.0})
            {
                page.Surface.LayoutTransform=new System.Windows.Media.ScaleTransform(scale,scale);UpdateLayout();
                answersGrid.BringIntoView();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
                Check(answersGrid.ActualWidth>250,"Answers reachable at scale "+scale+" "+language);
            }
            page.Surface.LayoutTransform=null;editor.OutputQuantity=language=="es"?"1,2,3":"1.2.3";editor.Preview();
            Check(editor.Results.Count==0 && editor.Feedback==editor["Number"],"Reject structurally invalid decimal "+language);
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
            var page=new CompositionWindow(studies){Owner=this};page.ShowIntegrated(); await page.InitializeAsync();var e=page.Editor;
            e.Product=e.Products.Single(p=>p.Label.StartsWith("T17-P",StringComparison.Ordinal)).Value;e.Version=e.Versions.First().Value;e.Open();page.RefreshView();
            e.OutputQuantity="120";e.Preview();
            Check(e.Results.Count==2&&e.Answers.Count==6,"Industrial preview with six answers "+language);
            var expected=new[]{e["QNeeds"],e["QBatches"],e["QVersion"],e["QLoss"],e["QCost"],e["QActual"]};
            Check(expected.All(q=>e.Answers.Any(a=>a.Question==q)),"Analyst questions present in preview "+language);
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

public partial class MainWindow
{
    private static T CompositionElementByKey<T>(DependencyObject root,string key) where T:FrameworkElement
    {
        T? found=Find(root);
        return found ?? throw new InvalidOperationException("Missing WPF control for help key: "+key);
        T? Find(DependencyObject node)
        {
            if(node is T element && FieldHelp.GetKey(node)==key) return element;
            var count=VisualTreeHelper.GetChildrenCount(node);
            for(var i=0;i<count;i++)
            {
                var child=VisualTreeHelper.GetChild(node,i);
                var result=Find(child);
                if(result is not null) return result;
            }
            return null;
        }
    }
    private static Button CompositionButtonByKey(DependencyObject root,string key) =>
        CompositionElementByKey<Button>(root,key);
    private static void CompositionClick(Button button)
    {
        if(!button.IsEnabled) throw new InvalidOperationException("Disabled WPF button: "+FieldHelp.GetKey(button));
        button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
    }
    private static void CompositionEnter(Button button)
    {
        if(!button.IsEnabled) throw new InvalidOperationException("Disabled WPF button: "+FieldHelp.GetKey(button));
        button.Focus();
        if(!button.IsKeyboardFocused) throw new InvalidOperationException("WPF button cannot take keyboard focus: "+FieldHelp.GetKey(button));
        var source=PresentationSource.FromVisual(button) ?? throw new InvalidOperationException("No presentation source for keyboard check.");
        button.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,source,Environment.TickCount,Key.Enter){RoutedEvent=Keyboard.KeyDownEvent});
    }
    private static async Task CompositionSettle(Dispatcher dispatcher,Func<bool>? done=null)
    {
        for(var attempt=0;attempt<40;attempt++)
        {
            await dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
            if(done is null || done()) return;
            await Task.Delay(25);
        }
        if(done is not null && !done()) throw new InvalidOperationException("WPF UI did not reach the expected state.");
    }
}
