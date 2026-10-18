using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Valdrio.Application;
using Valdrio.Domain;
using Valdrio.Localization;
namespace Valdrio.Desktop;

public sealed record AuxiliaryModeChoice(AuxiliaryCapacityMode Value, string Label);
public sealed partial class AuxiliaryCenterEdit : ObservableObject
{
    public required AuxiliaryCapacityRow Value { get; init; }
    public required string Name { get; init; }
    public required string Unit { get; init; }
    public required string Normal { get; init; }
    public required string Real { get; init; }
    public required IReadOnlyList<AuxiliaryModeChoice> Choices { get; init; }
    [ObservableProperty] private AuxiliaryModeChoice? mode;
    public ObservableCollection<AuxiliaryServiceEdit> Services { get; } = [];
}
public sealed partial class AuxiliaryServiceEdit : ObservableObject
{
    public required string Group { get; init; }
    public required string GroupLabel { get; init; }
    public required EntityId Destination { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    [ObservableProperty] private string quantity = "0";
}
public sealed partial class AuxiliaryCapacityEditor : ObservableObject
{
    private readonly StudyWorkspaceViewModel host;
    private readonly UiTextCatalog texts = new();
    private AllocationWorkspaceSnapshot? workspace;
    private CalculationRun? run;
    public ObservableCollection<AuxiliaryCenterEdit> Rows { get; } = [];
    public ObservableCollection<AnalystAnswer> Answers { get; } = [];
    [ObservableProperty] private AuxiliaryCenterEdit? selectedRow;
    [ObservableProperty] private string feedback = "";
    [ObservableProperty] private bool dirty;
    public string this[string key] => texts.Get("Aux." + key, host.PhysicalUiCulture);
    public string Context => workspace is null ? "" : workspace.Resources.Study.Name + " · " + workspace.Resources.Study.PeriodStart + " — " + workspace.Resources.Study.PeriodEnd;
    public bool CanEdit => host.PhysicalWritable && workspace?.Configuration.SecondaryClosed == false;
    public bool CanReopen => host.PhysicalWritable && workspace?.Configuration.SecondaryClosed == true;
    public bool CanSave => CanEdit && Dirty;
    public bool CanCalculate => host.PhysicalWritable && workspace is not null && Rows.Count > 0;
    public bool CanExport => run is not null && !Dirty;
    public string Summary => run?.Output?.Secondary is { } s
        ? string.Format(host.PhysicalFormatCulture,this["Coverage"],workspace!.Configuration.AuxiliaryCapacity?.Rows.Count(r=>r.Mode==AuxiliaryCapacityMode.OwnerOnceCapacity) ?? 0,Rows.Count) + " · " + this["PrincipalTotal"] + ": " + Money(s.Facts.Where(f => !f.IsCapacityVariance).Sum(f => f.PostedAmount.Amount)) + " · " +
          this["Variance"] + ": " + Money(s.Facts.Where(f => f.IsCapacityVariance).Sum(f => f.PostedAmount.Amount)) + " · " +
          this["Difference"] + ": " + Money(s.Reconciliations.Sum(r => r.Difference.Amount))
        : this["NoResult"];
    public AuxiliaryCapacityEditor(StudyWorkspaceViewModel host) { this.host = host; Reload(); }
    private string Qty(decimal? value) => value?.ToString("0.############", host.PhysicalFormatCulture) ?? this["Unknown"];
    private string Money(decimal value) => value.ToString("#,0.00########", host.PhysicalFormatCulture) + " " + workspace!.Resources.Study.Currency.FunctionalCurrencyCode;
    public void Reload()
    {
        var selected = SelectedRow?.Value.NodeId; Rows.Clear(); SelectedRow = null; Answers.Clear(); run = null;
        try
        {
            var study = host.Current ?? throw new InvalidOperationException();
            workspace = host.AuxiliaryAllocations!.ReadAllocations(study.CompanyId, study.Id);
            var physical = host.ActivePhysicalActivity!.ReadCurrent(study.CompanyId, study.Id);
            var model = workspace.Resources.Model.Structure;
            var choices = Enum.GetValues<AuxiliaryCapacityMode>().Select(m => new AuxiliaryModeChoice(m, this[m.ToString()])).ToArray();
            var groups = workspace.Resources.Resources.SelectMany(r => r.Parts).Select(p => workspace.Configuration.SecondaryPartRoutingGroups?
                .SingleOrDefault(g => g.PartId == p.Id)?.ComponentRoutingGroup ?? "all").Distinct().DefaultIfEmpty("all").Order().ToArray();
            foreach (var node in model.Nodes.Where(n => n.IsActive && n.Role == NodeRole.Auxiliary).OrderBy(n => n.Code))
            {
                var pool = model.Pools.Single(p => p.NodeId == node.Id);
                var saved = workspace.Configuration.AuxiliaryCapacity?.Rows.SingleOrDefault(r => r.NodeId == node.Id);
                var observation = physical.SingleOrDefault(o => o.PoolId == pool.Id);
                var row = new AuxiliaryCenterEdit { Value = new(node.Id, pool.Id, saved?.Mode ?? AuxiliaryCapacityMode.ThroughFlow, observation, saved?.ServiceSets ?? []),
                    Name = node.Code + " · " + workspace.Resources.Model.PhysicalCenters.Single(c => c.Id == node.PhysicalCenterId).Name,
                    Unit = workspace.Resources.Model.DriverUnits.Single(u => u.Id == pool.DriverUnitId).Symbol,
                    Normal = Qty(observation?.NormalQuantity), Real = Qty(observation?.RealQuantity), Choices = choices,
                    Mode = choices.Single(c => c.Value == (saved?.Mode ?? AuxiliaryCapacityMode.ThroughFlow)) };
                foreach (var group in groups)
                foreach (var destination in model.Nodes.Where(n => n.IsActive && n.Role is NodeRole.Auxiliary or NodeRole.Principal).OrderBy(n => n.Code))
                {
                    var line = new AuxiliaryServiceEdit { Group = group, GroupLabel = group == "all" ? this["AllComponents"] : group, Destination = destination.Id, Code = destination.Code, Name = destination.Code + " · " + workspace.Resources.Model.PhysicalCenters.Single(c => c.Id == destination.PhysicalCenterId).Name,
                        Quantity = Qty(saved?.ServiceSets.SingleOrDefault(s => s.ComponentRoutingGroup == group)?.Services.SingleOrDefault(s => s.DestinationNodeId == destination.Id)?.Quantity ?? 0) };
                    line.PropertyChanged += (_, _) => MarkDirty(); row.Services.Add(line);
                }
                row.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(row.Mode)) MarkDirty(); }; Rows.Add(row);
            }
            run = host.PhysicalRuns!.ReadRuns(study.CompanyId, study.Id).MatchingCompleted(workspace.Resources.Study.Revision);
            Dirty = workspace.Configuration.AuxiliaryCapacity is null && Rows.Count > 0 || Rows.Any(r => r.Mode?.Value == AuxiliaryCapacityMode.OwnerOnceCapacity &&
                CanonicalSnapshotJson.Serialize(r.Value.Observation) != CanonicalSnapshotJson.Serialize(workspace.Configuration.AuxiliaryCapacity?.Rows.SingleOrDefault(x => x.NodeId == r.Value.NodeId)?.Observation));
            if (Dirty) run = null;
            SelectedRow = Rows.FirstOrDefault(r => r.Value.NodeId == selected) ?? Rows.FirstOrDefault();
            Feedback = Rows.Count == 0 ? this["NoCenters"] : CanReopen ? this["Closed"] : Dirty ? workspace.Configuration.AuxiliaryCapacity is null ? this["Unsaved"] : this["Stale"] : this["Help"];
        }
        catch (Exception ex) when (ex is DomainRuleException or CompanyFileException or InvalidOperationException) { ShowError(ex); }
        Notify();
    }
    private void Notify() { foreach (var name in new[] { nameof(Context), nameof(CanEdit), nameof(CanReopen), nameof(CanSave), nameof(CanCalculate), nameof(CanExport), nameof(Summary) }) OnPropertyChanged(name); }
    private void MarkDirty() { Dirty = true; run = null; Feedback = this["Unsaved"]; RefreshAnswers(); Notify(); }
    partial void OnSelectedRowChanged(AuxiliaryCenterEdit? value) => RefreshAnswers();
    private AuxiliaryCapacityPlan Plan()
    {
        return new(Rows.Select(row => row.Value with { Mode = row.Mode!.Value,
            ServiceSets = row.Services.GroupBy(s => s.Group).Select(g => new AuxiliaryServiceSet(g.Key,
                g.Select(l => new AuxiliaryServiceQuantity(l.Destination, Parse(l.Quantity))).ToArray())).ToArray() }).ToArray());
    }
    private decimal Parse(string text)
    {
        if (!decimal.TryParse(text, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, host.PhysicalFormatCulture, out var value) || value < 0)
            throw AuxiliaryCapacityRules.Error("AUX_INPUT_INVALID", "Invalid");
        return value;
    }
    public void Save()
    {
        if (!CanSave || workspace is null) return;
        try
        {
            var study = workspace.Resources.Study;
            var plan = Plan();
            // The displayed T14 measurement must still be the one the user reviewed.
            var current = host.ActivePhysicalActivity!.ReadCurrent(study.CompanyId, study.Id);
            if (Rows.Any(r => r.Value.Observation is not null && !current.Any(o => o.Id == r.Value.Observation.Id && o.Version == r.Value.Observation.Version)))
                throw AuxiliaryCapacityRules.Error("AUX_PHYSICAL_STALE", "Stale");
            host.AuxiliaryAllocations!.ChangeAllocations(new(study.CompanyId, study.Id, study.Revision, new(Guid.NewGuid()), "local-user", workspace.Configuration.WithAuxiliaryCapacity(plan)));
            Reload(); Feedback = this["Saved"];
        }
        catch (Exception ex) when (ex is DomainRuleException or CompanyFileException or InvalidOperationException) { ShowError(ex); }
    }
    public void Reopen()
    {
        if (!CanReopen || workspace is null) return;
        try { var s = workspace.Resources.Study; host.AuxiliaryAllocations!.ChangeAllocations(new(s.CompanyId, s.Id, s.Revision, new(Guid.NewGuid()), "local-user", workspace.Configuration.WithSecondaryClosed(false))); Reload(); }
        catch (Exception ex) when (ex is DomainRuleException or CompanyFileException or InvalidOperationException) { ShowError(ex); }
    }
    public void Validate()
    {
        if (workspace is null) return;
        try { var candidate = new AllocationWorkspaceSnapshot(workspace.Resources, workspace.Keys, workspace.Configuration.WithAuxiliaryCapacity(Plan()), workspace.ConfigurationRevision);
            _ = SecondaryAllocationService.Calculate(candidate); Feedback = this["Valid"]; }
        catch (Exception ex) when (ex is DomainRuleException or CompanyFileException or InvalidOperationException) { ShowError(ex); }
    }
    public void Calculate()
    {
        if (workspace is null || !host.PhysicalWritable) return;
        if (Dirty) { Feedback = this["SaveFirst"]; return; }
        try
        {
            var s = workspace.Resources.Study;
            var started = host.PhysicalRuns!.PrepareRun(new(s.CompanyId, s.Id, s.Revision, new(Guid.NewGuid()), "local-user"));
            var output = CalculationRunService.Calculate(started); host.PhysicalRuns.PublishRun(started.Id, output);
            Reload(); Feedback = output.Secondary is not null ? this["Calculated"] : string.Join(" ", output.Diagnostics.Select(d => texts.Get(d.MessageKey, host.PhysicalUiCulture)));
        }
        catch (Exception ex) when (ex is DomainRuleException or CompanyFileException or InvalidOperationException) { ShowError(ex); }
    }
    private void ShowError(Exception ex) { Feedback = ex is DomainRuleException d ? texts.Get(d.Diagnostic.MessageKey, host.PhysicalUiCulture) : this["Failed"]; }
    public string? Export() => run is null ? null : CanonicalSnapshotJson.Serialize(run);
    public void Import(string csv)
    {
        if (!CanEdit) return;
        try
        {
            var lines = csv.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2 || lines[0].TrimStart('\uFEFF') != "Center;Group;Destination;Quantity") throw AuxiliaryCapacityRules.Error("AUX_INPUT_INVALID", "ImportHelp");
            var changes = new List<(AuxiliaryServiceEdit Line, string Value)>();
            foreach (var line in lines.Skip(1))
            {
                var c = line.Split(';'); if (c.Length != 4) throw AuxiliaryCapacityRules.Error("AUX_INPUT_INVALID", "ImportHelp");
                var node = workspace!.Resources.Model.Structure.Nodes.SingleOrDefault(n => n.Code == c[0]);
                var row = Rows.SingleOrDefault(r => r.Value.NodeId == node?.Id);
                var target = row?.Services.SingleOrDefault(s => s.Group == c[1] && s.Code == c[2]);
                if (target is null || changes.Any(x => x.Line == target)) throw AuxiliaryCapacityRules.Error("AUX_INPUT_INVALID", "ImportHelp");
                changes.Add((target, Qty(Parse(c[3]))));
            }
            foreach (var change in changes) change.Line.Quantity = change.Value;
            Feedback = this["Imported"];
        }
        catch (Exception ex) when (ex is DomainRuleException or InvalidOperationException) { ShowError(ex); }
    }
    private void RefreshAnswers()
    {
        Answers.Clear(); var row = SelectedRow; if (row is null) return;
        var secondary = run?.Output?.Secondary;
        var initial = run?.Output?.Primary?.Facts.Where(f => f.NodeId == row.Value.NodeId).Sum(f => f.PostedAmount.Amount);
        var incoming = run?.Output?.Transfers.Where(t => t.DestinationNodeId == row.Value.NodeId).Sum(t => t.Amount);
        var outgoing = run?.Output?.Transfers.Where(t => t.SourceNodeId == row.Value.NodeId).Sum(t => t.Amount);
        var variance = secondary?.Facts.Where(f => f.IsCapacityVariance && f.TerminalNodeId == row.Value.NodeId).Sum(f => f.PostedAmount.Amount);
        var evaluated = row.Mode?.Value == AuxiliaryCapacityMode.OwnerOnceCapacity;
        string Value(decimal? x) => x is null ? this["NoResult"] : Money(x.Value);
        Answers.Add(new(this["QCost"], this["Own"] + ": " + Value(initial) + "; " + this["Received"] + ": " + Value(incoming)));
        Answers.Add(new(this["QUse"], row.Real + " / " + row.Normal + " " + row.Unit));
        Answers.Add(new(this["QVariance"], evaluated ? Value(variance) + (variance < 0 ? " · " + this["Overactivity"] : "") : row.Mode?.Value == AuxiliaryCapacityMode.Pending ? this["PendingHelp"] : this["NotEvaluated"]));
        Answers.Add(new(this["QTransfer"], Value(outgoing)));
        Answers.Add(new(this["QDestinations"], string.Join(" · ", row.Services.Where(s => s.Quantity != "0").Select(s => s.GroupLabel + ": " + s.Name + " " + s.Quantity + " " + row.Unit))));
        string coverage;
        try { coverage = !evaluated ? this["NotEvaluated"] : string.Join(" · ", row.Services.GroupBy(s => s.Group).Select(g => g.First().GroupLabel + ": " + Qty(g.Sum(s => Parse(s.Quantity))) + " / " + row.Real + " " + row.Unit)); }
        catch (DomainRuleException) { coverage = this["Invalid"]; }
        Answers.Add(new(this["QCoverage"], coverage));
        Answers.Add(new(this["QBalance"], secondary is null ? this["NoResult"] : Value(initial) + " + " + Value(incoming) + " = " + Value(outgoing) + " + " + Value(variance) + ". " + this["CirculationHelp"]));
    }
}
