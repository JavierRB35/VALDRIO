using Valdrio.Calculation;
using Valdrio.Domain;

namespace Valdrio.Application;

// T11 candidate: terminal ledger and explanatory source links, never intermediate
// reciprocal movements as new economic cost. One LU is reused for every part.
public sealed record SecondaryAllocationFact(
    EntityId ResourceId, EntityId PartId, string Account, EntityId TerminalNodeId,
    ResourceBehavior Behavior, ResourceCostLayer CostLayer, ResourceCapacityTreatment CapacityTreatment,
    decimal RawAmount, decimal NumericAdjustment, decimal RoundingAdjustment, Money PostedAmount);

public sealed record SecondaryExplanationLink(
    EntityId ResourceId, EntityId PartId, EntityId PrimaryNodeId, EntityId TerminalNodeId,
    EntityId? PrimaryKeyVersionId, decimal PrimaryPostedAmount,
    decimal EffectiveShare, decimal RawContribution);

public sealed record SecondaryTerminalTotal(
    EntityId TerminalNodeId, ResourceBehavior Behavior, ResourceCostLayer CostLayer,
    ResourceCapacityTreatment CapacityTreatment, Money PostedAmount);

public sealed record SecondaryPartReconciliation(
    EntityId ResourceId, EntityId PartId, Money PrimaryInput, Money FinalOutput, Money Difference);

public sealed record SecondaryAuxiliaryGross(string AuxiliaryId, decimal GrossAmount);

// Frozen publication of the economically adjudicated terminal facts at a
// particular study input revision. Reciprocal flows remain separate metrics.
public sealed record SecondaryCalculationPublication(
    string SchemaVersion, EntityId StudyId, long SourceRevision, string CalculationFingerprint,
    string NumericPolicyVersion, string PrimaryPolicyVersion,
    IReadOnlyList<SecondaryAllocationFact> Facts,
    IReadOnlyList<SecondaryTerminalTotal> Totals,
    IReadOnlyList<SecondaryPartReconciliation> Reconciliations,
    IReadOnlyList<SecondaryExplanationLink> ExplanationLinks,
    IReadOnlyList<SecondaryAuxiliaryGross> AuxiliaryGrossFlows,
    decimal MaxCirculationAmplification, decimal MinNormalizedPivot)
{
    public IReadOnlyList<SecondaryAllocationFact> ByTerminal(EntityId node) =>
        FrozenList.Copy(Facts.Where(f => f.TerminalNodeId == node));
    public IReadOnlyList<SecondaryAllocationFact> ByResource(EntityId resource) =>
        FrozenList.Copy(Facts.Where(f => f.ResourceId == resource));
}

public sealed class SecondaryAllocationResult
{
    public const string Schema = "valdrio-secondary-allocation-v1";
    public SecondaryAllocationResult(PrimaryAllocationResult primary,
        IReadOnlyList<SecondaryAllocationFact> facts,
        IReadOnlyList<SecondaryExplanationLink> links,
        IReadOnlyList<SecondaryTerminalTotal> totals,
        IReadOnlyList<SecondaryPartReconciliation> reconciliations,
        ReciprocalSolution circulation)
    {
        Primary = primary;
        Facts = FrozenList.Copy(facts);
        ExplanationLinks = FrozenList.Copy(links);
        Totals = FrozenList.Copy(totals);
        Reconciliations = FrozenList.Copy(reconciliations);
        Circulation = circulation;
    }
    public PrimaryAllocationResult Primary { get; }
    public IReadOnlyList<SecondaryAllocationFact> Facts { get; }
    public IReadOnlyList<SecondaryExplanationLink> ExplanationLinks { get; }
    public IReadOnlyList<SecondaryTerminalTotal> Totals { get; }
    public IReadOnlyList<SecondaryPartReconciliation> Reconciliations { get; }
    public ReciprocalSolution Circulation { get; }
    public bool IsComplete => Primary.IsComplete && Reconciliations.All(r => r.Difference.Amount == 0m);
    public IReadOnlyList<SecondaryAllocationFact> ByResource(EntityId id) =>
        FrozenList.Copy(Facts.Where(f => f.ResourceId == id));
    public IReadOnlyList<SecondaryAllocationFact> ByTerminal(EntityId id) =>
        FrozenList.Copy(Facts.Where(f => f.TerminalNodeId == id));
}

