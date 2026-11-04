using Microsoft.VisualStudio.TestTools.UnitTesting;
using Valdrio.Domain;
using Valdrio.Reporting;

namespace Valdrio.Tests;

[TestClass]
public sealed class RunFunctionBalanceTests
{
    private static EntityId Id(int value) =>
        new(Guid.Parse($"00000000-0000-4000-8000-{value:D12}"));

    private static CalculationNodeDefinition Node(int n, NodeEconomicFunction function,
        NodeTariffPurpose purpose = NodeTariffPurpose.General, NodeRole role = NodeRole.Principal)
    {
        var tags = function == NodeEconomicFunction.Unspecified
            ? new[] { "legacy" }
            : NodeCostClassification.WithClassification(["legacy"], function, purpose);
        return new(Id(n), Id(100), Id(200), "N" + n, role, tags, true);
    }

    [TestMethod]
    public void PrincipalTotalsArePartitionedOnceByDestinationFunctionWithSignedAmounts()
    {
        var nodes = new[]
        {
            Node(1, NodeEconomicFunction.Production),
            Node(2, NodeEconomicFunction.Production, NodeTariffPurpose.DirectLabor),
            Node(3, NodeEconomicFunction.Commercial),
            Node(4, NodeEconomicFunction.Structure),
            Node(5, NodeEconomicFunction.Unspecified),
            Node(6, NodeEconomicFunction.Structure, role: NodeRole.Auxiliary)
        };
        var balances = new[]
        {
            new RunNodeBalance(Id(1), Id(200), NodeRole.Principal, 70m, 30m, 100m),
            new RunNodeBalance(Id(2), Id(200), NodeRole.Principal, 20m, 20m, 40m),
            new RunNodeBalance(Id(3), Id(200), NodeRole.Principal, 10m, 15m, 25m),
            new RunNodeBalance(Id(4), Id(200), NodeRole.Principal, 15m, 0m, 15m),
            new RunNodeBalance(Id(5), Id(200), NodeRole.Principal, -5m, 0m, -5m),
            new RunNodeBalance(Id(6), Id(200), NodeRole.Auxiliary, 800m, -800m, 0m)
        };

        var result = RunCostBalanceProjector.GroupByFunction(balances, nodes);
        Assert.AreEqual(140m, result.Single(r => r.Function == NodeEconomicFunction.Production).Final);
        Assert.AreEqual(2, result.Single(r => r.Function == NodeEconomicFunction.Production).NodeCount);
        Assert.AreEqual(25m, result.Single(r => r.Function == NodeEconomicFunction.Commercial).Final);
        Assert.AreEqual(15m, result.Single(r => r.Function == NodeEconomicFunction.Structure).Final);
        Assert.AreEqual(-5m, result.Single(r => r.Function == NodeEconomicFunction.Unspecified).Final);
        Assert.AreEqual(175m, result.Sum(r => r.Final));
        Assert.AreEqual(balances.Where(r => r.Role == NodeRole.Principal).Sum(r => r.Final),
            result.Sum(r => r.Final));
    }

    [TestMethod]
    public void MissingTerminalNodeDoesNotDisappearFromReconciliation()
    {
        var balances = new[] { new RunNodeBalance(Id(91), Id(200), NodeRole.Principal, 1m, 0m, 1m) };
        Assert.ThrowsExactly<ArgumentException>(() => RunCostBalanceProjector.GroupByFunction(balances, []));
    }
}
