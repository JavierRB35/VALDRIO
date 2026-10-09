using Valdrio.Application;
using Valdrio.Calculation;
using Valdrio.Domain;

namespace Valdrio.Tests;

[TestClass]
public sealed class T11ClassificationDefaultTests
{
    private static EntityId Id(int n) => EntityId.Parse($"00000000-0000-0000-0000-{n:D12}");
    private static readonly CurrencyCode Eur = new("EUR");

    [TestMethod]
    public void AssumedFixedIsDistinctFromReviewedFixedAndKeepsAmounts()
    {
        var classification = new ResourceClassification(ResourceBehavior.AssumedFixed, null,
            ResourceCostLayer.Unclassified, ResourceCapacityTreatment.None, null);
        var original = new ResourcePartDraft(Id(1), new Money(100m, Eur), ResourceRoutingKind.Pending);
        var changed = original.WithClassification(classification);
        Assert.AreEqual(original.Id, changed.Id);
        Assert.AreEqual(original.Amount, changed.Amount);
        Assert.AreEqual(original.Routing, changed.Routing);
        Assert.AreEqual(ResourceBehavior.Unclassified, original.Classification.Behavior);
        Assert.AreEqual(ResourceBehavior.AssumedFixed, changed.Classification.Behavior);
        Assert.IsNull(changed.Classification.Context);
    }

    [TestMethod]
    public void AssumedFixedWarnsAndRequiresReviewBeforeDefinitiveAbsorption()
    {
        var part = new ResourcePartDraft(Id(2), new Money(100m, Eur), ResourceRoutingKind.AllocationPool,
            poolId: Id(10), classification: new(ResourceBehavior.AssumedFixed, null,
                ResourceCostLayer.Unclassified, ResourceCapacityTreatment.None, null));
        var resource = new ResourceDraft(Id(1),
            [new ResourceSourceValue(Id(3), new FxConverter().Convert("example", new Money(100m, Eur), Eur, null))],
            [], [part]);
        var preliminary = ResourcePreparationRules.Prepare([resource], Eur, PrecisionPolicy.Pdec1Proposed);
        Assert.IsTrue(preliminary.IsValid);
        Assert.AreEqual("RES_BEHAVIOR_ASSUMED_FIXED", preliminary.Warnings.Single().Code);
        Assert.AreEqual(0m, preliminary.Coverage.Single().Difference.Amount);
        var final = ResourcePreparationRules.Prepare([resource], Eur, PrecisionPolicy.Pdec1Proposed,
            requireDefinitiveAbsorption: true);
        Assert.IsFalse(final.IsValid);
        Assert.AreEqual("RES_BEHAVIOR_ASSUMED_FIXED", final.Diagnostics.Single().Code);
    }
}
