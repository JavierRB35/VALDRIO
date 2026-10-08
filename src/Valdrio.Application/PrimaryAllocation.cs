using Valdrio.Calculation;
using Valdrio.Domain;

namespace Valdrio.Application;

// Read-only, unposted R0 projection. T11 owns durable run publication.
// Amounts in this ledger use the study's functional currency and PDEC-1 quantum.
public sealed record PrimaryAllocationFact(
    EntityId ResourceId, EntityId PartId, string Account, EntityId NodeId, EntityId PoolId,
    EntityId? KeyVersionId, EntityId? RuleId, string Selection,
    ResourceBehavior Behavior, ResourceCostLayer CostLayer, ResourceCapacityTreatment CapacityTreatment,
    decimal Weight, decimal Share, decimal RawAmount, decimal NumericAdjustment,
    decimal RoundingAdjustment, Money PostedAmount);

public sealed record PrimaryPartReconciliation(EntityId ResourceId, EntityId PartId,
    Money Input, Money Output, Money Difference, bool Covered);
public sealed record PrimaryResourceReconciliation(EntityId ResourceId,
    Money Effective, Money Direct, Money Pools, Money Excluded, Money Pending, Money Difference);
public sealed record PrimaryPoolTotal(EntityId NodeId, EntityId PoolId,
    ResourceBehavior Behavior, ResourceCostLayer CostLayer, ResourceCapacityTreatment CapacityTreatment,
    Money PostedAmount);
public sealed record PrimaryAllocationGap(EntityId PartId, DomainDiagnostic Diagnostic);

public sealed class PrimaryAllocationResult
{
    public const string Schema = "valdrio-primary-allocation-v1";
    public PrimaryAllocationResult(EntityId studyId, long revision, CurrencyCode currency, string policyVersion,
        IReadOnlyList<PrimaryAllocationFact> facts, IReadOnlyList<PrimaryPartReconciliation> parts,
        IReadOnlyList<PrimaryResourceReconciliation> resources, IReadOnlyList<PrimaryPoolTotal> pools,
        IReadOnlyList<PrimaryAllocationGap> gaps)
    {
        StudyId = studyId; Revision = revision; Currency = currency; PolicyVersion = policyVersion;
        Facts = FrozenList.Copy(facts); Parts = FrozenList.Copy(parts);
        Resources = FrozenList.Copy(resources); Pools = FrozenList.Copy(pools); Gaps = FrozenList.Copy(gaps);
    }
    public string SchemaVersion => Schema;
    public EntityId StudyId { get; }
    public long Revision { get; }
    public CurrencyCode Currency { get; }
    public string PolicyVersion { get; }
    public IReadOnlyList<PrimaryAllocationFact> Facts { get; }
    public IReadOnlyList<PrimaryPartReconciliation> Parts { get; }
    public IReadOnlyList<PrimaryResourceReconciliation> Resources { get; }
    public IReadOnlyList<PrimaryPoolTotal> Pools { get; }
    public IReadOnlyList<PrimaryAllocationGap> Gaps { get; }
    public bool IsComplete => Gaps.Count == 0 && Parts.All(p => p.Covered && p.Difference.Amount == 0m)
        && Resources.All(r => r.Difference.Amount == 0m && r.Pending.Amount == 0m);
    public IReadOnlyList<PrimaryAllocationFact> ByResource(EntityId id) =>
        FrozenList.Copy(Facts.Where(f => f.ResourceId == id));
    public IReadOnlyList<PrimaryAllocationFact> ByNode(EntityId id) =>
        FrozenList.Copy(Facts.Where(f => f.NodeId == id));
}

/// <summary>
/// Resolves published T08 keys against frozen T08 resource input. Never updates the
/// study or computes a total by independently rounding a SQL SUM.
/// Uncovered parts remain visible as gaps and cannot be presented as a completed run.
/// </summary>
public static class PrimaryAllocationService
{
    public static PrimaryAllocationResult Calculate(AllocationStudySnapshotInput snapshot) =>
        Calculate(snapshot.Allocations);

