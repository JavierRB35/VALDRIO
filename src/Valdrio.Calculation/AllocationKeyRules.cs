using Valdrio.Domain;

namespace Valdrio.Calculation;

public static class AllocationKeyRules
{
    public static void Validate(AllocationKeyVersion key, ModelStructure model, IReadOnlyList<UnitDefinition> units,
        PrecisionPolicy precision, bool publication)
    {
        if (key.CompanyId != model.CompanyId || key.ModelVersionId != model.Id ||
            key.Lines.Any(l => !model.Nodes.Any(n => n.Id == l.DestinationNodeId && n.IsActive)))
            throw AllocationDiagnostics.Error("KEY_SCOPE_INVALID", "Allocation.ScopeInvalid", key.Id);
        if (publication && key.Mode == AllocationKeyMode.DriverUnits && !key.UnitId.HasValue)
            throw AllocationDiagnostics.Error("KEY_UNIT_INVALID", "Allocation.UnitInvalid", key.Id);
        if (key.UnitId is { } unit && !units.Any(u => u.Id == unit))
            throw AllocationDiagnostics.Error("KEY_UNIT_INVALID", "Allocation.UnitInvalid", key.Id);
        decimal total = 0;
        try
        {
            foreach (var line in key.Lines)
            {
                if (key.Mode == AllocationKeyMode.Percentage) precision.ValidatePercentage(line.Value, "value");
                else precision.ValidateQuantity(line.Value, "value");
                if (key.UnitId is { } unitId)
                {
                    var definition = units.Single(u => u.Id == unitId);
                    if (CanonicalDecimal.Scale(line.Value) > definition.DecimalScale ||
                        definition.IsDiscrete && decimal.Truncate(line.Value) != line.Value)
                        throw AllocationDiagnostics.Error("KEY_UNIT_INVALID", "Allocation.UnitInvalid", key.Id);
                }
                if (line.Value < 0 || key.Mode == AllocationKeyMode.EqualShares && line.Value != 1m)
                    throw AllocationDiagnostics.Error("KEY_WEIGHT_INVALID", "Allocation.WeightInvalid", line.DestinationNodeId);
                total = checked(total + line.Value);
            }
        }
        catch (OverflowException) { throw AllocationDiagnostics.Error("KEY_TOTAL_OVERFLOW", "Allocation.TotalOverflow", key.Id); }
        if (!publication) return;
        if (key.Lines.Count == 0 || key.Mode == AllocationKeyMode.Percentage && total != 100m)
            throw AllocationDiagnostics.Error("KEY_INCOMPLETE", "Allocation.Incomplete", key.Id);
        if (key.Mode == AllocationKeyMode.DriverUnits && total == 0m)
            throw AllocationDiagnostics.Error("KEY_ZERO_DRIVER_TOTAL", "AllocationKey.ZeroDriverTotal");
    }

    public static BalancedRoundResult Preview(AllocationKeyVersion key, ModelStructure model,
        IReadOnlyList<UnitDefinition> units, Money source, PrecisionPolicy precision)
    {
        Validate(key, model, units, precision, true);
        PrecisionPolicyCompatibility.EnsureSame(source.PrecisionPolicy, precision, "source", "key");
        // Percentages permit 18 input places. Driver quantities retain their own 12-place limit.
        var weightPrecision = key.Mode == AllocationKeyMode.Percentage ? precision with { QuantityScale = precision.PercentageScale } : precision;
        var contributions = new ProportionalAllocator(weightPrecision).FromWeights(source.Amount,
            key.Lines.Select(l => new WeightedDestination(new(l.DestinationNodeId.ToString()), l.Value)).ToArray());
        return new BalancedRounder(precision).Round(source.Amount, contributions, precision.InternalLedgerQuantum);
    }

    public static AllocationKeyVersion ResolvePrimary(string account, EntityId? explicitKey,
        IReadOnlyList<PrimaryRule> rules, IReadOnlyList<AllocationKeyVersion> keys, ModelStructure model)
    {
        EntityId selected;
        if (explicitKey is { } manual) selected = manual;
        else
        {
            var candidates = rules.Where(r => r.IsActive && r.CompanyId == model.CompanyId && r.ModelVersionId == model.Id &&
                (r.SelectorKind == AccountSelectorKind.Exact ? StringComparer.Ordinal.Equals(account, r.SelectorValue) :
                    r.SelectorKind == AccountSelectorKind.Prefix && account.StartsWith(r.SelectorValue, StringComparison.Ordinal))).ToArray();
            if (candidates.Length == 0) throw AllocationDiagnostics.Error("KEY_RULE_MISSING", "Allocation.RuleMissing");
            var bestKind = candidates.Any(r => r.SelectorKind == AccountSelectorKind.Exact) ? AccountSelectorKind.Exact : AccountSelectorKind.Prefix;
            candidates = candidates.Where(r => r.SelectorKind == bestKind).ToArray();
            if (bestKind == AccountSelectorKind.Prefix)
            {
                var length = candidates.Max(r => r.SelectorValue.Length);
                candidates = candidates.Where(r => r.SelectorValue.Length == length).ToArray();
            }
            var priority = candidates.Max(r => r.Priority);
            candidates = candidates.Where(r => r.Priority == priority).ToArray();
            if (candidates.Length != 1) throw AllocationDiagnostics.Error("KEY_RULE_AMBIGUOUS", "Allocation.RuleAmbiguous");
            selected = candidates[0].KeyVersionId;
        }
        return RequiredKey(selected, AllocationStage.Primary, keys, model);
    }

