using Microsoft.VisualStudio.TestTools.UnitTesting;
using Valdrio.Calculation;
using Valdrio.Domain;

namespace Valdrio.Tests;
[TestClass]
public sealed class T15RatesTests
{
    private static RateSource[] Costs(decimal variable = 600) =>
        [new("fixed", ResourceBehavior.Fixed, RateComponentKind.OwnFixed, 1000), new("variable", ResourceBehavior.Variable, RateComponentKind.Variable, variable)];
    [TestMethod]
    public void T15_C15AndC18SeparateCapacityAndUnassignedWork()
    {
        var all = PrincipalRateCalculator.Calculate(Costs(), 100, 60, [new("a",40),new("b",20)]);
        Assert.AreEqual(20m, all.CombinedRate); Assert.AreEqual(1200m, all.AppliedCost);
        Assert.AreEqual(400m, all.CapacityVariance); Assert.AreEqual(0m, all.UnassignedCost);
        var partial = PrincipalRateCalculator.Calculate(Costs(), 100, 60, [new("a",40)]);
        Assert.AreEqual(800m, partial.AppliedCost); Assert.AreEqual(400m, partial.UnassignedCost);
        Assert.AreEqual(1600m, partial.Components.SelectMany(c=>c.Destinations).Sum(d=>d.PostedAmount));
    }
    [TestMethod]
    public void T15_NormalCapacitySeparatesFixedUnderactivityFromVariableCost()
    {
        var r = PrincipalRateCalculator.Calculate(
            [new("fixed", ResourceBehavior.Fixed, RateComponentKind.OwnFixed, 12000m),
             new("variable", ResourceBehavior.Variable, RateComponentKind.Variable, 6500m)],
            20000m, 18500m);
        Assert.AreEqual(0.6m, r.Components[0].Rate);
        Assert.AreEqual(6500m / 18500m, r.Components[1].Rate);
        Assert.AreEqual(900m, r.CapacityVariance);
        Assert.AreEqual(17600m, r.PerformedCost);
        Assert.AreEqual(18500m, r.SourceCost);
        Assert.AreEqual(18500m, r.CapacityVariance + r.PerformedCost);
        Assert.IsNull(r.AppliedCost); // Real order consumption is not known.
    }

    [TestMethod]
    public void T15_MissingObjectDataIsNotZeroConsumption()
    {
        var unknown = PrincipalRateCalculator.Calculate(Costs(),100,60);
        Assert.IsNull(unknown.AppliedCost); Assert.IsNull(unknown.UnassignedCost);
        Assert.AreEqual(1200m,unknown.PerformedCost); Assert.IsFalse(unknown.AssignmentKnown);
        var empty = PrincipalRateCalculator.Calculate(Costs(),100,60,[]);
        Assert.AreEqual(0m,empty.AppliedCost); Assert.AreEqual(1200m,empty.UnassignedCost);
    }
    [TestMethod]
    public void T15_C17SignedOveractivityConserves2200()
    {
        var r=PrincipalRateCalculator.Calculate(Costs(1200),100,120,[new("a",120)]);
        Assert.AreEqual(-200m,r.CapacityVariance); Assert.AreEqual(2400m,r.AppliedCost);
        Assert.AreEqual(2200m,r.AppliedCost+r.CapacityVariance+r.UnassignedCost);
    }
    [TestMethod]
    public void T15_ZeroAndMissingDenominatorsRemainDistinct()
    {
        Assert.IsNull(PrincipalRateCalculator.Calculate(Costs(),null,60).CombinedRate);
        Assert.IsNull(PrincipalRateCalculator.Calculate(Costs(),0,60).CombinedRate);
        Assert.IsNull(PrincipalRateCalculator.Calculate(Costs(),100,0).CombinedRate);
        var fixedOnly=PrincipalRateCalculator.Calculate([Costs()[0]],100,0,[]);
        Assert.AreEqual(10m,fixedOnly.CombinedRate); Assert.AreEqual(1000m,fixedOnly.CapacityVariance);
        Assert.AreEqual(0m,fixedOnly.PerformedCost);
        var undefined=PrincipalRateCalculator.Calculate([new("zero",ResourceBehavior.Variable,RateComponentKind.Variable,0)],100,null);
        Assert.IsNull(undefined.CombinedRate);
        var c16=PrincipalRateCalculator.Calculate(Costs(0),100,0,[]);
        Assert.AreEqual(10m,c16.Components[0].Rate);Assert.IsNull(c16.Components[1].Rate);
        Assert.AreEqual(1000m,c16.CapacityVariance);Assert.AreEqual(0m,c16.UnassignedCost);
        Assert.AreEqual("ACT_COST_WITH_ZERO_ACTIVITY",PrincipalRateCalculator.Calculate(Costs(100),100,0,[]).DiagnosticFacts.Single().Code);
        Assert.AreEqual("CAP_ZERO_DENOMINATOR",PrincipalRateCalculator.Calculate(Costs(0),0,100,[new("O",100)]).DiagnosticFacts.Single().Code);
    }
    [TestMethod]
    public void T15_ConsumedFixedIsNotAssessedAgainAndPeriodIsSeparate()
    {
        var sources=Costs().Concat(new[]{new RateSource("received",ResourceBehavior.Fixed,RateComponentKind.Consumed,300),
            new RateSource("period",ResourceBehavior.Fixed,RateComponentKind.Period,50)}).ToArray();
        var r=PrincipalRateCalculator.Calculate(sources,100,60,[new("a",40)]);
        Assert.AreEqual(25m,r.CombinedRate); Assert.AreEqual(400m,r.CapacityVariance);
        Assert.AreEqual(1000m,r.AppliedCost);Assert.AreEqual(500m,r.UnassignedCost);Assert.AreEqual(50m,r.PeriodCost);
        Assert.AreEqual(1950m,r.Components.SelectMany(c=>c.Destinations).Sum(d=>d.PostedAmount));
    }
    [TestMethod]
    public void T15_ExcessRealConsumptionBlocksRatherThanInventingNegativeUnassigned()
    {
        var r=PrincipalRateCalculator.Calculate(Costs(),100,60,[new("a",61)]);
        CollectionAssert.Contains(r.Diagnostics.ToArray(),"Rates.ExcessConsumption"); Assert.IsNull(r.AppliedCost);
    }
    [TestMethod]
    public void T15_RoundingConservesEachSignedSourceAtLedgerPrecision()
    {
        var r=PrincipalRateCalculator.Calculate([new("one",ResourceBehavior.Fixed,RateComponentKind.OwnFixed,1),
            new("credit",ResourceBehavior.Variable,RateComponentKind.Variable,-0.13m)],3,4,[new("a",1),new("b",2)]);
        foreach(var c in r.Components) Assert.AreEqual(c.Source.Amount,c.Destinations.Sum(d=>d.PostedAmount));
        Assert.AreEqual(0.87m,r.AppliedCost+r.CapacityVariance+r.UnassignedCost);
    }
}
