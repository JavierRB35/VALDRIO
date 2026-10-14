using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Valdrio.Application;
using Valdrio.Domain;
using Valdrio.Localization;

namespace Valdrio.Desktop;

public sealed record PhysicalQualityOption(PhysicalEvidenceQuality Value, string Label);

// Presentation-only row. Numeric input remains text until parsed with the explicit user's format culture.
public sealed partial class PhysicalActivityRow : ObservableObject
{
    public required EntityId NodeId { get; init; }
    public required EntityId PoolId { get; init; }
    public required string Node { get; init; }
    public required string Unit { get; init; }
    [ObservableProperty] private string normalText = "";
    [ObservableProperty] private string realText = "";
    [ObservableProperty] private string normalSource = "";
    [ObservableProperty] private string realSource = "";
    [ObservableProperty] private string normalDescription = "";
    [ObservableProperty] private string realDescription = "";
    [ObservableProperty] private PhysicalEvidenceQuality normalQuality = PhysicalEvidenceQuality.Calculated;
    [ObservableProperty] private PhysicalEvidenceQuality realQuality = PhysicalEvidenceQuality.Recorded;
    [ObservableProperty] private string normalZeroReason = "";
    [ObservableProperty] private string exceptionReason = "";
    [ObservableProperty] private string correctionReason = "";
    [ObservableProperty] private string status = "";
    [ObservableProperty] private string utilization = "";
    public required string Cost { get; init; }
    public long Version { get; set; }

    public void ReadFrom(PhysicalObservation? data, CultureInfo culture, string pending, string undefined, string annulled, string draft, string confirmed)
    {
        Version = data?.Version ?? 0;
        var available = data?.State != PhysicalObservationState.Annulled;
        NormalText = available ? data?.NormalQuantity?.ToString(culture) ?? "" : "";
        RealText = available ? data?.RealQuantity?.ToString(culture) ?? "" : "";
        NormalSource = available ? data?.NormalEvidence?.Source ?? "" : "";
        RealSource = available ? data?.RealEvidence?.Source ?? "" : "";
        NormalDescription = available ? data?.NormalEvidence?.Description ?? "" : "";
        RealDescription = available ? data?.RealEvidence?.Description ?? "" : "";
        NormalQuality = data?.NormalEvidence?.Quality ?? PhysicalEvidenceQuality.Calculated;
        RealQuality = data?.RealEvidence?.Quality ?? PhysicalEvidenceQuality.Recorded;
        NormalZeroReason = available ? data?.NormalZeroReason ?? "" : "";
        ExceptionReason = available ? data?.ExceptionReason ?? "" : "";
        CorrectionReason = "";
        Status = data?.State switch
        {
            PhysicalObservationState.Annulled => annulled,
            PhysicalObservationState.Draft => draft,
            PhysicalObservationState.Confirmed => confirmed,
            _ => pending
        };
        Utilization = data?.State == PhysicalObservationState.Annulled || data?.NormalQuantity is null ||
            data.RealQuantity is null ? pending :
            data.UtilizationPercent is decimal percentage ? percentage.ToString("0.##", culture) + " %" : undefined;
    }
}

// The form holds no independent cost calculation. T12 is read only via IRunStore.
public sealed partial class PhysicalActivityEditor : ObservableObject
{
    private readonly StudyDefinition study;
    private readonly IPhysicalActivityWorkspace activity;
    private readonly IModelWorkspace models;
    private readonly IRunStore? runs;
    private readonly CompanyFileSnapshot company;
    private readonly CultureInfo uiCulture, formatCulture;
    private readonly UiTextCatalog texts;
    [ObservableProperty] private PhysicalActivityRow? selectedRow;
    [ObservableProperty] private string feedback = "";

    public PhysicalActivityEditor(StudyWorkspaceViewModel host)
    {
        study = host.Current ?? throw new InvalidOperationException("Select a saved study first.");
        activity = host.ActivePhysicalActivity ?? throw new InvalidOperationException("Physical store unavailable.");
        models = host.PhysicalModels ?? throw new InvalidOperationException("Published model unavailable.");
        runs = host.PhysicalRuns;
        company = host.PhysicalCompany ?? throw new InvalidOperationException("Company unavailable.");
        uiCulture = host.PhysicalUiCulture; formatCulture = host.PhysicalFormatCulture;
        texts = new UiTextCatalog();
        IsReadOnly = !host.PhysicalWritable;
        QualityOptions = Enum.GetValues<PhysicalEvidenceQuality>()
            .Select(q => new PhysicalQualityOption(q, Label("Activity.Quality." + q))).ToArray();
        Reload();
    }

    public ObservableCollection<PhysicalActivityRow> Rows { get; } = [];
    public IReadOnlyList<PhysicalQualityOption> QualityOptions { get; }
    public bool IsReadOnly { get; }
    public bool IsWritable => !IsReadOnly;
    public string Title => Label("Activity.Title");
    public string Help => Label("Activity.Help");
    public string SourceLabel => Label("Activity.Source");
    public string QualityLabel => Label("Activity.Quality");
    public string ExceptionLabel => Label("Activity.Exception");
    public string ZeroReasonLabel => Label("Activity.ZeroReason");
    public string CorrectionLabel => Label("Activity.Correction");
    public string SaveDraftLabel => Label("Activity.SaveDraft");
    public string ConfirmLabel => Label("Activity.Confirm");
    public string AnnulLabel => Label("Activity.Annul");
    public string CloseLabel => Label("Activity.Cancel");
    public string NormalLabel => Label("Activity.Normal");
    public string RealLabel => Label("Activity.Real");
    public string NodeLabel => Label("Activity.Node");
    public string UnitLabel => Label("Activity.Unit");
    public string UtilizationLabel => Label("Activity.Utilization");
    public string CostLabel => Label("Activity.Cost");
    public string HistoryLabel => Label("Activity.History");
    public string StatusLabel => Label("Activity.Status");
    private string Label(string key) => texts.Get(key, uiCulture);

