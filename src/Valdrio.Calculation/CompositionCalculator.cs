using Valdrio.Domain;
namespace Valdrio.Calculation;

public sealed record CompositionPool(EntityId Id, EntityId ModelId, string Name, UnitId UnitId, bool Active);
public sealed record CompositionQuantity(string Path, EntityId LineId, EntityId TargetId, bool Activity,
    UnitId UnitId, decimal BasisCount, decimal BaseQuantity, decimal AdjustedQuantity,
    decimal ConvertedQuantity, decimal RequiredQuantity, string Name, bool Expanded = false, UnitId InputUnitId = default);
public sealed record CompositionPreview(decimal OutputQuantity, decimal StartedBatches,
    IReadOnlyList<CompositionQuantity> Lines, IReadOnlyList<CompositionTotal> Totals);
public sealed record CompositionTotal(EntityId TargetId, bool Activity, UnitId UnitId, string Name, decimal Quantity);

public static class CompositionCalculator
{
    public static DomainRuleException Error(string key) => new(new("COMPOSITION_" + key.ToUpperInvariant(), "Composition." + key, []));

    public static void Validate(CompositionCatalog catalog, IReadOnlyList<UnitDefinition> units,
        IReadOnlyList<CompositionPool> pools, bool requireActive = false, EntityId? selected = null)
    {
        if (!catalog.CompanyId.IsValid || catalog.Revision < 0 ||
            catalog.Items.Select(x => x.Id).Distinct().Count() != catalog.Items.Count ||
            catalog.Items.Select(x => x.Code).Distinct(StringComparer.Ordinal).Count() != catalog.Items.Count ||
            catalog.Versions.Select(x => x.Id).Distinct().Count() != catalog.Versions.Count)
            throw Error("InvalidCatalog");
        foreach (var item in catalog.Items)
            if (!item.Id.IsValid || string.IsNullOrWhiteSpace(item.Code) || string.IsNullOrWhiteSpace(item.Name) ||
                !Enum.IsDefined(item.Kind) || !units.Any(u => u.Id == item.UnitId) ||
                item.ReferenceUnitCost is < 0 || item.ReferenceUnitCost.HasValue && string.IsNullOrWhiteSpace(item.ReferenceCostSource) ||
                !item.ReferenceUnitCost.HasValue && item.ReferenceCostSource is not null)
                throw Error("InvalidItem");
        foreach (var group in catalog.Versions.GroupBy(v => v.ProductId))
        {
            if (!group.OrderBy(v => v.Number).Select(v => v.Number).SequenceEqual(Enumerable.Range(1, group.Count())) ||
                group.Count(v => v.State == CompositionState.Draft) > 1)
                throw Error("InvalidVersion");
        }
        var visited = 0;
        foreach (var version in catalog.Versions)
        {
            var output = catalog.Items.SingleOrDefault(x => x.Id == version.ProductId);
            if (!version.Id.IsValid || !version.ModelVersionId.IsValid || output is null ||
                output.Kind == CompositionItemKind.Material || version.BatchSize <= 0 || !Enum.IsDefined(version.State) ||
                version.Lines.Select(l => l.Id).Distinct().Count() != version.Lines.Count)
                throw Error("InvalidVersion");
            if (units.Single(u => u.Id == output.UnitId).IsDiscrete && version.BatchSize != decimal.Truncate(version.BatchSize))
                throw Error("InvalidQuantity");
            if (version.State == CompositionState.Published && version.Lines.Count == 0) throw Error("Empty");
            if (requireActive && version.Id == selected && !output.Active) throw Error("Inactive");
            foreach (var line in version.Lines)
            {
                if (!line.Id.IsValid || new[] { line.MaterialId, line.RecipeVersionId, line.PoolId }.Count(x => x.HasValue) != 1 ||
                    line.Quantity < 0 || line.ConversionFactor <= 0 || !Enum.IsDefined(line.Basis) || !Enum.IsDefined(line.LossKind) ||
                    line.LossPercent < 0 || line.LossKind == CompositionLossKind.None && line.LossPercent != 0 ||
                    line.LossKind == CompositionLossKind.Yield && (line.LossPercent <= 0 || line.LossPercent > 100))
                    throw Error("InvalidLine");
                var target = Target(catalog, pools, version, line);
                var sourceUnit = units.SingleOrDefault(u => u.Id == line.InputUnitId);
                var targetUnit = units.SingleOrDefault(u => u.Id == target.Unit);
                if (sourceUnit is null || targetUnit is null || sourceUnit.Dimension != targetUnit.Dimension ||
                    sourceUnit.Id == targetUnit.Id && line.ConversionFactor != 1)
                    throw Error("UnitMismatch");
                if (requireActive && version.Id == selected && !target.Active) throw Error("Inactive");
            }
            Visit(version, new HashSet<EntityId>(), requireActive && version.Id == selected);
        }
        void Visit(CompositionVersion version, HashSet<EntityId> products, bool active)
        {
            if (++visited > 100000) throw Error("TooDeep");
            if (!products.Add(version.ProductId)) throw Error("Cycle");
            if (products.Count > 64) throw Error("TooDeep");
            if (active && (!catalog.Items.Single(i => i.Id == version.ProductId).Active ||
                version.Lines.Any(l => !Target(catalog, pools, version, l).Active))) throw Error("Inactive");
            foreach (var line in version.Lines.Where(l => l.RecipeVersionId.HasValue))
                Visit(catalog.Versions.SingleOrDefault(v => v.Id == line.RecipeVersionId) ?? throw Error("MissingTarget"), products, active);
            products.Remove(version.ProductId);
        }
    }

