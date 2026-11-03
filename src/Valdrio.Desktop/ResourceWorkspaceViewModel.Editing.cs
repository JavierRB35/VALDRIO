using Valdrio.Application;
using Valdrio.Domain;
using Valdrio.Localization;
namespace Valdrio.Desktop;

public sealed partial class ResourceWorkspaceViewModel
{
    private decimal? partVariablePercentage;
    private IReadOnlyList<DirectResourceAssignment>? multipleAssignments;
    public bool HasMultipleAssignments => multipleAssignments?.Count > 1;
    public string FunctionalCurrency => workspace?.Study.Currency.FunctionalCurrencyCode.ToString() ?? "";
    public string MultipleAssignmentDetails => multipleAssignments is null ? "" : string.Join(Environment.NewLine,
        multipleAssignments.Select(a => (Objects.FirstOrDefault(o => o.Value == a.ObjectId)?.Label ?? a.ObjectId.ToString()) + " · " + MoneyText(a.Amount)));
    public string ResetReason { get; set; } = "";
    public bool ResetAcknowledged { get; set; }
    private decimal Number(ResourceEditorForm form, string key)
    {
        if (!form.Numbers.TryCommit(form[key], out var value)) throw ResourceWorkspaceRules.Error("RES_NUMBER_REQUIRED", "Resource.NumberRequired");
        return value;
    }
    private Money Functional(decimal value) => new(value, workspace!.Study.Currency.FunctionalCurrencyCode, workspace.Precision);
    private static BusinessDate Date(DateTime? value) => value is null
        ? throw ResourceWorkspaceRules.Error("RES_DATE_REQUIRED", "Resource.DateRequired") : new(DateOnly.FromDateTime(value.Value));
    private void ClearPart()
    {
        loading = true;
        try
        {
            partId = null; multipleAssignments = null; partVariablePercentage = null;
            foreach (var key in new[] { "Amount", "Exclusion", "Minimum", "Maximum", "Theoretical", "Practical", "Normal", "Actual" }) PartForm[key] = "";
            PartForm.Route = ResourceRoutingKind.Pending; PartForm.Behavior = ResourceBehavior.Unclassified; PartForm.Layer = ResourceCostLayer.Unclassified;
            PartForm.Capacity = ResourceCapacityTreatment.None; PartForm.PoolId = PartForm.ObjectId = null; PartForm.UnitId = null;
            PartForm.Start = workspace?.Study.PeriodStart.Value.ToDateTime(TimeOnly.MinValue); PartForm.End = workspace?.Study.PeriodEnd.Value.ToDateTime(TimeOnly.MinValue);
            PartForm.Numbers = new(Format); partDirty = false;
        }
        finally { loading = false; Refresh(); }
    }
    private void ClearAdjustment()
    {
        loading = true;
        try
        {
            foreach (var key in new[] { "Amount", "Type", "Reason", "Quote", "RateSource" }) AdjustmentForm[key] = "";
            AdjustmentForm["Currency"] = FunctionalCurrency; AdjustmentForm.RateDate = workspace?.Study.PeriodEnd.Value.ToDateTime(TimeOnly.MinValue);
            AdjustmentForm.Numbers = new(Format); adjustmentDirty = false;
        }
        finally { loading = false; Refresh(); }
    }
    private void ClearObject()
    {
        loading = true;
        try { ObjectForm["Code"] = ObjectForm["Name"] = ""; ObjectForm["Kind"] = ResourceObjectKind.Other.ToString(); ObjectForm.Numbers = new(Format); objectDirty = false; }
        finally { loading = false; Refresh(); }
    }
    public IReadOnlyList<ResourceChoice<ResourceObjectKind>> ObjectKinds => choices.Get<ResourceChoice<ResourceObjectKind>>((workspace, preferences.Current.UiLanguageTag, preferences.Current.FormatCultureTag, pseudo), () => Choices<ResourceObjectKind>());
    public ResourceObjectKind ObjectKind
    {
        get => Enum.TryParse<ResourceObjectKind>(ObjectForm["Kind"], out var value) ? value : ResourceObjectKind.Other;
        set => ObjectForm["Kind"] = value.ToString();
    }
    private void LoadPart()
    {
        if (PartChoice is not { } selected) return;
        var p = selected.Value; ClearPart(); loading = true;
        try
        {
            partVariablePercentage = p.Classification.VariablePercentage;
            partId = p.Id; PartForm["Amount"] = p.Amount.Amount.ToString("0.########", Format); PartForm.Route = p.Routing;
            PartForm.PoolId = p.PoolId; PartForm.ObjectId = p.DirectAssignments.Count == 1 ? p.DirectAssignments[0].ObjectId : null;
            multipleAssignments = p.DirectAssignments.Count > 1 ? p.DirectAssignments : null;
            PartForm["Exclusion"] = p.ExclusionReason ?? ""; PartForm.Behavior = p.Classification.Behavior;
            PartForm.Layer = p.Classification.Layer; PartForm.Capacity = p.Classification.CapacityTreatment;
            if (p.Classification.Context is { } c)
            {
                PartForm.UnitId = c.DriverUnitId; PartForm.Start = c.HorizonStart.ToDateTime(TimeOnly.MinValue); PartForm.End = c.HorizonEnd.ToDateTime(TimeOnly.MinValue);
                PartForm["Minimum"] = c.MinimumActivity.Value.ToString("0.############", Format); PartForm["Maximum"] = c.MaximumActivity.Value.ToString("0.############", Format);
            }
            if (p.Classification.CapacityOwner is { } owner)
            {
                PartForm["Theoretical"] = owner.Theoretical?.Value.ToString("0.############", Format) ?? "";
                PartForm["Practical"] = owner.Practical?.Value.ToString("0.############", Format) ?? "";
                PartForm["Normal"] = owner.Normal?.Value.ToString("0.############", Format) ?? "";
                PartForm["Actual"] = owner.Actual?.Value.ToString("0.############", Format) ?? "";
            }
        }
        finally { loading = false; partDirty = false; Refresh(); }
    }
    private bool RejectInput(string code, string key)
    { diagnostic = new(code, key, []); Refresh(); return false; }
    private bool PartInputValid()
    {
        var f = PartForm;
        if (!f.Numbers.TryCommit(f["Amount"], out _)) return RejectInput("RES_NUMBER_REQUIRED", "Resource.NumberRequired");
        if (f.Route == ResourceRoutingKind.AllocationPool && !f.PoolId.HasValue || f.Capacity == ResourceCapacityTreatment.AssessAtPool && !f.PoolId.HasValue)
            return RejectInput("RES_POOL_REQUIRED", "Resource.PoolRequired");
        if (f.Route == ResourceRoutingKind.DirectObject && multipleAssignments is null && !f.ObjectId.HasValue)
            return RejectInput("RES_OBJECT_SCOPE", "Resource.ObjectScope");
        if (f.Route == ResourceRoutingKind.Excluded && string.IsNullOrWhiteSpace(f["Exclusion"]))
            return RejectInput("RES_EXCLUSION_REASON", "Resource.ExclusionReasonRequired");
        if (partVariablePercentage is null && f.Behavior is ResourceBehavior.Fixed or ResourceBehavior.Variable || f.UnitId.HasValue)
        {
            if (!f.UnitId.HasValue) return RejectInput("RES_UNIT_SCOPE", "Resource.UnitScope");
            if (f.Start is null || f.End is null) return RejectInput("RES_DATE_REQUIRED", "Resource.DateRequired");
            if (f.Start > f.End) return RejectInput("RES_BEHAVIOR_HORIZON_INVALID", "Resource.BehaviorHorizonInvalid");
            if (!f.Numbers.TryCommit(f["Minimum"], out var min) || !f.Numbers.TryCommit(f["Maximum"], out var max))
                return RejectInput("RES_NUMBER_REQUIRED", "Resource.NumberRequired");
            if (min < 0 || max < min) return RejectInput("RES_BEHAVIOR_RANGE_INVALID", "Resource.BehaviorRangeInvalid");
        }
        if (f.Capacity == ResourceCapacityTreatment.AssessAtPool && new[] { "Theoretical", "Practical", "Normal", "Actual" }.Any(k => !string.IsNullOrWhiteSpace(f[k]) && !f.Numbers.TryCommit(f[k], out _)))
            return RejectInput("RES_NUMBER_REQUIRED", "Resource.NumberRequired");
        return true;
    }
    private ResourcePartDraft Part()
    {
        var f = PartForm; var amount = Functional(Number(f, "Amount"));
        ResourceBehaviorContext? context = null;
        var percentage = partVariablePercentage is null ? (decimal?)null : f.Behavior == ResourceBehavior.Fixed ? 0m : f.Behavior == ResourceBehavior.Variable ? 100m : f.Behavior == ResourceBehavior.Mixed ? partVariablePercentage : null;
        if (percentage is null && f.Behavior is ResourceBehavior.Fixed or ResourceBehavior.Variable || f.UnitId.HasValue)
        {
            var unit = f.UnitId ?? throw ResourceWorkspaceRules.Error("RES_UNIT_SCOPE", "Resource.UnitScope");
            context = new(unit, Date(f.Start).Value, Date(f.End).Value,
                new(Number(f, "Minimum"), unit, workspace!.Precision), new(Number(f, "Maximum"), unit, workspace!.Precision));
        }
        ResourceCapacityOwner? capacity = null;
        if (f.Capacity == ResourceCapacityTreatment.AssessAtPool)
        {
            var pool = workspace!.Model.Structure.Pools.SingleOrDefault(p => p.Id == f.PoolId)
                ?? throw ResourceWorkspaceRules.Error("RES_POOL_REQUIRED", "Resource.PoolRequired");
            Quantity? Q(string key) => string.IsNullOrWhiteSpace(f[key]) ? null : new(Number(f, key), pool.DriverUnitId, workspace.Precision);
            capacity = new(pool.Id, pool.NodeId, pool.DriverUnitId, Q("Theoretical"), Q("Practical"), Q("Normal"), Q("Actual"));
        }
        IReadOnlyList<DirectResourceAssignment> direct = [];
        if (f.Route == ResourceRoutingKind.DirectObject)
            direct = multipleAssignments ?? [new(f.ObjectId ?? throw ResourceWorkspaceRules.Error("RES_OBJECT_SCOPE", "Resource.ObjectScope"), amount)];
        return new(partId ?? new(Guid.NewGuid()), amount, f.Route, direct,
            f.Route == ResourceRoutingKind.AllocationPool ? f.PoolId : null,
            f.Route == ResourceRoutingKind.Excluded ? f["Exclusion"] : null,
            new(f.Behavior, context, f.Layer, f.Capacity, capacity) { VariablePercentage = percentage });
    }
    private void ApplyPart()
    {
        if (!PartInputValid()) return;
        var value = Part(); var previous = parts.ToArray();
        parts.RemoveAll(p => p.Id == value.Id); parts.Add(value);
        try { Recompute(); }
        catch { parts.Clear(); parts.AddRange(previous); throw; }
        dirty = true; ClearPart(); statusKey = "PartApplied"; RebuildRows();
    }
    private void AddPending()
    {
        Recompute(); var resource = preview!.Resources.Single(r => r.Id == editing!.Id);
        var remainder = ResourceWorkspaceRules.PendingRemainder(resource, workspace!.Study.Currency.FunctionalCurrencyCode, workspace.Precision);
        if (remainder.Amount.Amount == 0m) { statusKey = "AlreadyBalanced"; return; }
        parts.Add(remainder); dirty = true; Recompute(); statusKey = "PartApplied";
    }
    private void AddAdjustment()
    {
        var f = AdjustmentForm;
        if (!f.Numbers.TryCommit(f["Amount"], out _)) { RejectInput("RES_NUMBER_REQUIRED", "Resource.NumberRequired"); return; }
        if (string.IsNullOrWhiteSpace(f["Type"]) || string.IsNullOrWhiteSpace(f["Reason"]))
        { RejectInput("RES_INPUT_INVALID", "Resource.InputInvalid"); return; }
        if (!catalog.GetAll().Any(c => c.AlphaCode.ToString() == f["Currency"] && c.IsMonetary))
        { RejectInput("RES_INPUT_INVALID", "Resource.InputInvalid"); return; }
        if (f["Currency"] != FunctionalCurrency)
        {
            if (!f.Numbers.TryCommit(f["Quote"], out var quote) || quote <= 0 || CanonicalDecimal.Scale(quote) > 18) { RejectInput("RES_NUMBER_REQUIRED", "Resource.NumberRequired"); return; }
            if (f.RateDate is null) { RejectInput("RES_DATE_REQUIRED", "Resource.DateRequired"); return; }
            if (string.IsNullOrWhiteSpace(f["RateSource"])) { RejectInput("FX_SOURCE_REQUIRED", "Fx.SourceRequired"); return; }
        }
        var original = new Money(Number(f, "Amount"), new(f["Currency"]), workspace!.Precision);
        FxRate? rate = null;
        if (original.Currency != workspace.Study.Currency.FunctionalCurrencyCode)
        {
            var id = Guid.NewGuid().ToString("N");
            rate = new(new(id), original.Currency, workspace.Study.Currency.FunctionalCurrencyCode, Number(f, "Quote"), Date(f.RateDate), f["RateSource"], "manual-" + id);
        }
        var delta = new ResourceAdjustmentRequest(new(Guid.NewGuid()), original, rate, f["Type"], f["Reason"], null, catalog.GetRequired(original.Currency).Freeze());
        AddDelta(delta); ClearAdjustment();
    }
    private void AddDelta(ResourceAdjustmentRequest delta)
    {
        deltas.Add(delta);
        try { Recompute(); }
        catch { deltas.Remove(delta); throw; }
        dirty = true; statusKey = "AdjustmentApplied";
    }
    private void ReverseAdjustment()
    {
        if (AdjustmentChoice is not { } selected) return;
        if (string.IsNullOrWhiteSpace(AdjustmentForm["Reason"])) { RejectInput("RES_COMMAND_INVALID", "Resource.CommandInvalid"); return; }
        AddDelta(new(new(Guid.NewGuid()), null, null, "", AdjustmentForm["Reason"], selected.Value.Id, null)); ClearAdjustment();
    }
    public bool ShowAdjustmentFx => AdjustmentForm["Currency"] != FunctionalCurrency;
    private async Task SaveObject()
    {
        if (workspace is null || !CheckCulture()) return;
        if (string.IsNullOrWhiteSpace(ObjectForm["Code"]) || string.IsNullOrWhiteSpace(ObjectForm["Name"]))
        { RejectInput("RES_INPUT_INVALID", "Resource.InputInvalid"); return; }
        await Run(async token =>
        {
            var obj = new ResourceCostObject(new(Guid.NewGuid()), workspace.Study.CompanyId, workspace.Study.Id,
                ObjectForm["Code"], ObjectForm["Name"], ObjectKind);
            var command = new ResourceMutation(ResourceMutationKind.PutObject, workspace.Study.CompanyId, workspace.Study.Id,
                workspace.Study.Revision, new(Guid.NewGuid()), "local-user", this["ObjectCreatedReason"], null, [], [], obj, null, [], []);
            var receipt = await Task.Run(() => company.ActiveResources!.ChangeResources(command, token), token);
            workspace = new(receipt.ConfirmedStudy, workspace.Precision, workspace.Model, workspace.Origins, receipt.Ledger);
            history = history.Append(receipt).ToArray(); ClearObject();
            if (editing is not null) Recompute(); else { preview = workspace; RebuildRows(); }
            statusKey = "ObjectSaved";
            try { await studies.ApplyResourceReceiptAsync(receipt.ConfirmedStudy); }
            catch (Exception e) when (e is System.IO.IOException or CompanyFileException or InvalidOperationException) { statusKey = "SavedRefreshPending"; }
        });
    }
    private async Task Reset()
    {
        if (workspace is null || !ResetAcknowledged || string.IsNullOrWhiteSpace(ResetReason)) { statusKey = "ResetRequirements"; Refresh(); return; }
        await Run(async token =>
        {
            await Commit(new(ResourceMutationKind.ResetPreparation, workspace.Study.CompanyId, workspace.Study.Id,
                workspace.Study.Revision, new(Guid.NewGuid()), "local-user", ResetReason, null, [], [], null, null, [], []), token);
            ResetReason = ""; ResetAcknowledged = false; statusKey = "ResetSaved";
        });
    }
}
