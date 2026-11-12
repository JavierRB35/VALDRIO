using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Valdrio.Application;
using Valdrio.Domain;
namespace Valdrio.Desktop;
public partial class MainWindow
{
    internal async Task RunAuxiliaryChecks(string directory, string path)
    {
        Directory.CreateDirectory(directory); var main=viewModel!; var studies=main.Studies!;
        var checks=new List<string>();
        void Check(bool ok,string label) { if(!ok) throw new InvalidOperationException(label); checks.Add(label); }
        Check(!AuxiliaryStep.IsEnabled,"No company disables auxiliary menu");
        await main.Company!.OpenPath(path);
        studies.StudyChoice=studies.Studies.Single(s=>s.Value.Name.StartsWith("T16",StringComparison.Ordinal));
        await studies.LoadCommand.ExecuteAsync(null);
        foreach(var language in new[]{"es","en"})
        {
            main.ApplyPresentationPreset(language,language=="es"?"es-ES":"en-US","UTC",false,false);
            await NavigateWorkflow(10); UpdateWorkflow(); AuxiliaryStep.BringIntoView();
            await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
            Check(AuxiliaryStep.IsEnabled && AuxiliaryStep.Focus(),"Keyboard menu "+language+" enabled="+AuxiliaryStep.IsEnabled+" visible="+AuxiliaryStep.IsVisible+" step="+workflowStep+" can="+studies.CanOpenPhysical);
            var window=new AuxiliaryCapacityWindow(studies){Owner=this};window.Show(); await window.InitializeAsync();
            await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
            var editor=window.Editor;
            Check(editor.Rows.Count==2,"Two persisted auxiliaries "+language);
            Check(editor.Answers.Count==7,"Seven answers "+language);
            Check(editor.Summary.Contains("800",StringComparison.Ordinal),"C20 summary "+language);
            editor.SelectedRow=editor.Rows.Single(r=>r.Name.StartsWith("B",StringComparison.Ordinal));
            Check(editor.Answers[0].Answer.Contains("100",StringComparison.Ordinal),"Received A cost displayed "+language);
            Check(editor.Answers[3].Answer.Contains("400",StringComparison.Ordinal),"B transmits 400 "+language);
            VisualEvidenceCapture.Capture(window,Path.Combine(directory,"aux-"+language+".png"));
            var service=editor.SelectedRow.Services.Single(s=>s.Code=="P"); service.Quantity="40";
            Check(editor.Dirty && editor.CanSave && editor.Export() is null,"Edit invalidates answers and export "+language);
            editor.Validate(); Check(editor.Feedback==editor["CoverageMismatch"],"Missing ten hours blocks "+language);
            editor.Save(); Check(!editor.Dirty,"Incomplete draft saves "+language);
            editor.Calculate(); Check(editor.Feedback==editor["CoverageMismatch"],"Incomplete saved draft cannot calculate "+language);
            editor.Import("Center;Group;Destination;Quantity\nB;all;P;50"); editor.Save(); editor.Calculate();
            Check(editor.Export() is not null && editor.Summary.Contains("800",StringComparison.Ordinal),"Import save and calculate restores C20 "+language);
            var rates=new PrincipalRatesEditor(studies); Check(rates.Report!.Rows.Single().Result!.CombinedRate==16m,"T15 consumes auxiliary result once "+language);
            window.Width=760;window.Height=650;window.UpdateLayout();
            await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
            Check(((DataGrid)window.FindName("AnswersTable")).ActualWidth>400,"Narrow answer layout "+language);
            VisualEvidenceCapture.Capture(window,Path.Combine(directory,"aux-narrow-"+language+".png"));
            window.Width=1200;window.Height=850;
            foreach(var scale in new[]{1.25,1.5,2.0})
            {
                ((FrameworkElement)window.Content).LayoutTransform=new System.Windows.Media.ScaleTransform(scale,scale);
                window.UpdateLayout();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
                ((DataGrid)window.FindName("AnswersTable")).BringIntoView();window.UpdateLayout();
                Check(((ScrollViewer)window.FindName("AuxScroll")).VerticalOffset>0,"Answers reachable at scale "+scale+" "+language);
                VisualEvidenceCapture.Capture(window,Path.Combine(directory,"aux-scale-"+(int)(scale*100)+"-"+language+".png"));
            }
            window.Close();
        }
        var current=studies.Current!;
        var port=studies.AuxiliaryAllocations!;
        var w=port.ReadAllocations(current.CompanyId,current.Id);
        port.ChangeAllocations(new(current.CompanyId,current.Id,w.Resources.Study.Revision,new(Guid.NewGuid()),"ui-test",w.Configuration.WithPrimaryClosed(true)));
        await studies.LoadCommand.ExecuteAsync(null);
        await main.Allocations!.EnterAsync(); await NavigateWorkflow(7);
        await main.Allocations.CalculateSecondaryCommand.ExecuteAsync(null);
        Check(main.Allocations.PrincipalCosts.Count>0 && main.Allocations.PrincipalCostsFinalTotal.Contains("800",StringComparison.Ordinal),"Secondary retains 800 at principals");
        w=port.ReadAllocations(current.CompanyId,current.Id);
        var calculated=SecondaryAllocationService.Calculate(w);
        port.ChangeAllocations(new(current.CompanyId,current.Id,w.Resources.Study.Revision,new(Guid.NewGuid()),"ui-test",w.Configuration.WithSecondaryClosed(true,SecondaryAllocationService.ResultFingerprint(calculated,w.Configuration))));
        var closed=new AuxiliaryCapacityEditor(studies);Check(closed.CanReopen && !closed.CanEdit,"Closed secondary protects auxiliary editing");
        closed.Reopen();Check(closed.CanEdit,"Explicit reopen restores editing");closed.Calculate();
        var physicalWindow=new PhysicalActivityWindow(studies){Owner=this};physicalWindow.Show(); await physicalWindow.InitializeAsync();
        Check(((PhysicalActivityEditor)physicalWindow.DataContext).Rows.Count==3,"T14 exposes auxiliaries and principal");physicalWindow.Close();
        await studies.LoadCommand.ExecuteAsync(null);await NavigateWorkflow(8);
        Check(main.CostFlows!.NodeBalances.Count>0,"T12 shows retained capacity and final costs");
        current=studies.PhysicalStudies!.ReadStudies().Single(s=>s.Id==current.Id);
        var o=studies.ActivePhysicalActivity!.ReadCurrent(current.CompanyId,current.Id).Single(x=>x.NodeId==w.Configuration.AuxiliaryCapacity!.Rows[0].NodeId);
        studies.ActivePhysicalActivity.Save(new(current.CompanyId,current.Id,current.Revision,o.NodeId,o.PoolId,o.Version,PhysicalObservationState.Confirmed,
            null,null,100,40,o.NormalEvidence,o.RealEvidence,null,null,"UI correction","ui-test"));
        var stale=new AuxiliaryCapacityEditor(studies);Check(stale.Dirty && stale.CanSave && stale.Export() is null,"Changed T14 requires service review and enables save");
        File.WriteAllText(Path.Combine(directory,"checks.json"),JsonSerializer.Serialize(checks,new JsonSerializerOptions{WriteIndented=true}));
    }
}
