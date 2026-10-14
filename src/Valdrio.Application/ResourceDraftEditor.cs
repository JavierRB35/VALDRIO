using Valdrio.Calculation;
using Valdrio.Domain;

namespace Valdrio.Application;

public sealed record ResourceDraftEdit(ResourceDraft Draft, ResourcePreparation Preparation);

/// <summary>
/// Edits an isolated in-memory draft. Does not save a study, create audit events or increment its revision.
/// Persistent commands must add study/model scope and optimistic concurrency before integration.
/// </summary>
public static class ResourceDraftEditor
{
    public static ResourceDraftEdit ClassifyPart(ResourceDraft draft, EntityId partId,
        ResourceClassification classification, CurrencyCode currency, PrecisionPolicy precision)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(classification);
        RequirePart(draft, partId);
        var parts = draft.Parts.Select(part => part.Id == partId ? part.WithClassification(classification) : part);
        return Repartition(draft, parts, currency, precision);
    }

    public static ResourceDraftEdit SplitPart(ResourceDraft draft, EntityId partId,
        IEnumerable<ResourcePartDraft> replacements, CurrencyCode currency, PrecisionPolicy precision)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(replacements);
        var original = RequirePart(draft, partId);
        var children = replacements.ToArray();
        if (children.Length < 2) throw Error("RES_SPLIT_REQUIRES_PARTS", "Resource.SplitRequiresParts");
        var sum = new Money(0m, currency, precision);
        foreach (var child in children) sum += child.Amount;
        if (sum != original.Amount) throw Error("RES_SPLIT_MISMATCH", "Resource.SplitMismatch");
        return Repartition(draft, draft.Parts.Where(part => part.Id != partId).Concat(children), currency, precision);
    }

    public static ResourceDraftEdit Repartition(ResourceDraft draft, IEnumerable<ResourcePartDraft> parts,
        CurrencyCode currency, PrecisionPolicy precision)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var candidate = new ResourceDraft(draft.Id, draft.Sources, draft.Adjustments, parts) { AnalyticalOrigin = draft.AnalyticalOrigin };
        var preparation = ResourcePreparationRules.Prepare([candidate], currency, precision);
        if (!preparation.IsValid) throw new DomainRuleException(preparation.Diagnostics[0]);
        return new(candidate, preparation);
    }

    private static ResourcePartDraft RequirePart(ResourceDraft draft, EntityId partId)
    {
        partId.EnsureValid();
        var matches = draft.Parts.Where(part => part.Id == partId).ToArray();
        if (matches.Length != 1) throw Error("RES_PART_NOT_FOUND", "Resource.PartNotFound");
        return matches[0];
    }

    private static DomainRuleException Error(string code, string key) => new(new(code, key, []));
}
