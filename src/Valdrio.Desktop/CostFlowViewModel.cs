using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Valdrio.Application;
using Valdrio.Domain;
using Valdrio.Localization;
namespace Valdrio.Desktop;

public sealed record ReportConcentrationRow(string Node, string Role, decimal Amount, string Share, string Cumulative, string Behavior, string GraphId)
{ public string AmountDisplay { get; init; } = ""; }
public sealed record FlowRow(string Stage, string Resource, string Source, string Destination, string Rule,
    string Assigned, string Received, string Terminal, string Detail, string SourceId, string DestinationId);
public sealed record FlowNode(string Id, int Stage, string Label, string Amount, string? DisplayAmount = null);
public sealed record FlowArrow(string SourceId, string DestinationId, string Label, FlowRow? Row, bool Internal = false, string? DisplayLabel = null);
public sealed record RunChoice(CalculationRun Run, string Label);
public sealed record RunBalanceRow(string Label, string Role, string Primary, string SecondaryNet, string Final,
    decimal PrimaryValue, decimal SecondaryNetValue, decimal FinalValue, string? GraphId, string Exact, string Received = "", string Ceded = "", string Adjustment = "", string Growth = "");
public sealed record FunctionCostRow(string Function, string Cost, decimal NumericAmount, int NodeCount);
public sealed partial class CostFlowViewModel : ObservableObject
{
    private readonly CompanyFileViewModel company;
    private readonly StudyWorkspaceViewModel studies;
    private readonly UiTextCatalog texts;
    private readonly PresentationPreferencesService preferences;
    private RunHistory? history;
    private AllocationWorkspaceSnapshot? liveWorkspace;
    [ObservableProperty] private bool useCurrentPrimary = false;
    partial void OnUseCurrentPrimaryChanged(bool value) { if (value) SecondaryPhase = false; Present(); OnPropertyChanged(nameof(ReportContext)); }
    public string ExecutiveSummary { get; private set; } = "";
    public ObservableCollection<ReportConcentrationRow> Concentration { get; } = [];
    private readonly Dictionary<string, string[]> reportMembers = [];
    [ObservableProperty] private bool groupByCenter;
    partial void OnGroupByCenterChanged(bool value) => Present();
    [ObservableProperty] private bool secondaryPhase;
    partial void OnSecondaryPhaseChanged(bool value) { if (value) UseCurrentPrimary = false; Present(); }
    [ObservableProperty] private string reportFilter = "";
    partial void OnReportFilterChanged(string value) => Present();
    [ObservableProperty] private decimal concentrationThreshold = 80m;
    partial void OnConcentrationThresholdChanged(decimal value) => Present();
    public string ConcentrationThresholdHelp => this["ThresholdHelp"];
    public string PhaseLabel => this[SecondaryPhase ? "SecondaryReportPhase" : "PrimaryReportPhase"];
    public string DisplayReportAmount(decimal value) => UiCompactNumbers.Number(value, Format);
    public CultureInfo NumericCulture => Format;
    public string ReportTitle => this["ReportTitle"];
    public IEnumerable<string[]> ExportRows()
    {
        yield return [ReportTitle]; yield return [ReportContext]; yield return [CurrentState];
        yield return [PhaseLabel, this["Filter"], ReportFilter]; yield return [ExecutiveSummary];
        yield return [this["Node"], this["Role"], this["Assigned"], this["Participation"], this["Cumulative"], this["Behavior"]];
        foreach (var row in Concentration) yield return [row.Node, row.Role, DisplayReportAmount(row.Amount), row.Share, row.Cumulative, row.Behavior];
        yield return [this["GlobalScope"]];
        yield return [this["CenterBalances"]];
        yield return [this["Node"], this["PrimaryBalance"], this["Received"], this["Ceded"], this["Adjustments"], this["SecondaryNet"], this["FinalBalance"], this["Growth"]];
        foreach (var row in CenterBalances) yield return [row.Label, row.Primary, row.Received, row.Ceded, row.Adjustment, row.SecondaryNet, row.Final, row.Growth];
        yield return [this["Reconciliation"], this["ReconcileHelp"]];
        yield return [this["Resource"], this["Effective"], this["Direct"], this["Excluded"], this["Pending"], this["FinalBalance"], this["Difference"]];
        foreach (var row in Reconciliation) yield return [row.Resource, row.Source, row.Destination, row.Rule, row.Assigned, row.Received, row.Terminal];
        yield return [this["Details"]];
        yield return [this["Stage"], this["Resource"], this["Source"], this["Destination"], this["Rule"], this["Assigned"], this["Details"]];
        foreach (var row in allRows.Where(r => r.Stage == this[SecondaryPhase ? "FinalStage" : "PrimaryStage"]))
            yield return [row.Stage, row.Resource, row.Source, row.Destination, row.Rule, row.Assigned, row.Detail];
    }

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
    public ObservableCollection<RunBalanceRow> CenterBalances { get; } = [];
    public ObservableCollection<RunBalanceRow> NodeBalances { get; } = [];
    public ObservableCollection<FunctionCostRow> FunctionBalances { get; } = [];
    [ObservableProperty] private RunBalanceRow? selectedBalance;
    [ObservableProperty] private string effectiveTotal = "—";
    [ObservableProperty] private string finalTotal = "—";
    [ObservableProperty] private string reconciliationState = "—";
    [ObservableProperty] private bool showAllFlowLabels;
    partial void OnShowAllFlowLabelsChanged(bool value) => GraphChanged?.Invoke(this, EventArgs.Empty);
    partial void OnSelectedBalanceChanged(RunBalanceRow? value) { if (value?.GraphId is { } id) SelectNode(id); }
    public List<FlowNode> Nodes { get; } = [];
    public List<FlowArrow> Arrows { get; } = [];
    private readonly HashSet<string> highlightedNodes = new(StringComparer.Ordinal);
    private readonly HashSet<FlowArrow> highlightedArrows = [];
    public IReadOnlySet<string> HighlightedNodeIds => highlightedNodes;
    public IReadOnlySet<FlowArrow> HighlightedArrows => highlightedArrows;
    public bool HasHighlightedRoute => highlightedNodes.Count > 0;
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
    public string ReportContext
    {
        get
        {
            var study = (UseCurrentPrimary ? studies.Current : (SelectedRun?.Run.Snapshot.Input as AllocationStudySnapshotInput)?.Allocations.Resources.Study) ?? studies.Current;
            if (study is null) return company.Snapshot?.Name ?? "";
            var scenario = texts.Get("Study." + study.ScenarioKind, CultureInfo.GetCultureInfo(preferences.Current.UiLanguageTag));
            return (company.Snapshot?.Name ?? "") + " · " + study.Name + " · " + study.PeriodStart.Value.ToString("d", Format) + " – " + study.PeriodEnd.Value.ToString("d", Format) +
                " · " + scenario + " · " + study.Currency.FunctionalCurrencyCode + " · " + this["Revision"] + " " + study.Revision + " · " + PhaseLabel +
                (UseCurrentPrimary ? "" : " · " + SelectedRun?.Label);
        }
    }