    private static (EntityId Id, UnitId Unit, string Name, bool Active) Target(CompositionCatalog catalog,
        IReadOnlyList<CompositionPool> pools, CompositionVersion version, CompositionLine line)
    {
        if (line.PoolId is { } poolId)
        {
            var pool = pools.SingleOrDefault(p => p.Id == poolId && p.ModelId == version.ModelVersionId);
            if (pool is null) throw Error("MissingTarget");
            return (pool.Id, pool.UnitId, pool.Name, pool.Active);
        }
        CompositionItem? item;
        if (line.MaterialId is { } id)
        {
            item = catalog.Items.SingleOrDefault(x => x.Id == id && x.Kind == CompositionItemKind.Material);
        }
        else
        {
            var child = catalog.Versions.SingleOrDefault(v => v.Id == line.RecipeVersionId && v.State == CompositionState.Published);
            if (child is null || child.ModelVersionId != version.ModelVersionId) throw Error("MissingTarget");
            item = catalog.Items.SingleOrDefault(x => x.Id == child.ProductId && x.Kind == CompositionItemKind.SemiFinished);
        }
        if (item is null) throw Error("MissingTarget");
        return (item.Id, item.UnitId, item.Code + " · " + item.Name, item.Active);
    }

    public static CompositionPreview Preview(CompositionCatalog catalog, EntityId versionId, decimal quantity,
        IReadOnlyList<UnitDefinition> units, IReadOnlyList<CompositionPool> pools)
    {
        Validate(catalog, units, pools);
        var root = catalog.Versions.SingleOrDefault(v => v.Id == versionId) ?? throw Error("InvalidVersion");
        if (root.Lines.Count == 0) throw Error("Empty");
        var output = catalog.Items.Single(i => i.Id == root.ProductId);
        if (quantity <= 0 || units.Single(u => u.Id == output.UnitId).IsDiscrete && quantity != decimal.Truncate(quantity))
            throw Error("InvalidQuantity");
        var lines = new List<CompositionQuantity>();
        try
        {
            Expand(root, quantity, output.Code, 0);
            var totals = lines.Where(l => !l.Expanded).GroupBy(l => new { l.TargetId, l.Activity, l.UnitId })
                .Select(g => new CompositionTotal(g.Key.TargetId, g.Key.Activity, g.Key.UnitId, g.First().Name,
                    g.Sum(l => l.RequiredQuantity))).ToArray();
            return new(quantity, decimal.Ceiling(quantity / root.BatchSize), FrozenList.Copy(lines), FrozenList.Copy(totals));
        }
        catch (OverflowException) { throw Error("Overflow"); }
        catch (DivideByZeroException) { throw Error("Overflow"); }
        void Expand(CompositionVersion recipe, decimal amount, string path, int depth)
        {
            if (depth > 64 || lines.Count > 10000) throw Error("TooDeep");
            foreach (var line in recipe.Lines)
            {
                if (lines.Count >= 10000) throw Error("TooDeep");
                var target = Target(catalog, pools, recipe, line);
                var count = line.Basis switch
                {
                    ConsumptionBasis.PerUnit => amount,
                    ConsumptionBasis.PerStartedBatch => decimal.Ceiling(amount / recipe.BatchSize),
                    _ => 1m
                };
                var raw = checked(count * line.Quantity);
                var adjusted = line.LossKind switch
                {
                    CompositionLossKind.ExtraInput => checked(raw * (1 + line.LossPercent / 100m)),
                    CompositionLossKind.Yield => raw / (line.LossPercent / 100m),
                    _ => raw
                };
                var converted = checked(adjusted * line.ConversionFactor);
                var required = units.Single(u => u.Id == target.Unit).IsDiscrete ? decimal.Ceiling(converted) : converted;
                if (line.RecipeVersionId is { } child)
                {
                    lines.Add(new(path, line.Id, target.Id, false, target.Unit, count, raw, adjusted, converted, required, target.Name, true, line.InputUnitId));
                    if (required > 0) Expand(catalog.Versions.Single(v => v.Id == child), required, path + " / " + target.Name, depth + 1);
                }
                else lines.Add(new(path, line.Id, target.Id, line.PoolId.HasValue, target.Unit,
                    count, raw, adjusted, converted, required, target.Name, false, line.InputUnitId));
            }
        }
    }
}
