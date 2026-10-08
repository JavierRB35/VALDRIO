using System.Globalization;
using Valdrio.Application;
using Valdrio.Calculation;
using Valdrio.Domain;
using Valdrio.Localization;

namespace Valdrio.Tests;

[TestClass]
public sealed class T07ResourceClassificationTests
{
    private static readonly CurrencyCode Eur = new("EUR");
    private static readonly PrecisionPolicy Precision = PrecisionPolicy.Pdec1Proposed;
    private static EntityId Id(int value) => EntityId.Parse($"00000000-0000-0000-0000-{value:D12}");
    private static UnitId Hours => new(Id(20));
    private static Quantity Q(decimal value) => new(value, Hours);
    private static ResourceBehaviorContext Context => new(Hours, new(2026, 1, 1), new(2026, 12, 31), Q(0), Q(2000));
    private static ResourceClassification Fixed => new(ResourceBehavior.Fixed, Context,
        ResourceCostLayer.Production, ResourceCapacityTreatment.None, null);
    private static ResourcePartDraft Pool(ResourceClassification? classification = null, int id = 2, decimal amount = 100m) =>
        new(Id(id), new(amount, Eur), ResourceRoutingKind.AllocationPool, poolId: Id(10), classification: classification);
    private static ResourceDraft Draft(params ResourcePartDraft[] parts) => new(Id(1),
        [new(Id(50), new FxConverter().Convert("source-50", new(100m, Eur), Eur, null))], [], parts);
    private static ResourcePreparation Prepare(ResourcePartDraft part, bool absorption = false, bool capacity = false) =>
        ResourcePreparationRules.Prepare([Draft(part)], Eur, Precision, absorption, capacity);

    [TestMethod]
    public void FixedAndVariableNeedExplicitDriverHorizonAndRange()
    {
        foreach (var behavior in new[] { ResourceBehavior.Fixed, ResourceBehavior.Variable })
        {
            var classification = Fixed with { Behavior = behavior };
            Assert.IsTrue(Prepare(Pool(classification)).IsValid);
            Expect("RES_BEHAVIOR_CONTEXT_REQUIRED", () => Prepare(Pool(classification with { Context = null })));
        }
    }

    [TestMethod]
    public void ReversedHorizonAndInvalidActivityRangesAreRejected()
    {
        Expect("RES_BEHAVIOR_HORIZON_INVALID", () => Prepare(Pool(Fixed with
            { Context = Context with { HorizonStart = new(2027, 1, 1) } })));
        Expect("RES_BEHAVIOR_RANGE_INVALID", () => Prepare(Pool(Fixed with
            { Context = Context with { MinimumActivity = Q(-1) } })));
        Expect("RES_BEHAVIOR_RANGE_INVALID", () => Prepare(Pool(Fixed with
            { Context = Context with { MinimumActivity = Q(2001) } })));
        var sameDay = Context with { HorizonEnd = Context.HorizonStart, MaximumActivity = Q(0) };
        Assert.IsTrue(Prepare(Pool(Fixed with { Context = sameDay })).IsValid);
    }

    [TestMethod]
    public void ActivityUnitsAndPrecisionMustBeCompatible()
    {
        Expect("RES_ACTIVITY_UNIT_MISMATCH", () => Prepare(Pool(Fixed with
            { Context = Context with { MaximumActivity = new(2000m, new UnitId(Id(21))) } })));
        var other = Precision with { Version = "another-policy" };
        var exception = Assert.ThrowsExactly<DomainRuleException>(() => Prepare(Pool(Fixed with
            { Context = Context with { MaximumActivity = new(2000m, Hours, other) } })));
        Assert.AreEqual("PRECISION_POLICY_MISMATCH", exception.Diagnostic.Code);
    }

    [TestMethod]
    public void UnclassifiedIsVisibleWarningAndDoesNotBecomeVariable()
    {
        var part = Pool();
        var result = Prepare(part);
        Assert.IsTrue(result.IsValid);
        Assert.AreEqual(ResourceBehavior.Unclassified, part.Classification.Behavior);
        Assert.AreEqual("RES_BEHAVIOR_UNCLASSIFIED", result.Warnings.Single().Code);
        Assert.AreEqual(part.Id.ToString(), result.Warnings.Single().Arguments.Single().CanonicalValue);
        Assert.IsTrue(Prepare(part, absorption: true).IsValid);
    }

