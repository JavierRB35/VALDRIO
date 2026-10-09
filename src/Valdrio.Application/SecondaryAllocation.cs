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
    decimal EffectiveShare, decimal RawContribution,
    string ComponentRoutingGroup = "all",
    IReadOnlyList<EntityId>? SecondaryKeyVersionIds = null);

public sealed record SecondaryTerminalTotal(
    EntityId TerminalNodeId, ResourceBehavior Behavior, ResourceCostLayer CostLayer,
    ResourceCapacityTreatment CapacityTreatment, Money PostedAmount);

public sealed record SecondaryPartReconciliation(
    EntityId ResourceId, EntityId PartId, Money PrimaryInput, Money FinalOutput, Money Difference);

public sealed record SecondaryEconomicBridge(
    EntityId ResourceId, Money Effective, Money Direct, Money Excluded, Money Pending,
    Money Terminal, Money Difference);

public sealed record SecondaryGroupCirculation(string ComponentRoutingGroup, ReciprocalSolution Solution);

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
    IReadOnlyList<SecondaryEconomicBridge> ResourceBridges,
    IReadOnlyList<SecondaryGroupCirculation> GroupCirculations,
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
        ReciprocalSolution circulation,
        IReadOnlyList<SecondaryEconomicBridge>? resourceBridges = null,
        IReadOnlyList<SecondaryGroupCirculation>? groupCirculations = null)
    {
        Primary = primary;
        Facts = FrozenList.Copy(facts);
        ExplanationLinks = FrozenList.Copy(links);
        Totals = FrozenList.Copy(totals);
        Reconciliations = FrozenList.Copy(reconciliations);
        Circulation = circulation;
        ResourceBridges = FrozenList.Copy(resourceBridges ?? []);
        GroupCirculations = FrozenList.Copy(groupCirculations ?? []);
    }
    public PrimaryAllocationResult Primary { get; }
    public IReadOnlyList<SecondaryAllocationFact> Facts { get; }
    public IReadOnlyList<SecondaryExplanationLink> ExplanationLinks { get; }
    public IReadOnlyList<SecondaryTerminalTotal> Totals { get; }
    public IReadOnlyList<SecondaryPartReconciliation> Reconciliations { get; }
    public ReciprocalSolution Circulation { get; }
    public IReadOnlyList<SecondaryEconomicBridge> ResourceBridges { get; }
    public IReadOnlyList<SecondaryGroupCirculation> GroupCirculations { get; }
    public bool IsComplete => Primary.IsComplete && Reconciliations.All(r => r.Difference.Amount == 0m)
        && ResourceBridges.All(r => r.Difference.Amount == 0m);
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
        var precision = workspace.Resources.Precision;
        var model = workspace.Resources.Model.Structure;
        var configured = workspace.Configuration.SecondaryPartRoutingGroups ?? [];
        var mapping = configured.ToDictionary(g => g.PartId,
            g => g.ComponentRoutingGroup);
        var parts = primary.Facts.Select(f => f.PartId).Distinct().ToHashSet();
        var unrecognized = configured.Where(g => !parts.Contains(g.PartId))
            .Select(g => g.PartId.ToString()).Order(StringComparer.Ordinal).ToArray();
        if (unrecognized.Length != 0)
            throw Error("SECONDARY_GROUP_SOURCE_INVALID", "Secondary.GroupSourceInvalid",
                string.Join(", ", unrecognized));
        var grouped = primary.Facts.GroupBy(f =>
            mapping.TryGetValue(f.PartId, out var group) ? group : "all")
            .OrderBy(g => g.Key, StringComparer.Ordinal).ToArray();
        if (grouped.Length == 0)
        {
            // Validate the model graph even with a zero RHS. Zero cost does
            // not legalize a closed auxiliary component.
            grouped = primary.Facts.GroupBy(_ => "all").ToArray();
        }
        var groupSolutions = new List<SecondaryGroupCirculation>();
        var posted = new List<SecondaryAllocationFact>();
        var links = new List<SecondaryExplanationLink>();
        var reconciliations = new List<SecondaryPartReconciliation>();
        if (grouped.Length == 0)
        {
            var graph = ReciprocalGraphBuilder.FromPublishedSecondaryKeys(
                model, workspace.Keys, workspace.Configuration.SecondaryRules,
                workspace.Resources.Model.DriverUnits, precision);
            var factor = ReciprocalFactorization.Prepare(graph, cancellation);
            var zero = factor.Solve(
                factor.AuxiliaryIds.ToDictionary(id => id, _ => 0m, StringComparer.Ordinal),
                factor.TerminalIds.ToDictionary(id => id, _ => 0m, StringComparer.Ordinal), cancellation);
            groupSolutions.Add(new("all", zero));
        }
        foreach (var group in grouped)
        {
            cancellation.ThrowIfCancellationRequested();
            var missing = model.Nodes.Where(n => n.IsActive && n.Role == NodeRole.Auxiliary &&
                !workspace.Configuration.SecondaryRules.Any(r => r.IsActive && r.AuxiliaryNodeId == n.Id &&
                    StringComparer.Ordinal.Equals(r.ComponentRoutingGroup, group.Key) &&
                    workspace.Keys.Any(k => k.Id == r.KeyVersionId && k.Status == ModelStatus.Published &&
                        k.Stage == AllocationStage.Secondary)))
                .OrderBy(n => n.Code, StringComparer.Ordinal).Select(n => n.Code).ToArray();
            if (missing.Length != 0)
                throw Error("SECONDARY_MISSING_ASSIGNMENTS", "Secondary.MissingAssignments",
                    group.Key + ": " + string.Join(", ", missing));
            var graph = ReciprocalGraphBuilder.FromPublishedSecondaryKeys(
                model, workspace.Keys, workspace.Configuration.SecondaryRules,
                workspace.Resources.Model.DriverUnits, precision, group.Key);
            var filteredFacts = group.OrderBy(f => f.ResourceId).ThenBy(f => f.PartId)
                .ThenBy(f => f.NodeId).ToArray();
            var filteredParts = primary.Parts.Where(p =>
                filteredFacts.Any(f => f.PartId == p.PartId)).ToArray();
            // The complete primary reconciliation is preserved by the final
            // aggregate; subgroup economic sources are solved independently.
            var subgroup = new PrimaryAllocationResult(primary.StudyId, primary.Revision,
                primary.Currency, primary.PolicyVersion, filteredFacts, filteredParts, [], [], []);
            var calculated = Calculate(subgroup, graph, precision, cancellation);
            posted.AddRange(calculated.Facts);
            var routeKeys = workspace.Configuration.SecondaryRules.Where(r => r.IsActive &&
                    StringComparer.Ordinal.Equals(r.ComponentRoutingGroup, group.Key))
                .Select(r => r.KeyVersionId).Distinct().Order().ToArray();
            links.AddRange(calculated.ExplanationLinks.Select(l => l with
            {
                ComponentRoutingGroup = group.Key,
                SecondaryKeyVersionIds = FrozenList.Copy(routeKeys)
            }));
            reconciliations.AddRange(calculated.Reconciliations);
            groupSolutions.Add(new(group.Key, calculated.Circulation));
        }
        var canonicalFacts = posted.OrderBy(f => f.ResourceId).ThenBy(f => f.PartId)
            .ThenBy(f => f.TerminalNodeId).ToArray();
        var totals = canonicalFacts.GroupBy(f => (f.TerminalNodeId, f.Behavior, f.CostLayer, f.CapacityTreatment))
            .OrderBy(g => g.Key.TerminalNodeId).ThenBy(g => g.Key.Behavior)
            .ThenBy(g => g.Key.CostLayer).ThenBy(g => g.Key.CapacityTreatment)
            .Select(g => new SecondaryTerminalTotal(g.Key.TerminalNodeId, g.Key.Behavior,
                g.Key.CostLayer, g.Key.CapacityTreatment,
                new Money(g.Aggregate(0m, (sum, f) => checked(sum + f.PostedAmount.Amount)),
                    primary.Currency, precision))).ToArray();
        var aggregate = AggregateCirculation(groupSolutions);
        return new(primary, canonicalFacts, links.OrderBy(l => l.ResourceId)
            .ThenBy(l => l.PartId).ThenBy(l => l.PrimaryNodeId)
            .ThenBy(l => l.TerminalNodeId).ToArray(),
            totals, reconciliations.OrderBy(r => r.ResourceId).ThenBy(r => r.PartId).ToArray(),
            aggregate, BuildResourceBridges(primary, canonicalFacts, precision),
            groupSolutions);

    }

    private static ReciprocalSolution AggregateCirculation(
        IReadOnlyList<SecondaryGroupCirculation> groups)
    {
        var first = groups[0].Solution;
        if (groups.Count == 1) return first;
        var sameNodes = groups.All(g =>
            g.Solution.AuxiliaryIds.SequenceEqual(first.AuxiliaryIds) &&
            g.Solution.TerminalIds.SequenceEqual(first.TerminalIds));
        if (!sameNodes) throw Error("SECONDARY_GRAPH_INCONSISTENT", "Secondary.GraphInconsistent");
        var gross = first.AuxiliaryIds.Select((_, i) => groups.Aggregate(0m,
            (sum, g) => checked(sum + g.Solution.Gross[i]))).ToArray();
        var final = first.TerminalIds.Select((_, i) => groups.Aggregate(0m,
            (sum, g) => checked(sum + g.Solution.Final[i]))).ToArray();
        // There is no single economic factor matrix for a mixture of group
        // policies. Keep each group's own factors in GroupCirculations.
        return new(ReciprocalFactorization.Policy, first.AuxiliaryIds, first.TerminalIds,
            gross, final, [], groups.Max(g => g.Solution.AbsoluteResidual),
            groups.Max(g => g.Solution.RelativeResidual),
            groups.Max(g => g.Solution.MaxCirculationAmplification),
            groups.Min(g => g.Solution.MinNormalizedPivot),
            groups.SelectMany(g => g.Solution.Warnings).ToArray());
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
        return new(primary, facts, links, totals, parts, circulation, BuildResourceBridges(primary, facts, precision));
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
            return new(primary, facts, links, totals, reconciliations, gross,
                BuildResourceBridges(primary, facts, precision),
                [new SecondaryGroupCirculation("all", gross)]);
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
            FrozenList.Copy(calculated.ResourceBridges),
            FrozenList.Copy(calculated.GroupCirculations),
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
            Bridges = result.ResourceBridges,
            PartGroups = config.SecondaryPartRoutingGroups,
            result.GroupCirculations,
            ReciprocalPolicyVersion = result.Circulation.PolicyVersion,
            result.Circulation.AuxiliaryIds,
            result.Circulation.TerminalIds,
            result.Circulation.FactorsByAuxiliaryOrigin
        }));

    // Direct-to-object and excluded amounts never pass through the auxiliary
    // solver. Publish an explicit resource-level bridge so they cannot be
    // mistaken for missing cost or counted twice as terminal contributions.
    private static IReadOnlyList<SecondaryEconomicBridge> BuildResourceBridges(
        PrimaryAllocationResult primary, IReadOnlyList<SecondaryAllocationFact> terminal,
        PrecisionPolicy precision)
    {
        var result = new List<SecondaryEconomicBridge>();
        foreach (var resource in primary.Resources.OrderBy(r => r.ResourceId))
        {
            var terminalAmount = terminal.Where(f => f.ResourceId == resource.ResourceId)
                .Aggregate(0m, (sum, f) => checked(sum + f.PostedAmount.Amount));
            var counted = checked(resource.Direct.Amount + resource.Excluded.Amount +
                resource.Pending.Amount + terminalAmount);
            var diff = checked(resource.Effective.Amount - counted);
            if (diff != resource.Difference.Amount || diff != 0m)
                throw Error("SECONDARY_RESOURCE_BRIDGE_INVALID", "Secondary.ResourceBridgeInvalid",
                    resource.ResourceId.ToString());
            result.Add(new(resource.ResourceId, resource.Effective, resource.Direct,
                resource.Excluded, resource.Pending,
                new Money(terminalAmount, primary.Currency, precision),
                new Money(diff, primary.Currency, precision)));
        }
        return FrozenList.Copy(result);
    }

    private static DomainRuleException Error(string code, string key, string? id = null) =>
        new(new DomainDiagnostic(code, key, id is null ? [] :
            [new DiagnosticArgument("source", DiagnosticArgumentType.Identifier, id)]));
}
