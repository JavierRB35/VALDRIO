using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Valdrio.Application;
using Valdrio.Calculation;
using Valdrio.Domain;
using Valdrio.Localization;

namespace Valdrio.Desktop;

public sealed record AnalystAnswer(string Question, string Answer);
public sealed record PrincipalRateDisplay(PrincipalRateRow Value, string Name, string Unit, string Symbol,
    string Cost, string Numerator, string Denominator, string Rate, string Status);
public sealed record RateDetailDisplay(string Component, string Behavior, string Cost, string Base, string Rate, string Source);
public sealed record RateSummaryDisplay(string Component, string Cost, string Base, string Rate);
public sealed partial class PrincipalRatesEditor : ObservableObject
{
    private readonly StudyWorkspaceViewModel host;
    private readonly UiTextCatalog texts = new();
    public PrincipalRateReport? Report { get; private set; }
    public ObservableCollection<PrincipalRateDisplay> Rows { get; } = [];
    public ObservableCollection<AnalystAnswer> Answers { get; } = [];
    public ObservableCollection<RateDetailDisplay> Details { get; } = [];
    public ObservableCollection<RateSummaryDisplay> Breakdown { get; } = [];
    [ObservableProperty] private string capacitySummary = "";
    [ObservableProperty] private string reviewNotice = "";
    [ObservableProperty] private PrincipalRateDisplay? selectedRow;
    [ObservableProperty] private bool attentionOnly;
    [ObservableProperty] private int selectedRateTabIndex;
    [ObservableProperty] private string feedback = "";
    private PrincipalRateDisplay[] allRows = [];
    private HashSet<EntityId> directFactorNodes = [];
    private CalculationRun? sourceRun;
    public PrincipalRatesEditor(StudyWorkspaceViewModel host) { this.host = host; Reload(); }
    public string this[string key] => texts.Get("Rates." + key, host.PhysicalUiCulture);
    public string Context => host.PhysicalCompany?.Name + " · " + Report?.Study.Name + " · " +
        Report?.Study.PeriodStart.Value.ToString("d", host.PhysicalFormatCulture) + " – " +
        Report?.Study.PeriodEnd.Value.ToString("d", host.PhysicalFormatCulture) + " · " + host.ContextSummary;
    public string SelectedSummary => SelectedRow is not { } row ? "" : string.Format(host.PhysicalFormatCulture, this["SelectedSummary"],
        row.Rate, row.Cost, Quantity(row.Value.Observation?.NormalQuantity), Quantity(row.Value.Observation?.RealQuantity), row.Unit);
    public string Provenance => Report is null ? "" : this["Version"] + ": " + Report.Version + "\n" +
        this["EconomicRun"] + ": " + Report.EconomicRunId + "\n" + this["Policy"] + "\n" +
        this["PhysicalVersion"] + ": " + SelectedRow?.Value.Observation?.Version + "\n" +
        SelectedRow?.Value.Observation?.NormalEvidence?.Source + " · " + SelectedRow?.Value.Observation?.RealEvidence?.Source;
    public bool CanExport => Report?.Rows.Any(r => r.Result is not null) == true;
    private string Quantity(decimal? value) => value?.ToString("0.############", host.PhysicalFormatCulture) ?? this["Unavailable"];
    private string Money(decimal? value)
    {
        return value.HasValue ? UiCompactNumbers.Number(value.Value, host.PhysicalFormatCulture) + " " +
            Report!.Study.Currency.FunctionalCurrencyCode : this["Unavailable"];
    }
    private string Rate(decimal? value, string unit) => value.HasValue ?
        (decimal.Round(value.Value, 2) == value.Value ? "" : "≈ ") + UiCompactNumbers.Number(value.Value, host.PhysicalFormatCulture) + " " + Report!.Study.Currency.FunctionalCurrencyCode + "/" + unit : this["Unavailable"];
    private static bool NeedsCapacityReview(PrincipalRateRow row) => row.Result?.Components.Any(c =>
        c.Source.Kind == RateComponentKind.Consumed && c.Source.AssessmentId is null &&
        (c.Source.Behavior == ResourceBehavior.Fixed || c.Source.Behavior == ResourceBehavior.AssumedFixed)) == true;