    public static PrimaryAllocationResult Calculate(AllocationWorkspaceSnapshot input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var resources = input.Resources;
        var model = resources.Model.Structure;
        var precision = resources.Precision;
        var currency = resources.Study.Currency.FunctionalCurrencyCode;
        if (!resources.Preparation.IsValid)
            throw AllocationDiagnostics.Error("PRIMARY_RESOURCE_INVALID", "Primary.ResourceInvalid");
        var facts = new List<PrimaryAllocationFact>();
        var parts = new List<PrimaryPartReconciliation>();
        var gaps = new List<PrimaryAllocationGap>();
        var nodePools = model.Pools.ToDictionary(p => p.NodeId);
        var accountByResource = resources.Origins.ToDictionary(o => o.Id, o => o.Code);
        var seenParts = new HashSet<EntityId>();
        foreach (var resource in resources.Resources.OrderBy(r => r.Id))
        foreach (var part in resource.Parts.OrderBy(p => p.Id))
        {
            if (!seenParts.Add(part.Id))
                throw AllocationDiagnostics.Error("PRIMARY_PART_DUPLICATE", "Primary.PartDuplicate", part.Id);
            if (part.Routing == ResourceRoutingKind.Pending)
            {
                gaps.Add(new(part.Id, AllocationDiagnostics.Error("KEY_PART_PENDING", "Allocation.PartPending", part.Id).Diagnostic));
                continue;
            }
            if (part.Routing != ResourceRoutingKind.AllocationPool) continue;
            var account = accountByResource[resource.Id];
            try
            {
                var allocation = input.Configuration.AbsoluteAssignments.SingleOrDefault(a => a.PartId == part.Id);
                AllocationKeyVersion? key = null;
                PrimaryRule? rule = null;
                string selection;
                IReadOnlyList<(EntityId Node, decimal Weight, decimal Share, RoundedContribution Line)> lines;
                if (allocation is not null)
                {
                    AllocationKeyRules.ValidateAbsolute(allocation, part, resources.EconomicRevision, model);
                    selection = "absolute";
                    // Absolute entries already use exactly the ledger quantum.
                    lines = allocation.Lines.Select(l => (l.DestinationNodeId, 0m, 0m,
                        new RoundedContribution(new StableRoundingId(l.DestinationNodeId.ToString()), l.Amount.Amount, 0m, 0m, l.Amount.Amount))).ToArray();
                }
                else
                {
                    var manual = input.Configuration.ExplicitAssignments.SingleOrDefault(a => a.PartId == part.Id);
                    key = AllocationKeyRules.ResolvePrimary(account, manual?.KeyVersionId,
                        input.Configuration.PrimaryRules, input.Keys, model);
                    selection = manual is null ? "rule" : "manual";
                    if (manual is null)
                        rule = WinningRule(account, input.Configuration.PrimaryRules, model);
                    var rounded = AllocationKeyRules.Preview(key, model, resources.Model.DriverUnits, part.Amount, precision);
                    var weightTotal = key.Lines.Aggregate(0m, (sum, line) => checked(sum +
                        (key.Mode == AllocationKeyMode.EqualShares ? 1m : line.Value)));
                    lines = key.Lines.Select(l =>
                    {
                        var weight = key.Mode == AllocationKeyMode.EqualShares ? 1m : l.Value;
                        // Shares are descriptive; the economic raw amount is the one calculated by Calculation.
                        var share = key.Mode == AllocationKeyMode.Percentage ? weight / 100m : weight / weightTotal;
                        return (l.DestinationNodeId, weight, share,
                            rounded.Lines.Single(p => p.DestinationId.Value == l.DestinationNodeId.ToString()));
                    }).ToArray();
                }
                var staged = new List<PrimaryAllocationFact>();
                var total = new Money(0m, currency, precision);
                foreach (var item in lines)
                {
                    if (!nodePools.TryGetValue(item.Node, out var pool) ||
                        !model.Nodes.Any(n => n.Id == item.Node && n.IsActive))
                        throw AllocationDiagnostics.Error("KEY_SCOPE_INVALID", "Allocation.ScopeInvalid", item.Node);
                    if (part.Classification.CapacityTreatment == ResourceCapacityTreatment.AssessAtPool &&
                        part.Classification.CapacityOwner?.NodeId != item.Node && item.Line.PostedAmount != 0m)
                        throw AllocationDiagnostics.Error("RES_CAPACITY_OWNER_INVALID", "Resource.CapacityOwnerInvalid", part.Id);
                    var posted = new Money(item.Line.PostedAmount, currency, precision);
                    total += posted;
                    staged.Add(new(resource.Id, part.Id, account, item.Node, pool.Id,
                        key?.Id, rule?.Id, selection, part.Classification.Behavior, part.Classification.Layer,
                        part.Classification.CapacityTreatment, item.Weight, item.Share, item.Line.RawAmount,
                        item.Line.NumericAdjustment, item.Line.RoundingAdjustment, posted));
                }
                var difference = part.Amount - total;
                if (difference.Amount != 0m)
                    throw AllocationDiagnostics.Error("PRIMARY_NOT_BALANCED", "Primary.NotBalanced", part.Id);
                facts.AddRange(staged); // atomic per-part publication into the preview
                parts.Add(new(resource.Id, part.Id, part.Amount, total, difference, true));
            }
            catch (DomainRuleException ex)
            {
                gaps.Add(new(part.Id, ex.Diagnostic));
                var zero = new Money(0m, currency, precision);
                parts.Add(new(resource.Id, part.Id, part.Amount, zero, part.Amount, false));
            }
        }
        var frontier = resources.Preparation.Coverage.OrderBy(c => c.ResourceId).Select(c =>
            new PrimaryResourceReconciliation(c.ResourceId, c.Effective, c.Direct, c.Pools,
                c.Excluded, c.Pending, c.Difference)).ToArray();
        var totals = facts.GroupBy(f => (f.NodeId, f.PoolId, f.Behavior, f.CostLayer, f.CapacityTreatment))
            .OrderBy(g => g.Key.NodeId).ThenBy(g => g.Key.PoolId).ThenBy(g => g.Key.Behavior)
            .ThenBy(g => g.Key.CostLayer).ThenBy(g => g.Key.CapacityTreatment)
            .Select(g => new PrimaryPoolTotal(g.Key.NodeId, g.Key.PoolId, g.Key.Behavior,
                g.Key.CostLayer, g.Key.CapacityTreatment,
                new Money(g.Aggregate(0m, (sum, fact) => checked(sum + fact.PostedAmount.Amount)), currency, precision)))
            .ToArray();
        return new PrimaryAllocationResult(resources.Study.Id, resources.Study.Revision, currency,
            precision.Version, facts, parts, frontier, totals, gaps);
    }

    private static PrimaryRule WinningRule(string account, IReadOnlyList<PrimaryRule> rules, ModelStructure model)
    {
        var matches = rules.Where(r => r.IsActive && r.CompanyId == model.CompanyId && r.ModelVersionId == model.Id &&
            (r.SelectorKind == AccountSelectorKind.Exact && StringComparer.Ordinal.Equals(account, r.SelectorValue) ||
             r.SelectorKind == AccountSelectorKind.Prefix && account.StartsWith(r.SelectorValue, StringComparison.Ordinal))).ToArray();
        if (matches.Any(r => r.SelectorKind == AccountSelectorKind.Exact))
            matches = matches.Where(r => r.SelectorKind == AccountSelectorKind.Exact).ToArray();
        else
        {
            var longest = matches.Max(r => r.SelectorValue.Length);
            matches = matches.Where(r => r.SelectorValue.Length == longest).ToArray();
        }
        var priority = matches.Max(r => r.Priority);
        return matches.Single(r => r.Priority == priority); // ResolvePrimary already checked ties
    }
}
