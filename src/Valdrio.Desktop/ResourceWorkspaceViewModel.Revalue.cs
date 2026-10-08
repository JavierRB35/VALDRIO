using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using Valdrio.Application;
using Valdrio.Domain;
namespace Valdrio.Desktop;

public sealed partial class ResourceWorkspaceViewModel
{
    private FxRateSetVersion? revalueRates;
    private readonly List<ResourceRateChoice> revalueChoices = [];
    private readonly Dictionary<EntityId, IReadOnlyList<ResourcePartDraft>> revalueParts = [];
    private bool preparingRevalue;
    public bool IsRevaluing => revalueRates is not null;
    public bool CanEditRates => CanEdit && preparingRevalue && !IsRevaluing;
    public bool ShowRevalue => preparingRevalue || IsRevaluing;
    public string RevalueReason { get; set; } = "";
    public ObservableCollection<ResourceRevalueRow> RevalueRows { get; } = [];
    public string RevalueCoverage => preview is null ? "" : string.Join(Environment.NewLine, preview.Preparation.Coverage.Select(c =>
        preview.Origins.Single(o => o.Id == c.ResourceId).Code + " · " + Describe(c)));
    public IRelayCommand StartRevalueCommand { get; private set; } = null!;
    public IRelayCommand PrepareRevalueCommand { get; private set; } = null!;
    public IAsyncRelayCommand CommitRevalueCommand { get; private set; } = null!;
    public IRelayCommand CancelRevalueCommand { get; private set; } = null!;
    private void InitializeRevalueCommands()
    {
        StartRevalueCommand = new RelayCommand(() => Try(StartRevalue), () => CanEdit && !IsDirty);
        PrepareRevalueCommand = new RelayCommand(() => Try(PrepareRevalue), () => CanEdit && preparingRevalue && !IsRevaluing);
        CommitRevalueCommand = new AsyncRelayCommand(CommitRevalue, () => CanEdit && IsRevaluing && !HasResourceChanges && stage == 0);
        CancelRevalueCommand = new RelayCommand(() => { Discard(); Refresh(); }, () => !busy && ShowRevalue);
    }
    private void StartRevalue()
    {
        RevalueRows.Clear(); preparingRevalue = true; dirty = true;
        foreach (var r in workspace!.Resources)
        foreach (var value in r.Sources.Select(s => (s.SourceRowId, s.Conversion))
            .Concat(r.Adjustments.Where(a => a.ReversalOfId is null).Select(a => (a.Id, a.Conversion))))
        {
            if (value.Conversion.IsIdentity) continue;
            var row = new ResourceRevalueRow(r.Id, value.Item1, workspace.Origins.Single(o => o.Id == r.Id).Code, value.Conversion);
            row.Form.Numbers = new(Format);
            row.Form["Quote"] = value.Conversion.Quote.ToString("0.##################", Format);
            row.Form["Source"] = value.Conversion.Source; row.Form.RateDate = value.Conversion.RateDate!.Value.Value.ToDateTime(TimeOnly.MinValue);
            row.Form.Edited += () => { cultureAcknowledged = false; Refresh(); };
            RevalueRows.Add(row);
        }
        statusKey = RevalueRows.Count == 0 ? "NoForeignAmounts" : "RevalueInstructions"; Refresh();
    }
    private void PrepareRevalue()
    {
        if (workspace is null || RevalueRows.Count == 0) { statusKey = "NoForeignAmounts"; return; }
        var version = "manual-revalue-" + Guid.NewGuid().ToString("N"); var rates = new List<FxRate>();
        var choices = new List<ResourceRateChoice>();
        foreach (var row in RevalueRows)
        {
            var rate = new FxRate(new(Guid.NewGuid().ToString("N")), row.Previous.OriginalMoney.Currency, workspace.Study.Currency.FunctionalCurrencyCode,
                Number(row.Form, "Quote"), Date(row.Form.RateDate), row.Form["Source"], version);
            rates.Add(rate); choices.Add(new(row.ValueId, rate.Id));
        }
        var candidateRates = new FxRateSetVersion(version, workspace.Study.PeriodEnd, "manual", rates);
        var candidateCommand = new ResourceMutation(ResourceMutationKind.Revalue, workspace.Study.CompanyId, workspace.Study.Id,
            workspace.Study.Revision, new(Guid.NewGuid()), "local-user", "Preview", null, [], [], null, candidateRates,
            choices, []);
        var candidatePreview = ResourceWorkspaceRules.Preview(workspace, candidateCommand);
        // Publish review state only after every conversion and partition has validated.
        revalueChoices.Clear(); revalueChoices.AddRange(choices); revalueParts.Clear();
        revalueRates = candidateRates; preparingRevalue = false; dirty = false;
        editing = null; stage = 0; preview = candidatePreview;
        statusKey = "RevalueReconcile"; RebuildRows(); Refresh();
    }
    private ResourceMutation RevalueCommand(string explanation) => new(ResourceMutationKind.Revalue, workspace!.Study.CompanyId, workspace.Study.Id,
        workspace.Study.Revision, new(Guid.NewGuid()), "local-user", explanation, null, [], [], null, revalueRates,
        revalueChoices.ToArray(), revalueParts.Select(p => new ResourcePartReplacement(p.Key, p.Value)).ToArray());
    private void RecomputeRevalue()
    {
        var id = editing!.Id;
        revalueParts.TryGetValue(id, out var previous);
        revalueParts[id] = parts.ToArray();
        try { preview = ResourceWorkspaceRules.Preview(workspace!, RevalueCommand("Preview")); }
        catch
        {
            if (previous is null) revalueParts.Remove(id); else revalueParts[id] = previous;
            throw;
        }
    }
    private void KeepRevalueParts()
    {
        if (partDirty || adjustmentDirty || objectDirty) { statusKey = "ApplyFormsFirst"; Refresh(); return; }
        Try(() =>
        {
            RecomputeRevalue();
            if (preview!.Preparation.Coverage.Single(c => c.ResourceId == editing!.Id).Difference.Amount != 0)
                throw ResourceWorkspaceRules.Error("RES_REVALUE_UNBALANCED", "Resource.RevalueUnbalanced");
            editing = null; parts.Clear(); deltas.Clear(); dirty = false; stage = 0; statusKey = "RevaluePartsKept"; RebuildRows();
        });
    }
    private async Task CommitRevalue()
    {
        if (!CheckCulture()) return;
        await Run(token => Commit(RevalueCommand(RevalueReason), token));
    }
    private void CancelRevalue()
    {
        revalueRates = null; preparingRevalue = false; revalueChoices.Clear(); revalueParts.Clear(); RevalueRows.Clear(); RevalueReason = "";
    }
}
