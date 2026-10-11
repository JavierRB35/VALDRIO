using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Valdrio.Application;
namespace Valdrio.Desktop;
public partial class MainWindow
{
    internal async Task RunCostFlowChecks(string directory)
    {
        var main=viewModel!;var studies=main.Studies!;var flows=main.CostFlows!;
        var checks=new List<string>();
        void Check(bool yes,string label){if(!yes)throw new InvalidOperationException(label);checks.Add(label);}
        await NavigateWorkflow(2);studies.StudyChoice=studies.Studies.Single(s=>s.Value.Name.StartsWith("T12 DEMO"));await studies.LoadCommand.ExecuteAsync(null);
        foreach(var language in new[]{"es","en"})
        {
            main.ApplyPresentationPreset(language,language=="es"?"es-ES":"en-US","UTC",false,false);
            Width=1600;Height=1000;await NavigateWorkflow(8);await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);UpdateLayout();
            Check(flows.Nodes.Select(n=>n.Stage).Distinct().Count()==4,"four stages "+language);
            Check(flows.Arrows.Count(a=>a.Row?.Stage==flows["SecondaryStage"])==4,"four secondary arrows "+language);
            Check(flows.Arrows.Count(a=>a.Internal)==2,"reciprocal arrows "+language);
            Check(flows.Nodes.Single(n=>n.Stage==3).Amount=="6500 EUR","final 6500 "+language);
            Check(flows.CurrentState==flows["Current"],"current revision "+language);
            var original=CanonicalSnapshotJson.Serialize(flows.SelectedRun!.Run);
            var originalId=flows.SelectedRun.Run.Id;var originalCount=flows.Runs.Count;
            Check(flows.CalculateCommand.CanExecute(null),"explicit calculation enabled "+language);
            await flows.CalculateCommand.ExecuteAsync(null);
            Check(flows.Runs.Count==originalCount+1 && flows.SelectedRun?.Run.Status==CalculationRunStatus.Completed,"UI calculates and publishes "+language);
            var newest=flows.SelectedRun;flows.SelectedRun=flows.Runs.Single(r=>r.Run.Id==originalId);
            Check(flows.CurrentState==flows["Historical"],"older execution marked historical "+language);
            Check(original==CanonicalSnapshotJson.Serialize(flows.SelectedRun.Run),"older execution identical "+language);
            flows.SelectedRun=newest;original=CanonicalSnapshotJson.Serialize(flows.SelectedRun!.Run);
            await flows.EnterAsync();Check(original==CanonicalSnapshotJson.Serialize(flows.SelectedRun!.Run),"query leaves execution unchanged "+language);
            var node=flows.Nodes.First(n=>n.Stage==0);flows.SelectNode(node.Id);Check(flows.Rows.Count>=2,"source trace table "+language);
            flows.SelectArrow(flows.Arrows.First(a=>a.Internal));Check(flows.Rows.Count==1,"arrow detail "+language);
            flows.ClearSelectionCommand.Execute(null);
            RootScrollViewer.ScrollToTop();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
            VisualEvidenceCapture.Capture(this,Path.Combine(directory,language+"-screen.png"));
            // Render the entire graph independently of the viewport clip.
            var graph=CostFlowsPanel.GraphCanvas;var parent=(ScrollViewer)graph.Parent;parent.Content=null;
            var holder=new Window{Content=graph,Width=graph.Width+40,Height=graph.Height+40,ShowInTaskbar=false,WindowStyle=WindowStyle.None};
            holder.Show();holder.UpdateLayout();VisualEvidenceCapture.Capture(graph,Path.Combine(directory,language+"-graph.png"));
            holder.Content=null;holder.Close();parent.Content=graph;
            Width=960;await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);UpdateLayout();
            Check(CostFlowsPanel.DetailsGrid.IsVisible,"narrow details accessible "+language);
            VisualEvidenceCapture.Capture(this,Path.Combine(directory,language+"-narrow.png"));
            var first=graph.Children.OfType<Button>().First();Check(first.Focus()&&first.IsKeyboardFocusWithin,"graph keyboard focus "+language);
        }
        File.WriteAllText(Path.Combine(directory,"checks.json"),JsonSerializer.Serialize(new{Status="passed",Checks=checks},new JsonSerializerOptions{WriteIndented=true}));
    }
}
