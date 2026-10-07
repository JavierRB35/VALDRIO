using System.Text.Json.Serialization;

namespace Valdrio.Domain;

public enum ResourceRoutingKind { Pending, DirectObject, AllocationPool, Excluded }

// Preparation contracts only: no database identity, revision increment or publication is implied.
public sealed record ResourceSourceValue(EntityId SourceRowId, FxRateApplication Conversion);
public sealed record ResourceAdjustment(
    EntityId Id, FxRateApplication Conversion, string Type, string Reason,
    EntityId AuditEventId, EntityId? ReversalOfId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CurrencyMetadataSnapshot? CurrencyMetadata = null);
public sealed record DirectResourceAssignment(EntityId ObjectId, Money Amount);

public sealed class ResourcePartDraft
{
    [JsonConstructor]
    public ResourcePartDraft(EntityId id, Money amount, ResourceRoutingKind routing,
        IReadOnlyList<DirectResourceAssignment> directAssignments, EntityId? poolId,
        string? exclusionReason, ResourceClassification classification)
        : this(id, amount, routing, (IEnumerable<DirectResourceAssignment>)directAssignments,
            poolId, exclusionReason, classification) { }

    public ResourcePartDraft(EntityId id, Money amount, ResourceRoutingKind routing,
        IEnumerable<DirectResourceAssignment>? directAssignments = null,
        EntityId? poolId = null, string? exclusionReason = null,
        ResourceClassification? classification = null)
    {
        Id = id;
        Amount = amount;
        Routing = routing;
        DirectAssignments = FrozenList.Copy(directAssignments ?? []);
        PoolId = poolId;
        ExclusionReason = exclusionReason;
        Classification = classification ?? ResourceClassification.Unclassified;
    }

    public EntityId Id { get; }
    public Money Amount { get; }
    public ResourceRoutingKind Routing { get; }
    public IReadOnlyList<DirectResourceAssignment> DirectAssignments { get; }
    public EntityId? PoolId { get; }
    public string? ExclusionReason { get; }
    public ResourceClassification Classification { get; }

    public ResourcePartDraft WithClassification(ResourceClassification classification)
    {
        ArgumentNullException.ThrowIfNull(classification);
        var changed = new ResourcePartDraft(Id, Amount, Routing, DirectAssignments, PoolId, ExclusionReason, classification);
        classification.EnsureValid(changed);
        return changed;
    }
}

public sealed class ResourceDraft
{
    [JsonConstructor]
    public ResourceDraft(EntityId id, IReadOnlyList<ResourceSourceValue> sources,
        IReadOnlyList<ResourceAdjustment> adjustments, IReadOnlyList<ResourcePartDraft> parts)
        : this(id, (IEnumerable<ResourceSourceValue>)sources, adjustments, parts) { }

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
    public ResourcePreparation(IEnumerable<ResourceCoverage> coverage, IEnumerable<DomainDiagnostic> diagnostics,
        IEnumerable<DomainDiagnostic>? warnings = null)
    {
        Coverage = FrozenList.Copy(coverage);
        Diagnostics = FrozenList.Copy(diagnostics);
        Warnings = FrozenList.Copy(warnings ?? []);
    }

    public IReadOnlyList<ResourceCoverage> Coverage { get; }
    public IReadOnlyList<DomainDiagnostic> Diagnostics { get; }
    public IReadOnlyList<DomainDiagnostic> Warnings { get; }
    public bool IsValid => Diagnostics.Count == 0;
    // A valid draft may still have explicitly pending routes, even with a zero net amount.
    public bool HasPendingParts => Coverage.Any(value => value.HasPendingParts);
}
