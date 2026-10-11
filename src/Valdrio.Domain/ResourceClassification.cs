namespace Valdrio.Domain;

public enum ResourceBehavior { Unclassified, Fixed, Variable, MixedPending, AssumedFixed, Mixed }
// Economic perimeter is independent of fixed/variable behavior. Do not reuse legacy CostLayer.
public enum ResourceCostLayer { Unclassified, Production, Commercial, Structure }
public enum ResourceCapacityTreatment { None, AssessAtPool, PeriodCost }

public sealed record ResourceBehaviorContext(
    UnitId DriverUnitId, DateOnly HorizonStart, DateOnly HorizonEnd,
    Quantity MinimumActivity, Quantity MaximumActivity);

// Missing observations remain null. Choosing a capacity denominator belongs to the later policy.
public sealed record ResourceCapacityOwner(
    EntityId PoolId, EntityId NodeId, UnitId UnitId,
    Quantity? Theoretical = null, Quantity? Practical = null,
    Quantity? Normal = null, Quantity? Actual = null);

public sealed record ResourceClassification(
    ResourceBehavior Behavior,
    ResourceBehaviorContext? Context,
    ResourceCostLayer Layer,
    ResourceCapacityTreatment CapacityTreatment,
    ResourceCapacityOwner? CapacityOwner)
{
    // Explicit composition in primary allocation; null preserves historical classifications.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public decimal? VariablePercentage { get; init; }

    public static ResourceClassification Unclassified { get; } =
        new(ResourceBehavior.Unclassified, null, ResourceCostLayer.Unclassified,
            ResourceCapacityTreatment.None, null);

    public void EnsureValid(ResourcePartDraft part)
    {
        ArgumentNullException.ThrowIfNull(part);
        if (!Enum.IsDefined(Behavior) || !Enum.IsDefined(Layer) || !Enum.IsDefined(CapacityTreatment))
            throw Error("RES_CLASSIFICATION_INVALID", "Resource.ClassificationInvalid");
        if (VariablePercentage is { } percentage &&
            (percentage < 0m || percentage > 100m ||
             Behavior == ResourceBehavior.Fixed && percentage != 0m ||
             Behavior == ResourceBehavior.Variable && percentage != 100m ||
             Behavior == ResourceBehavior.Mixed && (percentage <= 0m || percentage >= 100m) ||
             Behavior is not (ResourceBehavior.Fixed or ResourceBehavior.Variable or ResourceBehavior.Mixed)))
            throw Error("RES_CLASSIFICATION_INVALID", "Resource.ClassificationInvalid");
        if (Behavior == ResourceBehavior.Mixed && VariablePercentage is null)
            throw Error("RES_CLASSIFICATION_INVALID", "Resource.ClassificationInvalid");
        if (Behavior is ResourceBehavior.Fixed or ResourceBehavior.Variable && Context is null && VariablePercentage is null)
            throw Error("RES_BEHAVIOR_CONTEXT_REQUIRED", "Resource.BehaviorContextRequired");
        if (Context is { } context)
        {
            context.DriverUnitId.EnsureValid();
            if (context.HorizonStart > context.HorizonEnd)
                throw Error("RES_BEHAVIOR_HORIZON_INVALID", "Resource.BehaviorHorizonInvalid");
            ValidateQuantity(context.MinimumActivity, context.DriverUnitId, part.Amount.PrecisionPolicy);
            ValidateQuantity(context.MaximumActivity, context.DriverUnitId, part.Amount.PrecisionPolicy);
            if (context.MinimumActivity.Value > context.MaximumActivity.Value)
                throw Error("RES_BEHAVIOR_RANGE_INVALID", "Resource.BehaviorRangeInvalid");
        }

        if (CapacityTreatment == ResourceCapacityTreatment.AssessAtPool)
        {
            if (CapacityOwner is not { } owner || part.Routing != ResourceRoutingKind.AllocationPool ||
                part.PoolId != owner.PoolId)
                throw Error("RES_CAPACITY_OWNER_INVALID", "Resource.CapacityOwnerInvalid");
            owner.PoolId.EnsureValid();
            owner.NodeId.EnsureValid();
            owner.UnitId.EnsureValid();
            // A behavior driver and a capacity unit can differ: each stays explicitly typed.
            foreach (var quantity in new[] { owner.Theoretical, owner.Practical, owner.Normal, owner.Actual })
                if (quantity is { } value) ValidateQuantity(value, owner.UnitId, part.Amount.PrecisionPolicy);
        }
        else if (CapacityOwner is not null)
            throw Error("RES_CAPACITY_OWNER_INVALID", "Resource.CapacityOwnerInvalid");
    }

    private static void ValidateQuantity(Quantity quantity, UnitId expectedUnit, PrecisionPolicy policy)
    {
        quantity.EnsureValid();
        PrecisionPolicyCompatibility.EnsureSame(policy, quantity.PrecisionPolicy, "resourcePart", "activity");
        if (quantity.UnitId != expectedUnit)
            throw Error("RES_ACTIVITY_UNIT_MISMATCH", "Resource.ActivityUnitMismatch");
        if (quantity.Value < 0m)
            throw Error("RES_BEHAVIOR_RANGE_INVALID", "Resource.BehaviorRangeInvalid");
    }

    private static DomainRuleException Error(string code, string key) => new(new(code, key, []));
}
