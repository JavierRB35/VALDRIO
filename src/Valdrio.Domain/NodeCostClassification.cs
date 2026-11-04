namespace Valdrio.Domain;

/// <summary>
/// Economic function and tariff purpose are independent of Principal/Auxiliary role.
/// The initial representation uses reserved function tags so existing published
/// ModelStructure snapshots and SQLite files are not rewritten or migrated.
/// </summary>
public enum NodeEconomicFunction
{
    Unspecified,
    Production,
    Commercial,
    Structure
}

public enum NodeTariffPurpose
{
    General,
    DirectLabor,
    Procurement
}

public static class NodeCostClassification
{
    public const string FunctionPrefix = "valdrio:cost-function:";
    public const string PurposePrefix = "valdrio:tariff-purpose:";

    public static NodeEconomicFunction Function(CalculationNodeDefinition node) =>
        Function(node.FunctionTags);

    public static NodeTariffPurpose Purpose(CalculationNodeDefinition node) =>
        Purpose(node.FunctionTags);

    public static NodeEconomicFunction Function(IEnumerable<string> tags)
    {
        var matches = tags.Where(tag => tag.StartsWith(FunctionPrefix, StringComparison.Ordinal)).ToArray();
        if (matches.Length != 1) return NodeEconomicFunction.Unspecified;
        return matches[0] switch
        {
            FunctionPrefix + "production" => NodeEconomicFunction.Production,
            FunctionPrefix + "commercial" => NodeEconomicFunction.Commercial,
            FunctionPrefix + "structure" => NodeEconomicFunction.Structure,
            _ => NodeEconomicFunction.Unspecified
        };
    }

    public static NodeTariffPurpose Purpose(IEnumerable<string> tags)
    {
        var matches = tags.Where(tag => tag.StartsWith(PurposePrefix, StringComparison.Ordinal)).ToArray();
        if (matches.Length != 1) return NodeTariffPurpose.General;
        return matches[0] switch
        {
            PurposePrefix + "labor" => NodeTariffPurpose.DirectLabor,
            PurposePrefix + "procurement" => NodeTariffPurpose.Procurement,
            _ => NodeTariffPurpose.General
        };
    }

    public static bool HasValidTags(IEnumerable<string> tags)
    {
        var all = tags.ToArray();
        var functions = all.Where(t => t.StartsWith(FunctionPrefix, StringComparison.Ordinal)).ToArray();
        var purposes = all.Where(t => t.StartsWith(PurposePrefix, StringComparison.Ordinal)).ToArray();
        return functions.Length <= 1 && purposes.Length <= 1 &&
            (functions.Length == 0 || Function(all) != NodeEconomicFunction.Unspecified) &&
            (purposes.Length == 0 ||
             purposes[0] == PurposePrefix + "general" ||
             purposes[0] == PurposePrefix + "labor" ||
             purposes[0] == PurposePrefix + "procurement");
    }

    public static IReadOnlyList<string> WithFunction(IEnumerable<string> tags, NodeEconomicFunction function)
    {
        if (!Enum.IsDefined(function)) throw new ArgumentOutOfRangeException(nameof(function));
        var updated = tags.Where(t => !t.StartsWith(FunctionPrefix, StringComparison.Ordinal)).ToList();
        if (function != NodeEconomicFunction.Unspecified)
            updated.Add(FunctionPrefix + (function switch
            {
                NodeEconomicFunction.Production => "production",
                NodeEconomicFunction.Commercial => "commercial",
                NodeEconomicFunction.Structure => "structure",
                _ => throw new ArgumentOutOfRangeException(nameof(function))
            }));
        return updated.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }

    public static IReadOnlyList<string> WithPurpose(IEnumerable<string> tags, NodeTariffPurpose purpose)
    {
        if (!Enum.IsDefined(purpose)) throw new ArgumentOutOfRangeException(nameof(purpose));
        var updated = tags.Where(t => !t.StartsWith(PurposePrefix, StringComparison.Ordinal)).ToList();
        updated.Add(PurposePrefix + (purpose switch
        {
            NodeTariffPurpose.General => "general",
            NodeTariffPurpose.DirectLabor => "labor",
            NodeTariffPurpose.Procurement => "procurement",
            _ => throw new ArgumentOutOfRangeException(nameof(purpose))
        }));
        return updated.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }

    public static IReadOnlyList<string> WithClassification(
        IEnumerable<string> tags, NodeEconomicFunction function, NodeTariffPurpose purpose)
    {
        if (function == NodeEconomicFunction.Unspecified ||
            !Enum.IsDefined(function) || !Enum.IsDefined(purpose))
            throw new ArgumentException("An explicit economic function and valid tariff purpose are required.");

        var preserved = tags.Where(tag =>
            !tag.StartsWith(FunctionPrefix, StringComparison.Ordinal) &&
            !tag.StartsWith(PurposePrefix, StringComparison.Ordinal)).ToList();
        preserved.Add(FunctionPrefix + (function switch
        {
            NodeEconomicFunction.Production => "production",
            NodeEconomicFunction.Commercial => "commercial",
            NodeEconomicFunction.Structure => "structure",
            _ => throw new ArgumentOutOfRangeException(nameof(function))
        }));
        preserved.Add(PurposePrefix + (purpose switch
        {
            NodeTariffPurpose.General => "general",
            NodeTariffPurpose.DirectLabor => "labor",
            NodeTariffPurpose.Procurement => "procurement",
            _ => throw new ArgumentOutOfRangeException(nameof(purpose))
        }));
        return preserved.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }

    public static bool IsDirectFactor(CalculationNodeDefinition node) =>
        Purpose(node) is NodeTariffPurpose.DirectLabor or NodeTariffPurpose.Procurement;
}
