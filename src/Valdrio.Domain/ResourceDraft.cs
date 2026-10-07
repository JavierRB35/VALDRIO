namespace Valdrio.Domain;

public enum ResourceRoutingKind { Pending, DirectObject, AllocationPool, Excluded }

// Preparation contracts only: no database identity, revision increment or publication is implied.
public sealed record ResourceSourceValue(EntityId SourceRowId, FxRateApplication Conversion);
public sealed record ResourceAdjustment(
    EntityId Id, FxRateApplication Conversion, string Type, string Reason,
    EntityId AuditEventId, EntityId? ReversalOfId = null);
public sealed record DirectResourceAssignment(EntityId ObjectId, Money Amount);

public sealed class ResourcePartDraft
{
    public ResourcePartDraft(EntityId id, Money amount, ResourceRoutingKind routing,
        IEnumerable<DirectResourceAssignment>? directAssignments = null,
        EntityId? poolId = null, string? exclusionReason = null)
    {
        Id = id;
        Amount = amount;
        Routing = routing;
        DirectAssignments = FrozenList.Copy(directAssignments ?? []);
        PoolId = poolId;
        ExclusionReason = exclusionReason;
    }

    public EntityId Id { get; }
    public Money Amount { get; }
    public ResourceRoutingKind Routing { get; }
    public IReadOnlyList<DirectResourceAssignment> DirectAssignments { get; }
    public EntityId? PoolId { get; }
    public string? ExclusionReason { get; }
}

public sealed class ResourceDraft
{
    public ResourceDraft(EntityId id, IEnumerable<ResourceSourceValue> sources,
        IEnumerable<ResourceAdjustment> adjustments, IEnumerable<ResourcePartDraft> parts)
    {
        Id = id;
        Sources = FrozenList.Copy(sources);
        Adjustments = FrozenList.Copy(adjustments);
        Parts = FrozenList.Copy(parts);
    }

    public EntityId Id { get; }
    public IReadOnlyList<ResourceSourceValue> Sources { get; }
    public IReadOnlyList<ResourceAdjustment> Adjustments { get; }
    public IReadOnlyList<ResourcePartDraft> Parts { get; }
}

public sealed record ResourceCoverage(
    EntityId ResourceId, Money ConvertedOriginal, Money AdjustmentTotal, Money Effective,
    Money Direct, Money Pools, Money Excluded, Money Pending, Money Difference,
    bool HasPendingParts);

public sealed class ResourcePreparation
{
    public ResourcePreparation(IEnumerable<ResourceCoverage> coverage, IEnumerable<DomainDiagnostic> diagnostics)
    {
        Coverage = FrozenList.Copy(coverage);
        Diagnostics = FrozenList.Copy(diagnostics);
    }

    public IReadOnlyList<ResourceCoverage> Coverage { get; }
    public IReadOnlyList<DomainDiagnostic> Diagnostics { get; }
    public bool IsValid => Diagnostics.Count == 0;
    // A valid draft may still have explicitly pending routes, even with a zero net amount.
    public bool HasPendingParts => Coverage.Any(value => value.HasPendingParts);
}
