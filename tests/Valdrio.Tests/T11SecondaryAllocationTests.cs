using Microsoft.VisualStudio.TestTools.UnitTesting;
using Valdrio.Application;
using Valdrio.Calculation;
using Valdrio.Domain;

namespace Valdrio.Tests;

[TestClass]
public sealed class T11SecondaryAllocationTests
{
    private static EntityId Id(string suffix) => new(Guid.Parse("00000000-0000-4000-8000-" + suffix));
    private static readonly EntityId A = Id("000000000001"), B = Id("000000000002");
    private static readonly EntityId P1 = Id("000000000003"), P2 = Id("000000000004");
    private static readonly CurrencyCode Eur = new("EUR");
    private static readonly PrecisionPolicy Precision = PrecisionPolicy.Pdec1Proposed;

    private static PrimaryAllocationFact Primary(int part, EntityId node, decimal amount,
        ResourceBehavior behavior = ResourceBehavior.Fixed) =>
        new(Id((part + 1000).ToString("D12")), Id((part + 2000).ToString("D12")),
            "6" + part, node, Id((part + 3000).ToString("D12")),
            null, null, "rule", behavior, ResourceCostLayer.Production,
            ResourceCapacityTreatment.None, 1m, 1m, amount, 0m, 0m,
            new Money(amount, Eur, Precision));

    private static PrimaryAllocationResult Source(params PrimaryAllocationFact[] facts) =>
        new(Id("000000000005"), 3, Eur, Precision.Version, facts, [], [], [], []);

    private static ReciprocalInput Graph() => new(
        [A.ToString(), B.ToString()], [P1.ToString(), P2.ToString()],
        [[0m, 0.1m], [0.2m, 0m]], [[0.6m, 0.3m], [0.3m, 0.5m]], false);

    [TestMethod]
    public void C09EffectiveTerminalLedgerBalancesExactlyByPart_AndByPrincipal()
    {
        var result = SecondaryAllocationService.Calculate(Source(
            Primary(1, A, 1000m), Primary(2, B, 500m, ResourceBehavior.Variable),
            Primary(3, P1, 2000m), Primary(4, P2, 3000m)), Graph(), Precision);
        Assert.IsTrue(result.IsComplete);
        Assert.AreEqual(6500m, result.Facts.Sum(f => f.PostedAmount.Amount));
        Assert.AreEqual(2857.14285715m,
            result.ByTerminal(P1).Sum(f => f.PostedAmount.Amount));
        Assert.AreEqual(3642.85714285m,
            result.ByTerminal(P2).Sum(f => f.PostedAmount.Amount));
        Assert.IsTrue(result.Reconciliations.All(r => r.Difference.Amount == 0m));
        Assert.IsTrue(result.Facts.Where(f => f.ResourceId == Id("000000001002"))
            .All(f => f.Behavior == ResourceBehavior.Variable));
        Assert.IsTrue(result.ExplanationLinks.Any(l => l.PrimaryNodeId == A && l.TerminalNodeId == P1));
        Assert.AreEqual(4, result.Reconciliations.Count);
        // Through-flow is diagnostic and must never be added to the terminal ledger.
        Assert.IsTrue(result.Circulation.Gross.Sum() > 1500m);
    }

    [TestMethod]
    public void PermutationOfPrimaryInputDoesNotChangeFinalLedger()
    {
        var facts = new[] { Primary(4, P2, 3000m), Primary(1, A, 1000m),
            Primary(3, P1, 2000m), Primary(2, B, 500m) };
        var lhs = SecondaryAllocationService.Calculate(Source(facts), Graph(), Precision);
        var rhs = SecondaryAllocationService.Calculate(Source(facts.Reverse().ToArray()), Graph(), Precision);
        CollectionAssert.AreEqual(lhs.Facts.ToArray(), rhs.Facts.ToArray());
        CollectionAssert.AreEqual(lhs.Totals.ToArray(), rhs.Totals.ToArray());
    }

    [TestMethod]
    public void OpenReciprocalGraphWithZeroAndNegativePrimaryAmountsIsValid()
    {
        var result = SecondaryAllocationService.Calculate(Source(
            Primary(1, A, -100m), Primary(2, P1, 100m)), Graph(), Precision);
        Assert.AreEqual(0m, result.Facts.Sum(f => f.PostedAmount.Amount));
        Assert.IsTrue(result.Reconciliations.All(r => r.Difference.Amount == 0m));
    }

