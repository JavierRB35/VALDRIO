using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Valdrio.Application;
using Valdrio.Calculation;
using Valdrio.Domain;
using Valdrio.Localization;
namespace Valdrio.Desktop;
public sealed record EstimateHistoryRow(AcceptedEstimate Value, string Label);
public sealed record EstimateDetailRow(string Name, string Quantity, string Rate, string Amount, string Source);
public sealed partial class EstimateEditor : ObservableObject
{
    private readonly StudyWorkspaceViewModel host;
    private readonly UiTextCatalog texts = new();
    private EntityId orderId = new(Guid.NewGuid());
    private EntityId? previousId;
    private EstimatePreview? preview;
    private bool loading;
    public string this[string key] => texts.Get("Estimate." + key, host.PhysicalUiCulture);
    public string Action(string key) => texts.Get("UX." + key, host.PhysicalUiCulture);
    public string Context => host.PhysicalCompany!.Name + " · " + host.Current!.Name;
    public bool Writable => host.PhysicalWritable;
    public bool CanAccept => Writable && preview is not null && !Busy;
    public bool CodeEditable => previousId is null;
    public bool Dirty { get; private set; }
    public ObservableCollection<CompositionChoice<EntityId>> Recipes { get; } = [];
    public ObservableCollection<CompositionChoice<EntityId>> Runs { get; } = [];
    public ObservableCollection<EstimateHistoryRow> History { get; } = [];
    public ObservableCollection<EstimateDetailRow> Details { get; } = [];
    public ObservableCollection<CompositionAnswer> Answers { get; } = [];
    [ObservableProperty] private string code = "";
    [ObservableProperty] private string quantity = "1";
    [ObservableProperty] private EntityId recipe;
    [ObservableProperty] private EntityId run;
    [ObservableProperty] private EstimateHistoryRow? selected;
    [ObservableProperty] private EstimateHistoryRow? comparison;
    [ObservableProperty] private string feedback = "";
    [ObservableProperty] private string summary = "";
    [ObservableProperty] private bool busy;
    partial void OnCodeChanged(string value) => Changed();
    partial void OnQuantityChanged(string value) => Changed();
    partial void OnRecipeChanged(EntityId value) => Changed();
    partial void OnRunChanged(EntityId value) => Changed();
    partial void OnBusyChanged(bool value) => OnPropertyChanged(nameof(CanAccept));
    private void Changed()
    {
        if (loading) return;
        Dirty = true; preview = null; Details.Clear(); Answers.Clear(); Summary = this["Pending"];
        OnPropertyChanged(nameof(CanAccept));
    }
    public EstimateEditor(StudyWorkspaceViewModel host) { this.host = host; }
    public async Task LoadAsync() => await Work(async () =>
    {
        var catalog = await Task.Run(() => host.ActiveCompositions!.ReadCompositions());
        var study = host.Current!;
        var runs = await Task.Run(() => host.PhysicalRuns!.ReadRuns(study.CompanyId, study.Id));
        loading = true; Recipes.Clear(); Runs.Clear();
        foreach (var v in catalog.Versions.Where(v => v.State == CompositionState.Published && v.ModelVersionId == study.SelectedModelVersionId))
            Recipes.Add(new(v.Id, catalog.Items.Single(i => i.Id == v.ProductId).Name + " · v" + v.Number));
        foreach (var r in runs.Runs.Where(r => r.Status == CalculationRunStatus.Completed &&
            r.Snapshot.Input is AllocationStudySnapshotInput input && input.Model.Structure.Id == study.SelectedModelVersionId))
            Runs.Add(new(r.Id, Stamp(r.CompletedAtUtc) + " · " + this["Revision"] + " " + ((AllocationStudySnapshotInput)r.Snapshot.Input).Allocations.Resources.Study.Revision));
        loading = false; await ReloadHistory(); New();
        Feedback = Recipes.Count == 0 ? this["NeedRecipe"] : Runs.Count == 0 ? this["MissingRun"] : this["Help"];
    });
    private async Task ReloadHistory()
    {
        var values = await Task.Run(() => host.ActiveEstimates!.ReadEstimates(host.Current!.Id)); History.Clear();
        foreach (var v in values) History.Add(new(v, v.Preview.Input.Request.OrderCode + " · v" + v.Version + " · " + Stamp(v.AcceptedAtUtc)));
    }
    public void New()
    {
        loading = true; orderId = new(Guid.NewGuid()); previousId = null; Code = ""; Quantity = "1";
        Recipe = Recipes.LastOrDefault()?.Value ?? default; Run = Runs.LastOrDefault()?.Value ?? default;
        loading = false; Changed(); Dirty = false; OnPropertyChanged(nameof(CodeEditable));
    }
    public void Revise()
    {
        if (Selected is null) return;
        var request = Selected.Value.Preview.Input.Request;
        loading = true; orderId = request.OrderId; previousId = Selected.Value.Id;
        Code = request.OrderCode; Quantity = Q(request.Quantity); Recipe = request.RecipeVersionId; Run = request.RunId;
        loading = false; Changed(); OnPropertyChanged(nameof(CodeEditable)); Feedback = this["RevisionHelp"];
    }
    public async Task PreviewAsync() => await Work(async () =>
    {
        preview = null; OnPropertyChanged(nameof(CanAccept)); Details.Clear(); Answers.Clear(); Summary = this["Pending"];
        if (!decimal.TryParse(Quantity.Trim(), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            host.PhysicalFormatCulture, out var quantity) || quantity <= 0) throw Valdrio.Calculation.EstimateCalculator.Error("Number");
        var request = new EstimateRequest(host.PhysicalCompany!.Id, host.Current!.Id, orderId, Code.Trim(), Recipe, Run, quantity, previousId);
        var catalog = await Task.Run(() => host.ActiveCompositions!.ReadCompositions());
        var pools = host.PhysicalModels!.ReadModels().Where(m => m.Status == ModelStatus.Published)
            .SelectMany(m => m.Structure.Pools.Select(p => new CompositionPool(p.Id, m.Structure.Id,
                m.Structure.Nodes.Single(n => n.Id == p.NodeId).Code + " · " + p.Description, p.DriverUnitId,
                m.Structure.Nodes.Single(n => n.Id == p.NodeId).IsActive))).ToArray();
        var physical = Valdrio.Calculation.CompositionCalculator.Preview(catalog, Recipe, quantity, host.PhysicalCompany.Units, pools);
        foreach (var line in physical.Lines.Where(l => !l.Expanded))
        {
            var unit = host.PhysicalCompany.Units.Single(u => u.Id == line.UnitId).Symbol;
            Details.Add(new(line.Name + " · " + line.Path, Q(line.RequiredQuantity) + " " + unit, "—", "—", this["PhysicalOnly"]));
        }
        Answers.Add(new(this["QQuantity"], Q(physical.OutputQuantity) + " · " + this["Batches"] + ": " + Q(physical.StartedBatches)));
        Summary = this["PhysicalOnly"];
        var result = await Task.Run(() => host.ActiveEstimates!.PreviewEstimate(request));
        preview = result; Display(result, this["PreviewState"]); Feedback = this["Ready"]; OnPropertyChanged(nameof(CanAccept));
    });
    public async Task<bool> AcceptAsync()
    {
        var saved = false; if (!CanAccept || preview is null) return false; var candidate = preview;
        await Work(async () =>
        {
            var value = await Task.Run(() => host.ActiveEstimates!.AcceptEstimate(candidate.Input.Request, candidate.InputHash, "local-user"));
            saved = true; Dirty = false; preview = null; previousId = value.Id;
            Display(value.Preview, "v" + value.Version + " · " + Stamp(value.AcceptedAtUtc));
            Feedback = this["Saved"]; OnPropertyChanged(nameof(CodeEditable)); OnPropertyChanged(nameof(CanAccept));
            await ReloadHistory(); Selected = History.Single(x => x.Value.Id == value.Id);
        }); return saved;
    }
    public void ShowHistory()
    {
        if (Selected is null) return; preview = null; OnPropertyChanged(nameof(CanAccept));
        Display(Selected.Value.Preview, "v" + Selected.Value.Version + " · " + Stamp(Selected.Value.AcceptedAtUtc)); Feedback = this["Frozen"];
    }
    public void Compare()
    {
        if (Selected is null || Comparison is null) { Feedback = this["ChooseTwo"]; return; }
        var a = Selected.Value; var b = Comparison.Value;
        if (a.Preview.Input.Request.OrderId != b.Preview.Input.Request.OrderId ||
            a.Preview.Input.Rates.Study.Currency.FunctionalCurrencyCode != b.Preview.Input.Rates.Study.Currency.FunctionalCurrencyCode)
        { Feedback = this["SameOrder"]; return; }
        ShowHistory();
        Answers.Add(new(this["Comparison"], $"v{a.Version} → v{b.Version}: {this["Quantity"]} {Q(a.Preview.Cost.OutputQuantity)} → {Q(b.Preview.Cost.OutputQuantity)}; {this["Total"]} {Money(a.Preview)} → {Money(b.Preview)}; Δ {Q(b.Preview.Cost.Total-a.Preview.Cost.Total)}; {this["UnitCost"]} {Q(a.Preview.Cost.UnitCost)} → {Q(b.Preview.Cost.UnitCost)}"));
        Answers.Add(new(this["Sources"], SourceLabel(a.Preview) + " → " + SourceLabel(b.Preview))); Feedback = this["CompareHelp"];
    }
    private string SourceLabel(EstimatePreview value)
    {
        var recipe=value.Input.Catalog.Versions.Single(x=>x.Id==value.Input.Request.RecipeVersionId);
        var product=value.Input.Catalog.Items.Single(x=>x.Id==recipe.ProductId);
        return product.Name+" · v"+recipe.Number+" · "+value.Input.Rates.Study.Name+" · "+this["Revision"]+" "+value.Input.Rates.Study.Revision
            +" · "+this["Rates"]+": "+string.Join("; ",value.Input.Rates.Rows.Where(x=>x.Observation is not null).Select(x=>x.Name+" v"+x.Observation!.Version));
    }
    private string Stamp(UtcInstant? value) => value is null ? "—" : TimeZoneInfo.ConvertTime(DateTimeOffset.Parse(value.Value.ToCanonicalString(), CultureInfo.InvariantCulture), TimeZoneInfo.FindSystemTimeZoneById(host.PhysicalTimeZoneId)).ToString("g", host.PhysicalFormatCulture) + " " + host.PhysicalTimeZoneId;
    private string Q(decimal value) => value.ToString("0.############", host.PhysicalFormatCulture);
    private string Money(EstimatePreview value) => Q(value.Cost.Total) + " " + value.Input.Rates.Study.Currency.FunctionalCurrencyCode;
    private void Display(EstimatePreview value, string state)
    {
        var currency=value.Input.Rates.Study.Currency.FunctionalCurrencyCode.ToString(); Details.Clear(); Answers.Clear();
        foreach(var line in value.Cost.Lines)
        {
            var unit=value.Input.Units.Single(x=>x.Id==line.Quantity.UnitId).Symbol;
            Details.Add(new(line.Quantity.Name+" · "+line.Quantity.Path,Q(line.Quantity.RequiredQuantity)+" "+unit,
                Q(line.UnitCost)+" "+currency+"/"+unit,Q(line.Amount)+" "+currency,line.Quantity.Activity?this["FrozenRate"]:line.Source));
        }
        Summary=value.Input.Request.OrderCode+" · "+state;
        Answers.Add(new(this["QCost"],Money(value)));
        var recipe=value.Input.Catalog.Versions.Single(x=>x.Id==value.Input.Request.RecipeVersionId);
        var product=value.Input.Catalog.Items.Single(x=>x.Id==recipe.ProductId);
        var outputUnit=value.Input.Units.Single(x=>x.Id==product.UnitId).Symbol;
        Answers.Add(new(this["QUnit"],Q(value.Cost.UnitCost)+" "+currency+"/"+outputUnit));
        Answers.Add(new(this["QQuantity"],Q(value.Cost.OutputQuantity)+" · "+this["Batches"]+": "+Q(value.Cost.StartedBatches)));
        Answers.Add(new(this["QSources"],SourceLabel(value))); Answers.Add(new(this["QMissing"],this["Complete"]));
        Answers.Add(new(this["QVersion"],state)); Answers.Add(new(this["QReal"],this["NoActual"]));
    }
    private async Task Work(Func<Task> action)
    {
        if(Busy)return; Busy=true;host.SetCompositionBusy(true);
        try { await action(); }
        catch(DomainRuleException ex){Feedback=new DomainDiagnosticFormatter(texts).Format(ex.Diagnostic,host.PhysicalUiCulture,host.PhysicalFormatCulture).Message; foreach(var unit in host.PhysicalCompany!.Units) Feedback=Feedback.Replace(unit.Id.ToString(),unit.Symbol,StringComparison.Ordinal);}
        catch(CompanyFileException ex){Feedback=texts.Get(ex.Diagnostic.MessageKey,host.PhysicalUiCulture);}
        finally {Busy=false;host.SetCompositionBusy(false);}
    }
}
