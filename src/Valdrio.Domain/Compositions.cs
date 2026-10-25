using System.Text.Json.Serialization;

namespace Valdrio.Domain;

public enum CompositionItemKind { Product, Service, SemiFinished, Material }
public enum ConsumptionBasis { PerUnit, PerStartedBatch, PerOrder }
public enum CompositionLossKind { None, ExtraInput, Yield }
public enum CompositionState { Draft, Published }

public sealed record CompositionItem(EntityId Id, string Code, string Name, UnitId UnitId,
    CompositionItemKind Kind, bool Active)
{
    // Optional reference amount, kept outside recipe versions so prices can be updated
    // without changing the physical composition. Older catalogue payloads omit these.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? ReferenceUnitCost { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReferenceCostSource { get; init; }
}

// Exactly one target: direct material, a frozen semi-finished version, or a model pool.
public sealed record CompositionLine(EntityId Id, EntityId? MaterialId, EntityId? RecipeVersionId,
    EntityId? PoolId, decimal Quantity, UnitId InputUnitId, decimal ConversionFactor,
    ConsumptionBasis Basis, CompositionLossKind LossKind, decimal LossPercent);

public sealed record CompositionVersion(EntityId Id, EntityId ProductId, int Number,
    EntityId ModelVersionId, decimal BatchSize, CompositionState State, string Note,
    IReadOnlyList<CompositionLine> Lines);

public sealed record CompositionCatalog(EntityId CompanyId, long Revision,
    IReadOnlyList<CompositionItem> Items, IReadOnlyList<CompositionVersion> Versions);
