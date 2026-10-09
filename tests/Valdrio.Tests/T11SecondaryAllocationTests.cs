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

}