    [TestMethod]
    public void MixedCanRemainInDraftButBlocksDefinitiveAbsorption()
    {
        var part = Pool(Fixed with { Behavior = ResourceBehavior.MixedPending });
        Assert.IsTrue(Prepare(part).IsValid);
        Assert.AreEqual("RES_BEHAVIOR_MIXED_PENDING", Prepare(part).Warnings.Single().Code);
        var definitive = Prepare(part, absorption: true);
        Assert.IsFalse(definitive.IsValid);
        Assert.AreEqual("RES_BEHAVIOR_MIXED_PENDING", definitive.Diagnostics.Single().Code);
    }

    [TestMethod]
    public void CapacityBelongsToExactDestinationPoolAndNode()
    {
        var owner = new ResourceCapacityOwner(Id(10), Id(11), Hours, Normal: Q(100), Actual: Q(0));
        var classification = Fixed with { CapacityTreatment = ResourceCapacityTreatment.AssessAtPool, CapacityOwner = owner };
        var part = Pool(classification);
        Assert.IsTrue(Prepare(part, absorption: true, capacity: true).IsValid);
        Assert.IsNull(part.Classification.CapacityOwner!.Practical);
        Assert.AreEqual(0m, part.Classification.CapacityOwner.Actual!.Value.Value);
        Expect("RES_CAPACITY_OWNER_INVALID", () => Prepare(Pool(classification with { CapacityOwner = null })));
        Expect("RES_CAPACITY_OWNER_INVALID", () => Prepare(Pool(classification with { CapacityOwner = owner with { PoolId = Id(12) } })));
        Expect("RES_CAPACITY_OWNER_INVALID", () => Prepare(new(Id(2), new(100m, Eur), ResourceRoutingKind.DirectObject,
            [new(Id(30), new(100m, Eur))], classification: classification)));
    }

    [TestMethod]
    public void CapacityObservationsKeepOwnUnitsAndCannotBeNegative()
    {
        var owner = new ResourceCapacityOwner(Id(10), Id(11), Hours, Actual: Q(-1));
        var classification = Fixed with { CapacityTreatment = ResourceCapacityTreatment.AssessAtPool, CapacityOwner = owner };
        Expect("RES_BEHAVIOR_RANGE_INVALID", () => Prepare(Pool(classification)));
        Expect("RES_ACTIVITY_UNIT_MISMATCH", () => Prepare(Pool(classification with
            { CapacityOwner = owner with { Actual = new(1m, new UnitId(Id(21))) } })));
    }

    [TestMethod]
    public void CapacityClassificationGateIsExplicit()
    {
        var owner = new ResourceCapacityOwner(Id(10), Id(11), Hours);
        foreach (var behavior in new[] { ResourceBehavior.Unclassified, ResourceBehavior.MixedPending })
        {
            var classification = ResourceClassification.Unclassified with
                { Behavior = behavior, CapacityTreatment = ResourceCapacityTreatment.AssessAtPool, CapacityOwner = owner };
            var part = Pool(classification);
            Assert.IsTrue(Prepare(part).IsValid);
            Assert.IsFalse(Prepare(part, capacity: true).IsValid);
            Assert.AreEqual(1, Prepare(part, capacity: true).Diagnostics.Count);
        }
        // Being fixed alone does not select or activate a capacity policy.
        Assert.IsTrue(Prepare(Pool(Fixed), capacity: true).IsValid);
        Assert.AreEqual(ResourceCapacityTreatment.None, Fixed.CapacityTreatment);
    }

    [TestMethod]
    public void CostLayerAndPeriodTreatmentDoNotChangeBehaviorOrMoney()
    {
        var production = Pool(Fixed);
        var commercial = production.WithClassification(Fixed with
            { Layer = ResourceCostLayer.Commercial, CapacityTreatment = ResourceCapacityTreatment.PeriodCost });
        Assert.AreEqual(production.Amount, commercial.Amount);
        Assert.AreEqual(ResourceBehavior.Fixed, commercial.Classification.Behavior);
        Assert.IsTrue(Prepare(commercial).IsValid);
        Expect("RES_CAPACITY_OWNER_INVALID", () => Prepare(Pool(commercial.Classification with
            { CapacityOwner = new(Id(10), Id(11), Hours) })));
        Assert.AreEqual(ResourceCostLayer.Production, production.Classification.Layer);
    }

