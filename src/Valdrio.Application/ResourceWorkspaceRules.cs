using Valdrio.Calculation;
using Valdrio.Domain;

namespace Valdrio.Application;

public static class ResourceWorkspaceRules
{
    public static PublishedModelStructure ApplyStudyNodes(PublishedModelStructure model, StudyNodeEdits? edits)
    {
        if (edits is null) return model;
        var original = model.Structure;
        // Editing an analysis preserves identities used by keys, resources and historical snapshots.
        if (!original.Nodes.Select(n => n.Id).Order().SequenceEqual(edits.Nodes.Select(n => n.Id).Order()) ||
            !original.Pools.Select(p => p.Id).Order().SequenceEqual(edits.Pools.Select(p => p.Id).Order()) ||
            edits.Pools.Any(p => original.Pools.Single(o => o.Id == p.Id).NodeId != p.NodeId))
            throw Error("MODEL_STUDY_NODE_IDENTITY", "Model.StudyNodeIdentity");
        var structure = new ModelStructure(original.Id, original.CompanyId, original.FamilyId,
            original.VersionNumber, original.ParentVersionId, original.Description, original.PrecisionPolicyVersion,
            original.MethodPolicy, edits.Nodes, edits.Pools);
        var diagnostics = ModelRules.ValidateStructure(structure, model.PhysicalCenters,
            model.DriverUnits.Select(u => new DriverUnitState(u, true, 0)).ToArray());
        var error = diagnostics.FirstOrDefault();
        if (error is not null) throw new DomainRuleException(error);
        return new(structure, model.PhysicalCenters, model.DriverUnits);
    }
    public static IReadOnlyList<Money> OriginalTotals(ResourceDraft resource, PrecisionPolicy precision) => FrozenList.Copy(
        resource.Sources.GroupBy(s => s.Conversion.OriginalMoney.Currency).OrderBy(g => g.Key.Value, StringComparer.Ordinal)
            .Select(g => new Money(g.Aggregate(0m, (sum, s) => checked(sum + s.Conversion.OriginalMoney.Amount)), g.Key, precision)));
    public static DomainRuleException Error(string code, string key) => new(new(code, key, []));
    public static FxRate? Rate(FxRateApplication c) => c.IsIdentity ? null : new(c.RateId!.Value,
        c.OriginalMoney.Currency, c.ConvertedOriginalMoney.Currency, c.Quote, c.RateDate!.Value, c.Source, c.RateSetVersionId!);

    public static IReadOnlyList<ResourceDraft> Materialize(ResourceWorkspaceSnapshot workspace)
    {
        var entries = workspace.Ledger.Entries;
        if (entries.Select(e => e.Id).Distinct().Count() != entries.Count ||
            workspace.Origins.Select(o => o.Id).Distinct().Count() != workspace.Origins.Count ||
            entries.Any(e => !workspace.Origins.Any(o => o.Id == e.Id)))
            throw Error("RES_SOURCE_CHANGED", "Resource.SourceChanged");
        return FrozenList.Copy(workspace.Origins.Select(origin =>
        {
            var sources = origin.Rows.Select(r => new ResourceSourceValue(r.Id, r.Conversion)).ToArray();
            var saved = entries.SingleOrDefault(e => e.Id == origin.Id);
            if (saved is not null)
            {
                if (saved.Sources.Count != sources.Length || saved.Sources.Any(s =>
                    !sources.Any(o => o.SourceRowId == s.SourceRowId && o.Conversion.OriginalMoney == s.Conversion.OriginalMoney)))
                    throw Error("RES_SOURCE_CHANGED", "Resource.SourceChanged");
                return saved;
            }
            var total = new Money(0m, workspace.Study.Currency.FunctionalCurrencyCode, workspace.Precision);
            foreach (var source in sources) total += source.Conversion.ConvertedOriginalMoney;
            return new ResourceDraft(origin.Id, sources, [], [new ResourcePartDraft(
                ResourceOrigins.StableId("pending", origin.Id.ToString()), total, ResourceRoutingKind.Pending)]);
        }));
    }