    public static AllocationKeyVersion ResolveSecondary(EntityId sourceNode, string componentGroup,
        IReadOnlyList<SecondaryRule> rules, IReadOnlyList<AllocationKeyVersion> keys, ModelStructure model)
    {
        if (!model.Nodes.Any(n => n.Id == sourceNode && n.IsActive && n.Role == NodeRole.Auxiliary))
            throw AllocationDiagnostics.Error("KEY_AUXILIARY_INVALID", "Allocation.AuxiliaryInvalid", sourceNode);
        var matched = rules.Where(r => r.IsActive && r.CompanyId == model.CompanyId && r.ModelVersionId == model.Id &&
            r.AuxiliaryNodeId == sourceNode && StringComparer.Ordinal.Equals(r.ComponentRoutingGroup, componentGroup)).ToArray();
        if (matched.Length == 0) throw AllocationDiagnostics.Error("KEY_RULE_MISSING", "Allocation.RuleMissing", sourceNode);
        if (matched.Length != 1) throw AllocationDiagnostics.Error("KEY_RULE_AMBIGUOUS", "Allocation.RuleAmbiguous", sourceNode);
        var key = RequiredKey(matched[0].KeyVersionId, AllocationStage.Secondary, keys, model);
        if (key.Mode == AllocationKeyMode.DriverUnits && model.Pools.Single(p => p.NodeId == sourceNode).DriverUnitId != key.UnitId)
            throw AllocationDiagnostics.Error("KEY_UNIT_INVALID", "Allocation.UnitInvalid", key.Id);
        // Secondary self-services are always a valid routing choice, including keys
        // persisted before this policy change with SelfAllocationPolicy.Forbidden.
        // Reachability / closed-component checks belong to the reciprocal solver.
        // A 100% self-loop is rejected later as GRAPH_CLOSED_COMPONENT.
        return key;
    }

    public static void ValidateAbsolute(AbsolutePartAllocation allocation, ResourcePartDraft part, long revision, ModelStructure model)
    {
        if (allocation.PartId != part.Id || allocation.SourceRevision != revision || allocation.SourceAmount != part.Amount ||
            allocation.Lines.Count == 0 || allocation.Lines.Any(l => !model.Nodes.Any(n => n.Id == l.DestinationNodeId && n.IsActive) ||
                l.Amount.Currency != part.Amount.Currency || l.Amount.PrecisionPolicy != part.Amount.PrecisionPolicy ||
                part.Amount.Amount > 0 && l.Amount.Amount < 0 || part.Amount.Amount < 0 && l.Amount.Amount > 0 ||
                part.Amount.Amount == 0 && l.Amount.Amount != 0))
            throw AllocationDiagnostics.Error("KEY_ABSOLUTE_INVALID", "Allocation.AbsoluteInvalid", part.Id);
        try
        {
            if (allocation.Lines.Sum(l => l.Amount.Amount) != part.Amount.Amount)
                throw AllocationDiagnostics.Error("KEY_ABSOLUTE_INCOMPLETE", "Allocation.AbsoluteIncomplete", part.Id);
        }
        catch (OverflowException) { throw AllocationDiagnostics.Error("KEY_TOTAL_OVERFLOW", "Allocation.TotalOverflow", part.Id); }
    }

    private static AllocationKeyVersion RequiredKey(EntityId id, AllocationStage stage,
        IReadOnlyList<AllocationKeyVersion> keys, ModelStructure model) =>
        keys.SingleOrDefault(k => k.Id == id && k.CompanyId == model.CompanyId && k.ModelVersionId == model.Id &&
            k.Stage == stage && k.Status == ModelStatus.Published)
        ?? throw AllocationDiagnostics.Error("KEY_PUBLICATION_REQUIRED", "Allocation.PublicationRequired", id);
}