    [TestMethod]
    public void ClosedGraphFailsEvenWhenSourceIsZero()
    {
        var graph = Graph() with { Q = [[0m, 1m], [1m, 0m]], B = [[0m, 0m], [0m, 0m]] };
        var ex = Assert.ThrowsExactly<DomainRuleException>(() =>
            SecondaryAllocationService.Calculate(Source(Primary(1, P1, 0m)), graph, Precision));
        Assert.AreEqual("GRAPH_CLOSED_COMPONENT", ex.Diagnostic.Code);
    }
    [TestMethod]
    public void ClosingSecondaryLocksPrimary_AndReopeningPrimaryDiscardsItsSeal()
    {
        var config = AllocationConfiguration.Empty;
        var closed = config.WithSecondaryClosed(true, "sha256-example");
        Assert.IsTrue(closed.PrimaryClosed);
        Assert.IsTrue(closed.SecondaryClosed);
        Assert.AreEqual("sha256-example", closed.SecondaryCalculationHash);
        var reopenedSecondary = closed.WithSecondaryClosed(false);
        Assert.IsTrue(reopenedSecondary.PrimaryClosed);
        Assert.IsFalse(reopenedSecondary.SecondaryClosed);
        Assert.IsNull(reopenedSecondary.SecondaryCalculationHash);
        var reopenedPrimary = closed.WithPrimaryClosed(false);
        Assert.IsFalse(reopenedPrimary.PrimaryClosed);
        Assert.IsFalse(reopenedPrimary.SecondaryClosed);
        Assert.IsNull(reopenedPrimary.SecondaryCalculationHash);
        Assert.ThrowsExactly<DomainRuleException>(() =>
            new AllocationConfiguration([], [], [], [], [], false, true, "invalid"));
    }

    [TestMethod]
    public void CalculationFingerprintIsIndependentOfInputFactOrder()
    {
        var facts = new[] { Primary(2, B, 500m), Primary(1, A, 1000m),
            Primary(3, P1, 2000m), Primary(4, P2, 3000m) };
        var first = SecondaryAllocationService.Calculate(Source(facts), Graph(), Precision);
        var second = SecondaryAllocationService.Calculate(Source(facts.Reverse().ToArray()), Graph(), Precision);
        Assert.AreEqual(
            SecondaryAllocationService.ResultFingerprint(first, AllocationConfiguration.Empty),
            SecondaryAllocationService.ResultFingerprint(second, AllocationConfiguration.Empty));
    }

    [TestMethod]
    public void PublicationKeepsCanonicalEightDecimalFactsAndIndependentGrossFlows()
    {
        var calculation = SecondaryAllocationService.Calculate(Source(
            Primary(1, A, 1000m), Primary(2, B, 500m, ResourceBehavior.Variable),
            Primary(3, P1, 2000m), Primary(4, P2, 3000m)), Graph(), Precision);
        var publication = SecondaryAllocationService.Publish(calculation, AllocationConfiguration.Empty, 17);
        Assert.AreEqual(SecondaryAllocationResult.Schema, publication.SchemaVersion);
        Assert.AreEqual(17L, publication.SourceRevision);
        Assert.AreEqual(calculation.Primary.StudyId, publication.StudyId);
        Assert.AreEqual(6500m, publication.Facts.Sum(f => f.PostedAmount.Amount));
        Assert.AreEqual(2857.14285715m, publication.ByTerminal(P1).Sum(f => f.PostedAmount.Amount));
        Assert.AreEqual(3642.85714285m, publication.ByTerminal(P2).Sum(f => f.PostedAmount.Amount));
        Assert.AreEqual(4, publication.Reconciliations.Count);
        Assert.AreEqual(2, publication.AuxiliaryGrossFlows.Count);
        Assert.IsTrue(publication.AuxiliaryGrossFlows.Sum(f => f.GrossAmount) > 1500m);
        Assert.AreEqual(SecondaryAllocationService.ResultFingerprint(calculation, AllocationConfiguration.Empty),
            publication.CalculationFingerprint);
        var canonical = CanonicalSnapshotJson.Serialize(publication);
        Assert.AreEqual(canonical, CanonicalSnapshotJson.Serialize(
            CanonicalSnapshotJson.Deserialize<SecondaryCalculationPublication>(canonical)));
    }

