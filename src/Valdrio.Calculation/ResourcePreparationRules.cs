using Valdrio.Domain;

namespace Valdrio.Calculation;

/// <summary>Pure T07 preparation. Persistence, scope resolution and final publication are separate gates.</summary>
public static class ResourcePreparationRules
{
    public static ResourceAdjustment CreateAdjustment(EntityId id, Money originalDelta,
        CurrencyCode functionalCurrency, FxRate? rate, string type, string reason, EntityId auditEventId)
    {
        id.EnsureValid();
        auditEventId.EnsureValid();
        RequireReason(type, reason);
        return new(id, new FxConverter().Convert(id.ToString(), originalDelta, functionalCurrency, rate),
            type, reason, auditEventId);
    }

    public static ResourceAdjustment ReverseAdjustment(ResourceAdjustment original,
        EntityId reversalId, string reason, EntityId auditEventId)
    {
        ArgumentNullException.ThrowIfNull(original);
        ValidateAdjustment(original);
        if (original.ReversalOfId.HasValue || reversalId == original.Id)
            throw Error("RES_REVERSAL_INVALID", "Resource.ReversalInvalid");
        var conversion = original.Conversion;
        var reversed = CreateAdjustment(reversalId,
            new Money(-conversion.OriginalMoney.Amount, conversion.OriginalMoney.Currency,
                conversion.OriginalMoney.PrecisionPolicy),
            conversion.ConvertedOriginalMoney.Currency, Rate(conversion), original.Type, reason, auditEventId);
        return reversed with { ReversalOfId = original.Id, CurrencyMetadata = original.CurrencyMetadata };
    }

