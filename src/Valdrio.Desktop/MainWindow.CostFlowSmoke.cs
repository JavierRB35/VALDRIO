using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
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
            Check(flows.CenterBalances.Sum(r=>r.FinalValue)==6500m && flows.NodeBalances.Sum(r=>r.SecondaryNetValue)==0m,
                "centre summary conserves final cost without reciprocal double counting "+language);
            Check(flows.NodeBalances.All(r=>r.PrimaryValue+r.SecondaryNetValue==r.FinalValue),"node bridge reconciles "+language);
            Check(flows.ReconciliationState==flows["Balanced"],"resource reconciliation state "+language);
            var original=CanonicalSnapshotJson.Serialize(flows.SelectedRun!.Run);
            var originalId=flows.SelectedRun.Run.Id;var originalCount=flows.Runs.Count;
            Check(flows.CalculateCommand.CanExecute(null),"explicit calculation enabled "+language);
            await flows.CalculateCommand.ExecuteAsync(null);
            Check(flows.Runs.Count==originalCount && flows.SelectedRun?.Run.Id==originalId &&
                  flows.Status==flows["NoChanges"],"unchanged study reuses prior completed run without saving "+language);
            Check(original==CanonicalSnapshotJson.Serialize(flows.SelectedRun!.Run),"prior run is byte-identical "+language);
            await flows.EnterAsync();Check(original==CanonicalSnapshotJson.Serialize(flows.SelectedRun!.Run),"query leaves execution unchanged "+language);
            var node=flows.Nodes.First(n=>n.Stage==0);flows.SelectNode(node.Id);Check(flows.Rows.Count>=2,"source trace table "+language);
            Check(flows.HighlightedNodeIds.Contains(node.Id) && flows.HighlightedArrows.Any(a=>a.SourceId==node.Id),"full primary path selected "+language);
            Check(flows.HighlightedNodeIds.Any(id=>id.StartsWith("3:",StringComparison.Ordinal)),"selected source traces into final costs "+language);
            var reciprocal=flows.Arrows.First(a=>a.Internal);
            flows.SelectArrow(reciprocal);
            Check(flows.Rows.Count==1 && flows.HighlightedArrows.Contains(reciprocal),"selected transfer highlights the complete route "+language);
            Check(flows.HighlightedNodeIds.Any(id=>id.StartsWith("1:",StringComparison.Ordinal)) &&
                  flows.HighlightedNodeIds.Any(id=>id.StartsWith("3:",StringComparison.Ordinal)),
                  "highlight spans upstream primary and downstream final "+language);
            Check(flows.Arrows.Where(a=>a.Row is not null).All(a=>a.DisplayLabel?.Contains('%')==true),
                  "graph transfers include percentages "+language);
            Check(flows.Nodes.Where(n=>n.Stage is 0 or 1 or 3).All(n=>
                  n.DisplayAmount is { } label && Regex.IsMatch(label, @"[.,][0-9]{2} EUR$")),
                  "node money uses exactly two decimals "+language);
            flows.ClearSelectionCommand.Execute(null);
            flows.SelectedBalance=flows.NodeBalances.First(r=>r.FinalValue==6500m);
            Check(flows.Rows.Any(r=>r.Stage==flows["FinalStage"]) && flows.HasHighlightedRoute,"summary selects final trace "+language);
            flows.SelectedBalance=null; flows.ClearSelectionCommand.Execute(null);
            Check(!flows.HasHighlightedRoute,"clearing detail removes route highlight "+language);
            RootScrollViewer.ScrollToTop();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
            VisualEvidenceCapture.Capture(this,Path.Combine(directory,language+"-screen.png"));
            // Both graphs must render separately, not as a clipped giant fixed-width canvas.
            foreach (var (graph, label) in new[] {
                (CostFlowsPanel.PrimaryGraphCanvas,"primary"),
                (CostFlowsPanel.SecondaryGraphCanvas,"secondary") })
            {
                Check(graph.Children.OfType<Button>().Any(),"graph has selectable nodes "+label+" "+language);
                var parent=(ScrollViewer)graph.Parent;
                Check(graph.Width<=Math.Max(550,parent.ActualWidth+40),"diagram fits viewport when possible "+label+" "+language);
                parent.Content=null;
                var holder=new Window{Content=graph,Width=graph.Width+40,Height=Math.Min(graph.Height+40,1100),ShowInTaskbar=false,WindowStyle=WindowStyle.None};
                holder.Show();holder.UpdateLayout();
                VisualEvidenceCapture.Capture(graph,Path.Combine(directory,language+"-"+label+"-graph.png"));
                holder.Content=null;holder.Close();parent.Content=graph;
            }
            Width=960;await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);UpdateLayout();
            Check(CostFlowsPanel.DetailsGrid.IsVisible,"narrow details accessible "+language);
            VisualEvidenceCapture.Capture(this,Path.Combine(directory,language+"-narrow.png"));
            var first=CostFlowsPanel.PrimaryGraphCanvas.Children.OfType<Button>().First();
            Check(first.Focus()&&first.IsKeyboardFocusWithin,"graph keyboard focus "+language);
            flows.ShowAllFlowLabels=true;
            Check(CostFlowsPanel.PrimaryGraphCanvas.Children.OfType<Button>().Count() > flows.Nodes.Count(n=>n.Stage is 0 or 1),
                "all transfer labels remain available "+language);
            flows.ShowAllFlowLabels=false;
            var edgePath=CostFlowsPanel.PrimaryGraphCanvas.Children.OfType<System.Windows.Shapes.Path>().First();
            Check(edgePath.Focus(),"connection accepts keyboard focus "+language);
            edgePath.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,
                PresentationSource.FromVisual(edgePath)!,0,System.Windows.Input.Key.Enter) { RoutedEvent=System.Windows.Input.Keyboard.KeyDownEvent });
            Check(flows.Rows.Count==1 && flows.HasHighlightedRoute,"enter selects connection "+language);
            flows.ClearSelectionCommand.Execute(null);
            foreach(var scale in new[]{1.25,1.5,2.0})
            {
                CostFlowsPanel.LayoutTransform=new System.Windows.Media.ScaleTransform(scale,scale);
                await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);UpdateLayout();
                Check(CostFlowsPanel.CenterBalanceGrid.ActualWidth>0 && CostFlowsPanel.DetailsGrid.IsVisible,
                    "scaled layout remains accessible "+scale+" "+language);
            }
            CostFlowsPanel.LayoutTransform=System.Windows.Media.Transform.Identity;
        }
        File.WriteAllText(Path.Combine(directory,"checks.json"),JsonSerializer.Serialize(new{Status="passed",Checks=checks},new JsonSerializerOptions{WriteIndented=true}));
    }
    internal async Task RunT13CostFlowChecks(string directory, string studyName, decimal expectedTotal, int expectedPrincipals)
    {
        var main=viewModel!;var studies=main.Studies!;var flows=main.CostFlows!;
        var checks=new List<string>();
        void Check(bool yes,string label){if(!yes)throw new InvalidOperationException(label);checks.Add(label);}
        await NavigateWorkflow(2);studies.StudyChoice=studies.Studies.Single(s=>s.Value.Name == studyName);await studies.LoadCommand.ExecuteAsync(null);
        foreach(var language in new[]{"es","en"})
        {
            main.ApplyPresentationPreset(language,language=="es"?"es-ES":"en-US","UTC",false,false);
            Width=1600;Height=1000;await NavigateWorkflow(8);await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);UpdateLayout();
            Check(flows.Nodes.Select(n=>n.Stage).Distinct().Count()==4,"four stages "+language);
            Check(flows.Arrows.Count(a=>a.Row?.Stage==flows["SecondaryStage"])>=4,"reciprocal and principal transfers "+language);
            Check(flows.Arrows.Count(a=>a.Internal)>=2,"reciprocal internal arrows "+language);
            Check(flows.Nodes.Count(n=>n.Stage==3)==expectedPrincipals &&
                flows.SelectedRun?.Run.Output?.Secondary?.Facts.Sum(f=>f.PostedAmount.Amount)==expectedTotal,
                "T13 final total and principal nodes "+language);
            Check(flows.CurrentState==flows["Current"],"current revision "+language);
            Check(flows.CenterBalances.Sum(r=>r.FinalValue)==expectedTotal && flows.NodeBalances.Sum(r=>r.SecondaryNetValue)==0m,
                "centre summary conserves final cost without reciprocal double counting "+language);
            Check(flows.NodeBalances.All(r=>r.PrimaryValue+r.SecondaryNetValue==r.FinalValue),"node bridge reconciles "+language);
            Check(flows.ReconciliationState==flows["Balanced"],"resource reconciliation state "+language);
            var original=CanonicalSnapshotJson.Serialize(flows.SelectedRun!.Run);
            var originalId=flows.SelectedRun.Run.Id;var originalCount=flows.Runs.Count;
            Check(flows.CalculateCommand.CanExecute(null),"explicit calculation enabled "+language);
            await flows.CalculateCommand.ExecuteAsync(null);
            Check(flows.Runs.Count==originalCount && flows.SelectedRun?.Run.Id==originalId &&
                  flows.Status==flows["NoChanges"],"unchanged study reuses prior completed run without saving "+language);
            Check(original==CanonicalSnapshotJson.Serialize(flows.SelectedRun!.Run),"prior run is byte-identical "+language);
            await flows.EnterAsync();Check(original==CanonicalSnapshotJson.Serialize(flows.SelectedRun!.Run),"query leaves execution unchanged "+language);
            var node=flows.Nodes.First(n=>n.Stage==0);flows.SelectNode(node.Id);Check(flows.Rows.Count>=2,"source trace table "+language);
            Check(flows.HighlightedNodeIds.Contains(node.Id) && flows.HighlightedArrows.Any(a=>a.SourceId==node.Id),"full primary path selected "+language);
            Check(flows.HighlightedNodeIds.Any(id=>id.StartsWith("3:",StringComparison.Ordinal)),"selected source traces into final costs "+language);
            var reciprocal=flows.Arrows.First(a=>a.Internal);
            flows.SelectArrow(reciprocal);
            Check(flows.Rows.Count==1 && flows.HighlightedArrows.Contains(reciprocal),"selected transfer highlights the complete route "+language);
            Check(flows.HighlightedNodeIds.Any(id=>id.StartsWith("1:",StringComparison.Ordinal)) &&
                  flows.HighlightedNodeIds.Any(id=>id.StartsWith("3:",StringComparison.Ordinal)),
                  "highlight spans upstream primary and downstream final "+language);
            Check(flows.Arrows.Where(a=>a.Row is not null).All(a=>a.DisplayLabel?.Contains('%')==true),
                  "graph transfers include percentages "+language);
            Check(flows.Nodes.Where(n=>n.Stage is 0 or 1 or 3).All(n=>
                  n.DisplayAmount is { } label && Regex.IsMatch(label, @"[.,][0-9]{2} EUR$")),
                  "node money uses exactly two decimals "+language);
            flows.ClearSelectionCommand.Execute(null);
            Check(!flows.HasHighlightedRoute,"clearing detail removes route highlight "+language);
            RootScrollViewer.ScrollToTop();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
            VisualEvidenceCapture.Capture(this,Path.Combine(directory,language+"-screen.png"));
            // Both graphs must render separately, not as a clipped giant fixed-width canvas.
            foreach (var (graph, label) in new[] {
                (CostFlowsPanel.PrimaryGraphCanvas,"primary"),
                (CostFlowsPanel.SecondaryGraphCanvas,"secondary") })
            {
                Check(graph.Children.OfType<Button>().Any(),"graph has selectable nodes "+label+" "+language);
                var parent=(ScrollViewer)graph.Parent;
                Check(graph.Width<=Math.Max(550,parent.ActualWidth+40),"diagram fits viewport when possible "+label+" "+language);
                parent.Content=null;
                var holder=new Window{Content=graph,Width=graph.Width+40,Height=Math.Min(graph.Height+40,1100),ShowInTaskbar=false,WindowStyle=WindowStyle.None};
                holder.Show();holder.UpdateLayout();
                VisualEvidenceCapture.Capture(graph,Path.Combine(directory,language+"-"+label+"-graph.png"));
                holder.Content=null;holder.Close();parent.Content=graph;
            }
            Width=960;await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);UpdateLayout();
            Check(CostFlowsPanel.DetailsGrid.IsVisible,"narrow details accessible "+language);
            VisualEvidenceCapture.Capture(this,Path.Combine(directory,language+"-narrow.png"));
            var first=CostFlowsPanel.PrimaryGraphCanvas.Children.OfType<Button>().First();
            Check(first.Focus()&&first.IsKeyboardFocusWithin,"graph keyboard focus "+language);
        }
        File.WriteAllText(Path.Combine(directory,"checks.json"),JsonSerializer.Serialize(new{Status="passed",Study=studyName,ExpectedTotal=expectedTotal,Checks=checks},new JsonSerializerOptions{WriteIndented=true}));
    }

}