// No SQL/UI formula. Secondary keys are published T08 rules, and all costs
// originate in the immutable primary facts; circular gross flows are diagnostics.
public static class SecondaryAllocationService
{
    public static SecondaryAllocationResult Calculate(AllocationStudySnapshotInput snapshot,
        CancellationToken cancellation = default) =>
        Calculate(snapshot.Allocations, cancellation);

    public static SecondaryAllocationResult Calculate(AllocationWorkspaceSnapshot workspace,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var primary = PrimaryAllocationService.Calculate(workspace);
        if (!primary.IsComplete)
            throw Error("SECONDARY_PRIMARY_INCOMPLETE", "Secondary.PrimaryIncomplete");
        var active = workspace.Resources.Model.Structure.Nodes.Where(n => n.IsActive).ToArray();
        if (!active.Any(n => n.Role == NodeRole.Auxiliary))
            return CalculateDirectOnly(primary, active.Where(n => n.Role == NodeRole.Principal)
                .Select(n => n.Id).ToArray(), workspace.Resources.Precision, cancellation);
        // Current T08 model has no part -> custom routing-group mapping. Refuse to
        // silently treat a cost component as "all" when its route is different.
        var unmappedGroups = workspace.Configuration.SecondaryRules
            .Where(r => r.IsActive && !StringComparer.Ordinal.Equals(r.ComponentRoutingGroup, "all"))
            .Select(r => r.ComponentRoutingGroup).Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).ToArray();
        if (unmappedGroups.Length != 0)
            throw Error("SECONDARY_GROUP_MAPPING_REQUIRED", "Secondary.GroupMappingRequired",
                string.Join(", ", unmappedGroups));
        var model = workspace.Resources.Model.Structure;
        var missing = model.Nodes.Where(n => n.IsActive && n.Role == NodeRole.Auxiliary &&
            !workspace.Configuration.SecondaryRules.Any(r => r.IsActive && r.AuxiliaryNodeId == n.Id &&
                StringComparer.Ordinal.Equals(r.ComponentRoutingGroup, "all") &&
                workspace.Keys.Any(k => k.Id == r.KeyVersionId && k.Status == ModelStatus.Published &&
                    k.Stage == AllocationStage.Secondary)))
            .OrderBy(n => n.Code, StringComparer.Ordinal).Select(n => n.Code).ToArray();
        if (missing.Length != 0)
            throw Error("SECONDARY_MISSING_ASSIGNMENTS", "Secondary.MissingAssignments",
                string.Join(", ", missing));
        var graph = ReciprocalGraphBuilder.FromPublishedSecondaryKeys(
            workspace.Resources.Model.Structure, workspace.Keys,
            workspace.Configuration.SecondaryRules, workspace.Resources.Model.DriverUnits,
            workspace.Resources.Precision);
        return Calculate(primary, graph, workspace.Resources.Precision, cancellation);
    }

    // A study may have no auxiliary cost centers at all. In that case T11
    // remains a closable no-op transfer, not a spurious "missing solver" error.
    public static SecondaryAllocationResult CalculateDirectOnly(PrimaryAllocationResult primary,
        IReadOnlyList<EntityId> principalIds, PrecisionPolicy precision, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(primary);
        ArgumentNullException.ThrowIfNull(principalIds);
        ArgumentNullException.ThrowIfNull(precision);
        if (!primary.IsComplete)
            throw Error("SECONDARY_PRIMARY_INCOMPLETE", "Secondary.PrimaryIncomplete");
        if (!StringComparer.Ordinal.Equals(primary.PolicyVersion, precision.Version))
            throw Error("SECONDARY_PRECISION_POLICY_INVALID", "Secondary.PrecisionMismatch");
        var terminals = principalIds.Distinct().Order().ToArray();
        if (terminals.Length == 0 || terminals.Length != principalIds.Count)
            throw Error("SECONDARY_PRIMARY_NODE_INVALID", "Secondary.PrimaryNodeInvalid",
                terminals.Length == 0 ? "no-active-principals" : "duplicate-principal-id");
        var terminalSet = terminals.ToHashSet();
        if (primary.Facts.Any(f => !terminalSet.Contains(f.NodeId)))
            throw Error("SECONDARY_PRIMARY_NODE_INVALID", "Secondary.PrimaryNodeInvalid",
                primary.Facts.First(f => !terminalSet.Contains(f.NodeId)).NodeId.ToString());
        var facts = primary.Facts.OrderBy(f => f.ResourceId).ThenBy(f => f.PartId).ThenBy(f => f.NodeId)
            .Select(f => new SecondaryAllocationFact(f.ResourceId, f.PartId, f.Account, f.NodeId,
                f.Behavior, f.CostLayer, f.CapacityTreatment, f.PostedAmount.Amount, 0m, 0m,
                f.PostedAmount)).ToArray();
        var links = primary.Facts.OrderBy(f => f.ResourceId).ThenBy(f => f.PartId).ThenBy(f => f.NodeId)
            .Select(f => new SecondaryExplanationLink(f.ResourceId, f.PartId, f.NodeId, f.NodeId,
                f.KeyVersionId, f.PostedAmount.Amount, 1m, f.PostedAmount.Amount)).ToArray();
        var parts = facts.GroupBy(f => (f.ResourceId, f.PartId))
            .OrderBy(g => g.Key.ResourceId).ThenBy(g => g.Key.PartId).Select(g =>
        {
            cancellation.ThrowIfCancellationRequested();
            var amount = g.Aggregate(0m, (total, f) => checked(total + f.PostedAmount.Amount));
            var money = new Money(amount, primary.Currency, precision);
            return new SecondaryPartReconciliation(g.Key.ResourceId, g.Key.PartId, money, money,
                new Money(0m, primary.Currency, precision));
        }).ToArray();
        var totals = facts.GroupBy(f => (f.TerminalNodeId, f.Behavior, f.CostLayer, f.CapacityTreatment))
            .OrderBy(g => g.Key.TerminalNodeId).ThenBy(g => g.Key.Behavior)
            .ThenBy(g => g.Key.CostLayer).ThenBy(g => g.Key.CapacityTreatment)
            .Select(g => new SecondaryTerminalTotal(g.Key.TerminalNodeId, g.Key.Behavior,
                g.Key.CostLayer, g.Key.CapacityTreatment,
                new Money(g.Aggregate(0m, (sum, f) => checked(sum + f.PostedAmount.Amount)),
                    primary.Currency, precision))).ToArray();
        var finalAmounts = terminals.Select(node => facts.Where(f => f.TerminalNodeId == node)
            .Aggregate(0m, (sum, f) => checked(sum + f.PostedAmount.Amount))).ToArray();
        var circulation = new ReciprocalSolution(ReciprocalFactorization.Policy,
            Array.Empty<string>(), terminals.Select(t => t.ToString()).ToArray(),
            Array.Empty<decimal>(), finalAmounts, Array.Empty<decimal[]>(),
            0m, 0m, 0m, 1m, Array.Empty<DomainDiagnostic>());
        return new(primary, facts, links, totals, parts, circulation);
    }

    // Pure seam for corpus C09 and for tests independent from SQLite/WPF.
    public static SecondaryAllocationResult Calculate(PrimaryAllocationResult primary,
        ReciprocalInput graph, PrecisionPolicy precision, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(primary);
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(precision);
        if (!primary.IsComplete)
            throw Error("SECONDARY_PRIMARY_INCOMPLETE", "Secondary.PrimaryIncomplete");
        if (!StringComparer.Ordinal.Equals(primary.PolicyVersion, precision.Version))
            throw Error("SECONDARY_PRECISION_POLICY_INVALID", "Secondary.PrecisionMismatch");
        var factor = ReciprocalFactorization.Prepare(graph, cancellation);
        var auxIds = factor.AuxiliaryIds.ToArray();
        var terminalIds = factor.TerminalIds.ToArray();
        var auxIndex = auxIds.Select((id, index) => (id, index)).ToDictionary(t => t.id, t => t.index, StringComparer.Ordinal);
        var terminalIndex = terminalIds.Select((id, index) => (id, index)).ToDictionary(t => t.id, t => t.index, StringComparer.Ordinal);
        var factors = factor.EffectiveFactors;
        var currency = primary.Currency;
        var auxTotals = auxIds.ToDictionary(id => id, _ => 0m, StringComparer.Ordinal);
        var terminalTotals = terminalIds.ToDictionary(id => id, _ => 0m, StringComparer.Ordinal);
        var facts = new List<SecondaryAllocationFact>();
        var links = new List<SecondaryExplanationLink>();
        var reconciliations = new List<SecondaryPartReconciliation>();
        var rounder = new BalancedRounder(precision);
        try
        {
            foreach (var part in primary.Facts.GroupBy(f => (f.ResourceId, f.PartId))
                .OrderBy(g => g.Key.ResourceId).ThenBy(g => g.Key.PartId))
            {
                cancellation.ThrowIfCancellationRequested();
                var first = part.First();
                if (part.Any(f => f.Behavior != first.Behavior || f.CostLayer != first.CostLayer ||
                    f.CapacityTreatment != first.CapacityTreatment || f.Account != first.Account))
                    throw Error("SECONDARY_SOURCE_INCONSISTENT", "Secondary.SourceInconsistent", first.PartId.ToString());
                var values = new decimal[terminalIds.Length];
                decimal source = 0m;
                foreach (var fact in part.OrderBy(f => f.NodeId))
                {
                    source = checked(source + fact.PostedAmount.Amount);
                    var sourceId = fact.NodeId.ToString();
                    if (auxIndex.TryGetValue(sourceId, out var ai))
                        auxTotals[sourceId] = checked(auxTotals[sourceId] + fact.PostedAmount.Amount);
                    else if (terminalIndex.ContainsKey(sourceId))
                        terminalTotals[sourceId] = checked(terminalTotals[sourceId] + fact.PostedAmount.Amount);
                    else
                        throw Error("SECONDARY_PRIMARY_NODE_INVALID", "Secondary.PrimaryNodeInvalid", sourceId);
                    for (var j = 0; j < terminalIds.Length; j++)
                    {
                        var share = auxIndex.TryGetValue(sourceId, out ai) ? factors[ai][j] :
                            StringComparer.Ordinal.Equals(sourceId, terminalIds[j]) ? 1m : 0m;
                        var raw = checked(fact.PostedAmount.Amount * share);
                        values[j] = checked(values[j] + raw);
                        if (raw != 0m)
                            links.Add(new(fact.ResourceId, fact.PartId, fact.NodeId,
                                new EntityId(Guid.Parse(terminalIds[j])), fact.KeyVersionId,
                                fact.PostedAmount.Amount, share, raw));
                    }
                }
                var rounded = rounder.Round(source, terminalIds.Select((id, i) =>
                    new RoundingContribution(new StableRoundingId(id), values[i])).ToArray(),
                    precision.InternalLedgerQuantum);
                decimal posted = 0m;
                for (var j = 0; j < terminalIds.Length; j++)
                {
                    var line = rounded.Lines.Single(l => l.DestinationId.Value == terminalIds[j]);
                    posted = checked(posted + line.PostedAmount);
                    if (line.PostedAmount == 0m && values[j] == 0m) continue;
                    facts.Add(new(first.ResourceId, first.PartId, first.Account,
                        new EntityId(Guid.Parse(terminalIds[j])), first.Behavior, first.CostLayer,
                        first.CapacityTreatment, line.RawAmount, line.NumericAdjustment,
                        line.RoundingAdjustment, new Money(line.PostedAmount, currency, precision)));
                }
                reconciliations.Add(new(first.ResourceId, first.PartId, new Money(source, currency, precision),
                    new Money(posted, currency, precision), new Money(checked(source - posted), currency, precision)));
            }
            var gross = factor.Solve(auxTotals, terminalTotals, cancellation);
            var totals = facts.GroupBy(f => (f.TerminalNodeId, f.Behavior, f.CostLayer, f.CapacityTreatment))
                .OrderBy(g => g.Key.TerminalNodeId).ThenBy(g => g.Key.Behavior)
                .ThenBy(g => g.Key.CostLayer).ThenBy(g => g.Key.CapacityTreatment)
                .Select(g => new SecondaryTerminalTotal(g.Key.TerminalNodeId, g.Key.Behavior,
                    g.Key.CostLayer, g.Key.CapacityTreatment,
                    new Money(g.Aggregate(0m, (sum, f) => checked(sum + f.PostedAmount.Amount)), currency, precision)))
                .ToArray();
            return new(primary, facts, links, totals, reconciliations, gross);
        }
        catch (OverflowException) { throw Error("SECONDARY_NUMERIC_OVERFLOW", "Secondary.NumericOverflow"); }
    }

    // Persist the posted 8-decimal ledger and its explanatory source links,
    // without serializing a mutable LU factorization or double-counting circulation.
    public static SecondaryCalculationPublication Publish(
        SecondaryAllocationResult calculated, AllocationConfiguration configuration, long inputRevision)
    {
        ArgumentNullException.ThrowIfNull(calculated);
        ArgumentNullException.ThrowIfNull(configuration);
        if (!calculated.IsComplete || inputRevision < 0)
            throw Error("SECONDARY_CLOSE_INVALID", "Secondary.CloseInvalid");
        return new(SecondaryAllocationResult.Schema, calculated.Primary.StudyId,
            inputRevision, ResultFingerprint(calculated, configuration),
            calculated.Circulation.PolicyVersion, calculated.Primary.PolicyVersion,
            FrozenList.Copy(calculated.Facts), FrozenList.Copy(calculated.Totals),
            FrozenList.Copy(calculated.Reconciliations), FrozenList.Copy(calculated.ExplanationLinks),
            FrozenList.Copy(calculated.Circulation.AuxiliaryIds.Select((id, i) =>
                new SecondaryAuxiliaryGross(id, calculated.Circulation.Gross[i]))),
            calculated.Circulation.MaxCirculationAmplification,
            calculated.Circulation.MinNormalizedPivot);
    }

    // Persist only a reproducible seal, not an opaque mutable balance cache.
    // Include adopted secondary rules even when two rules happen to yield equal amounts.
    public static string ResultFingerprint(SecondaryAllocationResult result, AllocationConfiguration config) =>
        PublishedModelStructure.Hash(CanonicalSnapshotJson.Serialize(new
        {
            Schema = SecondaryAllocationResult.Schema,
            result.Primary.Currency, PrimaryPolicyVersion = result.Primary.PolicyVersion,
            Primary = result.Primary.Facts.OrderBy(f => f.ResourceId).ThenBy(f => f.PartId).ThenBy(f => f.NodeId).ToArray(),
            SecondaryRules = config.SecondaryRules.Where(r => r.IsActive).OrderBy(r => r.Id).ToArray(),
            Final = result.Facts,
            Result = result.Totals,
            ReciprocalPolicyVersion = result.Circulation.PolicyVersion,
            result.Circulation.AuxiliaryIds,
            result.Circulation.TerminalIds,
            result.Circulation.FactorsByAuxiliaryOrigin
        }));

    private static DomainRuleException Error(string code, string key, string? id = null) =>
        new(new DomainDiagnostic(code, key, id is null ? [] :
            [new DiagnosticArgument("source", DiagnosticArgumentType.Identifier, id)]));
}