    public static void ValidateScope(ResourceWorkspaceSnapshot workspace)
    {
        var objects = workspace.Ledger.Objects;
        if (objects.Select(o => o.Id).Distinct().Count() != objects.Count ||
            objects.Select(o => o.Code).Distinct(StringComparer.Ordinal).Count() != objects.Count)
            throw Error("RES_OBJECT_INVALID", "Resource.ObjectInvalid");
        foreach (var item in objects)
        {
            item.Id.EnsureValid();
            if (item.CompanyId != workspace.Study.CompanyId || item.StudyId != workspace.Study.Id ||
                string.IsNullOrWhiteSpace(item.Code) || string.IsNullOrWhiteSpace(item.Name) || !Enum.IsDefined(item.Kind))
                throw Error("RES_OBJECT_INVALID", "Resource.ObjectInvalid");
        }
        var pools = workspace.Model.Structure.Pools.ToDictionary(p => p.Id);
        var nodes = workspace.Model.Structure.Nodes.ToDictionary(n => n.Id);
        var units = workspace.Model.DriverUnits.ToDictionary(u => u.Id);
        foreach (var part in workspace.Resources.SelectMany(r => r.Parts))
        {
            if (part.DirectAssignments.Any(a => !objects.Any(o => o.Id == a.ObjectId)))
                throw Error("RES_OBJECT_SCOPE", "Resource.ObjectScope");
            if (part.PoolId is { } pool && (!pools.TryGetValue(pool, out var destination) || !nodes[destination.NodeId].IsActive))
                throw Error("RES_POOL_SCOPE", "Resource.PoolScope");
            if (part.Classification.Context is { } context)
            {
                if (!units.TryGetValue(context.DriverUnitId, out var definition)) throw Error("RES_UNIT_SCOPE", "Resource.UnitScope");
                CheckUnit(context.MinimumActivity, definition); CheckUnit(context.MaximumActivity, definition);
            }
            if (part.Classification.CapacityOwner is { } owner)
            {
                if (!pools.TryGetValue(owner.PoolId, out var definition) || definition.NodeId != owner.NodeId ||
                    definition.DriverUnitId != owner.UnitId || !units.ContainsKey(owner.UnitId))
                    throw Error("RES_CAPACITY_OWNER_INVALID", "Resource.CapacityOwnerInvalid");
                foreach (var q in new[] { owner.Theoretical, owner.Practical, owner.Normal, owner.Actual })
                    if (q is { } quantity) CheckUnit(quantity, units[owner.UnitId]);
            }
        }
    }
    private static void CheckUnit(Quantity quantity, UnitDefinition unit)
    {
        if (decimal.Round(quantity.Value, unit.DecimalScale) != quantity.Value || unit.IsDiscrete && decimal.Truncate(quantity.Value) != quantity.Value)
            throw Error("RES_ACTIVITY_UNIT_MISMATCH", "Resource.ActivityUnitMismatch");
    }

    public static ResourceLedger Apply(ResourceWorkspaceSnapshot before, ResourceMutation command)
        => ApplyCore(before, command, true);

    public static ResourceWorkspaceSnapshot Preview(ResourceWorkspaceSnapshot before, ResourceMutation command) =>
        new(before.Study, before.Precision, before.Model, before.Origins, ApplyCore(before, command, false));

    // Only an explicit editor action creates a pending remainder, including a negative remainder.
    public static ResourcePartDraft PendingRemainder(ResourceDraft resource, CurrencyCode currency, PrecisionPolicy precision)
    {
        var totals = ResourcePreparationRules.Prepare([resource], currency, precision).Coverage.Single();
        return new(new EntityId(Guid.NewGuid()), totals.Difference, ResourceRoutingKind.Pending);
    }