    private void PopulateBreakdown(PrincipalRateDisplay selected)
    {
        Breakdown.Clear(); CapacitySummary = ""; ReviewNotice = "";
        if (selected.Value.Result is not { } result) return;
        foreach (var kind in new[] { RateComponentKind.OwnFixed, RateComponentKind.Variable, RateComponentKind.Consumed, RateComponentKind.Period })
        {
            var items = result.Components.Where(c => c.Source.Kind == kind).ToArray();
            if (items.Length == 0) continue;
            var isPeriod = kind == RateComponentKind.Period;
            var basis = isPeriod ? this["NotApplicable"] : Quantity(items.First().Denominator) + " " + selected.Symbol +
                " (" + this[kind == RateComponentKind.OwnFixed ? "Normal" : "Real"] + ")";
            var rate = isPeriod ? this["NotApplicable"] : Rate(items.All(c => c.Rate.HasValue) ?
                items.Sum(c => c.Rate!.Value) : null, selected.Symbol);
            Breakdown.Add(new(this[kind.ToString()], Money(items.Sum(c => c.Source.Amount)), basis, rate));
        }
        if (!result.Components.Any(c => c.Source.Kind == RateComponentKind.OwnFixed))
            CapacitySummary = this["NoCapacityAssessment"];
        else if (result.CapacityVariance is { } variance)
            CapacitySummary = this["CapacityResult"] + ": " + Money(variance) + " · " +
                this[variance < 0 ? "Overactivity" : "Underactivity"];
        ReviewNotice = NeedsCapacityReview(selected.Value) ? this["ReviewFixedConsumed"] : "";
    }

