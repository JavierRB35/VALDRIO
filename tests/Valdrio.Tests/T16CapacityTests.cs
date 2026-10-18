using Valdrio.Calculation;
using Valdrio.Domain;
namespace Valdrio.Tests;
[TestClass]
public sealed class T16CapacityTests
{
    [TestMethod]
    [DataRow(1000,100,50,500,500)]
    [DataRow(1000,100,120,1200,-200)]
    [DataRow(1000,100,0,0,1000)]
    [DataRow(-1000,100,50,-500,-500)]
    public void SignedCapacityConservesSource(int cost,int normal,int real,int applied,int variance)
    {
        var result=AuxiliaryCapacityCalculator.Assess(cost,normal,real,PrecisionPolicy.Pdec1Proposed);
        Assert.AreEqual((decimal)applied,result.Applied);Assert.AreEqual((decimal)variance,result.Variance);
        Assert.AreEqual((decimal)cost,result.Applied+result.Variance);
    }
    [TestMethod]
    public void ZeroNormalAndFractionalAmounts()
    {
        Assert.ThrowsExactly<DomainRuleException>(()=>AuxiliaryCapacityCalculator.Assess(10,0,1,PrecisionPolicy.Pdec1Proposed));
        var result=AuxiliaryCapacityCalculator.Assess(1,3,1,PrecisionPolicy.Pdec1Proposed);
        Assert.AreEqual(1m,result.Applied+result.Variance);
    }
}