    [TestMethod]
    public void EffectiveFactorsAndDirectMultipleRightHandSidesAgreePerSource()
    {
        var primary = Source(Primary(1, A, 1000m), Primary(2, B, 500m),
            Primary(3, P1, 2000m), Primary(4, P2, 3000m));
        var graph = Graph();
        var factor = ReciprocalFactorization.Prepare(graph);
        var result = SecondaryAllocationService.Calculate(primary, graph, Precision);
        foreach (var source in primary.Facts)
        {
            var auxiliaries = factor.AuxiliaryIds.ToDictionary(id => id,
                id => StringComparer.Ordinal.Equals(id, source.NodeId.ToString()) ? source.PostedAmount.Amount : 0m);
            var terminals = factor.TerminalIds.ToDictionary(id => id,
                id => StringComparer.Ordinal.Equals(id, source.NodeId.ToString()) ? source.PostedAmount.Amount : 0m);
            var direct = factor.Solve(auxiliaries, terminals);
            for (var i = 0; i < factor.TerminalIds.Count; i++)
            {
                var target = new EntityId(Guid.Parse(factor.TerminalIds[i]));
                var fromEffectiveFactors = result.Facts
                    .Where(f => f.PartId == source.PartId && f.TerminalNodeId == target)
                    .Sum(f => f.RawAmount);
                Assert.IsTrue(Math.Abs(direct.Final[i] - fromEffectiveFactors) <= 0.00000001m,
                    "Factor/RHS disagreement for " + source.PartId + " => " + target);
            }
        }
    }

    [TestMethod]
    public void WithoutAuxiliariesTheSecondaryPhaseIsAnIdentityWithAnEmptyCirculation()
    {
        var primary = Source(Primary(3, P1, 2000m), Primary(4, P2, -500m));
        var result = SecondaryAllocationService.CalculateDirectOnly(primary, [P1, P2], Precision);
        Assert.IsTrue(result.IsComplete);
        Assert.AreEqual(1500m, result.Facts.Sum(f => f.PostedAmount.Amount));
        Assert.AreEqual(2000m, result.ByTerminal(P1).Sum(f => f.PostedAmount.Amount));
        Assert.AreEqual(-500m, result.ByTerminal(P2).Sum(f => f.PostedAmount.Amount));
        Assert.AreEqual(0, result.Circulation.AuxiliaryIds.Count);
        Assert.AreEqual(0m, result.Circulation.MaxCirculationAmplification);
        Assert.IsTrue(result.Reconciliations.All(r => r.Difference.Amount == 0m));
        var published = SecondaryAllocationService.Publish(result, AllocationConfiguration.Empty, 4);
        Assert.AreEqual(0, published.AuxiliaryGrossFlows.Count);
        Assert.ThrowsExactly<DomainRuleException>(() =>
            SecondaryAllocationService.CalculateDirectOnly(Source(Primary(1, A, 100m)),
                [P1, P2], Precision));
    }

    [TestMethod]
    public void ResourceBridgeShowsDirectExcludedAndTerminalWithoutDoubleCounting()
    {
        var origin = Primary(9, P1, 100m);
        Money M(decimal value) => new(value, Eur, Precision);
        var resource = new PrimaryResourceReconciliation(origin.ResourceId,
            M(160m), M(20m), M(100m), M(40m), M(0m), M(0m));
        var primary = new PrimaryAllocationResult(Id("000000000005"), 3, Eur, Precision.Version,
            [origin], [], [resource], [], []);
        var result = SecondaryAllocationService.CalculateDirectOnly(primary, [P1], Precision);
        Assert.IsTrue(result.IsComplete);
        var bridge = result.ResourceBridges.Single();
        Assert.AreEqual(160m, bridge.Effective.Amount);
        Assert.AreEqual(20m, bridge.Direct.Amount);
        Assert.AreEqual(40m, bridge.Excluded.Amount);
        Assert.AreEqual(100m, bridge.Terminal.Amount);
        Assert.AreEqual(0m, bridge.Difference.Amount);
        var receipt = SecondaryAllocationService.Publish(result, AllocationConfiguration.Empty, 3);
        Assert.AreEqual(bridge, receipt.ResourceBridges.Single());
        Assert.AreEqual(100m, receipt.Facts.Sum(x => x.PostedAmount.Amount));

        // A synthetic inconsistent primary summary must not be laundered into
        // an apparently complete secondary publication.
        var incorrect = new PrimaryAllocationResult(Id("000000000005"), 3, Eur, Precision.Version,
            [origin], [], [resource with { Effective = M(170m) }], [], []);
        var failure = Assert.ThrowsExactly<DomainRuleException>(() =>
            SecondaryAllocationService.CalculateDirectOnly(incorrect, [P1], Precision));
        Assert.AreEqual("SECONDARY_RESOURCE_BRIDGE_INVALID", failure.Diagnostic.Code);
    }

