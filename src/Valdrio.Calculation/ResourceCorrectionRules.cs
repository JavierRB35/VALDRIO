using Valdrio.Domain;

namespace Valdrio.Calculation;

public sealed record ResourceCorrectionDraft(Money Delta, IReadOnlyList<ResourcePartDraft> Parts);

/// <summary>Pure cumulative-correction rule. IDs are supplied by the caller; no clock or persistence.</summary>
public static class ResourceCorrectionRules
{
    public static ResourceCorrectionDraft? Prepare(ResourceDraft resource, Money requestedTotal, EntityId pendingPartId)
    {
        ArgumentNullException.ThrowIfNull(resource);
        var coverage = ResourcePreparationRules.Prepare([resource], requestedTotal.Currency,
            requestedTotal.PrecisionPolicy).Coverage.Single();
        var delta = requestedTotal - coverage.AdjustmentTotal;
        if (delta.Amount == 0m) return null;

        var parts = resource.Parts.ToList();
        var pendingIndex = parts.FindLastIndex(p => p.Routing == ResourceRoutingKind.Pending);
        if (pendingIndex >= 0)
        {
            var pending = parts[pendingIndex];
            parts[pendingIndex] = new(pending.Id, pending.Amount + delta, pending.Routing,
                pending.DirectAssignments, pending.PoolId, pending.ExclusionReason, pending.Classification);
        }
        else
        {
            pendingPartId.EnsureValid();
            parts.Add(new(pendingPartId, delta, ResourceRoutingKind.Pending));
        }
        return new(delta, FrozenList.Copy(parts));
    }
}