    private decimal? Parse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (!decimal.TryParse(raw.Trim(), NumberStyles.Number, formatCulture, out var number))
            throw PhysicalActivityRules.Error("ACT_INPUT_INVALID", "Activity.InputInvalid");
        return number;
    }
    private PhysicalEvidence? Evidence(decimal? value, PhysicalEvidenceQuality quality,
        string source, string description) =>
        value is null && string.IsNullOrWhiteSpace(source) && string.IsNullOrWhiteSpace(description) ? null :
            new(quality, source.Trim(), description.Trim());

    public void Reload()
    {
        var selectedId = SelectedRow?.PoolId;
        var model = models.ReadModels().Single(m =>
            m.Structure.Id == study.SelectedModelVersionId && m.Status == ModelStatus.Published);
        var latest = activity.ReadCurrent(study.CompanyId, study.Id).ToDictionary(x => x.PoolId);
        var publishedRun = runs?.ReadRuns(study.CompanyId, study.Id);
        var result = publishedRun?.Runs.FirstOrDefault(r => r.Id == publishedRun.CurrentRunId)?.Output?.Secondary;
        Rows.Clear();
        foreach (var node in model.Structure.Nodes.Where(n => n.Role == NodeRole.Principal && n.IsActive).OrderBy(n => n.Code))
        {
            var pool = model.Structure.Pools.Single(p => p.NodeId == node.Id);
            var unit = company.Units.Single(u => u.Id == pool.DriverUnitId);
            var cost = result is null ? Label("Activity.CostUnavailable") :
                result.Totals.Where(x => x.TerminalNodeId == node.Id).Sum(x => x.PostedAmount.Amount)
                    .ToString("N2", formatCulture) + " " + study.Currency.FunctionalCurrencyCode;
            var center = model.Publication?.PhysicalCenters.SingleOrDefault(c => c.Id == node.PhysicalCenterId)?.Name;
            var row = new PhysicalActivityRow
            {
                NodeId = node.Id, PoolId = pool.Id,
                Node = string.IsNullOrWhiteSpace(center) ? node.Code : center + " · " + node.Code,
                Unit = unit.Code, Cost = cost
            };
            row.ReadFrom(latest.GetValueOrDefault(pool.Id), formatCulture, Label("Activity.Pending"),
                Label("Activity.UtilizationUndefined"), Label("Activity.AnnulledRow"),
                Label("Activity.State.Draft"), Label("Activity.State.Confirmed"));
            Rows.Add(row);
        }
        SelectedRow = Rows.FirstOrDefault(r => r.PoolId == selectedId) ?? Rows.FirstOrDefault();
        Feedback = "";
    }

    public void Save(bool confirm)
    {
        var row = SelectedRow;
        if (row is null || IsReadOnly) return;
        try
        {
            var normal = Parse(row.NormalText);
            var real = Parse(row.RealText);
            var command = new SavePhysicalObservation(study.CompanyId, study.Id, study.Revision, row.NodeId, row.PoolId,
                row.Version, confirm ? PhysicalObservationState.Confirmed : PhysicalObservationState.Draft,
                null, null, normal, real,
                Evidence(normal, row.NormalQuality, row.NormalSource, row.NormalDescription),
                Evidence(real, row.RealQuality, row.RealSource, row.RealDescription),
                string.IsNullOrWhiteSpace(row.NormalZeroReason) ? null : row.NormalZeroReason.Trim(),
                string.IsNullOrWhiteSpace(row.ExceptionReason) ? null : row.ExceptionReason.Trim(),
                string.IsNullOrWhiteSpace(row.CorrectionReason) ? null : row.CorrectionReason.Trim(), "local-user");
            _ = activity.Save(command);
            Reload();
        }
        catch (DomainRuleException e) { Feedback = Label(e.Diagnostic.MessageKey); }
        catch (CompanyFileException e) { Feedback = Label(e.Diagnostic.MessageKey); }
        catch (Exception) { Feedback = Label("Activity.SaveFailed"); }
    }

    public void Annul()
    {
        var row = SelectedRow;
        if (row is null || IsReadOnly) return;
        try
        {
            _ = activity.Annul(new(study.CompanyId, study.Id, study.Revision, row.NodeId, row.PoolId,
                row.Version, row.CorrectionReason, "local-user"));
            Reload();
        }
        catch (DomainRuleException e) { Feedback = Label(e.Diagnostic.MessageKey); }
        catch (CompanyFileException e) { Feedback = Label(e.Diagnostic.MessageKey); }
        catch (Exception) { Feedback = Label("Activity.SaveFailed"); }
    }

    public string History()
    {
        if (SelectedRow is not { } row) return "";
        var versions = activity.ReadHistory(study.CompanyId, study.Id, row.PoolId);
        return string.Join(Environment.NewLine, versions.Select(v =>
            string.Format(formatCulture, "v{0} · {1} · {2}: {3} / {4} · {5}",
                v.Version, v.RecordedAtUtc, Label("Activity.State." + v.State), v.NormalQuantity?.ToString(formatCulture) ?? "—",
                v.RealQuantity?.ToString(formatCulture) ?? "—", v.CorrectionReason ?? v.ExceptionReason ?? "—")));
    }
}