    private static string RunState(CalculationRun run) => run.Status == CalculationRunStatus.Failed && run.Output is { } output && !output.Diagnostics.Any(d => d.Code is "RUN_OVERFLOW" or "RUN_INPUT_REQUIRED") ? "Partial" : run.Status.ToString();
    public bool HasError => !UseCurrentPrimary && SelectedRun?.Run is { Status: CalculationRunStatus.Failed } run &&
        (run.Output is null || run.Output.Diagnostics.Any(d => d.Code is "RUN_OVERFLOW" or "RUN_INPUT_REQUIRED"));
    public string ExecutionDuration => SelectedRun?.Run is { CompletedAtUtc: { } completed } run
        ? string.Format(Format, this["Duration"], (completed.Value - run.StartedAtUtc.Value).TotalSeconds)
        : this["DurationUnavailable"];
    partial void OnSelectedRunChanged(RunChoice? value)
    {
        UseCurrentPrimary = false;
        if (value?.Run.Snapshot.Input is AllocationStudySnapshotInput { Allocations.Configuration.SecondaryClosed: true }) SecondaryPhase = true;
        Present();
        if (value is not null) Status = CurrentState;
        RebuildAuxiliaryReport();
        OnPropertyChanged(nameof(ExecutionDuration));
        OnPropertyChanged(nameof(ReportContext));
    }
    partial void OnSelectedRowChanged(FlowRow? value) { if (value is not null) Selection = value.Detail; }
    public async Task EnterAsync()
    {
        if (cancellation is not null) return;
        try
        {
            if (company.ActiveRuns is { } store && studies.Current is { } study)
                history = await Task.Run(() => store.ReadRuns(study.CompanyId, study.Id));
            else history = null;
            liveWorkspace = company.ActiveAllocations is { } allocations && studies.Current is { } selected ? await Task.Run(() => allocations.ReadAllocations(selected.CompanyId, selected.Id)) : null;
            RebuildChoices(); Status = Runs.Count == 0 ? this["NoRuns"] : this["ReadOnlyResults"];
        }
        catch (Exception ex) when (ex is CompanyFileException or DomainRuleException or InvalidOperationException)
        { Status = this["LoadFailed"]; history = null; liveWorkspace = null; RebuildChoices(); }
        Commands();
    }
    private void RebuildChoices()
    {
        var id = SelectedRun?.Run.Id;
        Runs.Clear();
        if (history is not null)
            foreach (var run in history.Runs.Reverse())
                Runs.Add(new(run, $"{TimeZoneInfo.ConvertTime(run.StartedAtUtc.Value, TimeZoneInfo.FindSystemTimeZoneById(preferences.Current.DisplayTimeZoneId)).ToString("g", Format) + " " + preferences.Current.DisplayTimeZoneId} · {this[RunState(run)]} · {this["Revision"]} {run.Snapshot.InputRevision}"));
        SelectedRun = Runs.FirstOrDefault(r => r.Run.Id == history?.CurrentRunId) ?? Runs.FirstOrDefault(r => r.Run.Id == id) ?? Runs.FirstOrDefault();
        UseCurrentPrimary = Runs.Count == 0;
        RebuildAuxiliaryReport();
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
            // Query only, before capturing a snapshot: an unchanged calculation
            // must not persist an extra snapshot, audit event or execution row.
            var latest = await Task.Run(() => store.ReadRuns(study.CompanyId, study.Id, token), token);
            var previous = latest.MatchingCompleted(study.Revision);
            if (previous is not null)
            {
                history = latest;
                RebuildChoices();
                SelectedRun = Runs.First(r => r.Run.Id == previous.Id);
                UseCurrentPrimary = false;
                Status = this["NoChanges"];
                return;
            }
            run = await Task.Run(() => store.PrepareRun(new(study.CompanyId, study.Id, study.Revision, new EntityId(Guid.NewGuid()), "local-user"), token), token);
            Status = this["Calculating"];
            var output = await Task.Run(() => CalculationRunService.Calculate(run, token));
            Status = this["Publishing"];
            var published = await Task.Run(() => store.PublishRun(run.Id, output, token));
            Status = this[RunState(published)];
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
        if (run is not null)
        {
            SelectedRun = Runs.FirstOrDefault(r => r.Run.Id == run.Id);
            UseCurrentPrimary = false;
        }
        if (SelectedRun?.Run.Status != CalculationRunStatus.Completed) Status = operationStatus;
    }
    // Trace upstream and downstream of the chosen node or transfer, including
    // reciprocal cycles. The visited sets ensure termination in cyclic graphs.
    private void HighlightRoute(string? nodeId, FlowArrow? chosenArrow = null)
    {
        highlightedNodes.Clear(); highlightedArrows.Clear();
        if (nodeId is null && chosenArrow is null) { GraphChanged?.Invoke(this, EventArgs.Empty); return; }
        var origin = chosenArrow?.SourceId ?? nodeId!;
        var destination = chosenArrow?.DestinationId ?? nodeId!;
        highlightedNodes.Add(origin);
        highlightedNodes.Add(destination);
        if (chosenArrow is not null) highlightedArrows.Add(chosenArrow);
        void Walk(string start, bool upstream)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal) { start };
            var pending = new Queue<string>();
            pending.Enqueue(start);
            while (pending.Count > 0)
            {
                var current = pending.Dequeue();
                foreach (var edge in Arrows.Where(a => upstream ? a.DestinationId == current : a.SourceId == current))
                {
                    highlightedArrows.Add(edge);
                    var next = upstream ? edge.SourceId : edge.DestinationId;
                    highlightedNodes.Add(next);
                    if (visited.Add(next)) pending.Enqueue(next);
                }
            }
        }
        Walk(origin, upstream: true);
        Walk(destination, upstream: false);
        GraphChanged?.Invoke(this, EventArgs.Empty);
    }
    public void SelectNode(string? id)
    {
        if (id is not null && reportMembers.TryGetValue(id, out var members))
        {
            Rows.Clear();
            foreach (var row in allRows.Where(r => members.Contains(r.SourceId) || members.Contains(r.DestinationId))) Rows.Add(row);
            highlightedNodes.Clear(); highlightedArrows.Clear(); foreach (var member in members) highlightedNodes.Add(member);
            Selection = Concentration.FirstOrDefault(r => r.GraphId == id)?.Node ?? id;
            GraphChanged?.Invoke(this, EventArgs.Empty); return;
        }
        if (SelectedBalance?.GraphId != id) SelectedBalance = null;
        SelectedRow = null;
        Rows.Clear();
        foreach (var row in allRows.Where(r => id is null || r.SourceId == id || r.DestinationId == id)) Rows.Add(row);
        Selection = id is null ? this["SelectHint"] : Nodes.FirstOrDefault(n => n.Id == id)?.Label ?? this["SelectHint"];
        if (id?.StartsWith("0:", StringComparison.Ordinal) == true)
            Selection += Environment.NewLine + this["SelectedContributions"] + Environment.NewLine + string.Join(Environment.NewLine,
                allRows.Where(r => r.SourceId == id && r.Stage == this["PrimaryStage"]).Select(r => r.Destination + ": " + r.Assigned));
        HighlightRoute(id);
    }
    public void SelectArrow(FlowArrow arrow)
    {
        SelectedBalance = null;
        if (arrow.Row is null)
        {
            SelectNode(arrow.SourceId);
            HighlightRoute(null, arrow);
            return;
        }
        Rows.Clear(); Rows.Add(arrow.Row); SelectedRow = arrow.Row; Selection = arrow.Row.Detail;
        HighlightRoute(null, arrow);
    }
    private void Present()
    {
        Concentration.Clear(); reportMembers.Clear(); ExecutiveSummary = "";
        Nodes.Clear(); Arrows.Clear(); Rows.Clear(); Reconciliation.Clear(); allRows.Clear();
        SelectedBalance = null; CenterBalances.Clear(); NodeBalances.Clear(); FunctionBalances.Clear();
        EffectiveTotal = FinalTotal = ReconciliationState = "—";
        highlightedNodes.Clear(); highlightedArrows.Clear();
        Summary = Diagnostics = CurrentState = ""; Selection = this["SelectHint"];
        OnPropertyChanged(nameof(HasError));
        var run = UseCurrentPrimary ? null : SelectedRun?.Run;
        var w = UseCurrentPrimary ? liveWorkspace : (run?.Snapshot.Input as AllocationStudySnapshotInput)?.Allocations;
        if (w is null)
        {
            CurrentState = this["Partial"];
            ExecutiveSummary = this[SecondaryPhase ? "SecondaryPending" : "NoRuns"];
            GraphChanged?.Invoke(this, EventArgs.Empty); OnPropertyChanged(string.Empty); return;
        }
        CurrentState = UseCurrentPrimary ? this["CurrentPrimary"] : run?.Status == CalculationRunStatus.Completed ? this[history?.CurrentRunId == run.Id ? "Current" : "Historical"] :
            (run?.Snapshot.InputRevision != studies.Current?.Revision ? this["Historical"] + " · " : "") + this["Partial"];
        var model = w.Resources.Model;
        var centers = model.PhysicalCenters.ToDictionary(c => c.Id);
        var nodes = model.Structure.Nodes.ToDictionary(n => n.Id);
        string Node(EntityId id) { var n = nodes[id]; var c = centers[n.PhysicalCenterId]; return $"{c.Code} · {c.Name} / {model.Structure.Pools.FirstOrDefault(p => p.NodeId == id)?.Description} · {n.Code} · {this[n.Role.ToString()]}"; }
        string Account(EntityId id) { var o = w.Resources.Origins.Single(o => o.Id == id); var description = w.Configuration.Descriptions.FirstOrDefault(d => d.Account == o.Code)?.Description ?? string.Join(" / ", o.Descriptions); return o.Code + " — " + description; }
        string Origin(EntityId id)
        {
            var origin = w.Resources.Origins.Single(o => o.Id == id);
            var ledger = w.Resources.Ledger.Entries.SingleOrDefault(r => r.Id == id);
            return string.Join(Environment.NewLine, origin.Rows.Select(r =>
                this["ImportedSource"] + ": " + r.FileName + " · " + this["Record"] + " " + r.RecordNumber + " · " +
                r.Conversion.OriginalMoney.Amount.ToString(Format) + " " + r.Conversion.OriginalMoney.Currency + " → " + Amount(r.Conversion.ConvertedOriginalMoney.Amount))) +
                Environment.NewLine + string.Join(Environment.NewLine, (ledger?.Adjustments ?? []).Select(a =>
                    this["Adjustments"] + ": " + Amount(a.Conversion.ConvertedOriginalMoney.Amount) + " · " + a.Type + " · " + a.Reason));
        }
        string Key(EntityId? id) => id is null ? "—" : w.Keys.FirstOrDefault(k => k.Id == id)?.Name ?? id.ToString()!;
        string Amount(decimal value) => UiCompactNumbers.Money(value, w.Resources.Study.Currency.FunctionalCurrencyCode.Value, Format);
        // Compact graph-only numbers; persisted audit and detail remain exact.
        string GraphAmount(decimal value) => Amount(value);
        string GraphPercent(decimal share) => UiCompactNumbers.Percent(share * 100m, Format);
        string nodeId(int stage, EntityId id) => stage + ":" + id;
        var output = run?.Output;
        if (output?.Diagnostics is { } diagnosticList)
            Diagnostics = string.Join(Environment.NewLine, diagnosticList.Select(d => new DomainDiagnosticFormatter(texts).Format(d,
                CultureInfo.GetCultureInfo(preferences.Current.UiLanguageTag), Format).Message));
        foreach(var n in nodes.Values) Diagnostics=Diagnostics.Replace(n.Id.ToString(),Node(n.Id),StringComparison.Ordinal);
        if (run?.Status == CalculationRunStatus.Started) Diagnostics = this["Interrupted"];
        PrimaryAllocationResult? primary = output?.Primary;
        if (UseCurrentPrimary)
        {
            try { primary = PrimaryAllocationService.Calculate(w); }
            catch (DomainRuleException ex) { Diagnostics = new DomainDiagnosticFormatter(texts).Format(ex.Diagnostic, CultureInfo.GetCultureInfo(preferences.Current.UiLanguageTag), Format).Message; }
        }
        if (primary is not null)
        {
            foreach (var r in primary.Resources)
            {
                Nodes.Add(new(nodeId(0, r.ResourceId), 0, Account(r.ResourceId), Amount(r.Effective.Amount), GraphAmount(r.Effective.Amount)));
                var terminal = output?.Secondary?.ResourceBridges.SingleOrDefault(b => b.ResourceId == r.ResourceId);
                Reconciliation.Add(new(this["OriginStage"], Account(r.ResourceId), Amount(r.Effective.Amount),
                    Amount(r.Direct.Amount), Amount(r.Excluded.Amount), Amount(r.Pending.Amount),
                    terminal is null ? "—" : Amount(terminal.Terminal.Amount), terminal is null ? "—" : Amount(terminal.Difference.Amount),
                    this["ReconcileHelp"], nodeId(0,r.ResourceId), ""));
            }
            foreach (var group in primary.Facts.GroupBy(f => f.NodeId))
                Nodes.Add(new(nodeId(1,group.Key),1,Node(group.Key),Amount(group.Sum(f => f.PostedAmount.Amount)), GraphAmount(group.Sum(f => f.PostedAmount.Amount))));
            foreach (var fact in primary.Facts)
            {
                var row = new FlowRow(this["PrimaryStage"],Account(fact.ResourceId),Account(fact.ResourceId),Node(fact.NodeId),Key(fact.KeyVersionId),
                    Amount(fact.PostedAmount.Amount),Amount(fact.PostedAmount.Amount),"—",
                    $"{Key(fact.KeyVersionId)} · {GraphPercent(fact.Share)} · {Amount(fact.PostedAmount.Amount)}" + Environment.NewLine + Origin(fact.ResourceId),
                    nodeId(0,fact.ResourceId),nodeId(1,fact.NodeId));
                allRows.Add(row); Arrows.Add(new(row.SourceId,row.DestinationId,row.Assigned + " · " + GraphPercent(fact.Share),row,DisplayLabel:GraphAmount(fact.PostedAmount.Amount)+" · "+GraphPercent(fact.Share)));
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
                initial -= secondary.Facts.Where(f => f.IsCapacityVariance && f.TerminalNodeId == id &&
                    (w.Configuration.SecondaryPartRoutingGroups?.FirstOrDefault(g=>g.PartId==f.PartId)?.ComponentRoutingGroup ?? "all")==group.ComponentRoutingGroup).Sum(f=>f.PostedAmount.Amount);
                var received=output.Transfers.Where(t=>t.Internal && t.DestinationNodeId==id && t.Group==group.ComponentRoutingGroup).Sum(t=>t.Amount);
                Nodes.Add(new(graphId,2,Node(id)+(group.ComponentRoutingGroup=="all"?"":" · "+group.ComponentRoutingGroup),
                    string.Format(Format,this["Available"],UiCompactNumbers.Number(initial,Format),UiCompactNumbers.Number(received,Format),Amount(group.Solution.Gross[i])),
                     this["GrossAmount"]+": "+GraphAmount(group.Solution.Gross[i])));
                Arrows.Add(new(nodeId(1,id),graphId,Amount(initial),null,DisplayLabel:GraphAmount(initial)));
            }
            foreach (var group in secondary.Facts.GroupBy(f=>f.TerminalNodeId))
                Nodes.Add(new(nodeId(3,group.Key),3,Node(group.Key) + (group.Any(f=>f.IsCapacityVariance) ? " · " + texts.Get("Aux.Variance", CultureInfo.GetCultureInfo(preferences.Current.UiLanguageTag)) : ""),Amount(group.Sum(f=>f.PostedAmount.Amount)),GraphAmount(group.Sum(f=>f.PostedAmount.Amount))));
            foreach (var capacity in secondary.Facts.Where(f=>f.IsCapacityVariance))
                Arrows.Add(new(nodeId(1,capacity.TerminalNodeId),nodeId(3,capacity.TerminalNodeId),Amount(capacity.PostedAmount.Amount),null,DisplayLabel:GraphAmount(capacity.PostedAmount.Amount)));
            foreach (var flow in output.Transfers)
            {
                var source=nodeId(2,flow.SourceNodeId)+":"+flow.Group;
                var dest=flow.Internal ? nodeId(2,flow.DestinationNodeId)+":"+flow.Group : nodeId(3,flow.DestinationNodeId);
                var row=new FlowRow(this["SecondaryStage"],this["AllResources"],Node(flow.SourceNodeId),Node(flow.DestinationNodeId),(flow.MeasuredServices ? texts.Get("Aux.MeasuredServices", CultureInfo.GetCultureInfo(preferences.Current.UiLanguageTag)) : Key(flow.KeyId)),Amount(flow.Amount),Amount(flow.Amount),flow.Internal?"—":Node(flow.DestinationNodeId),
                    this["GrossHelp"]+" · "+flow.Group+" · "+(flow.MeasuredServices ? texts.Get("Aux.MeasuredServices", CultureInfo.GetCultureInfo(preferences.Current.UiLanguageTag)) : Key(flow.KeyId))+" · "+GraphPercent(flow.Share),source,dest);
                allRows.Add(row); Arrows.Add(new(source,dest,(flow.Internal ? nodes[flow.SourceNodeId].Code+" → "+nodes[flow.DestinationNodeId].Code+"\n" : "")+GraphPercent(flow.Share)+" · "+Amount(flow.Amount),row,flow.Internal,
                    (flow.Internal ? nodes[flow.SourceNodeId].Code+" → "+nodes[flow.DestinationNodeId].Code+" · " : "")+GraphAmount(flow.Amount)+" · "+GraphPercent(flow.Share)));
            }
            foreach (var group in primary!.Facts.Where(f=>nodes[f.NodeId].Role==NodeRole.Principal).GroupBy(f=>f.NodeId))
                Arrows.Add(new(nodeId(1,group.Key),nodeId(3,group.Key),Amount(group.Sum(f=>f.PostedAmount.Amount)),null,DisplayLabel:GraphAmount(group.Sum(f=>f.PostedAmount.Amount))));
            foreach (var fact in secondary.Facts)
            {
                var links=secondary.ExplanationLinks.Where(l=>l.ResourceId==fact.ResourceId && l.PartId==fact.PartId && l.TerminalNodeId==fact.TerminalNodeId && (l.SourceBehavior is null || l.SourceBehavior==fact.Behavior)).ToArray();
                allRows.Add(new(this["FinalStage"],Account(fact.ResourceId),string.Join("; ",links.Select(l=>Node(l.PrimaryNodeId)).Distinct()),Node(fact.TerminalNodeId),
                    string.Join("; ",links.SelectMany(l=>(l.SecondaryKeyVersionIds ?? []).Append(l.PrimaryKeyVersionId ?? default)).Where(id=>id.IsValid).Select(id=>Key(id)).Distinct()),
                    Amount(fact.PostedAmount.Amount),Amount(fact.PostedAmount.Amount),Node(fact.TerminalNodeId),
                    string.Join(Environment.NewLine,links.Select(l=>Node(l.PrimaryNodeId)+" → "+Node(l.TerminalNodeId)+" · "+Amount(l.RawContribution)))+Environment.NewLine+
                    this["Adjustments"]+": "+Amount(fact.NumericAdjustment+fact.RoundingAdjustment)+Environment.NewLine+Origin(fact.ResourceId),nodeId(0,fact.ResourceId),nodeId(3,fact.TerminalNodeId)));
            }
            Summary=string.Format(Format,this["Summary"],Amount(secondary.ResourceBridges.Sum(r=>r.Effective.Amount)),Amount(secondary.Facts.Sum(f=>f.PostedAmount.Amount)),Amount(secondary.ResourceBridges.Sum(r=>r.Difference.Amount)));
            if (run?.Status == CalculationRunStatus.Completed)
            {
                EffectiveTotal = GraphAmount(secondary.ResourceBridges.Sum(r => r.Effective.Amount));
                FinalTotal = GraphAmount(secondary.Facts.Sum(f => f.PostedAmount.Amount));
                ReconciliationState = this[secondary.ResourceBridges.All(r => r.Difference.Amount == 0m) ? "Balanced" : "Unbalanced"];
                var balances = Valdrio.Reporting.RunCostBalanceProjector.Build(run);
                RunBalanceRow Balance(string label, string role, decimal p, decimal s, decimal f, string? id, EntityId[] members)
                {
                    var received = output.Transfers.Where(t => members.Contains(t.DestinationNodeId) && !members.Contains(t.SourceNodeId)).Sum(t => t.Amount);
                    var ceded = output.Transfers.Where(t => members.Contains(t.SourceNodeId) && !members.Contains(t.DestinationNodeId)).Sum(t => t.Amount);
                    var adjustment = f - p - received + ceded;
                    return new(label, role, GraphAmount(p), GraphAmount(s), GraphAmount(f), p, s, f, id,
                        this["PrimaryBalance"] + ": " + Amount(p) + Environment.NewLine +
                        this["Received"] + ": " + Amount(received) + Environment.NewLine + this["Ceded"] + ": " + Amount(ceded) + Environment.NewLine +
                        this["Adjustments"] + ": " + Amount(adjustment) + Environment.NewLine + this["FinalBalance"] + ": " + Amount(f),
                        GraphAmount(received), GraphAmount(ceded), GraphAmount(adjustment), p == 0 ? this["UndefinedGrowth"] : GraphPercent(s / p));
                }
                foreach (var balance in balances.OrderBy(b => Node(b.NodeId), StringComparer.CurrentCulture))
                    NodeBalances.Add(Balance(Node(balance.NodeId), this[balance.Role.ToString()], balance.Primary,
                        balance.SecondaryNet, balance.Final, nodeId(balance.Role == NodeRole.Principal ? 3 : 1, balance.NodeId), [balance.NodeId]));
                foreach (var group in balances.GroupBy(b => b.CenterId).OrderBy(g => centers[g.Key].Code, StringComparer.CurrentCulture))
                    CenterBalances.Add(Balance(centers[group.Key].Code + " · " + centers[group.Key].Name, "",
                        group.Sum(b => b.Primary), group.Sum(b => b.SecondaryNet), group.Sum(b => b.Final), null, group.Select(b => b.NodeId).ToArray()));

                // Destination-based functional summary of frozen principal-node
                // costs. Not sales margin, inventory valuation or a new ledger.
                foreach (var group in Valdrio.Reporting.RunCostBalanceProjector.GroupByFunction(
                    balances, model.Structure.Nodes))
                    FunctionBalances.Add(new(this["Function." + group.Function],
                        GraphAmount(group.Final), group.Final, group.NodeCount));
            }
        }
        if (primary is not null)
        {
            var pending = primary.Resources.Where(r => r.Pending.Amount != 0 || primary.Parts.Any(p => p.ResourceId == r.ResourceId && !p.Covered)).ToArray();
            var assigned = primary.Facts.Sum(f => f.PostedAmount.Amount);
            var effective = primary.Resources.Sum(r => r.Effective.Amount);
            EffectiveTotal = GraphAmount(effective);
            var positive = pending.Sum(r => Math.Max(0, r.Pending.Amount));
            var credits = pending.Sum(r => Math.Min(0, r.Pending.Amount));
            ExecutiveSummary = string.Format(Format, this["Executive"], GraphAmount(effective), GraphAmount(assigned), pending.Length,
                GraphAmount(positive), GraphAmount(credits), GraphAmount(positive + credits), GraphAmount(positive - credits),
                GraphAmount(primary.Resources.Sum(r => r.Excluded.Amount)), GraphAmount(primary.Resources.Sum(r => r.Direct.Amount)));
            if (pending.Length > 0)
                Diagnostics = this["Incomplete"] + Environment.NewLine + string.Join(Environment.NewLine, pending.Select(r => Account(r.ResourceId) + " · " + Amount(r.Pending.Amount) + " · " + this["ResolvePrimary"]));
            if (UseCurrentPrimary)
            {
                CurrentState += " · " + this[primary.IsComplete ? "PrimaryComplete" : "Partial"];
                ReconciliationState = this[primary.Resources.All(r => r.Difference.Amount == 0) ? "Balanced" : "Unbalanced"];
                FinalTotal = this["SecondaryPending"];
            }
            var auxiliaryCount = nodes.Values.Count(n => n.IsActive && n.Role == NodeRole.Auxiliary);
            var evaluated = w.Configuration.AuxiliaryCapacity?.Rows.Count(r => r.Mode == AuxiliaryCapacityMode.OwnerOnceCapacity) ?? 0;
            ExecutiveSummary += Environment.NewLine + string.Format(Format, this["CapacityCoverage"], evaluated, auxiliaryCount);
            var costs = SecondaryPhase
                ? (output?.Secondary?.Facts.Where(f => !f.IsCapacityVariance).GroupBy(f => f.TerminalNodeId).Select(g => (Id: g.Key, Amount: g.Sum(f => f.PostedAmount.Amount))) ?? []).ToArray()
                : primary.Facts.GroupBy(f => f.NodeId).Select(g => (Id: g.Key, Amount: g.Sum(f => f.PostedAmount.Amount))).ToArray();
            var groups = costs.GroupBy(c => GroupByCenter ? nodes[c.Id].PhysicalCenterId : c.Id).Select(g => new
            {
                Id = g.Key, Amount = g.Sum(c => c.Amount), Members = g.Select(c => c.Id).ToArray(),
                Label = GroupByCenter ? centers[g.Key].Name + " · " + centers[g.Key].Code : Node(g.Key),
                Role = string.Join(" / ", g.Select(c => this[nodes[c.Id].Role.ToString()]).Distinct())
            });
            var filtered = groups.Where(c => string.IsNullOrWhiteSpace(ReportFilter) || c.Label.Contains(ReportFilter, StringComparison.CurrentCultureIgnoreCase) || c.Role.Contains(ReportFilter, StringComparison.CurrentCultureIgnoreCase)).OrderByDescending(c => c.Amount).ToArray();
            var positiveTotal = filtered.Where(c => c.Amount > 0).Sum(c => c.Amount);
            var net = filtered.Sum(c => c.Amount); decimal cumulative = 0;
            foreach (var c in filtered)
            {
                cumulative += Math.Max(0, c.Amount);
                var behaviorFacts = SecondaryPhase
                    ? (output?.Secondary?.Facts.Where(f => !f.IsCapacityVariance && c.Members.Contains(f.TerminalNodeId)).Select(f => (f.Behavior, Amount: f.PostedAmount.Amount)) ?? [])
                    : primary.Facts.Where(f => c.Members.Contains(f.NodeId)).Select(f => (f.Behavior, Amount: f.PostedAmount.Amount));
                var behavior = string.Join(" · ", behaviorFacts.GroupBy(f => f.Behavior).Select(g => texts.Get("AllocationUI.SecondaryBehavior" + g.Key, CultureInfo.GetCultureInfo(preferences.Current.UiLanguageTag)) + ": " + GraphAmount(g.Sum(f => f.Amount))));
                var graphId = GroupByCenter ? "center:" + c.Id : nodeId(SecondaryPhase ? 3 : 1, c.Id);
                if (GroupByCenter) reportMembers[graphId] = c.Members.Select(id => nodeId(SecondaryPhase ? 3 : 1, id)).ToArray();
                Concentration.Add(new(c.Label, c.Role, c.Amount,
                    c.Amount > 0 && net > 0 ? GraphPercent(c.Amount / positiveTotal) : "—",
                    c.Amount > 0 && net > 0 ? GraphPercent(cumulative / positiveTotal) : "—", behavior, graphId)
                    { AmountDisplay = DisplayReportAmount(c.Amount) });
            }
            ExecutiveSummary += Environment.NewLine + this["FilteredNet"] + ": " + GraphAmount(net) + " · " + this["PositiveBase"] + ": " + GraphAmount(positiveTotal) + " · " + this["Credits"] + ": " + GraphAmount(filtered.Where(c => c.Amount < 0).Sum(c => c.Amount));
        }
        foreach(var row in allRows) Rows.Add(row);
        OnPropertyChanged(nameof(ExecutiveSummary)); OnPropertyChanged(nameof(ReportContext)); OnPropertyChanged(nameof(PhaseLabel));
        OnPropertyChanged(nameof(HasError)); GraphChanged?.Invoke(this,EventArgs.Empty);
    }
}