    private (string Numerator, string Denominator) CalculationBasis(PrincipalRateRow row, string unit)
    {
        if (row.Result is not { } result) return (this["Unavailable"], this["Unavailable"]);
        var components = result.Components.Where(c => c.Source.Kind != RateComponentKind.Period).ToArray();
        if (components.Length == 0) return (this["Unavailable"], this["Unavailable"]);
        var normal = components.Where(c => c.Source.Kind == RateComponentKind.OwnFixed).ToArray();
        var real = components.Where(c => c.Source.Kind != RateComponentKind.OwnFixed).ToArray();
        var normalText = normal.Length == 0 ? null : Money(normal.Sum(c => c.Source.Amount));
        var realText = real.Length == 0 ? null : Money(real.Sum(c => c.Source.Amount));
        if (normalText is not null && realText is not null)
            return (this["Normal"] + ": " + normalText + " · " + this["Real"] + ": " + realText,
                this["Normal"] + ": " + Quantity(row.Observation?.NormalQuantity) + " " + unit +
                " · " + this["Real"] + ": " + Quantity(row.Observation?.RealQuantity) + " " + unit);
        var normalOnly = normalText is not null;
        return (normalText ?? realText!,
            Quantity(normalOnly ? row.Observation?.NormalQuantity : row.Observation?.RealQuantity) + " " + unit +
            " (" + this[normalOnly ? "Normal" : "Real"] + ")");
    }
    public void Reload()
    {
        var selected = SelectedRow?.Value.PoolId;
        Rows.Clear(); Answers.Clear(); Details.Clear(); Breakdown.Clear(); CapacitySummary = ""; ReviewNotice = "";
        allRows = []; directFactorNodes.Clear(); SelectedRow = null; Report = null;
        try
        {
            var old = host.Current ?? throw new InvalidOperationException();
            var study = host.PhysicalStudies!.ReadStudies().Single(s => s.Id == old.Id);
            var model = host.PhysicalModels!.ReadModels().Single(m => m.Structure.Id == study.SelectedModelVersionId).Publication!;
            Report = PrincipalRateReportService.Read(study, model, host.PhysicalRuns?.ReadRuns(study.CompanyId, study.Id),
                host.ActivePhysicalActivity!.ReadCurrent(study.CompanyId, study.Id));
            sourceRun = host.PhysicalRuns?.ReadRuns(study.CompanyId, study.Id).Runs.SingleOrDefault(r => r.Id == Report.EconomicRunId);
            // Study-level adapted nodes are frozen with the economic run; do not
            // classify the rate from a different published structure revision.
            var rateNodes = sourceRun?.Snapshot.Input is AllocationStudySnapshotInput input
                ? input.Allocations.Resources.Model.Structure.Nodes
                : model.Structure.Nodes;
            directFactorNodes = rateNodes.Where(NodeCostClassification.IsDirectFactor).Select(n => n.Id).ToHashSet();
            allRows = Report.Rows.Select(row =>
            {
                var definition = host.PhysicalCompany!.Units.Single(u => u.Id == row.UnitId);
                var unit = DefaultActivityUnitCatalog.IsPreset(definition) ? texts.Get("ActivityUnit." + definition.Code, host.PhysicalUiCulture) : definition.Code;
                var basis = CalculationBasis(row, unit);
                return new PrincipalRateDisplay(row, row.Name, unit, definition.Symbol, Money(row.Cost), basis.Numerator, basis.Denominator,
                    Rate(row.Result?.CombinedRate, definition.Symbol),
                    row.Diagnostics.Count == 0 ? this[NeedsCapacityReview(row) ? "ReviewCapacityShort" : "Calculated"] :
                        row.Name + ": " + string.Join(" ", row.Diagnostics.Select(k => texts.Get(k, host.PhysicalUiCulture))) + BlockingAccounts(row));
            }).ToArray();
            Filter(); SelectedRow = Rows.FirstOrDefault(r => r.Value.PoolId == selected) ?? Rows.FirstOrDefault();
            Feedback = allRows.Length == 0 ? this["NoCenters"] : Rows.Count == 0 ?
                this[SelectedRateTabIndex == 1 ? "NoDirectFactors" : "NoGeneralCenters"] : this["SelectCenter"];
            if (Report.AuxiliaryCapacityPartiallyAssessed) Feedback += " " + texts.Get("Aux.NotEvaluated", host.PhysicalUiCulture);
        }
        catch (Exception ex) when (ex is DomainRuleException or CompanyFileException or InvalidOperationException)
        { Feedback = ex switch {
            DomainRuleException d => new DomainDiagnosticFormatter(texts).Format(d.Diagnostic, host.PhysicalUiCulture, host.PhysicalFormatCulture).Message,
            CompanyFileException f => new DomainDiagnosticFormatter(texts).Format(f.Diagnostic, host.PhysicalUiCulture, host.PhysicalFormatCulture).Message,
            _ => this["LoadFailed"] }; }
        OnPropertyChanged(nameof(Context)); OnPropertyChanged(nameof(Provenance)); OnPropertyChanged(nameof(SelectedSummary)); OnPropertyChanged(nameof(CanExport));
    }
    partial void OnAttentionOnlyChanged(bool value) => Filter();
    partial void OnSelectedRateTabIndexChanged(int value) => Filter();
    private string BlockingAccounts(PrincipalRateRow row)
    {
        if (sourceRun?.Snapshot.Input is not AllocationStudySnapshotInput input || sourceRun.Output?.Secondary is not { } secondary) return "";
        var parts = input.Allocations.Resources.Resources.SelectMany(r => r.Parts).ToDictionary(p => p.Id);
        var accounts = secondary.Facts.Where(f => f.TerminalNodeId == row.NodeId &&
            (f.Behavior is ResourceBehavior.Unclassified or ResourceBehavior.MixedPending or ResourceBehavior.Mixed ||
             f.CapacityTreatment == ResourceCapacityTreatment.AssessAtPool && !f.CapacityNotEvaluated && f.AssessmentId is null &&
             parts.TryGetValue(f.PartId, out var part) && (part.Classification.CapacityOwner?.NodeId != row.NodeId || part.Classification.CapacityOwner?.PoolId != row.PoolId)))
            .Select(f => f.Account).Distinct().ToArray();
        return accounts.Length == 0 ? "" : " · " + string.Join(", ", accounts);
    }
    private void Filter()
    {
        var id = SelectedRow?.Value.PoolId;
        Rows.Clear();
        foreach (var row in allRows.Where(r =>
            directFactorNodes.Contains(r.Value.NodeId) == (SelectedRateTabIndex == 1) &&
            (!AttentionOnly || r.Value.Diagnostics.Count > 0 || NeedsCapacityReview(r.Value))))
            Rows.Add(row);
        if (allRows.Length > 0)
            Feedback = Rows.Count == 0
                ? this[SelectedRateTabIndex == 1 ? "NoDirectFactors" : "NoGeneralCenters"]
                : this["SelectCenter"];
        SelectedRow = Rows.FirstOrDefault(r => r.Value.PoolId == id) ?? Rows.FirstOrDefault();
    }
    partial void OnSelectedRowChanged(PrincipalRateDisplay? value)
    {
        Answers.Clear(); Details.Clear(); Breakdown.Clear(); CapacitySummary = ""; ReviewNotice = "";
        OnPropertyChanged(nameof(Provenance)); OnPropertyChanged(nameof(SelectedSummary));
        if (value is null) return;
        var row = value.Value; var result = row.Result;
        var reason = row.Diagnostics.Count > 0 ? value.Status : result?.CombinedRate is null ? this["UndefinedRate"] : this["AssignmentLater"];
        var composition = reason;
        if (result is not null)
        {
            foreach (var c in result.Components)
                Details.Add(new(this[c.Source.Kind.ToString()], this["Behavior." + c.Source.Behavior], Money(c.Source.Amount),
                    c.Source.Kind == RateComponentKind.Period ? this["NotApplicable"] : Quantity(c.Denominator) + " " + value.Unit,
                    Rate(c.Rate, value.Symbol), c.Source.Account ?? c.Source.Id));
            composition = string.Join(" · ", result.Components.GroupBy(c => c.Source.Kind).OrderBy(g => g.Key).Select(g =>
                this[g.Key.ToString()] + ": " + Money(g.Sum(c => c.Source.Amount)) +
                (g.Key == RateComponentKind.Period ? "" : " / " + Quantity(g.First().Denominator) + " " + value.Symbol + " = " +
                Rate(g.All(c => c.Rate.HasValue) ? g.Sum(c => c.Rate!.Value) : null, value.Symbol)))) + ". " +
                this["Normal"] + ": " + Quantity(row.Observation?.NormalQuantity) + " " + value.Unit + "; " +
                this["Real"] + ": " + Quantity(row.Observation?.RealQuantity) + " " + value.Unit + ".";
        }
        PopulateBreakdown(value);
        var variance = result?.CapacityVariance is { } v ? Money(v) + " · " + this[v < 0 ? "Overactivity" : "Underactivity"] : reason;
        if (result?.CapacityVariance is not null && !result.Components.Any(c => c.Source.Kind == RateComponentKind.OwnFixed))
            variance = this["NoCapacityAssessment"];
        var reconciliation = result?.PerformedCost is { } performed ?
            this["Partial"] + " " + Money(result.SourceCost) + " = " + Money(result.CapacityVariance) + " (" + this["Capacity"] + ") + " +
            Money(performed) + " (" + this["Performed"] + ")" + (result.PeriodCost == 0 ? "" : " + " + Money(result.PeriodCost) + " (" + this["Period"] + ")") + ". " + this["AssignmentLater"] : reason;
        string[] answers = [result?.CombinedRate is not null ? value.Rate : reason, composition,
            this["AssignmentLater"], variance, this["UnknownAssignment"], row.Diagnostics.Count == 0 && result?.CombinedRate is not null ? this["NoIssues"] : reason, reconciliation];
        for (int i = 0; i < answers.Length; i++) Answers.Add(new(this["Question" + (i + 1)], answers[i]));
    }
}
