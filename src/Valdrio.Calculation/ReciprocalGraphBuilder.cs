using Valdrio.Domain;

namespace Valdrio.Calculation;

// Converts the published T08 secondary keys for one routing group into the
// canonical T10 matrix. No study persistence, UI, currency or cost formula.
public static class ReciprocalGraphBuilder
{
    public static ReciprocalInput FromPublishedSecondaryKeys(
        ModelStructure model,
        IReadOnlyList<AllocationKeyVersion> keys,
        IReadOnlyList<SecondaryRule> rules,
        IReadOnlyList<UnitDefinition> units,
        PrecisionPolicy precision,
        string componentRoutingGroup = "all")
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(units);
        ArgumentNullException.ThrowIfNull(precision);
        ArgumentException.ThrowIfNullOrWhiteSpace(componentRoutingGroup);

        var auxiliary = model.Nodes.Where(node => node.IsActive && node.Role == NodeRole.Auxiliary)
            .OrderBy(node => node.Id).ToArray();
        var principal = model.Nodes.Where(node => node.IsActive && node.Role == NodeRole.Principal)
            .OrderBy(node => node.Id).ToArray();
        if (auxiliary.Length == 0 || principal.Length == 0)
            throw new DomainRuleException(new DomainDiagnostic("GRAPH_SCHEMA_INVALID", "Reciprocal.SchemaInvalid", []));

        var auxiliaryIndex = auxiliary.Select((node, index) => (node.Id, index))
            .ToDictionary(pair => pair.Id, pair => pair.index);
        var terminalIndex = principal.Select((node, index) => (node.Id, index))
            .ToDictionary(pair => pair.Id, pair => pair.index);
        var q = Enumerable.Range(0, auxiliary.Length).Select(_ => new decimal[auxiliary.Length]).ToArray();
        var b = Enumerable.Range(0, auxiliary.Length).Select(_ => new decimal[principal.Length]).ToArray();
        // Business rule: every secondary key may allocate to its own auxiliary.
        // Preserve legacy serialized SelfAllocation values but do not treat them as permissions.

        for (var i = 0; i < auxiliary.Length; i++)
        {
            var source = auxiliary[i].Id;
            var key = AllocationKeyRules.ResolveSecondary(source, componentRoutingGroup, rules, keys, model);
            AllocationKeyRules.Validate(key, model, units, precision, true);


            // A key's relative weights are normalized once in ID order. Its
            // final route receives the decimal division remainder; nothing is
            // silently assigned to an unlisted destination.
            var lines = key.Lines.OrderBy(line => line.DestinationNodeId).ToArray();
            var total = key.Mode == AllocationKeyMode.Percentage ? 100m :
                lines.Aggregate(0m, (sum, line) => checked(sum + line.Value));
            if (total <= 0m)
                throw AllocationDiagnostics.Error("KEY_ZERO_DRIVER_TOTAL", "AllocationKey.ZeroDriverTotal", key.Id);
            decimal remaining = 1m;
            for (var j = 0; j < lines.Length; j++)
            {
                var line = lines[j];
                var fraction = j == lines.Length - 1 ? remaining : line.Value / total;
                remaining -= fraction;
                if (auxiliaryIndex.TryGetValue(line.DestinationNodeId, out var ai))
                    q[i][ai] = fraction;
                else if (terminalIndex.TryGetValue(line.DestinationNodeId, out var ti))
                    b[i][ti] = fraction;
                else
                    throw AllocationDiagnostics.Error("KEY_SCOPE_INVALID", "Allocation.ScopeInvalid", key.Id);
            }
        }
        return new ReciprocalInput(
            auxiliary.Select(n => n.Id.ToString()).ToArray(),
            principal.Select(n => n.Id.ToString()).ToArray(), q, b, true);
    }
}