    private static ResourceLedger ApplyCore(ResourceWorkspaceSnapshot before, ResourceMutation command, bool requireBalanced)
    {
        command.RequestId.EnsureValid();
        if (command.CompanyId != before.Study.CompanyId || command.StudyId != before.Study.Id ||
            !Enum.IsDefined(command.Kind) || string.IsNullOrWhiteSpace(command.Actor) || string.IsNullOrWhiteSpace(command.Reason))
            throw Error("RES_COMMAND_INVALID", "Resource.CommandInvalid");
        StudyRules.CheckRevision(command.ExpectedRevision, before.Study.Revision, before.Study.Id);
        if (before.Study.State != StudyState.Open) throw StudyRules.Error("STUDY_CLOSED", "Study.ClosedDiagnostic", before.Study.Id);
        var entries = before.Ledger.Entries.ToList(); var objects = before.Ledger.Objects.ToList();
        var nodeEdits = before.Ledger.NodeEdits;
        if (command.Kind != ResourceMutationKind.EditStudyNodes && command.NodeEdits is not null)
            throw Error("RES_COMMAND_INVALID", "Resource.CommandInvalid");
        var currency = before.Study.Currency.FunctionalCurrencyCode;
        switch (command.Kind)
        {
            case ResourceMutationKind.EditStudyNodes:
                RequireSimple(command);
                if (command.NodeEdits is null || command.CostObject is not null)
                    throw Error("RES_COMMAND_INVALID", "Resource.CommandInvalid");
                _ = ApplyStudyNodes(before.Model, command.NodeEdits);
                nodeEdits = command.NodeEdits;
                break;
            case ResourceMutationKind.EditResource:
                if (command.ResourceId is not { } id || command.CostObject is not null || command.RateSet is not null ||
                    command.RateChoices.Count != 0 || command.Replacements.Count != 0) throw Error("RES_COMMAND_INVALID", "Resource.CommandInvalid");
                var current = before.Resources.SingleOrDefault(r => r.Id == id) ?? throw Error("RES_NOT_FOUND", "Resource.NotFound");
                var adjustments = current.Adjustments.ToList();
                foreach (var request in command.Adjustments)
                {
                    if (request.ReversalOfId is { } reversed)
                    {
                        if (request.OriginalDelta is not null || request.Rate is not null || request.CurrencyMetadata is not null || request.Type.Length != 0)
                            throw Error("RES_COMMAND_INVALID", "Resource.CommandInvalid");
                        var original = adjustments.SingleOrDefault(a => a.Id == reversed) ?? throw Error("RES_REVERSAL_INVALID", "Resource.ReversalInvalid");
                        adjustments.Add(ResourcePreparationRules.ReverseAdjustment(original, request.Id, request.Reason, request.Id));
                    }
                    else
                    {
                        if (request.OriginalDelta is not { } delta || request.CurrencyMetadata is not { } metadata ||
                            metadata.AlphaCode != delta.Currency || !metadata.IsMonetary)
                            throw Error("RES_COMMAND_INVALID", "Resource.CommandInvalid");
                        metadata.EnsureValid();
                        adjustments.Add(ResourcePreparationRules.CreateAdjustment(request.Id, delta, currency,
                            request.Rate, request.Type, request.Reason, request.Id) with { CurrencyMetadata = metadata });
                    }
                }
                entries.RemoveAll(e => e.Id == id);
                entries.Add(new(id, current.Sources, adjustments, command.Parts));
                break;
            case ResourceMutationKind.PutObject:
                RequireSimple(command);
                if (command.CostObject is not { } item) throw Error("RES_OBJECT_INVALID", "Resource.ObjectInvalid");
                objects.RemoveAll(o => o.Id == item.Id); objects.Add(item);
                break;
            case ResourceMutationKind.ResetPreparation:
                RequireSimple(command);
                if (command.CostObject is not null) throw Error("RES_COMMAND_INVALID", "Resource.CommandInvalid");
                entries.Clear(); // Explicit, reasoned operation. The immutable history retains every previous state.
                break;
            case ResourceMutationKind.ClassifyBatch:
                // One auditable, atomic classification-only update over several accounts.
                // No source, amount, route, direct assignment, FX, layer or capacity ownership may change.
                if (command.ResourceId is not null || command.CostObject is not null ||
                    command.Parts.Count != 0 || command.Adjustments.Count != 0 ||
                    command.RateSet is not null || command.RateChoices.Count != 0 ||
                    command.Replacements.Count == 0 ||
                    command.Replacements.Select(r => r.ResourceId).Distinct().Count() != command.Replacements.Count)
                    throw Error("RES_COMMAND_INVALID", "Resource.CommandInvalid");
                foreach (var replacement in command.Replacements)
                {
                    var source = before.Resources.SingleOrDefault(r => r.Id == replacement.ResourceId)
                        ?? throw Error("RES_NOT_FOUND", "Resource.NotFound");
                    if (source.Parts.Count != replacement.Parts.Count ||
                        replacement.Parts.Select(p => p.Id).Distinct().Count() != replacement.Parts.Count)
                        throw Error("RES_COMMAND_INVALID", "Resource.CommandInvalid");
                    foreach (var candidate in replacement.Parts)
                    {
                        var original = source.Parts.SingleOrDefault(p => p.Id == candidate.Id)
                            ?? throw Error("RES_COMMAND_INVALID", "Resource.CommandInvalid");
                        if (original.Amount != candidate.Amount || original.Routing != candidate.Routing ||
                            original.PoolId != candidate.PoolId || original.ExclusionReason != candidate.ExclusionReason ||
                            !original.DirectAssignments.SequenceEqual(candidate.DirectAssignments) ||
                            original.Classification.Layer != candidate.Classification.Layer ||
                            original.Classification.CapacityTreatment != candidate.Classification.CapacityTreatment ||
                            original.Classification.CapacityOwner != candidate.Classification.CapacityOwner)
                            throw Error("RES_COMMAND_INVALID", "Resource.CommandInvalid");
                        candidate.Classification.EnsureValid(candidate);
                    }
                    entries.RemoveAll(r => r.Id == source.Id);
                    entries.Add(new ResourceDraft(source.Id, source.Sources, source.Adjustments, replacement.Parts));
                }
                break;
            case ResourceMutationKind.Revalue:
                if (command.ResourceId is not null || command.CostObject is not null || command.Parts.Count != 0 || command.Adjustments.Count != 0 ||
                    command.RateSet is null) throw Error("RES_COMMAND_INVALID", "Resource.CommandInvalid");
                entries = Revalue(before, command);
                break;
        }
        var ledger = new ResourceLedger(entries, objects, nodeEdits);
        var result = new ResourceWorkspaceSnapshot(before.Study, before.Precision, before.Model, before.Origins, ledger);
        if (requireBalanced && !result.Preparation.IsValid) throw new DomainRuleException(result.Preparation.Diagnostics[0]);
        return ledger;
    }
    private static void RequireSimple(ResourceMutation c)
    {
        if (c.ResourceId is not null || c.Parts.Count != 0 || c.Adjustments.Count != 0 || c.RateSet is not null || c.RateChoices.Count != 0 || c.Replacements.Count != 0)
            throw Error("RES_COMMAND_INVALID", "Resource.CommandInvalid");
    }
    private static List<ResourceDraft> Revalue(ResourceWorkspaceSnapshot before, ResourceMutation command)
    {
        if (command.RateChoices.Select(c => c.ValueId).Distinct().Count() != command.RateChoices.Count ||
            command.Replacements.Select(r => r.ResourceId).Distinct().Count() != command.Replacements.Count ||
            command.Replacements.Any(p => !before.Resources.Any(r => r.Id == p.ResourceId)))
            throw Error("RES_COMMAND_INVALID", "Resource.CommandInvalid");
        var selections = command.RateChoices.ToDictionary(c => c.ValueId, c => c.RateId);
        var used = new HashSet<EntityId>(); var converter = new FxConverter();
        var result = new List<ResourceDraft>();
        FxRateApplication Convert(EntityId id, FxRateApplication previous)
        {
            FxRate? rate = null;
            if (previous.OriginalMoney.Currency != before.Study.Currency.FunctionalCurrencyCode)
            {
                if (!selections.TryGetValue(id, out var selected)) throw Error("RES_REVALUE_RATE_REQUIRED", "Resource.RevalueRateRequired");
                used.Add(id); rate = command.RateSet!.GetRequired(selected);
            }
            return converter.Convert(previous.ApplicationId, previous.OriginalMoney, before.Study.Currency.FunctionalCurrencyCode, rate);
        }
        foreach (var entry in before.Resources)
        {
            var sources = entry.Sources.Select(s => new ResourceSourceValue(s.SourceRowId, Convert(s.SourceRowId, s.Conversion))).ToArray();
            var adjustments = entry.Adjustments.Where(a => a.ReversalOfId is null).Select(a => a with { Conversion = Convert(a.Id, a.Conversion) }).ToList();
            foreach (var reversal in entry.Adjustments.Where(a => a.ReversalOfId.HasValue))
                adjustments.Add(ResourcePreparationRules.ReverseAdjustment(adjustments.Single(a => a.Id == reversal.ReversalOfId),
                    reversal.Id, reversal.Reason, reversal.AuditEventId));
            var parts = command.Replacements.SingleOrDefault(p => p.ResourceId == entry.Id)?.Parts ?? entry.Parts;
            result.Add(new(entry.Id, sources, adjustments, parts));
        }
        if (!used.SetEquals(selections.Keys)) throw Error("RES_COMMAND_INVALID", "Resource.CommandInvalid");
        return result;
    }
}
