using Valdrio.Application;
using Valdrio.Domain;
using Valdrio.Localization;
namespace Valdrio.Desktop;

public sealed partial class ResourceWorkspaceViewModel
{
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
            partId = null; multipleAssignments = null;
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
    public IReadOnlyList<ResourceChoice<ResourceObjectKind>> ObjectKinds => Choices<ResourceObjectKind>();
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
    private ResourcePartDraft Part()
    {
        var f = PartForm; var amount = Functional(Number(f, "Amount"));
        ResourceBehaviorContext? context = null;
        if (f.Behavior is ResourceBehavior.Fixed or ResourceBehavior.Variable || f.UnitId.HasValue)
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
            new(f.Behavior, context, f.Layer, f.Capacity, capacity));
    }
    private void ApplyPart()
    {
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
        var f = AdjustmentForm; var original = new Money(Number(f, "Amount"), new(f["Currency"]), workspace!.Precision);
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
        AddDelta(new(new(Guid.NewGuid()), null, null, "", AdjustmentForm["Reason"], selected.Value.Id, null)); ClearAdjustment();
    }
    public bool ShowAdjustmentFx => AdjustmentForm["Currency"] != FunctionalCurrency;
    private async Task SaveObject()
    {
        if (workspace is null || !CheckCulture()) return;
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
