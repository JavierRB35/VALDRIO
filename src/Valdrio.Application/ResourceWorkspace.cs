using System.Text.Json.Serialization;
using Valdrio.Calculation;
using Valdrio.Domain;

namespace Valdrio.Application;

public enum ResourceObjectKind { Product, Service, Order, Other }
public sealed record ResourceCostObject(EntityId Id, EntityId CompanyId, EntityId StudyId,
    string Code, string Name, ResourceObjectKind Kind);
public sealed record ResourceOriginRow(EntityId Id, EntityId BatchId, int RecordNumber, bool Earlier,
    string FileName, FxRateApplication Conversion, string? Description = null, int? DescriptionColumn = null, string? FileHash = null);
public sealed record ResourceOrigin(EntityId Id, string Code, IReadOnlyList<ResourceOriginRow> Rows)
{
    // Semantic projection uses the explicitly saved column map, never a guessed header.
    [JsonIgnore] public IReadOnlyList<string> Descriptions => FrozenList.Copy(Rows.Select(r => r.Description)
        .Where(d => !string.IsNullOrWhiteSpace(d)).Select(d => d!).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
    [JsonIgnore] public bool HasDescriptionConflict => Descriptions.Count > 1;
}
public sealed record ResourceAdjustmentRequest(EntityId Id, Money? OriginalDelta, FxRate? Rate,
    string Type, string Reason, EntityId? ReversalOfId, CurrencyMetadataSnapshot? CurrencyMetadata);
public sealed record ResourceRateChoice(EntityId ValueId, FxRateId RateId);
public sealed record ResourcePartReplacement(EntityId ResourceId, IReadOnlyList<ResourcePartDraft> Parts);
public enum ResourceMutationKind { EditResource, PutObject, ResetPreparation, Revalue, ClassifyBatch }

// A request freezes the user's choices; the application reconstructs every economic result.
public sealed class ResourceMutation
{
    public ResourceMutation(ResourceMutationKind kind, EntityId companyId, EntityId studyId, long expectedRevision,
        EntityId requestId, string actor, string reason, EntityId? resourceId,
        IReadOnlyList<ResourcePartDraft> parts, IReadOnlyList<ResourceAdjustmentRequest> adjustments,
        ResourceCostObject? costObject, FxRateSetVersion? rateSet,
        IReadOnlyList<ResourceRateChoice> rateChoices, IReadOnlyList<ResourcePartReplacement> replacements)
    {
        Kind = kind; CompanyId = companyId; StudyId = studyId; ExpectedRevision = expectedRevision;
        RequestId = requestId; Actor = actor; Reason = reason; ResourceId = resourceId;
        Parts = FrozenList.Copy(parts); Adjustments = FrozenList.Copy(adjustments); CostObject = costObject;
        RateSet = rateSet; RateChoices = FrozenList.Copy(rateChoices);
        Replacements = FrozenList.Copy(replacements.Select(r => new ResourcePartReplacement(r.ResourceId, FrozenList.Copy(r.Parts))));
    }
    public ResourceMutationKind Kind { get; }
    public EntityId CompanyId { get; }
    public EntityId StudyId { get; }
    public long ExpectedRevision { get; }
    public EntityId RequestId { get; }
    public string Actor { get; }
    public string Reason { get; }
    public EntityId? ResourceId { get; }
    public IReadOnlyList<ResourcePartDraft> Parts { get; }
    public IReadOnlyList<ResourceAdjustmentRequest> Adjustments { get; }
    public ResourceCostObject? CostObject { get; }
    public FxRateSetVersion? RateSet { get; }
    public IReadOnlyList<ResourceRateChoice> RateChoices { get; }
    public IReadOnlyList<ResourcePartReplacement> Replacements { get; }
}

public sealed class ResourceLedger
{
    public ResourceLedger(IReadOnlyList<ResourceDraft> entries, IReadOnlyList<ResourceCostObject> objects)
    {
        Entries = FrozenList.Copy(entries.OrderBy(e => e.Id).Select(e => new ResourceDraft(e.Id,
            e.Sources.OrderBy(s => s.SourceRowId).ToArray(), e.Adjustments.OrderBy(a => a.Id).ToArray(),
            e.Parts.OrderBy(p => p.Id).Select(p => new ResourcePartDraft(p.Id, p.Amount, p.Routing,
                p.DirectAssignments.OrderBy(a => a.ObjectId).ToArray(), p.PoolId, p.ExclusionReason, p.Classification)).ToArray())));
        Objects = FrozenList.Copy(objects.OrderBy(o => o.Id));
    }
    public IReadOnlyList<ResourceDraft> Entries { get; }
    public IReadOnlyList<ResourceCostObject> Objects { get; }
    public static ResourceLedger Empty => new([], []);
}

public sealed record ResourceMutationReceipt(ResourceMutation Request, StudyDefinition PreviousStudy, StudyDefinition ConfirmedStudy,
    ResourceLedger Ledger, UtcInstant AtUtc);

public interface IResourceWorkspace
{
    ResourceWorkspaceSnapshot ReadResources(EntityId companyId, EntityId studyId, CancellationToken token = default);
    ResourceMutationReceipt ChangeResources(ResourceMutation command, CancellationToken token = default);
    IReadOnlyList<ResourceMutationReceipt> ReadResourceHistory(EntityId companyId, EntityId studyId, CancellationToken token = default);
}

public sealed class ResourceWorkspaceSnapshot
{
    public ResourceWorkspaceSnapshot(StudyDefinition study, PrecisionPolicy precision, PublishedModelStructure model,
        IReadOnlyList<ResourceOrigin> origins, ResourceLedger ledger, long economicRevision = -1)
    {
        Study = study; Precision = precision; Model = model; Origins = FrozenList.Copy(origins); Ledger = ledger;
        EconomicRevision = economicRevision < 0 ? study.Revision : economicRevision;
        if (study.CompanyId != model.Structure.CompanyId || study.SelectedModelVersionId != model.Structure.Id ||
            precision.Version != model.Structure.PrecisionPolicyVersion)
            throw ResourceWorkspaceRules.Error("RES_SCOPE_INVALID", "Resource.ScopeInvalid");
        Resources = ResourceWorkspaceRules.Materialize(this);
        Preparation = ResourcePreparationRules.Prepare(Resources, study.Currency.FunctionalCurrencyCode, precision);
        ResourceWorkspaceRules.ValidateScope(this);
    }
    [JsonIgnore] public long EconomicRevision { get; }
    public StudyDefinition Study { get; }
    public PrecisionPolicy Precision { get; }
    public PublishedModelStructure Model { get; }
    public IReadOnlyList<ResourceOrigin> Origins { get; }
    public ResourceLedger Ledger { get; }
    [JsonIgnore] public IReadOnlyList<ResourceDraft> Resources { get; }
    [JsonIgnore] public ResourcePreparation Preparation { get; }
}
