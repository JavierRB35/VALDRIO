using Valdrio.Domain;

namespace Valdrio.Application;

// Codes and numeric metadata are stable; translated names are UI-only.
public sealed record ActivityUnitPreset(
    string Code, string Dimension, string Symbol, int DecimalScale, bool IsDiscrete);

public static class DefaultActivityUnitCatalog
{
    public static IReadOnlyList<ActivityUnitPreset> All { get; } = Array.AsReadOnly(new ActivityUnitPreset[]
    {
        new("unit", "count", "u", 0, true),
        new("labor_hour", "time", "h", 12, false),
        new("machine_hour", "time", "h", 12, false),
        new("minute", "time", "min", 12, false),
        new("batch", "batch", "batch", 0, true),
        new("kilogram", "mass", "kg", 12, false),
        new("liter", "volume", "L", 12, false),
        new("meter", "length", "m", 12, false),
        new("square_meter", "area", "m²", 12, false),
        new("cubic_meter", "volume", "m³", 12, false),
        new("kilometer", "length", "km", 12, false),
        new("kilowatt_hour", "energy", "kWh", 12, false),
        new("order", "count", "order", 0, true),
        new("operation", "count", "operation", 0, true),
        new("service", "count", "service", 0, true),
        new("delivery", "count", "delivery", 0, true),
        new("transaction", "count", "transaction", 0, true),
        new("setup", "count", "setup", 0, true),
        new("pallet", "count", "pallet", 0, true),
        new("cycle", "count", "cycle", 0, true)
    });

    public static bool HasMissing(IEnumerable<UnitDefinition> existing)
    {
        ArgumentNullException.ThrowIfNull(existing);
        var codes = existing.Select(x => x.Code).ToHashSet(StringComparer.Ordinal);
        return All.Any(p => !codes.Contains(p.Code));
    }

    // Existing custom definitions with a coinciding code are not relabeled.
    public static bool IsPreset(UnitDefinition unit) =>
        All.Any(p => p.Code == unit.Code && p.Dimension == unit.Dimension
            && p.Symbol == unit.Symbol && p.DecimalScale == unit.DecimalScale
            && p.IsDiscrete == unit.IsDiscrete);

    public static IReadOnlyList<UnitDefinition> Missing(IEnumerable<UnitDefinition> existing)
    {
        ArgumentNullException.ThrowIfNull(existing);
        var codes = existing.Select(x => x.Code).ToHashSet(StringComparer.Ordinal);
        return All.Where(p => !codes.Contains(p.Code))
            .Select(p => new UnitDefinition(new UnitId(new EntityId(Guid.NewGuid())),
                p.Code, p.Dimension, p.Symbol, p.DecimalScale, p.IsDiscrete))
            .ToArray();
    }
}
