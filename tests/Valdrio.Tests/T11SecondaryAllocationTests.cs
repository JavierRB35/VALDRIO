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
}
