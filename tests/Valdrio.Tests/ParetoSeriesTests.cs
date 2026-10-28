using Valdrio.Desktop;
namespace Valdrio.Tests;

[TestClass]
public sealed class ParetoSeriesTests
{
    [TestMethod]
    public void OrdersCostsAndAccumulatesToOneHundredWithoutChangingInput()
    {
        ParetoCost[] input = [new("B", 30), new("C", 10), new("A", 60)];
        var points = ParetoSeries.Build(input);
        CollectionAssert.AreEqual(new[] { "A", "B", "C" }, points.Select(x => x.Label).ToArray());
        CollectionAssert.AreEqual(new decimal?[] { 60, 90, 100 }, points.Select(x => x.CumulativePercent).ToArray());
        Assert.AreEqual("B", input[0].Label);
    }
    [TestMethod]
    public void CreditsRemainSignedAndPercentageUsesPositiveTotal()
    {
        var points = ParetoSeries.Build([new("Credit", -20), new("Cost", 120)]);
        Assert.AreEqual(100m, points[0].CumulativePercent);
        Assert.AreEqual(-20m, points[1].Amount);
        Assert.IsNull(points[1].CumulativePercent);
    }
    [TestMethod]
    public void EmptyZeroAndNegativeTotalsDoNotInventPercentages()
    {
        Assert.AreEqual(0, ParetoSeries.Build([]).Count);
        foreach (var costs in new ParetoCost[][] { [new("Zero", 0)], [new("A", 10), new("B", -10)], [new("Credit", -10)] })
            Assert.IsTrue(ParetoSeries.Build(costs).All(x => x.CumulativePercent is null));
    }
    [TestMethod]
    public void TiesAreDeterministicAndDuplicateLabelsDoNotMergeNodes()
    {
        var points = ParetoSeries.Build([new("B", 1), new("A", 1), new("A", 1)]);
        Assert.AreEqual(3, points.Count);
        Assert.AreEqual("B", points[2].Label);
        Assert.AreEqual(100m, points[2].CumulativePercent);
    }
}
