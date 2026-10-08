using System.Globalization;
using Valdrio.Calculation;
using Valdrio.Domain;
using Valdrio.Localization;

namespace Valdrio.Tests;

[TestClass]
public sealed class T07ResourcePreparationTests
{
    private static readonly CurrencyCode Eur = new("EUR");
    private static readonly CurrencyCode Usd = new("USD");
    private static EntityId Id(int number) => EntityId.Parse($"00000000-0000-0000-0000-{number:D12}");
    private static Money Money(decimal value) => new(value, Eur);
    private static ResourceSourceValue Source(int id, decimal amount) =>
        new(Id(id), new FxConverter().Convert($"source-{id}", Money(amount), Eur, null));
    private static ResourcePartDraft Pending(int id, decimal amount) => new(Id(id), Money(amount), ResourceRoutingKind.Pending);
    private static ResourceDraft Resource(int id, decimal amount, params ResourcePartDraft[] parts) =>
        new(Id(id), [Source(id + 100, amount)], [], parts);
    private static ResourcePreparation Prepare(params ResourceDraft[] resources) =>
        ResourcePreparationRules.Prepare(resources, Eur, PrecisionPolicy.Pdec1Proposed);
    private static ResourceAdjustment Adjustment() => ResourcePreparationRules.CreateAdjustment(
        Id(20), Money(-100m), Eur, null, "Correction", "Remove non-cost item", Id(21));
    private static ResourcePartDraft Direct(int id, decimal amount) => new(Id(id), Money(amount),
        ResourceRoutingKind.DirectObject, [new(Id(900), Money(amount))]);

    [TestMethod]
    public void C04_AdjustedResourceHasThreeDisjointRoutes()
    {
        var resource = new ResourceDraft(Id(1), [Source(100, 1000m)], [Adjustment()],
            [Direct(2, 300m), new(Id(3), Money(500m), ResourceRoutingKind.AllocationPool, poolId: Id(901)),
             new(Id(4), Money(100m), ResourceRoutingKind.Excluded, exclusionReason: "Outside study")]);
        var result = Prepare(resource);
        Assert.IsTrue(result.IsValid);
        var totals = result.Coverage.Single();
        Assert.AreEqual(1000m, totals.ConvertedOriginal.Amount);
        Assert.AreEqual(-100m, totals.AdjustmentTotal.Amount);
        Assert.AreEqual(900m, totals.Effective.Amount);
        Assert.AreEqual(300m, totals.Direct.Amount);
        Assert.AreEqual(500m, totals.Pools.Amount);
        Assert.AreEqual(100m, totals.Excluded.Amount);
        Assert.AreEqual(0m, totals.Difference.Amount);
        Assert.IsFalse(result.HasPendingParts);
    }

    [TestMethod]
    public void C05_ExtraPartBlocksAndReportsSignedDifference()
    {
        var result = Prepare(new ResourceDraft(Id(1), [Source(100, 1000m)], [Adjustment()],
            [Direct(2, 300m), Pending(3, 500m), Pending(4, 100m), Pending(5, 100m)]));
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual("RES_PARTITION_MISMATCH", result.Diagnostics.Single().Code);
        Assert.AreEqual(-100m, result.Coverage.Single().Difference.Amount);
    }

