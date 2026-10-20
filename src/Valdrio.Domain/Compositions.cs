namespace Valdrio.Domain;

public enum CompositionItemKind { Product, Service, SemiFinished, Material }
public enum ConsumptionBasis { PerUnit, PerStartedBatch, PerOrder }
public enum CompositionLossKind { None, ExtraInput, Yield }
public enum CompositionState { Draft, Published }

public sealed record CompositionItem(EntityId Id, string Code, string Name, UnitId UnitId,
    CompositionItemKind Kind, bool Active);

// Exactly one target: direct material, a frozen semi-finished version, or a model pool.
public sealed record CompositionLine(EntityId Id, EntityId? MaterialId, EntityId? RecipeVersionId,
    EntityId? PoolId, decimal Quantity, UnitId InputUnitId, decimal ConversionFactor,
    ConsumptionBasis Basis, CompositionLossKind LossKind, decimal LossPercent);

public sealed record CompositionVersion(EntityId Id, EntityId ProductId, int Number,
    EntityId ModelVersionId, decimal BatchSize, CompositionState State, string Note,
    IReadOnlyList<CompositionLine> Lines);

public sealed record CompositionCatalog(EntityId CompanyId, long Revision,
    IReadOnlyList<CompositionItem> Items, IReadOnlyList<CompositionVersion> Versions);
