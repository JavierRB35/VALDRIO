using System.Text.Json;
using Valdrio.Domain;

namespace Valdrio.Application;

// The format has its own envelope; nested values reuse the strict canonical value codec.
public static class StudySnapshotJson
{
    public static IStudySnapshotInput Read(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("SchemaVersion", out var schema) || schema.ValueKind != JsonValueKind.String)
            throw new JsonException("Missing study snapshot schema.");
        return schema.GetString() switch
        {
            StudySnapshotInput.Schema => Deserialize(json),
            ResourceStudySnapshotInput.Schema => ResourceStudySnapshotJson.Deserialize(json),
            ImportedStudySnapshotInput.Schema or ImportedStudySnapshotInput.GroupedSchema or ImportedStudySnapshotInput.LedgerSchema or ImportedStudySnapshotInput.InternationalSchema => ImportedStudySnapshotJson.Deserialize(json),
            _ => throw new JsonException("Unsupported study snapshot schema.")
        };
    }
    public static string Serialize(IStudySnapshotInput input) => input switch
    {
        StudySnapshotInput v1 => Serialize(v1),
        ResourceStudySnapshotInput v6 => ResourceStudySnapshotJson.Serialize(v6),
        ImportedStudySnapshotInput v2 => ImportedStudySnapshotJson.Serialize(v2),
        _ => throw new JsonException("Unsupported study snapshot input.")
    };
    public static string EconomicJson(IStudySnapshotInput input) => input switch
    {
        StudySnapshotInput v1 => EconomicJson(v1),
        ResourceStudySnapshotInput v6 => ResourceStudySnapshotJson.EconomicJson(v6),
        ImportedStudySnapshotInput v2 => ImportedStudySnapshotJson.EconomicJson(v2),
        _ => throw new JsonException("Unsupported study snapshot input.")
    };
    public static string EconomicHash(IStudySnapshotInput input) => PublishedModelStructure.Hash(EconomicJson(input));
    public static string Serialize(StudySnapshotInput input)
    {
        using var doc = JsonDocument.Parse(CanonicalSnapshotJson.Serialize(input));
        return "{\"SchemaVersion\":\"" + StudySnapshotInput.Schema + "\",\"Value\":" + doc.RootElement.GetProperty("Value").GetRawText() + "}";
    }
    public static StudySnapshotInput Deserialize(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 2 ||
            root.EnumerateObject().Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() != 2 ||
            !root.TryGetProperty("SchemaVersion", out var schema) || schema.ValueKind != JsonValueKind.String || schema.GetString() != StudySnapshotInput.Schema ||
            !root.TryGetProperty("Value", out var value) || value.ValueKind != JsonValueKind.Object)
            throw new JsonException("Unsupported or incomplete study snapshot envelope.");
        return CanonicalSnapshotJson.Deserialize<StudySnapshotInput>("{\"SchemaVersion\":\"" + CanonicalSnapshotJson.SchemaVersion + "\",\"Value\":" + value.GetRawText() + "}");
    }

    private sealed record CurrencyEconomy(CurrencyCode Code, string NumericCode, int MinorUnitDigits, bool IsMonetary, string CatalogVersion);
    private sealed record NodeEconomy(EntityId Id, EntityId PhysicalCenterId, NodeRole Role, IReadOnlyList<string> FunctionTags, bool IsActive);
    private sealed record PoolEconomy(EntityId Id, EntityId NodeId, UnitId DriverUnitId, CostLayer CostLayer, ModelPolicyReference CapacityPolicy);
    private sealed record UnitEconomy(UnitId Id, string Dimension, int DecimalScale, bool IsDiscrete);
    private sealed record EconomicProjection(string SchemaVersion, string PreparationVersion, EntityId CompanyId,
        BusinessDate PeriodStart, BusinessDate PeriodEnd, StudyPurpose Purpose, StudyScenario ScenarioKind,
        CurrencyEconomy Currency, CurrencyValidity CurrencyValidity, PrecisionPolicy PrecisionPolicy, EntityId ModelVersionId, ModelPolicyReference MethodPolicy,
        IReadOnlyList<NodeEconomy> Nodes, IReadOnlyList<PoolEconomy> Pools, IReadOnlyList<UnitEconomy> Units,
        IReadOnlyList<ResourceSnapshotInput> Resources, IReadOnlyList<FxRateApplication> AppliedFx,
        IReadOnlyList<ActivitySnapshotInput> Activity, IReadOnlyList<ConsumptionSnapshotInput> Consumptions);

    public static string EconomicJson(StudySnapshotInput input)
    {
        var c = input.StudyContext; var currency = c.Currency.CurrencyMetadataSnapshot; var m = input.Model;
        var projection = new EconomicProjection("valdrio-study-economy-v1", input.InputPreparationVersion, c.CompanyId,
            c.PeriodStart, c.PeriodEnd, c.Purpose, c.ScenarioKind,
            new(currency.AlphaCode, currency.NumericCode, currency.MinorUnitDigits, currency.IsMonetary, currency.CatalogVersion),
            c.CurrencyValidity, input.PrecisionPolicy, m.Structure.Id, m.Structure.MethodPolicy,
            FrozenList.Copy(m.Structure.Nodes.OrderBy(n => n.Id).Select(n => new NodeEconomy(n.Id, n.PhysicalCenterId, n.Role, n.FunctionTags, n.IsActive))),
            FrozenList.Copy(m.Structure.Pools.OrderBy(p => p.Id).Select(p => new PoolEconomy(p.Id, p.NodeId, p.DriverUnitId, p.CostLayer, p.CapacityPolicy))),
            FrozenList.Copy(m.DriverUnits.OrderBy(u => u.Id).Select(u => new UnitEconomy(u.Id, u.Dimension, u.DecimalScale, u.IsDiscrete))),
            input.Resources, input.AppliedFx, input.Activity, input.Consumptions);
        return CanonicalSnapshotJson.Serialize(projection);
    }
    public static string EconomicHash(StudySnapshotInput input) => PublishedModelStructure.Hash(EconomicJson(input));
}