    [TestMethod]
    public void UnknownClassificationCodesFailClosed()
    {
        Expect("RES_CLASSIFICATION_INVALID", () => Prepare(Pool(Fixed with { Behavior = (ResourceBehavior)99 })));
        Expect("RES_CLASSIFICATION_INVALID", () => Prepare(Pool(Fixed with { Layer = (ResourceCostLayer)99 })));
        Expect("RES_CLASSIFICATION_INVALID", () => Prepare(Pool(Fixed with { CapacityTreatment = (ResourceCapacityTreatment)99 })));
    }

    [TestMethod]
    public void ClassifyingDraftReturnsNewResourceAndPreservesSourcesAndAmounts()
    {
        var original = Draft(Pool());
        var edit = ResourceDraftEditor.ClassifyPart(original, Id(2), Fixed, Eur, Precision);
        Assert.AreEqual(ResourceBehavior.Unclassified, original.Parts.Single().Classification.Behavior);
        Assert.AreEqual(ResourceBehavior.Fixed, edit.Draft.Parts.Single().Classification.Behavior);
        Assert.AreSame(original.Sources.Single(), edit.Draft.Sources.Single());
        Assert.AreEqual(100m, edit.Preparation.Coverage.Single().Effective.Amount);
        Assert.AreEqual(0, edit.Preparation.Warnings.Count);
        Expect("RES_PART_NOT_FOUND", () => ResourceDraftEditor.ClassifyPart(original, Id(99), Fixed, Eur, Precision));
    }

    [TestMethod]
    public void SplitMixedPartIntoFixedAndVariableConservesOriginalAndUnblocksAbsorption()
    {
        var original = Draft(Pool(Fixed with { Behavior = ResourceBehavior.MixedPending }));
        var children = new[] { Pool(Fixed, 3, 40m), Pool(Fixed with { Behavior = ResourceBehavior.Variable }, 4, 60m) };
        var edit = ResourceDraftEditor.SplitPart(original, Id(2), children, Eur, Precision);
        Assert.IsTrue(ResourcePreparationRules.Prepare([edit.Draft], Eur, Precision, requireDefinitiveAbsorption: true).IsValid);
        Assert.AreEqual(2, edit.Draft.Parts.Count);
        Assert.AreEqual(ResourceBehavior.MixedPending, original.Parts.Single().Classification.Behavior);
        Assert.AreSame(original.Sources.Single(), edit.Draft.Sources.Single());
        Assert.AreEqual(0m, edit.Preparation.Coverage.Single().Difference.Amount);
    }

    [TestMethod]
    public void FailedSplitLeavesOriginalIntactAndCannotReuseSiblingId()
    {
        var original = Draft(Pool(Fixed));
        Expect("RES_SPLIT_MISMATCH", () => ResourceDraftEditor.SplitPart(original, Id(2),
            [Pool(Fixed, 3, 40m), Pool(Fixed, 4, 70m)], Eur, Precision));
        Expect("RES_SPLIT_REQUIRES_PARTS", () => ResourceDraftEditor.SplitPart(original, Id(2),
            [Pool(Fixed, 3, 100m)], Eur, Precision));
        var siblings = Draft(Pool(Fixed, 2, 50m), Pool(Fixed, 3, 50m));
        Expect("RES_DUPLICATE_ID", () => ResourceDraftEditor.SplitPart(siblings, Id(2),
            [Pool(Fixed, 3, 20m), Pool(Fixed, 4, 30m)], Eur, Precision));
        Assert.AreEqual(100m, original.Parts.Single().Amount.Amount);
        Assert.AreEqual(2, siblings.Parts.Count);
    }

    [TestMethod]
    public void RepartitionChecksWholeResourceBeforeReturningCandidate()
    {
        var original = Draft();
        Expect("RES_PARTITION_MISMATCH", () => ResourceDraftEditor.Repartition(original, [Pool(Fixed, 2, 90m)], Eur, Precision));
        Assert.AreEqual(0, original.Parts.Count);
        var edit = ResourceDraftEditor.Repartition(original, [Pool(Fixed)], Eur, Precision);
        Assert.IsTrue(edit.Preparation.IsValid);
    }

    private static void Expect(string code, Action action)
    {
        var error = Assert.ThrowsExactly<DomainRuleException>(action).Diagnostic;
        Assert.AreEqual(code, error.Code);
        foreach (var language in new[] { "en", "es" })
            Assert.IsTrue(new UiTextCatalog().ReadAll(CultureInfo.GetCultureInfo(language)).ContainsKey(error.MessageKey));
    }
}
