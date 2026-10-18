using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Threading;
using Valdrio.Application;

namespace Valdrio.Desktop;
public partial class MainWindow
{
    internal async Task RunRatesChecks(string directory, string path)
    {
        Directory.CreateDirectory(directory);
        var main=viewModel!;var studies=main.Studies!;
        var checks=new List<string>();
        void Check(bool condition,string name){if(!condition)throw new InvalidOperationException(name);checks.Add(name);}
        Check(!RatesStep.IsEnabled,"No project: rates disabled");
        await main.Company!.OpenPath(path);
        await NavigateWorkflow(10);
        Check(RatesHome.IsVisible && !OpenRatesButton.IsEnabled,"No analysis: no invented results");
        studies.StudyChoice=studies.Studies.Single(s=>s.Value.Name.StartsWith("T15",StringComparison.Ordinal));
        await studies.LoadCommand.ExecuteAsync(null);
        var revision=studies.Current!.Revision;
        foreach(var language in new[]{"es","en"})
        {
            main.ApplyPresentationPreset(language,language=="es"?"es-ES":"en-US","UTC",false,false);
            await NavigateWorkflow(10);
            Check(RatesStep.IsEnabled && RatesStep.Focus(),"Menu focus "+language);
            Check((string?)RatesStep.Tag=="Active" && AllocationPhase.IsExpanded,"Menu active "+language);
            var window=new PrincipalRatesWindow(studies){Owner=this};window.Show();
            await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
            var editor=window.Editor;
            var grid=(DataGrid)window.FindName("CentersTable");
            Check(editor.Rows.Count==2,"Both real centres loaded "+language);
            grid.SelectedIndex=0;grid.Focus();
            Check(editor.Answers.Count==7,"Seven answers "+language);
            Check(editor.SelectedRow!.Value.Result!.CombinedRate==20m,"Real persisted 20/h "+language);
            Check(editor.Answers[4].Answer==editor["UnknownAssignment"],"Unknown assignment not zero "+language);
            Check(editor.Answers[6].Answer.Contains(editor["Partial"],StringComparison.Ordinal),"Partial reconciliation labelled "+language);
            VisualEvidenceCapture.Capture(window,Path.Combine(directory,"rates-"+language+".png"));
            grid.SelectedIndex=1;
            Check(editor.SelectedRow!.Value.Result!.CombinedRate==5m && editor.Answers[0].Answer.Contains("5",StringComparison.Ordinal),"Selection updates answers "+language);
            grid.SelectedIndex=0;
            window.Width=760;window.Height=650;window.UpdateLayout();
            await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
            Check(((DataGrid)window.FindName("AnswersTable")).ActualWidth>400,"Narrow answers retain width "+language);
            VisualEvidenceCapture.Capture(window,Path.Combine(directory,"rates-narrow-"+language+".png"));
            window.Width=1200;window.Height=850;
            var surface=(FrameworkElement)window.Content;
            foreach(var scale in new[]{1.25,1.5,2.0})
            {
                surface.LayoutTransform=new System.Windows.Media.ScaleTransform(scale,scale);
                window.UpdateLayout();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
                Check(((DataGrid)window.FindName("AnswersTable")).ActualWidth>300,"Scaled answer layout "+scale+" "+language);
                VisualEvidenceCapture.Capture(window,Path.Combine(directory,"rates-scale-"+(int)(scale*100)+"-"+language+".png"));
                ((DataGrid)window.FindName("AnswersTable")).BringIntoView();
                window.UpdateLayout();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
                Check(((ScrollViewer)window.FindName("RatesScroll")).VerticalOffset>0,"Answers reachable by scrolling "+scale+" "+language);
                VisualEvidenceCapture.Capture(window,Path.Combine(directory,"answers-scale-"+(int)(scale*100)+"-"+language+".png"));
                ((ScrollViewer)window.FindName("RatesScroll")).ScrollToTop();
            }
            surface.LayoutTransform=System.Windows.Media.Transform.Identity;
            editor.AttentionOnly=true;Check(editor.Rows.Count==0 && editor.Answers.Count==0,"Filter clears old answers "+language);
            editor.AttentionOnly=false;Check(editor.Rows.Count==2 && editor.Answers.Count==7,"Filter restores selection "+language);
            var exported=CanonicalSnapshotJson.Serialize(editor.Report);
            File.WriteAllText(Path.Combine(directory,"rates-"+language+".json"),exported);
            Check(CanonicalSnapshotJson.Deserialize<PrincipalRateReport>(exported).Rows.Count==2,"Frozen export readable "+language);
            var original=editor.Rows[0].Value.Observation!;
            studies.ActivePhysicalActivity!.Save(new(original.CompanyId,original.StudyId,revision,original.NodeId,original.PoolId,original.Version,
                PhysicalObservationState.Draft,null,null,100,60,original.NormalEvidence,original.RealEvidence,null,null,"WPF disposable test","test"));
            editor.Reload();
            Check(editor.Rows[0].Value.Result is null && editor.Rows[0].Value.Cost==1600m,"Draft invalidates rates but preserves known cost "+language);
            Check(!editor.Answers[0].Answer.Contains("20 EUR",StringComparison.Ordinal),"No stale rate answer "+language);
            editor.AttentionOnly=true;Check(editor.Rows.Count==1,"Attention filter identifies missing confirmation "+language);
            var latest=studies.ActivePhysicalActivity.ReadCurrent(original.CompanyId,original.StudyId).Single(o=>o.PoolId==original.PoolId);
            studies.ActivePhysicalActivity.Save(new(original.CompanyId,original.StudyId,revision,original.NodeId,original.PoolId,latest.Version,
                PhysicalObservationState.Confirmed,null,null,100,60,original.NormalEvidence,original.RealEvidence,null,null,null,"test"));
            editor.AttentionOnly=false;editor.Reload();Check(editor.Rows[0].Value.Result!.CombinedRate==20m,"Confirmed data restores calculation "+language);
            window.Close();
            Check(studies.Current!.Revision==revision,"Reading rates never changes study "+language);
        }
        // Invoke the actual menu Click and verify that it opens the implemented window.
        bool opened=false;var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(200)};
        timer.Tick+=(_,_)=>{var w=OwnedWindows.OfType<PrincipalRatesWindow>().FirstOrDefault();if(w is null)return;opened=true;timer.Stop();w.Close();};
        timer.Start();((IInvokeProvider)new ButtonAutomationPeer(RatesStep).GetPattern(PatternInterface.Invoke)).Invoke();
        await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);timer.Stop();
        Check(opened,"Real menu opens rates window");
        await GoBackWorkflow();Check(workflowStep==9,"Back returns to activity");
        File.WriteAllText(Path.Combine(directory,"checks.json"),JsonSerializer.Serialize(checks,new JsonSerializerOptions{WriteIndented=true}));
    }
}
