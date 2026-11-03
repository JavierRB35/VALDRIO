using Valdrio.Calculation;
using Valdrio.Domain;
namespace Valdrio.Application;

public enum AuxiliaryCapacityMode { Pending, ThroughFlow, OwnerOnceCapacity }
public sealed record AuxiliaryServiceQuantity(EntityId DestinationNodeId, decimal Quantity);
public sealed record AuxiliaryServiceSet(string ComponentRoutingGroup, IReadOnlyList<AuxiliaryServiceQuantity> Services)
{
    private IReadOnlyList<AuxiliaryServiceQuantity> services = FrozenList.Copy(Services);
    public IReadOnlyList<AuxiliaryServiceQuantity> Services { get => services; init => services = FrozenList.Copy(value); }
}
public sealed record AuxiliaryCapacityRow(EntityId NodeId, EntityId PoolId, AuxiliaryCapacityMode Mode,
    PhysicalObservation? Observation, IReadOnlyList<AuxiliaryServiceSet> ServiceSets)
{
    private IReadOnlyList<AuxiliaryServiceSet> serviceSets = FrozenList.Copy(ServiceSets);
    public IReadOnlyList<AuxiliaryServiceSet> ServiceSets { get => serviceSets; init => serviceSets = FrozenList.Copy(value); }
}
public sealed record AuxiliaryCapacityPlan(IReadOnlyList<AuxiliaryCapacityRow> Rows)
{
    private IReadOnlyList<AuxiliaryCapacityRow> rows = FrozenList.Copy(Rows);
    public IReadOnlyList<AuxiliaryCapacityRow> Rows { get => rows; init => rows = FrozenList.Copy(value); }
}

public static class AuxiliaryCapacityRules
{
    public static DomainRuleException Error(string code, string key) => new(new(code, "Aux." + key, []));
    public static void Validate(ResourceWorkspaceSnapshot resources, AuxiliaryCapacityPlan? plan)
    {
        if (plan is null) return;
        var model = resources.Model.Structure; var study = resources.Study;
        if (plan.Rows.Select(r => r.NodeId).Distinct().Count() != plan.Rows.Count)
            throw Error("AUX_INPUT_INVALID", "Invalid");
        foreach (var row in plan.Rows)
        {
            if (!Enum.IsDefined(row.Mode) || !model.Nodes.Any(n => n.Id == row.NodeId && n.IsActive && n.Role == NodeRole.Auxiliary) ||
                !model.Pools.Any(p => p.Id == row.PoolId && p.NodeId == row.NodeId) ||
                row.ServiceSets.Select(s => s.ComponentRoutingGroup).Distinct().Count() != row.ServiceSets.Count)
                throw Error("AUX_INPUT_INVALID", "Invalid");
            if (row.Observation is { } o && (o.CompanyId != study.CompanyId || o.StudyId != study.Id ||
                o.ModelVersionId != model.Id || o.NodeId != row.NodeId || o.PoolId != row.PoolId ||
                o.UnitId != model.Pools.Single(p => p.Id == row.PoolId).DriverUnitId ||
                o.PeriodStart != study.PeriodStart || o.PeriodEnd != study.PeriodEnd)) throw Error("AUX_INPUT_INVALID", "Invalid");
            foreach (var set in row.ServiceSets)
            {
                if (string.IsNullOrWhiteSpace(set.ComponentRoutingGroup) || set.ComponentRoutingGroup != set.ComponentRoutingGroup.Trim() ||
                    set.Services.Select(s => s.DestinationNodeId).Distinct().Count() != set.Services.Count)
                    throw Error("AUX_INPUT_INVALID", "Invalid");
                foreach (var service in set.Services)
                {
                    if (service.Quantity < 0 || !model.Nodes.Any(n => n.Id == service.DestinationNodeId && n.IsActive &&
                        n.Role is NodeRole.Auxiliary or NodeRole.Principal)) throw Error("AUX_INPUT_INVALID", "Invalid");
                    resources.Precision.ValidateQuantity(service.Quantity, "service");
                }
            }
        }
    }
    public static bool IsCurrent(AuxiliaryCapacityPlan? plan, IReadOnlyList<PhysicalObservation> current) =>
        plan is null || plan.Rows.Where(r => r.Mode == AuxiliaryCapacityMode.OwnerOnceCapacity).All(r =>
            r.Observation is not null && current.Any(o => o.Id == r.Observation.Id && o.Version == r.Observation.Version &&
                CanonicalSnapshotJson.Serialize(o) == CanonicalSnapshotJson.Serialize(r.Observation)));
}