    [TestMethod]
    public void HistoricalConfigurationsOmitNewGroupField_AndExplicitGroupsRoundTrip()
    {
        var legacy = CanonicalSnapshotJson.Serialize(AllocationConfiguration.Empty);
        Assert.IsFalse(legacy.Contains("SecondaryPartRoutingGroups", StringComparison.Ordinal));
        Assert.AreEqual(legacy, CanonicalSnapshotJson.Serialize(
            CanonicalSnapshotJson.Deserialize<AllocationConfiguration>(legacy)));

        var part = Id("000000001234");
        var configured = AllocationConfiguration.Empty.WithSecondaryPartRoutingGroups(
            [new SecondaryPartRoutingGroup(part, "operations")]);
        var json = CanonicalSnapshotJson.Serialize(configured);
        Assert.IsTrue(json.Contains("SecondaryPartRoutingGroups", StringComparison.Ordinal));
        var replay = CanonicalSnapshotJson.Deserialize<AllocationConfiguration>(json);
        Assert.AreEqual(json, CanonicalSnapshotJson.Serialize(replay));
        Assert.AreEqual("operations",
            replay.SecondaryPartRoutingGroups!.Single().ComponentRoutingGroup);
        Assert.AreEqual("operations",
            configured.WithPrimaryClosed(true).SecondaryPartRoutingGroups!.Single().ComponentRoutingGroup);
        Assert.AreEqual("operations",
            configured.WithSecondaryClosed(true, "example").SecondaryPartRoutingGroups!.Single().ComponentRoutingGroup);
    }

    [TestMethod]
    public void Extreme100AuxiliaryRingPreserves1000SourcesAndDoesNotPostGrossCosts()
    {
        const int count = 100;
        var auxiliaries = Enumerable.Range(0, count)
            .Select(i => Id((100000 + i).ToString("D12"))).ToArray();
        var sourceIds = auxiliaries.Select(id => id.ToString()).ToArray();
        var terminals = new[] { P1.ToString(), P2.ToString() };
        var q = Enumerable.Range(0, count).Select(_ => new decimal[count]).ToArray();
        var b = Enumerable.Range(0, count).Select(_ => new[] { 0.9m, 0m }).ToArray();
        for (var i = 0; i < count; i++) q[i][(i + 1) % count] = 0.1m;
        var graph = new ReciprocalInput(sourceIds, terminals, q, b, false);
        var facts = Enumerable.Range(0, 1000).Select(i =>
        {
            var baseFact = Primary(i + 100, auxiliaries[i % count], (i % 2 == 0 ? 1m : -0.5m));
            return baseFact;
        }).Append(Primary(2000, P2, 5m)).ToArray();
        var origin = Source(facts);
        var result = SecondaryAllocationService.Calculate(origin, graph, Precision);
        Assert.IsTrue(result.IsComplete);
        Assert.AreEqual(255m, result.Facts.Sum(x => x.PostedAmount.Amount));
        Assert.AreEqual(250m, result.ByTerminal(P1).Sum(x => x.PostedAmount.Amount));
        Assert.AreEqual(5m, result.ByTerminal(P2).Sum(x => x.PostedAmount.Amount));
        Assert.AreEqual(1001, result.Reconciliations.Count);
        Assert.AreEqual(0m, result.Reconciliations.Sum(x => x.Difference.Amount));
        Assert.IsTrue(result.Circulation.Gross.Sum() > 200m);
        Assert.AreEqual(100, result.Circulation.AuxiliaryIds.Count);
    }

    [TestMethod]
    public void MicroscopicPositiveNegativeAndZeroSourceAmountsNeverLoseEightDigitQuantum()
    {
        var graph = new ReciprocalInput([A.ToString()], [P1.ToString(), P2.ToString()],
            [[0m]], [[0.5m, 0.5m]], false);
        var origin = Source(
            Primary(11, A, 0.00000001m),
            Primary(12, A, -0.00000001m),
            Primary(13, A, 0m));
        var result = SecondaryAllocationService.Calculate(origin, graph, Precision);
        Assert.IsTrue(result.IsComplete);
        Assert.AreEqual(0m, result.Facts.Sum(f => f.PostedAmount.Amount));
        foreach (var part in origin.Facts)
            Assert.AreEqual(part.PostedAmount.Amount, result.Facts
                .Where(f => f.PartId == part.PartId).Sum(f => f.PostedAmount.Amount));
        Assert.AreEqual(0m, result.Reconciliations.Sum(r => r.Difference.Amount));
    }

