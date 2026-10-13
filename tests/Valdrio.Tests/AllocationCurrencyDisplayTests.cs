using Valdrio.Desktop;
using Valdrio.Domain;

namespace Valdrio.Tests;

[TestClass]
public sealed class AllocationCurrencyDisplayTests
{
    private static readonly PrecisionPolicy Policy = PrecisionPolicy.Pdec1Proposed;

    [TestMethod]
    public void ThirdsReconcileInCurrencyMinorUnitsWithoutChangingInput()
    {
        var original = new[] { 0.33333333m, 0.33333333m, 0.33333334m };
        var projected = AllocationCurrencyDisplay.Project(original, 0.01m, Policy);
        CollectionAssert.AreEqual(new[] { 0.33m, 0.33m, 0.34m }, projected.ToArray());
        Assert.AreEqual(1.00m, projected.Sum());
        Assert.AreEqual(1.00m, original.Sum());
    }

    [TestMethod]
    public void CafeExampleDisplaysTwoEqualTerminalTotalsAndReconciledBreakdown()
    {
        var source = new (decimal Direct, decimal Auxiliary)[]
        {
            (1833.33333334m, 916.66666668m),
            (1833.33333332m, 916.66666666m)
        };
        var shown = AllocationCurrencyDisplay.ProjectBreakdown(source, 0.01m, Policy);
        Assert.AreEqual(2750.00m, shown[0].Final);
        Assert.AreEqual(2750.00m, shown[1].Final);
        Assert.AreEqual(5500.00m, shown.Sum(row => row.Final));
        Assert.AreEqual(3666.67m, shown.Sum(row => row.Direct));
        Assert.AreEqual(1833.33m, shown.Sum(row => row.Auxiliary));
        Assert.IsTrue(shown.All(row => row.Direct + row.Auxiliary == row.Final));
        Assert.AreEqual(2750.00000002m, source[0].Direct + source[0].Auxiliary);
        Assert.AreEqual(2749.99999998m, source[1].Direct + source[1].Auxiliary);
    }

    [TestMethod]
    public void ExplanationLinesMatchAlreadyPresentedContribution()
    {
        var source = new[] { 916.66666668m, 916.66666666m };
        var shown = AllocationCurrencyDisplay.ProjectToTarget(source, 1833.33m, 0.01m, Policy);
        Assert.AreEqual(1833.33m, shown.Sum());
        Assert.IsTrue(shown.All(value => value * 100m == decimal.Truncate(value * 100m)));
    }

    [TestMethod]
    public void ZeroMinorUnitsAndCreditsRemainConserved()
    {
        var jpy = AllocationCurrencyDisplay.Project(new[] { 0.33333333m, 0.33333333m, 0.33333334m },
            1m, Policy);
        Assert.AreEqual(1m, jpy.Sum());
        Assert.IsTrue(jpy.All(value => value == decimal.Truncate(value)));
        var credits = AllocationCurrencyDisplay.Project(new[] { -0.33333333m, -0.33333333m, -0.33333334m },
            0.001m, Policy);
        Assert.AreEqual(-1m, credits.Sum());
        Assert.IsTrue(credits.All(value => value * 1000m == decimal.Truncate(value * 1000m)));
    }
}
