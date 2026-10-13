using Valdrio.Calculation;
using Valdrio.Desktop;

namespace Valdrio.Tests;

[TestClass]
public sealed class SecondaryFullFlowProjectorTests
{
    [TestMethod]
    public void ReciprocalEdgesAndTerminalEdgesUseTheSameGrossAsPublishedLu()
    {
        // Costs: A=10,000; B=5,000. A -> B 20%, P 80%;
        // B -> A 10%, P 90%. Both directions must be visible.
        var graph = new ReciprocalInput(
            ["A", "B"], ["P"],
            [[0m, 0.20m], [0.10m, 0m]],
            [[0.80m], [0.90m]], true);
        var solver = ReciprocalFactorization.Prepare(graph);
        var solution = solver.Solve(
            new Dictionary<string, decimal> { ["A"] = 10000m, ["B"] = 5000m },
            new Dictionary<string, decimal> { ["P"] = 0m });
        var flows = SecondaryFlowProjector.Project(graph, solution);
        Assert.AreEqual(4, flows.Count);
        Assert.IsTrue(flows.Any(edge =>
            edge.Stage == SecondaryFlowStage.Reciprocal && edge.SourceId == "A" && edge.TargetId == "B"));
        Assert.IsTrue(flows.Any(edge =>
            edge.Stage == SecondaryFlowStage.Reciprocal && edge.SourceId == "B" && edge.TargetId == "A"));
        Assert.IsTrue(flows.Any(edge =>
            edge.Stage == SecondaryFlowStage.Final && edge.SourceId == "A" && edge.TargetId == "P"));
        Assert.IsTrue(flows.Any(edge =>
            edge.Stage == SecondaryFlowStage.Final && edge.SourceId == "B" && edge.TargetId == "P"));
        var intoPrincipal = flows.Where(edge => edge.Stage == SecondaryFlowStage.Final)
            .Sum(edge => edge.Amount);
        Assert.IsTrue(Math.Abs(intoPrincipal - 15000m) < 0.000001m,
            "Terminal arrows must sum to the original allocation, not include gross recirculation.");
        Assert.IsTrue(flows.Where(edge => edge.Stage == SecondaryFlowStage.Reciprocal)
            .Sum(edge => edge.Amount) > 0m);
    }

    [TestMethod]
    public void SameAuxiliarySelfServiceIsShownWithoutInfiniteIteration()
    {
        var graph = new ReciprocalInput(["A"], ["P"], [[0.25m]], [[0.75m]], true);
        var solution = ReciprocalFactorization.Prepare(graph).Solve(
            new Dictionary<string, decimal> { ["A"] = 600m },
            new Dictionary<string, decimal> { ["P"] = 0m });
        var arrows = SecondaryFlowProjector.Project(graph, solution);
        Assert.AreEqual(2, arrows.Count);
        Assert.IsTrue(arrows.Any(x => x.Stage == SecondaryFlowStage.Reciprocal
            && x.SourceId == "A" && x.TargetId == "A" && Math.Abs(x.Amount - 200m) < 0.000001m));
        Assert.IsTrue(arrows.Any(x => x.Stage == SecondaryFlowStage.Final
            && x.TargetId == "P" && Math.Abs(x.Amount - 600m) < 0.000001m));
    }

    [TestMethod]
    public void PublishedCanonicalAuxiliaryOrderCannotRedirectArrows()
    {
        // Graph input order intentionally differs from solver's canonical order.
        var graph = new ReciprocalInput(["B", "A"], ["P"],
            [[0m, 0.10m], [0.20m, 0m]], [[0.90m], [0.80m]], true);
        var solution = ReciprocalFactorization.Prepare(graph).Solve(
            new Dictionary<string, decimal> { ["A"] = 10000m, ["B"] = 5000m },
            new Dictionary<string, decimal> { ["P"] = 0m });
        var arrows = SecondaryFlowProjector.Project(graph, solution);
        var aToB = arrows.Single(x => x.Stage == SecondaryFlowStage.Reciprocal
            && x.SourceId == "A" && x.TargetId == "B");
        var bToA = arrows.Single(x => x.Stage == SecondaryFlowStage.Reciprocal
            && x.SourceId == "B" && x.TargetId == "A");
        Assert.IsTrue(aToB.Amount > bToA.Amount,
            "Always map solver grosses by entity ID, not by the input array index.");
    }
}