    [TestMethod]
    public void ZeroSourceDoesNotMakeMathematicallyClosedCycleAcceptable()
    {
        var graph = new ReciprocalInput([A.ToString(), B.ToString()],
            [P1.ToString()], [[0m, 1m], [1m, 0m]], [[0m], [0m]], false);
        var source = Source(Primary(1, A, 0m), Primary(2, P1, 0m));
        var failure = Assert.ThrowsExactly<DomainRuleException>(() =>
            SecondaryAllocationService.Calculate(source, graph, Precision));
        Assert.AreEqual("GRAPH_CLOSED_COMPONENT", failure.Diagnostic.Code);
    }

    [TestMethod]
    public void HighlyAmplifiedOpenCycleMustBeRejectedInsteadOfPostingAnUnstableLedger()
    {
        var graph = new ReciprocalInput([A.ToString()], [P1.ToString()],
            [[0.9999999999995m]], [[0.0000000000005m]], true);
        var source = Source(Primary(1, A, 1m));
        var failure = Assert.ThrowsExactly<DomainRuleException>(() =>
            SecondaryAllocationService.Calculate(source, graph, Precision));
        Assert.AreEqual("SOLVER_SENSITIVE", failure.Diagnostic.Code);
    }

    [TestMethod]
    public void ExplainListsOnlyKeysThatCanActuallyReachTheSelectedPrincipal()
    {
        var c = Id("000000000007");
        var company = Id("000000000008");
        var model = Id("000000000009");
        var aKey = Id("000000000010");
        var bKey = Id("000000000011");
        var cKey = Id("000000000012");
        var rules = new[] {
            new SecondaryRule(Id("000000000013"), company, model, A, "all", aKey, true),
            new SecondaryRule(Id("000000000014"), company, model, B, "all", bKey, true),
            new SecondaryRule(Id("000000000015"), company, model, c, "all", cKey, true)
        };
        var graph = new ReciprocalInput([A.ToString(), B.ToString(), c.ToString()],
            [P1.ToString(), P2.ToString()],
            [[0m, 1m, 0m], [0m, 0m, 0m], [0m, 0m, 0m]],
            [[0m, 0m], [1m, 0m], [0m, 1m]], false);
        var toP1 = SecondaryAllocationService.ContributingSecondaryKeyVersions(
            graph, rules, A, P1);
        CollectionAssert.AreEquivalent(new[] { aKey, bKey }, toP1.ToArray());
        Assert.AreEqual(0, SecondaryAllocationService.ContributingSecondaryKeyVersions(
            graph, rules, A, P2).Count);
        Assert.AreEqual(0, SecondaryAllocationService.ContributingSecondaryKeyVersions(
            graph, rules, P2, P2).Count);
        CollectionAssert.AreEquivalent(new[] { cKey },
            SecondaryAllocationService.ContributingSecondaryKeyVersions(graph, rules, c, P2).ToArray());
    }

    [TestMethod]
    public void NinetySevenTerminalsShareOneInternalQuantumWithoutMoneyCreation()
    {
        var terminals = Enumerable.Range(0, 97)
            .Select(i => Id((200000 + i).ToString("D12"))).ToArray();
        var routes = Enumerable.Repeat(1m / 97m, 97).ToArray();
        routes[^1] = 1m - routes.Take(96).Sum();
        var graph = new ReciprocalInput([A.ToString()],
            terminals.Select(id => id.ToString()).ToArray(), [[0m]], [routes], false);
        var positive = SecondaryAllocationService.Calculate(
            Source(Primary(555, A, 0.00000001m)), graph, Precision);
        var negative = SecondaryAllocationService.Calculate(
            Source(Primary(555, A, -0.00000001m)), graph, Precision);
        Assert.AreEqual(0.00000001m, positive.Facts.Sum(f => f.PostedAmount.Amount));
        Assert.AreEqual(-0.00000001m, negative.Facts.Sum(f => f.PostedAmount.Amount));
        Assert.AreEqual(1, positive.Facts.Count(f => f.PostedAmount.Amount != 0m));
        Assert.AreEqual(1, negative.Facts.Count(f => f.PostedAmount.Amount != 0m));
        Assert.IsTrue(positive.Reconciliations.All(r => r.Difference.Amount == 0m));
        Assert.IsTrue(negative.Reconciliations.All(r => r.Difference.Amount == 0m));
    }

}