    public static ResourcePreparation Prepare(IEnumerable<ResourceDraft> resources,
        CurrencyCode functionalCurrency, PrecisionPolicy precisionPolicy,
        bool requireDefinitiveAbsorption = false, bool capacityRequiresBehavior = false)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(precisionPolicy);
        // Validate even an empty draft.
        var zero = new Money(0m, functionalCurrency, precisionPolicy);
        var coverage = new List<ResourceCoverage>();
        var diagnostics = new List<DomainDiagnostic>();
        var warnings = new List<DomainDiagnostic>();
        var resourceIds = new HashSet<EntityId>();
        var sourceIds = new HashSet<EntityId>();
        var partIds = new HashSet<EntityId>();
        var adjustmentIds = new HashSet<EntityId>();
        var auditIds = new HashSet<EntityId>();
        var applicationIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var resource in resources.OrderBy(value => value.Id))
        {
            resource.Id.EnsureValid();
            if (!resourceIds.Add(resource.Id)) throw Error("RES_DUPLICATE_ID", "Resource.DuplicateId");
            if (resource.AnalyticalOrigin is { } analytical)
            {
                analytical.ApplicationId.EnsureValid();
                if (resource.Sources.Count != 0 || string.IsNullOrWhiteSpace(analytical.Code) || string.IsNullOrWhiteSpace(analytical.Name))
                    throw Error("RES_SOURCE_REQUIRED", "Resource.SourceRequired");
            }
            else if (resource.Sources.Count == 0) throw Error("RES_SOURCE_REQUIRED", "Resource.SourceRequired");
            var converted = zero;
            foreach (var source in resource.Sources.OrderBy(value => value.SourceRowId))
            {
                source.SourceRowId.EnsureValid();
                if (!sourceIds.Add(source.SourceRowId))
                    throw Error("RES_SOURCE_REUSED", "Resource.SourceReused");
                ValidateConversion(source.Conversion);
                if (!applicationIds.Add(source.Conversion.ApplicationId))
                    throw Error("RES_DUPLICATE_ID", "Resource.DuplicateId");
                converted += source.Conversion.ConvertedOriginalMoney;
            }

            var adjustmentTotal = zero;
            var byId = new Dictionary<EntityId, ResourceAdjustment>();
            foreach (var adjustment in resource.Adjustments.OrderBy(value => value.Id))
            {
                ValidateAdjustment(adjustment);
                if (!adjustmentIds.Add(adjustment.Id) || !auditIds.Add(adjustment.AuditEventId) ||
                    !applicationIds.Add(adjustment.Conversion.ApplicationId))
                    throw Error("RES_DUPLICATE_ID", "Resource.DuplicateId");
                byId.Add(adjustment.Id, adjustment);
                adjustmentTotal += adjustment.Conversion.ConvertedOriginalMoney;
            }
            var reversedIds = new HashSet<EntityId>();
            foreach (var adjustment in resource.Adjustments.Where(value => value.ReversalOfId.HasValue))
            {
                var originalId = adjustment.ReversalOfId!.Value;
                if (!reversedIds.Add(originalId) || !byId.TryGetValue(originalId, out var original) ||
                    original.ReversalOfId.HasValue ||
                    ReverseAdjustment(original, adjustment.Id, adjustment.Reason, adjustment.AuditEventId) != adjustment)
                    throw Error("RES_REVERSAL_INVALID", "Resource.ReversalInvalid");
            }

            var effective = converted + adjustmentTotal;
            var direct = zero;
            var pools = zero;
            var excluded = zero;
            var pending = zero;
            var hasPending = false;
            foreach (var part in resource.Parts.OrderBy(value => value.Id))
            {
                part.Id.EnsureValid();
                if (!partIds.Add(part.Id)) throw Error("RES_DUPLICATE_ID", "Resource.DuplicateId");
                _ = zero + part.Amount; // Currency and precision-policy compatibility, even for zero.
                ValidateRoute(part, zero);
                part.Classification.EnsureValid(part);
                if (part.Routing is ResourceRoutingKind.DirectObject or ResourceRoutingKind.AllocationPool)
                {
                    var classification = part.Classification;
                    if (classification.Behavior == ResourceBehavior.Unclassified)
                    {
                        var unclassified = PartDiagnostic(part, "RES_BEHAVIOR_UNCLASSIFIED", "Resource.BehaviorUnclassified");
                        if (capacityRequiresBehavior && classification.CapacityTreatment == ResourceCapacityTreatment.AssessAtPool)
                            diagnostics.Add(unclassified);
                        else warnings.Add(unclassified);
                    }
                    if (classification.Behavior == ResourceBehavior.AssumedFixed)
                    {
                        var assumed = PartDiagnostic(part, "RES_BEHAVIOR_ASSUMED_FIXED", "Resource.BehaviorAssumedFixed");
                        if (requireDefinitiveAbsorption ||
                            (capacityRequiresBehavior && classification.CapacityTreatment == ResourceCapacityTreatment.AssessAtPool))
                            diagnostics.Add(assumed);
                        else warnings.Add(assumed);
                    }
                    if (classification.Behavior == ResourceBehavior.MixedPending)
                    {
                        var mixed = PartDiagnostic(part, "RES_BEHAVIOR_MIXED_PENDING", "Resource.BehaviorMixedPending");
                        if (requireDefinitiveAbsorption ||
                            (capacityRequiresBehavior && classification.CapacityTreatment == ResourceCapacityTreatment.AssessAtPool))
                            diagnostics.Add(mixed);
                        else warnings.Add(mixed);
                    }
                }
                switch (part.Routing)
                {
                    case ResourceRoutingKind.DirectObject: direct += part.Amount; break;
                    case ResourceRoutingKind.AllocationPool: pools += part.Amount; break;
                    case ResourceRoutingKind.Excluded: excluded += part.Amount; break;
                    case ResourceRoutingKind.Pending: pending += part.Amount; hasPending = true; break;
                }
            }

            if (resource.Parts.Count == 0)
                diagnostics.Add(new("RES_PARTS_REQUIRED", "Resource.PartsRequired",
                    [new("resourceId", DiagnosticArgumentType.Identifier, resource.Id.ToString())]));
            var difference = effective - direct - pools - excluded - pending;
            coverage.Add(new(resource.Id, converted, adjustmentTotal, effective,
                direct, pools, excluded, pending, difference, hasPending));
            if (difference.Amount != 0m)
                diagnostics.Add(new("RES_PARTITION_MISMATCH", "Resource.PartitionMismatch",
                    [new("resourceId", DiagnosticArgumentType.Identifier, resource.Id.ToString()),
                     new("difference", DiagnosticArgumentType.Decimal, CanonicalDecimal.Format(difference.Amount))]));
        }
        return new(coverage, diagnostics, warnings);
    }

    private static void ValidateRoute(ResourcePartDraft part, Money zero)
    {
        if (!Enum.IsDefined(part.Routing) ||
            (part.Routing != ResourceRoutingKind.DirectObject && part.DirectAssignments.Count != 0) ||
            (part.Routing != ResourceRoutingKind.AllocationPool && part.PoolId.HasValue) ||
            (part.Routing != ResourceRoutingKind.Excluded && part.ExclusionReason is not null))
            throw Error("RES_ROUTE_CONFLICT", "Resource.RouteConflict");

        switch (part.Routing)
        {
            case ResourceRoutingKind.DirectObject:
                if (part.DirectAssignments.Count == 0)
                    throw Error("RES_DIRECT_MISMATCH", "Resource.DirectMismatch");
                var assigned = zero;
                var objects = new HashSet<EntityId>();
                foreach (var assignment in part.DirectAssignments.OrderBy(value => value.ObjectId))
                {
                    assignment.ObjectId.EnsureValid();
                    if (!objects.Add(assignment.ObjectId)) throw Error("RES_DUPLICATE_ID", "Resource.DuplicateId");
                    assigned += assignment.Amount;
                }
                if (assigned != part.Amount) throw Error("RES_DIRECT_MISMATCH", "Resource.DirectMismatch");
                break;
            case ResourceRoutingKind.AllocationPool:
                if (!part.PoolId.HasValue) throw Error("RES_POOL_REQUIRED", "Resource.PoolRequired");
                part.PoolId.Value.EnsureValid();
                break;
            case ResourceRoutingKind.Excluded:
                if (string.IsNullOrWhiteSpace(part.ExclusionReason))
                    throw Error("RES_EXCLUSION_REASON_REQUIRED", "Resource.ExclusionReasonRequired");
                break;
        }
    }

    private static void ValidateAdjustment(ResourceAdjustment adjustment)
    {
        adjustment.Id.EnsureValid();
        adjustment.AuditEventId.EnsureValid();
        adjustment.ReversalOfId?.EnsureValid();
        RequireReason(adjustment.Type, adjustment.Reason);
        ValidateConversion(adjustment.Conversion);
        if (adjustment.CurrencyMetadata is { } metadata)
        {
            metadata.EnsureValid();
            if (metadata.AlphaCode != adjustment.Conversion.OriginalMoney.Currency || !metadata.IsMonetary)
                throw Error("RES_ADJUSTMENT_CURRENCY_INVALID", "Resource.AdjustmentCurrencyInvalid");
        }
    }

    private static void RequireReason(string type, string reason)
    {
        if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(reason))
            throw Error("RES_ADJUSTMENT_REASON_REQUIRED", "Resource.AdjustmentReasonRequired");
    }

    private static void ValidateConversion(FxRateApplication conversion)
    {
        ArgumentNullException.ThrowIfNull(conversion);
        conversion.EnsureValid();
        var recomputed = new FxConverter().Convert(conversion.ApplicationId, conversion.OriginalMoney,
            conversion.ConvertedOriginalMoney.Currency, Rate(conversion));
        if (recomputed != conversion) throw Error("RES_CONVERSION_INVALID", "Resource.ConversionInvalid");
    }

    private static FxRate? Rate(FxRateApplication conversion) => conversion.IsIdentity ? null :
        new(conversion.RateId!.Value, conversion.OriginalMoney.Currency,
            conversion.ConvertedOriginalMoney.Currency, conversion.Quote, conversion.RateDate!.Value,
            conversion.Source, conversion.RateSetVersionId!);

    private static DomainRuleException Error(string code, string key) => new(new(code, key, []));

    private static DomainDiagnostic PartDiagnostic(ResourcePartDraft part, string code, string key) =>
        new(code, key, [new("partId", DiagnosticArgumentType.Identifier, part.Id.ToString())]);
}
