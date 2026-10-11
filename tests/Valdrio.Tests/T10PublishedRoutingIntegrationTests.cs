using Valdrio.Calculation;
using Valdrio.Domain;

namespace Valdrio.Tests;

[TestClass]
public sealed class T10PublishedRoutingIntegrationTests
{
    [TestMethod]
    public void PublishedT08SecondaryKeysBecomeCanonicalQAndB()
    {
        var company = NewId();
        var modelId = NewId();
        var center = NewId();
        var auxiliaryA = NewId();
        var auxiliaryB = NewId();
        var principalOne = NewId();
        var principalTwo = NewId();
        var unitId = new UnitId(NewId());
        var unit = new UnitDefinition(unitId, "hour", "time", "h", 12, false);
        var nodes = new[]
        {
            new CalculationNodeDefinition(auxiliaryA, modelId, center, "A", NodeRole.Auxiliary, [], true),
            new CalculationNodeDefinition(auxiliaryB, modelId, center, "B", NodeRole.Auxiliary, [], true),
            new CalculationNodeDefinition(principalOne, modelId, center, "P1", NodeRole.Principal, [], true),
            new CalculationNodeDefinition(principalTwo, modelId, center, "P2", NodeRole.Principal, [], true)
        };
        var pools = nodes.Select(node => new CostPoolDefinition(NewId(), modelId, node.Id, unitId,
            node.Code, CostLayer.Fixed, ModelPolicyReference.PendingCapacity)).ToArray();
        var model = new ModelStructure(modelId, company, modelId, 1, null, "Synthetic routes",
            PrecisionPolicy.Pdec1Proposed.Version, ModelPolicyReference.PendingMethod, nodes, pools);
        var a = Key(company, modelId, [
            new(auxiliaryB, 10m), new(principalOne, 60m), new(principalTwo, 30m)]);
        var b = Key(company, modelId, [
            new(auxiliaryA, 20m), new(principalOne, 30m), new(principalTwo, 50m)]);
        var rules = new[]
        {
            new SecondaryRule(NewId(), company, modelId, auxiliaryA, "all", a.Id, true),
            new SecondaryRule(NewId(), company, modelId, auxiliaryB, "all", b.Id, true)
        };

        var input = ReciprocalGraphBuilder.FromPublishedSecondaryKeys(model, [b, a],
            rules.Reverse().ToArray(), [unit], PrecisionPolicy.Pdec1Proposed);
        var prepared = ReciprocalFactorization.Prepare(input);
        var solution = prepared.Solve(
            new Dictionary<string, decimal> { [auxiliaryA.ToString()] = 1000m, [auxiliaryB.ToString()] = 500m },
            new Dictionary<string, decimal> { [principalOne.ToString()] = 2000m, [principalTwo.ToString()] = 3000m });
        // IDs are canonically ordered (GUID), not by node Code or key order.
        var finalById = solution.TerminalIds.Zip(solution.Final)
            .ToDictionary(pair => pair.First, pair => pair.Second);
        Assert.IsTrue(Math.Abs(finalById[principalOne.ToString()] - 20000m / 7m) < 0.00000001m);
        Assert.IsTrue(Math.Abs(finalById[principalTwo.ToString()] - 25500m / 7m) < 0.00000001m);

        // Legacy keys persisted as Forbidden must allow real secondary self-service.
        var selfKey = Key(company, modelId, [
            new(auxiliaryA, 10m), new(principalOne, 60m), new(principalTwo, 30m)]);
        var selfRules = rules.Select(rule => rule.AuxiliaryNodeId == auxiliaryA
            ? rule with { KeyVersionId = selfKey.Id } : rule).ToArray();
        var selfGraph = ReciprocalGraphBuilder.FromPublishedSecondaryKeys(model,
            [selfKey, b], selfRules, [unit], PrecisionPolicy.Pdec1Proposed);
        var ai = Array.IndexOf(selfGraph.AuxiliaryIds, auxiliaryA.ToString());
        Assert.IsTrue(selfGraph.AllowSelf);
        Assert.AreEqual(0.1m, selfGraph.Q[ai][ai]);
        var selfSolved = ReciprocalFactorization.Prepare(selfGraph).Solve(
            new Dictionary<string, decimal> { [auxiliaryA.ToString()] = 1000m, [auxiliaryB.ToString()] = 500m },
            new Dictionary<string, decimal> { [principalOne.ToString()] = 2000m, [principalTwo.ToString()] = 3000m });
        Assert.IsTrue(Math.Abs(selfSolved.Final.Sum() - 6500m) <= 0.00000001m);

        // A fully closed 100%-to-self route is not a forbidden allocation;
        // it is a mathematically unsolvable graph with no path to any principal.
        var onlySelf = Key(company, modelId, [new(auxiliaryA, 100m)]);
        var closedRules = rules.Select(rule => rule.AuxiliaryNodeId == auxiliaryA
            ? rule with { KeyVersionId = onlySelf.Id } : rule).ToArray();
        var closedInput = ReciprocalGraphBuilder.FromPublishedSecondaryKeys(model,
            [onlySelf, b], closedRules, [unit], PrecisionPolicy.Pdec1Proposed);
        Assert.AreEqual("GRAPH_CLOSED_COMPONENT",
            Assert.ThrowsExactly<DomainRuleException>(() =>
                ReciprocalFactorization.Prepare(closedInput)).Diagnostic.Code);
    }

    private static EntityId NewId() => new(Guid.NewGuid());

    private static AllocationKeyVersion Key(EntityId company, EntityId model,
        IReadOnlyList<AllocationKeyLine> lines)
    {
        var id = NewId();
        return new AllocationKeyVersion(id, company, model, id, 1, null, 0, "Synthetic",
            AllocationStage.Secondary, AllocationKeyMode.Percentage, null, SelfAllocationPolicy.Forbidden,
            ModelStatus.Published, UtcInstant.ParseIso("2026-10-09T11:00:00Z"), lines);
    }
}