public static class AuxiliaryCapacityService
{
    // Measured destinations replace the routing weights only for explicitly evaluated auxiliaries.
    // Unmeasured rows keep their published T11 key. A missing selection is never a silent fallback.
    public static ReciprocalInput Graph(AllocationWorkspaceSnapshot w, string group)
    {
        var graph = ReciprocalGraphBuilder.FromPublishedSecondaryKeys(w.Resources.Model.Structure, w.Keys,
            w.Configuration.SecondaryRules, w.Resources.Model.DriverUnits, w.Resources.Precision, group);
        var plan = w.Configuration.AuxiliaryCapacity;
        if (plan is null) return graph;
        foreach (var id in graph.AuxiliaryIds)
        {
            var row = plan.Rows.SingleOrDefault(r => r.NodeId.ToString() == id);
            if (row is null || row.Mode == AuxiliaryCapacityMode.Pending) throw AuxiliaryCapacityRules.Error("AUX_CAPACITY_PENDING", "PendingHelp");
            if (row.Mode == AuxiliaryCapacityMode.ThroughFlow) continue;
            var o = row.Observation;
            if (o?.State != PhysicalObservationState.Confirmed || o.NormalQuantity is null || o.RealQuantity is null)
                throw AuxiliaryCapacityRules.Error("AUX_CAPACITY_PENDING", "PendingHelp");
            var set = row.ServiceSets.SingleOrDefault(s => s.ComponentRoutingGroup == group);
            if (set is null || set.Services.Sum(s => s.Quantity) != o.RealQuantity)
                throw AuxiliaryCapacityRules.Error("AUX_SERVICE_COVERAGE_MISMATCH", "CoverageMismatch");
            var i = Array.IndexOf(graph.AuxiliaryIds, id);
            Array.Clear(graph.Q[i]); Array.Clear(graph.B[i]);
            if (o.RealQuantity == 0) continue;
            var lines = set.Services.Where(s => s.Quantity > 0).OrderBy(s => s.DestinationNodeId).ToArray();
            decimal remaining = 1;
            for (var j = 0; j < lines.Length; j++)
            {
                var share = j == lines.Length - 1 ? remaining : lines[j].Quantity / o.RealQuantity.Value;
                remaining -= share;
                var ai = Array.IndexOf(graph.AuxiliaryIds, lines[j].DestinationNodeId.ToString());
                if (ai >= 0) graph.Q[i][ai] = share;
                else graph.B[i][Array.IndexOf(graph.TerminalIds, lines[j].DestinationNodeId.ToString())] = share;
            }
        }
        // Remove inactive measured auxiliaries from the effective graph. Incoming service needs a real exit.
        var idle = plan.Rows.Where(r => r.Mode == AuxiliaryCapacityMode.OwnerOnceCapacity && r.Observation?.RealQuantity == 0)
            .Select(r => r.NodeId.ToString()).ToHashSet();
        foreach (var id in idle)
        {
            var j = Array.IndexOf(graph.AuxiliaryIds, id);
            if (j >= 0 && graph.Q.Any(q => q[j] != 0)) throw AuxiliaryCapacityRules.Error("AUX_ZERO_REAL_RECEIVED", "ZeroRealReceived");
        }
        var keep = Enumerable.Range(0, graph.AuxiliaryIds.Length).Where(i => !idle.Contains(graph.AuxiliaryIds[i])).ToArray();
        return new(keep.Select(i => graph.AuxiliaryIds[i]).ToArray(), graph.TerminalIds,
            keep.Select(i => keep.Select(j => graph.Q[i][j]).ToArray()).ToArray(), keep.Select(i => graph.B[i]).ToArray(), true);
    }
    public static SecondaryAllocationResult Calculate(AllocationWorkspaceSnapshot w, CancellationToken token = default)
    {
        var plan = w.Configuration.AuxiliaryCapacity!;
        AuxiliaryCapacityRules.Validate(w.Resources, plan);
        var primary = PrimaryAllocationService.Calculate(w);
        if (!primary.IsComplete) throw AuxiliaryCapacityRules.Error("SECONDARY_PRIMARY_INCOMPLETE", "PrimaryIncomplete");
        var precision = w.Resources.Precision;
        var facts = new List<SecondaryAllocationFact>(); var links = new List<SecondaryExplanationLink>();
        var solutions = new List<SecondaryGroupCirculation>();
        var factors = new Dictionary<string, ReciprocalFactorization>();
        var parts = w.Resources.Resources.SelectMany(r => r.Parts).ToDictionary(p => p.Id);
        string Group(EntityId id) => w.Configuration.SecondaryPartRoutingGroups?.SingleOrDefault(g => g.PartId == id)?.ComponentRoutingGroup ?? "all";
        var groups = primary.Facts.Select(f => Group(f.PartId)).Distinct().DefaultIfEmpty("all").Order(StringComparer.Ordinal);
        foreach (var group in groups)
        {
            var graph = Graph(w, group);
            var hash = CanonicalSnapshotJson.Serialize(graph);
            ReciprocalFactorization? factor = null;
            if (graph.AuxiliaryIds.Length > 0 && !factors.TryGetValue(hash, out factor)) factors[hash] = factor = ReciprocalFactorization.Prepare(graph, token);
            var aux = graph.AuxiliaryIds.ToDictionary(id => id, _ => 0m); var terminals = graph.TerminalIds.ToDictionary(id => id, _ => 0m);
            foreach (var source in primary.Facts.Where(f => Group(f.PartId) == group).OrderBy(f => f.PartId).ThenBy(f => f.NodeId).ThenBy(f => f.Behavior))
            {
                token.ThrowIfCancellationRequested();
                var row = plan.Rows.SingleOrDefault(r => r.NodeId == source.NodeId);
                var amount = source.PostedAmount.Amount; string? assessment = null;
                if (row?.Mode == AuxiliaryCapacityMode.OwnerOnceCapacity)
                {
                    if (source.Behavior is ResourceBehavior.Unclassified or ResourceBehavior.MixedPending or ResourceBehavior.Mixed)
                        throw new DomainRuleException(new("AUX_CLASSIFICATION_REQUIRED", "Aux.ClassificationSourceRequired",
                            [new("account", DiagnosticArgumentType.Text, source.Account),
                             new("node", DiagnosticArgumentType.Text, w.Resources.Model.Structure.Nodes.Single(n => n.Id == source.NodeId).Code),
                             new("rule", DiagnosticArgumentType.Text, source.KeyVersionId is { } key ? w.Keys.Single(k => k.Id == key).Name : source.Selection)]));
                    if (source.Behavior is ResourceBehavior.Fixed or ResourceBehavior.AssumedFixed && source.CapacityTreatment == ResourceCapacityTreatment.AssessAtPool)
                    {
                        var owner = parts[source.PartId].Classification?.CapacityOwner;
                        if (owner?.PoolId != row.PoolId || owner.NodeId != row.NodeId || owner.UnitId != row.Observation!.UnitId)
                            throw AuxiliaryCapacityRules.Error("AUX_CAPACITY_OWNER_INVALID", "OwnerInvalid");
                        var split = AuxiliaryCapacityCalculator.Assess(amount, row.Observation!.NormalQuantity!.Value,
                            row.Observation.RealQuantity!.Value, precision);
                        assessment = PublishedModelStructure.Hash(CanonicalSnapshotJson.Serialize(new { source.PartId, row.PoolId, row.Observation, amount }));
                        facts.Add(new(source.ResourceId, source.PartId, source.Account, source.NodeId, source.Behavior, source.CostLayer,
                            source.CapacityTreatment, split.Variance, 0, 0, new(split.Variance, primary.Currency, precision))
                            { CapacityOwnerPoolId = row.PoolId, AssessmentId = assessment, IsCapacityVariance = true });
                        links.Add(new(source.ResourceId,source.PartId,source.NodeId,source.NodeId,source.KeyVersionId,
                            source.PostedAmount.Amount,source.PostedAmount.Amount == 0 ? 0 : split.Variance/source.PostedAmount.Amount,split.Variance,group)
                            { AssessmentId = assessment, SourceBehavior = source.Behavior });
                        amount = split.Applied;
                    }
                    if (row.Observation!.RealQuantity == 0 && amount != 0)
                        throw AuxiliaryCapacityRules.Error("AUX_ZERO_REAL_COST", "ZeroRealCost");
                }
                if (amount == 0) continue;
                var adjusted = source with { PostedAmount = new(amount, primary.Currency, precision), RawAmount = amount, NumericAdjustment = 0, RoundingAdjustment = 0 };
                var subset = new PrimaryAllocationResult(primary.StudyId, primary.Revision, primary.Currency, primary.PolicyVersion, [adjusted], [], [], [], []);
                var result = factor is null
                    ? SecondaryAllocationService.CalculateDirectOnly(subset, graph.TerminalIds.Select(EntityId.Parse).ToArray(), precision, token)
                    : SecondaryAllocationService.Calculate(subset, graph, precision, token, factor);
                facts.AddRange(result.Facts.Select(f => f with { CapacityOwnerPoolId = assessment is null ? null : row!.PoolId, AssessmentId = assessment, CapacityNotEvaluated = row?.Mode == AuxiliaryCapacityMode.ThroughFlow }));
                links.AddRange(result.ExplanationLinks.Select(l => l with { ComponentRoutingGroup = group, AssessmentId = assessment, SourceBehavior = source.Behavior, CapacityNotEvaluated = row?.Mode == AuxiliaryCapacityMode.ThroughFlow }));
                if (aux.ContainsKey(source.NodeId.ToString())) aux[source.NodeId.ToString()] += amount;
                else terminals[source.NodeId.ToString()] += amount;
            }
            solutions.Add(new(group, factor is null
                ? new(ReciprocalFactorization.Policy, [], graph.TerminalIds, [], graph.TerminalIds.Select(id => terminals[id]).ToArray(), [], 0, 0, 0, 1, [])
                : factor.Solve(aux, terminals, token)));
        }
        var consolidated = facts.GroupBy(f => new { f.ResourceId, f.PartId, f.Account, f.TerminalNodeId, f.Behavior, f.CostLayer,
            f.CapacityTreatment, f.CapacityOwnerPoolId, f.AssessmentId, f.IsCapacityVariance, f.CapacityNotEvaluated }).Select(g => g.First() with {
                RawAmount = g.Sum(f => f.RawAmount), NumericAdjustment = g.Sum(f => f.NumericAdjustment), RoundingAdjustment = g.Sum(f => f.RoundingAdjustment),
                PostedAmount = new(g.Sum(f => f.PostedAmount.Amount), primary.Currency, precision) });
        var ordered = consolidated.OrderBy(f => f.ResourceId).ThenBy(f => f.PartId).ThenBy(f => f.TerminalNodeId).ThenBy(f => f.IsCapacityVariance).ThenBy(f => f.AssessmentId).ToArray();
        var totals = ordered.GroupBy(f => (f.TerminalNodeId, f.Behavior, f.CostLayer, f.CapacityTreatment)).Select(g =>
            new SecondaryTerminalTotal(g.Key.TerminalNodeId, g.Key.Behavior, g.Key.CostLayer, g.Key.CapacityTreatment, new(g.Sum(f => f.PostedAmount.Amount), primary.Currency, precision))).ToArray();
        var reconciliations = primary.Facts.GroupBy(f => (f.ResourceId, f.PartId)).Select(g => {
            var input = g.Sum(f => f.PostedAmount.Amount); var output = ordered.Where(f => f.PartId == g.Key.PartId).Sum(f => f.PostedAmount.Amount);
            return new SecondaryPartReconciliation(g.Key.ResourceId, g.Key.PartId, new(input, primary.Currency, precision), new(output, primary.Currency, precision), new(input-output, primary.Currency, precision));
        }).ToArray();
        return new(primary, ordered, links, totals, reconciliations, SecondaryAllocationService.AggregateCirculation(solutions),
            SecondaryAllocationService.BuildResourceBridges(primary, ordered, precision), solutions);
    }
}