    [TestMethod]
    public void C30_OppositeDifferencesRemainTwoErrors()
    {
        var result = Prepare(Resource(1, 100m, Pending(3, 90m)), Resource(2, 100m, Pending(4, 110m)));
        Assert.AreEqual(0m, result.Coverage.Sum(value => value.Difference.Amount));
        CollectionAssert.AreEqual(new[] { 10m, -10m }, result.Coverage.Select(value => value.Difference.Amount).ToArray());
        Assert.AreEqual(2, result.Diagnostics.Count);
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void SameOriginCannotBeCountedAsMaterialAndPoolEvenIfBothResourcesBalance()
    {
        var source = Source(100, 100m);
        Expect("RES_SOURCE_REUSED", () => Prepare(new ResourceDraft(Id(1), [source], [], [Direct(3, 100m)]),
            new(Id(2), [source], [], [new(Id(4), Money(100m), ResourceRoutingKind.AllocationPool, poolId: Id(900))])));
    }

    [TestMethod]
    public void PartCannotBeDirectAndPoolSimultaneously() => Expect("RES_ROUTE_CONFLICT", () =>
        Prepare(Resource(1, 100m, new ResourcePartDraft(Id(2), Money(100m), ResourceRoutingKind.DirectObject,
            [new(Id(900), Money(100m))], poolId: Id(901)))));

    [TestMethod]
    public void ExclusionRequiresReason() => Expect("RES_EXCLUSION_REASON_REQUIRED", () =>
        Prepare(Resource(1, 100m, new ResourcePartDraft(Id(2), Money(100m), ResourceRoutingKind.Excluded, exclusionReason: " "))));

    [TestMethod]
    public void DirectAssignmentsMustConserveTheirOwnPart()
    {
        var part = new ResourcePartDraft(Id(2), Money(100m), ResourceRoutingKind.DirectObject,
            [new(Id(900), Money(30m)), new(Id(901), Money(70m))]);
        Assert.IsTrue(Prepare(Resource(1, 100m, part)).IsValid);
        Expect("RES_DIRECT_MISMATCH", () => Prepare(Resource(1, 100m,
            new ResourcePartDraft(Id(2), Money(100m), ResourceRoutingKind.DirectObject, [new(Id(900), Money(90m))]))));
    }

    [TestMethod]
    public void SignedAndZeroPendingPartsStayVisible()
    {
        var negative = Prepare(Resource(1, -100m, Direct(2, -40m), Pending(3, -60m)));
        Assert.IsTrue(negative.IsValid);
        Assert.AreEqual(-60m, negative.Coverage.Single().Pending.Amount);
        var zero = Prepare(Resource(1, 0m, Pending(2, 10m), Pending(3, -10m)));
        Assert.IsTrue(zero.IsValid);
        Assert.IsTrue(zero.HasPendingParts);
        Assert.AreEqual(0m, zero.Coverage.Single().Pending.Amount);
        Assert.IsTrue(Prepare(Resource(1, 0m, Pending(2, 0m))).HasPendingParts);
    }

    [TestMethod]
    public void MissingPartitionIsNotSilentlyCreated()
    {
        var result = Prepare(Resource(1, 100m));
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(100m, result.Coverage.Single().Difference.Amount);
    }

    [TestMethod]
    public void AdjustmentRequiresTypeAndReason()
    {
        Expect("RES_ADJUSTMENT_REASON_REQUIRED", () => ResourcePreparationRules.CreateAdjustment(
            Id(1), Money(-100m), Eur, null, "Correction", " ", Id(2)));
        Expect("RES_ADJUSTMENT_REASON_REQUIRED", () => ResourcePreparationRules.CreateAdjustment(
            Id(1), Money(-100m), Eur, null, " ", "Reason", Id(2)));
    }

    [TestMethod]
    public void ForeignAdjustmentNeedsRateAndReversalKeepsOriginalQuote()
    {
        Expect("FX_RATE_MISSING", () => ResourcePreparationRules.CreateAdjustment(
            Id(20), new(-100m, Usd), Eur, null, "Correction", "Reason", Id(21)));
        var rate = new FxRate(new("usd-eur"), Usd, Eur, 0.9m,
            new(new DateOnly(2026, 10, 7)), "Synthetic fixture", "rates-v1");
        var adjustment = ResourcePreparationRules.CreateAdjustment(Id(20), new(-100m, Usd),
            Eur, rate, "Correction", "Reason", Id(21));
        var reversal = ResourcePreparationRules.ReverseAdjustment(adjustment, Id(22), "Undo", Id(23));
        Assert.AreEqual(-90m, adjustment.Conversion.ConvertedOriginalMoney.Amount);
        Assert.AreEqual(90m, reversal.Conversion.ConvertedOriginalMoney.Amount);
        Assert.AreEqual(100m, reversal.Conversion.OriginalMoney.Amount);
        Assert.AreEqual(Usd, reversal.Conversion.OriginalMoney.Currency);
        Assert.AreEqual(rate.Id, reversal.Conversion.RateId);
        Assert.AreEqual(adjustment.Id, reversal.ReversalOfId);
        var result = Prepare(new ResourceDraft(Id(1), [Source(100, 1000m)], [reversal, adjustment], [Pending(2, 1000m)]));
        Assert.IsTrue(result.IsValid);
        Assert.AreEqual(0m, result.Coverage.Single().AdjustmentTotal.Amount);
    }

    [TestMethod]
    public void ReversalMustReferToOneLocalAdjustmentAndCannotBeRepeated()
    {
        var original = Adjustment();
        var reversal = ResourcePreparationRules.ReverseAdjustment(original, Id(22), "Undo", Id(23));
        Expect("RES_REVERSAL_INVALID", () => Prepare(new ResourceDraft(Id(1), [Source(100, 1000m)], [reversal], [Pending(2, 1100m)])));
        var second = ResourcePreparationRules.ReverseAdjustment(original, Id(24), "Undo again", Id(25));
        Expect("RES_REVERSAL_INVALID", () => Prepare(new ResourceDraft(Id(1), [Source(100, 1000m)],
            [original, reversal, second], [Pending(2, 1100m)])));
        Expect("RES_REVERSAL_INVALID", () => Prepare(new ResourceDraft(Id(1), [Source(100, 1000m)],
            [original, reversal with { Type = "Changed" }], [Pending(2, 1000m)])));
    }

    [TestMethod]
    public void TamperedFxBridgeIsRejectedEvenIfItsArithmeticBalances()
    {
        var rate = new FxRate(new("usd-eur"), Usd, Eur, 0.9m,
            new(new DateOnly(2026, 10, 7)), "Synthetic fixture", "v1");
        var conversion = new FxConverter().Convert("source", new(100m, Usd), Eur, rate);
        var forged = conversion with { ConvertedOriginalMoney = Money(95m), ConvertedRaw = 95m };
        Expect("RES_CONVERSION_INVALID", () => Prepare(new ResourceDraft(Id(1), [new(Id(100), forged)], [], [Pending(2, 95m)])));
    }

    [TestMethod]
    public void OriginalCurrenciesAreNotAddedNominally()
    {
        var rate = new FxRate(new("usd-eur"), Usd, Eur, 0.9m,
            new(new DateOnly(2026, 10, 7)), "Synthetic fixture", "v1");
        var foreign = new ResourceSourceValue(Id(101), new FxConverter().Convert("foreign", new(100m, Usd), Eur, rate));
        var result = Prepare(new ResourceDraft(Id(1), [Source(100, 100m), foreign], [], [Pending(2, 190m)]));
        Assert.IsTrue(result.IsValid);
        Assert.AreEqual(190m, result.Coverage.Single().ConvertedOriginal.Amount);
    }

    [TestMethod]
    public void IncompatibleMoneyAndDuplicateIdsAreRejected()
    {
        Expect("MONEY_CURRENCY_MISMATCH", () => Prepare(Resource(1, 100m,
            new ResourcePartDraft(Id(2), new(100m, Usd), ResourceRoutingKind.Pending))));
        Expect("RES_DUPLICATE_ID", () => Prepare(Resource(1, 100m, Pending(2, 50m), Pending(2, 50m))));
        Expect("RES_DUPLICATE_ID", () => Prepare(Resource(1, 0m), Resource(1, 0m)));
        Expect("RES_ROUTE_CONFLICT", () => Prepare(Resource(1, 0m, new ResourcePartDraft(Id(2), Money(0m), (ResourceRoutingKind)99))));
    }

    [TestMethod]
    public void DraftFreezesInputCollections()
    {
        var sources = new List<ResourceSourceValue> { Source(100, 100m) };
        var assignments = new List<DirectResourceAssignment> { new(Id(900), Money(100m)) };
        var parts = new List<ResourcePartDraft> { new ResourcePartDraft(Id(2), Money(100m), ResourceRoutingKind.DirectObject, assignments) };
        var draft = new ResourceDraft(Id(1), sources, [], parts);
        sources.Clear(); assignments.Clear(); parts.Clear();
        Assert.IsTrue(Prepare(draft).IsValid);
        Assert.AreEqual(1, draft.Sources.Count);
        Assert.AreEqual(1, draft.Parts.Single().DirectAssignments.Count);
    }

    [TestMethod]
    public void CultureAndInputOrderDoNotChangeCoverageOrCanonicalDiagnostics()
    {
        var previous = CultureInfo.CurrentCulture;
        var previousUi = CultureInfo.CurrentUICulture;
        try
        {
            var a = Resource(1, 100m, Pending(3, 90m));
            var b = Resource(2, 100m, Pending(4, 110m));
            var baseline = Prepare(a, b);
            foreach (var culture in new[] { "es-ES", "en-US", "tr-TR" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
                var result = Prepare(b, a);
                CollectionAssert.AreEqual(baseline.Coverage.ToArray(), result.Coverage.ToArray());
                CollectionAssert.AreEqual(new[] { "10", "-10" }, result.Diagnostics.Select(value => value.Arguments[1].CanonicalValue).ToArray());
            }
        }
        finally { CultureInfo.CurrentCulture = previous; CultureInfo.CurrentUICulture = previousUi; }
    }

    private static void Expect(string code, Action action)
    {
        var exception = Assert.ThrowsExactly<DomainRuleException>(action);
        Assert.AreEqual(code, exception.Diagnostic.Code);
        var catalog = new UiTextCatalog();
        foreach (var language in new[] { "en", "es" })
            Assert.IsTrue(catalog.ReadAll(CultureInfo.GetCultureInfo(language)).ContainsKey(exception.Diagnostic.MessageKey));
    }
}
