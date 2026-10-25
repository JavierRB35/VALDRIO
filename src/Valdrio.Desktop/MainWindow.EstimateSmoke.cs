using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Valdrio.Application;
namespace Valdrio.Desktop;
public partial class MainWindow
{
    internal async Task RunEstimateChecks(string directory)
    {
        Directory.CreateDirectory(directory);
        var main=viewModel!;var studies=main.Studies!;var checks=new List<string>();
        void Check(bool condition,string message) { if(!condition)throw new InvalidOperationException(message);checks.Add(message); }
        async Task Idle(){await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);UpdateLayout();}
        await NavigateWorkflow(2);
        studies.StudyChoice=studies.Studies.Single(s=>s.Value.Name.StartsWith("T15 ·",StringComparison.Ordinal));
        await studies.LoadCommand.ExecuteAsync(null);
        var economic=CanonicalSnapshotJson.Serialize(studies.AuxiliaryAllocations!.ReadAllocations(studies.Current!.CompanyId,studies.Current.Id));
        foreach(var language in new[]{"es","en"})
        {
            main.ApplyPresentationPreset(language,language=="es"?"es-ES":"en-US","UTC",false,false);
            Width=1280;Height=920;
            var page=new EstimateWindow(studies){Owner=this};page.ShowIntegrated();await page.InitializeAsync();await Idle();
            UpdateWorkflow();var editor=page.Editor;
            Check(EstimateStep.IsEnabled && EstimateStep.Tag as string=="Active","Estimates integrated in results menu "+language);
            Check(editor.History.Count>=2 && editor.Recipes.Count>=2,"Saved versions and recipes available "+language);
            editor.Code="UI-"+language;editor.Quantity="120";await editor.PreviewAsync();await Idle();
            Check(editor.CanAccept && editor.Answers.Count==7 && editor.Details.Count==2,"Complete preview with analyst answers "+language);
            Check(!editor.Feedback.StartsWith("Estimate.",StringComparison.Ordinal),"Localized feedback "+language);
            Check(await editor.AcceptAsync() && !editor.Dirty && !editor.CanAccept,"Accept freezes and disables duplicate acceptance "+language);
            var first=editor.Selected!.Value;
            editor.Revise();editor.Quantity="125";await editor.PreviewAsync();
            Check(editor.CanAccept && await editor.AcceptAsync(),"New version accepted "+language);
            var second=editor.Selected!.Value;
            Check(second.Version==2 && first.Preview.Cost.OutputQuantity==120,"First version remains unchanged "+language);
            editor.Selected=editor.History.Single(x=>x.Value.Id==first.Id);
            editor.Comparison=editor.History.Single(x=>x.Value.Id==second.Id);editor.Compare();await Idle();
            Check(editor.Answers.Count==9 && !editor.CanAccept,"Explicit frozen comparison "+language);
            var scroll=((Grid)page.Surface!).Children.OfType<ScrollViewer>().Single();
            scroll.ScrollToTop();await Idle();VisualEvidenceCapture.Capture(this,Path.Combine(directory,language+"-orders.png"));
            scroll.ScrollToBottom();await Idle();VisualEvidenceCapture.Capture(this,Path.Combine(directory,language+"-comparison.png"));
            Check(page.AnswersGrid.Focus(),"Result table accepts keyboard focus "+language);
            editor.New();editor.Code="UI-invalid";editor.Quantity=language=="es"?"1.5":"1,5";await editor.PreviewAsync();
            Check(!editor.CanAccept && editor.Details.Count==0,"Wrong decimal separator cannot produce an accepted estimate "+language);
            page.DiscardDecision=()=>false;Check(!page.CanLeave(),"Unaccepted draft can keep editing "+language);
            Width=800;Height=650;await Idle();scroll.ScrollToTop();await Idle();
            VisualEvidenceCapture.Capture(this,Path.Combine(directory,language+"-compact.png"));
            page.DiscardDecision=()=>true;page.Close();await Idle();
            Check(!workspacePages.Contains(page),"Return from integrated page "+language);
        }
        Check(economic==CanonicalSnapshotJson.Serialize(studies.AuxiliaryAllocations!.ReadAllocations(studies.Current!.CompanyId,studies.Current.Id)),"Orders do not alter actual resource allocation");
        File.WriteAllText(Path.Combine(directory,"checks.json"),JsonSerializer.Serialize(checks,new JsonSerializerOptions{WriteIndented=true}));
    }
}
