using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Valdrio.Application;
using Valdrio.Domain;
using Valdrio.Localization;
namespace Valdrio.Desktop;

public sealed record FlowRow(string Stage, string Resource, string Source, string Destination, string Rule,
    string Assigned, string Received, string Terminal, string Detail, string SourceId, string DestinationId);
public sealed record FlowNode(string Id, int Stage, string Label, string Amount);
public sealed record FlowArrow(string SourceId, string DestinationId, string Label, FlowRow? Row, bool Internal = false);
public sealed record RunChoice(CalculationRun Run, string Label);
public sealed partial class CostFlowViewModel : ObservableObject
{
    private readonly CompanyFileViewModel company;
    private readonly StudyWorkspaceViewModel studies;
    private readonly UiTextCatalog texts;
    private readonly PresentationPreferencesService preferences;
    private RunHistory? history;
    private CancellationTokenSource? cancellation;
    private readonly List<FlowRow> allRows = [];
    public CostFlowViewModel(CompanyFileViewModel company, StudyWorkspaceViewModel studies, UiTextCatalog texts, PresentationPreferencesService preferences)
    {
        var earlierBusy = company.WorkspaceBusy; company.WorkspaceBusy = () => cancellation is not null || earlierBusy?.Invoke() == true;
        this.company = company; this.studies = studies; this.texts = texts; this.preferences = preferences;
        CalculateCommand = new AsyncRelayCommand(CalculateAsync, () => cancellation is null && company.CanWrite && studies.Current is not null);
        RefreshCommand = new AsyncRelayCommand(EnterAsync, () => cancellation is null);
        CancelCommand = new RelayCommand(() => cancellation?.Cancel(), () => cancellation is not null);
        ClearSelectionCommand = new RelayCommand(() => SelectNode(null));
        preferences.Changed += (_, _) => RebuildChoices();
    }
    public string this[string key] => texts.Get("Run." + key, CultureInfo.GetCultureInfo(preferences.Current.UiLanguageTag));
    public string[] Stages => [this["OriginStage"], this["PrimaryStage"], this["SecondaryStage"], this["FinalStage"]];
    private CultureInfo Format => CultureInfo.GetCultureInfo(preferences.Current.FormatCultureTag);
    public ObservableCollection<RunChoice> Runs { get; } = [];
    public ObservableCollection<FlowRow> Rows { get; } = [];
    public ObservableCollection<FlowRow> Reconciliation { get; } = [];
    public List<FlowNode> Nodes { get; } = [];
    public List<FlowArrow> Arrows { get; } = [];
    public event EventHandler? GraphChanged;
    [ObservableProperty] private RunChoice? selectedRun;
    [ObservableProperty] private FlowRow? selectedRow;
    [ObservableProperty] private string status = "";
    [ObservableProperty] private string currentState = "";
    [ObservableProperty] private string summary = "";
    [ObservableProperty] private string diagnostics = "";
    [ObservableProperty] private string selection = "";
    public IAsyncRelayCommand CalculateCommand { get; }
    public IAsyncRelayCommand RefreshCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public IRelayCommand ClearSelectionCommand { get; }
    public bool HasError => SelectedRun?.Run.Status == CalculationRunStatus.Failed;
    partial void OnSelectedRunChanged(RunChoice? value) => Present();
    partial void OnSelectedRowChanged(FlowRow? value) { if (value is not null) Selection = value.Detail; }
    public async Task EnterAsync()
    {
        if (cancellation is not null) return;
        try
        {
            if (company.ActiveRuns is { } store && studies.Current is { } study)
                history = await Task.Run(() => store.ReadRuns(study.CompanyId, study.Id));
            else history = null;
            RebuildChoices(); Status = Runs.Count == 0 ? this["NoRuns"] : this["ReadOnlyResults"];
        }
        catch (Exception ex) when (ex is CompanyFileException or DomainRuleException or InvalidOperationException)
        { Status = this["LoadFailed"]; history = null; RebuildChoices(); }
        Commands();
    }
    private void RebuildChoices()
    {
        var id = SelectedRun?.Run.Id;
        Runs.Clear();
        if (history is not null)
            foreach (var run in history.Runs.Reverse())
                Runs.Add(new(run, $"{run.StartedAtUtc.Value.ToString("u", Format)} · {this[run.Status.ToString()]} · {this["Revision"]} {run.Snapshot.InputRevision}"));
        SelectedRun = Runs.FirstOrDefault(r => r.Run.Id == id) ?? Runs.FirstOrDefault();
        Present(); OnPropertyChanged(string.Empty);
    }
    private void Commands() { company.NotifyWorkspaceStateChanged(); CalculateCommand.NotifyCanExecuteChanged(); RefreshCommand.NotifyCanExecuteChanged(); CancelCommand.NotifyCanExecuteChanged(); }
    private async Task CalculateAsync()
    {
        if (company.ActiveRuns is not { } store || studies.Current is not { } study || cancellation is not null) return;
        cancellation = new(); var token = cancellation.Token; Commands();
        CalculationRun? run = null;
        try
        {
            Status = this["Preparing"];
            run = await Task.Run(() => store.PrepareRun(new(study.CompanyId, study.Id, study.Revision, new EntityId(Guid.NewGuid()), "local-user"), token));
            Status = this["Calculating"];
            var output = await Task.Run(() => CalculationRunService.Calculate(run, token));
            Status = this["Publishing"];
            var published = await Task.Run(() => store.PublishRun(run.Id, output, token));
            Status = this[published.Status.ToString()];
        }
        catch (OperationCanceledException)
        {
            try { if (run is not null) await Task.Run(() => store.CancelRun(run.Id)); Status = this["Cancelled"]; }
            catch (Exception ex) when (ex is CompanyFileException or InvalidOperationException) { Status = this["LoadFailed"]; }
        }
        catch (Exception ex) when (ex is DomainRuleException or CompanyFileException or InvalidOperationException)
        {
            // Keep a failed publication as Started: no partial results and no false cancellation.
            Status = ex is DomainRuleException rule ? new DomainDiagnosticFormatter(texts).Format(rule.Diagnostic,
                CultureInfo.GetCultureInfo(preferences.Current.UiLanguageTag), Format).Message : this["LoadFailed"];
        }
        finally { cancellation.Dispose(); cancellation = null; Commands(); }
        var operationStatus = Status;
        await EnterAsync();
        if (run is not null) SelectedRun = Runs.FirstOrDefault(r => r.Run.Id == run.Id);
        if (SelectedRun?.Run.Status != CalculationRunStatus.Completed) Status = operationStatus;
    }
    public void SelectNode(string? id)
    {
        Rows.Clear();
        foreach (var row in allRows.Where(r => id is null || r.SourceId == id || r.DestinationId == id)) Rows.Add(row);
        Selection = id is null ? this["SelectHint"] : Nodes.FirstOrDefault(n => n.Id == id)?.Label ?? this["SelectHint"];
    }
    public void SelectArrow(FlowArrow arrow)
    {
        if (arrow.Row is null) { SelectNode(arrow.SourceId); return; }
        Rows.Clear(); Rows.Add(arrow.Row); SelectedRow = arrow.Row; Selection = arrow.Row.Detail;
    }
    private void Present()
    {
        Nodes.Clear(); Arrows.Clear(); Rows.Clear(); Reconciliation.Clear(); allRows.Clear();
        Summary = Diagnostics = CurrentState = ""; Selection = this["SelectHint"];
        OnPropertyChanged(nameof(HasError));
        if (SelectedRun?.Run is not { } run) { GraphChanged?.Invoke(this, EventArgs.Empty); return; }
        CurrentState = this[history?.CurrentRunId == run.Id ? "Current" : "Historical"];
        if (run.Snapshot.Input is not AllocationStudySnapshotInput input) return;
        var w = input.Allocations; var model = w.Resources.Model;
        var centers = model.PhysicalCenters.ToDictionary(c => c.Id);
        var nodes = model.Structure.Nodes.ToDictionary(n => n.Id);
        string Node(EntityId id) { var n = nodes[id]; var c = centers[n.PhysicalCenterId]; return $"{c.Code} · {c.Name} / {n.Code} · {this[n.Role.ToString()]}"; }
        string Account(EntityId id) { var o = w.Resources.Origins.Single(o => o.Id == id); var description = w.Configuration.Descriptions.FirstOrDefault(d => d.Account == o.Code)?.Description ?? string.Join(" / ", o.Descriptions); return o.Code + " — " + description; }
        string Key(EntityId? id) => id is null ? "—" : w.Keys.FirstOrDefault(k => k.Id == id)?.Name ?? id.ToString()!;
        string Amount(decimal value) => value.ToString("0.############################", Format) + " " + input.StudyContext.Currency.FunctionalCurrencyCode;
        string nodeId(int stage, EntityId id) => stage + ":" + id;
        var output = run.Output;
        if (output?.Diagnostics is { } diagnosticList)
            Diagnostics = string.Join(Environment.NewLine, diagnosticList.Select(d => new DomainDiagnosticFormatter(texts).Format(d,
                CultureInfo.GetCultureInfo(preferences.Current.UiLanguageTag), Format).Message));
        foreach(var n in nodes.Values) Diagnostics=Diagnostics.Replace(n.Id.ToString(),Node(n.Id),StringComparison.Ordinal);
        if (run.Status == CalculationRunStatus.Started) Diagnostics = this["Interrupted"];
        var primary = output?.Primary;
        if (primary is not null)
        {
            foreach (var r in primary.Resources)
            {
                Nodes.Add(new(nodeId(0, r.ResourceId), 0, Account(r.ResourceId), Amount(r.Effective.Amount)));
                var terminal = output?.Secondary?.ResourceBridges.SingleOrDefault(b => b.ResourceId == r.ResourceId);
                Reconciliation.Add(new(this["OriginStage"], Account(r.ResourceId), Amount(r.Effective.Amount),
                    Amount(r.Direct.Amount), Amount(r.Excluded.Amount), Amount(r.Pending.Amount),
                    terminal is null ? "—" : Amount(terminal.Terminal.Amount), terminal is null ? "—" : Amount(terminal.Difference.Amount),
                    this["ReconcileHelp"], nodeId(0,r.ResourceId), ""));
            }
            foreach (var group in primary.Facts.GroupBy(f => f.NodeId))
                Nodes.Add(new(nodeId(1,group.Key),1,Node(group.Key),Amount(group.Sum(f => f.PostedAmount.Amount))));
            foreach (var fact in primary.Facts)
            {
                var row = new FlowRow(this["PrimaryStage"],Account(fact.ResourceId),Account(fact.ResourceId),Node(fact.NodeId),Key(fact.KeyVersionId),
                    Amount(fact.PostedAmount.Amount),Amount(fact.PostedAmount.Amount),"—",
                    $"{Key(fact.KeyVersionId)} · {(fact.Share * 100m).ToString("0.########",Format)} % · {Amount(fact.PostedAmount.Amount)}",
                    nodeId(0,fact.ResourceId),nodeId(1,fact.NodeId));
                allRows.Add(row); Arrows.Add(new(row.SourceId,row.DestinationId,row.Assigned + " · " + (fact.Share*100m).ToString("0.####",Format)+" %",row));
            }
        }
        if (output?.Secondary is { } secondary)
        {
            foreach (var group in secondary.GroupCirculations)
            for (var i=0;i<group.Solution.AuxiliaryIds.Count;i++)
            {
                var id = EntityId.Parse(group.Solution.AuxiliaryIds[i]);
                var graphId = nodeId(2,id)+":"+group.ComponentRoutingGroup;
                
                var initial = primary!.Facts.Where(f => f.NodeId == id && (w.Configuration.SecondaryPartRoutingGroups?.FirstOrDefault(g=>g.PartId==f.PartId)?.ComponentRoutingGroup ?? "all")==group.ComponentRoutingGroup).Sum(f=>f.PostedAmount.Amount);
                var received=output.Transfers.Where(t=>t.Internal && t.DestinationNodeId==id && t.Group==group.ComponentRoutingGroup).Sum(t=>t.Amount);
                Nodes.Add(new(graphId,2,Node(id)+(group.ComponentRoutingGroup=="all"?"":" · "+group.ComponentRoutingGroup),
                    string.Format(Format,this["Available"],initial.ToString("0.############################",Format),received.ToString("0.############################",Format),Amount(group.Solution.Gross[i]))));
                Arrows.Add(new(nodeId(1,id),graphId,Amount(initial),null));
            }
            foreach (var group in secondary.Facts.GroupBy(f=>f.TerminalNodeId))
                Nodes.Add(new(nodeId(3,group.Key),3,Node(group.Key),Amount(group.Sum(f=>f.PostedAmount.Amount))));
            foreach (var flow in output.Transfers)
            {
                var source=nodeId(2,flow.SourceNodeId)+":"+flow.Group;
                var dest=flow.Internal ? nodeId(2,flow.DestinationNodeId)+":"+flow.Group : nodeId(3,flow.DestinationNodeId);
                var row=new FlowRow(this["SecondaryStage"],this["AllResources"],Node(flow.SourceNodeId),Node(flow.DestinationNodeId),Key(flow.KeyId),Amount(flow.Amount),Amount(flow.Amount),flow.Internal?"—":Node(flow.DestinationNodeId),
                    this["GrossHelp"]+" · "+flow.Group+" · "+Key(flow.KeyId)+" · "+(flow.Share*100m).ToString("0.########",Format)+" %",source,dest);
                allRows.Add(row); Arrows.Add(new(source,dest,(flow.Internal ? nodes[flow.SourceNodeId].Code+" → "+nodes[flow.DestinationNodeId].Code+"\n" : "")+(flow.Share*100m).ToString("0.####",Format)+" % · "+Amount(flow.Amount),row,flow.Internal));
            }
            foreach (var group in primary!.Facts.Where(f=>nodes[f.NodeId].Role==NodeRole.Principal).GroupBy(f=>f.NodeId))
                Arrows.Add(new(nodeId(1,group.Key),nodeId(3,group.Key),Amount(group.Sum(f=>f.PostedAmount.Amount)),null));
            foreach (var fact in secondary.Facts)
            {
                var links=secondary.ExplanationLinks.Where(l=>l.ResourceId==fact.ResourceId && l.PartId==fact.PartId && l.TerminalNodeId==fact.TerminalNodeId && (l.SourceBehavior is null || l.SourceBehavior==fact.Behavior)).ToArray();
                allRows.Add(new(this["FinalStage"],Account(fact.ResourceId),string.Join("; ",links.Select(l=>Node(l.PrimaryNodeId)).Distinct()),Node(fact.TerminalNodeId),
                    string.Join("; ",links.SelectMany(l=>(l.SecondaryKeyVersionIds ?? []).Append(l.PrimaryKeyVersionId ?? default)).Where(id=>id.IsValid).Select(id=>Key(id)).Distinct()),
                    Amount(fact.PostedAmount.Amount),Amount(fact.PostedAmount.Amount),Node(fact.TerminalNodeId),
                    string.Join(Environment.NewLine,links.Select(l=>Node(l.PrimaryNodeId)+" → "+Node(l.TerminalNodeId)+" · "+Amount(l.RawContribution)))+Environment.NewLine+
                    this["Adjustments"]+": "+Amount(fact.NumericAdjustment+fact.RoundingAdjustment),nodeId(0,fact.ResourceId),nodeId(3,fact.TerminalNodeId)));
            }
            Summary=string.Format(Format,this["Summary"],Amount(secondary.ResourceBridges.Sum(r=>r.Effective.Amount)),Amount(secondary.Facts.Sum(f=>f.PostedAmount.Amount)),Amount(secondary.ResourceBridges.Sum(r=>r.Difference.Amount)));
        }
        foreach(var row in allRows) Rows.Add(row);
        OnPropertyChanged(nameof(HasError)); GraphChanged?.Invoke(this,EventArgs.Empty);
    }
}
