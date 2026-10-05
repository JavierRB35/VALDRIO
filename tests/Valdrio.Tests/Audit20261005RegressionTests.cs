using System.Text.Json;
using Valdrio.Application;
using Valdrio.Calculation;
using Valdrio.Domain;

namespace Valdrio.Tests;

[TestClass]
public sealed class Audit20261005RegressionTests
{
    private static readonly CurrencyCode Usd = new("USD");
    private static readonly CurrencyCode Eur = new("EUR");
    private static JsonSerializerOptions Options()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new CanonicalDecimalJsonConverter());
        return options;
    }

    [TestMethod]
    public void A01_BusinessDate_RoundTripsWithoutDefaulting()
    {
        var original = BusinessDate.ParseIso("2026-10-05");
        Assert.AreEqual(original, RoundTrip(original));
    }

    [TestMethod]
    public void A01_UtcInstant_RoundTripsWithoutDefaulting()
    {
        var original = UtcInstant.ParseIso("2026-10-05T07:35:00Z");
        Assert.AreEqual(original, RoundTrip(original));
    }

    [TestMethod]
    public void A01_Quantity_RoundTripsUnitAndPolicy()
    {
        var original = new Quantity(2.5m, UnitId.Parse("11111111-1111-1111-1111-111111111111"));
        Assert.AreEqual(original, RoundTrip(original));
    }

    [TestMethod]
    public void A01_FxRate_RoundTripsIdentityAndDate()
    {
        var original = Rate("rate-1", "set-1", 0.9m);
        Assert.AreEqual(original, RoundTrip(original));
    }

    [TestMethod]
    public void A02_DelimitersCannotAliasDifferentFxIdentities()
    {
        var converter = new FxConverter();
        var first = converter.Convert("a", new Money(100m, Usd), Eur, Rate("C", "A|B", 0.9m));
        var second = converter.Convert("a", new Money(100m, Usd), Eur, Rate("B|C", "A", 0.9m));
        Assert.AreNotEqual(FxConversionHash.Compute(first), FxConversionHash.Compute(second));
    }

    [TestMethod]
    public void A04_FxRateRejectsDefaultIdentifier()
    {
        Assert.ThrowsExactly<ArgumentException>(() => _ = new FxRate(default, Usd, Eur, 0.9m,
            BusinessDate.ParseIso("2026-10-05"), "audit", "set"));
    }

    [TestMethod]
    public void A04_QuantityRejectsDefaultUnit()
    {
        Assert.ThrowsExactly<ArgumentException>(() => _ = new Quantity(1m, default));
    }

    [TestMethod]
    public void A04_RoundingRejectsDefaultDestination()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new BalancedRounder().Round(
            1m, [new RoundingContribution(default, 1m)], 0.00000001m));
    }

    [TestMethod]
    public void A04_QuantityAdditionRejectsDefaultOperands()
    {
        Assert.ThrowsExactly<ArgumentException>(() => _ = default(Quantity) + default(Quantity));
    }

    [TestMethod]
    public void A05_QuantizationUsesExactProductAtAdmittedMagnitude()
    {
        var result = new FxConverter().Convert("limit", new Money(999999999999.99999999m, Usd),
            Eur, Rate("limit", "set", 0.500000000000000001m));
        // Independent rational reference recorded in the audit, not the decimal product under test.
        Assert.AreEqual(500000000000.00000099m, result.ConvertedOriginalMoney.Amount);
    }

    [TestMethod]
    public void A05_QuantizationPreservesNegativeMirrorAtAdmittedMagnitude()
    {
        var result = new FxConverter().Convert("limit", new Money(-999999999999.99999999m, Usd),
            Eur, Rate("limit", "set", 0.500000000000000001m));
        Assert.AreEqual(-500000000000.00000099m, result.ConvertedOriginalMoney.Amount);
    }

    [TestMethod]
    public void A06_BalancedResultDoesNotExposeMutableArray()
    {
        var result = new BalancedRounder().Round(1m,
            [new RoundingContribution(new StableRoundingId("A"), 1m)], 0.00000001m);
        Assert.IsFalse(result.Lines is RoundedContribution[]);
    }

    [TestMethod]
    public void A06_BalancedResultCopiesSuppliedCollection()
    {
        RoundedContribution[] input = [new(new StableRoundingId("A"), 1m, 0m, 0m, 1m)];
        var result = new BalancedRoundResult(1m, 0.00000001m, input);
        input[0] = input[0] with { PostedAmount = 999m };
        Assert.AreEqual(1m, result.PostedTotal);
    }

    [TestMethod]
    public void A06_CurrencyResultCopiesSuppliedCollection()
    {
        CurrencyProjectionLine[] input = [new(new StableRoundingId("A"), 1m, 1m, 0m)];
        var result = new CurrencyProjectionResult(1m, 1m, 0m, 0.01m, input);
        input[0] = input[0] with { ProjectedAmount = 999m };
        Assert.AreEqual(1m, result.ProjectedTotal);
    }

    [TestMethod]
    public void A06_RevalueCandidateCopiesSuppliedCollection()
    {
        var app = new FxConverter().Convert("a", new Money(100m, Usd), Eur, Rate("r", "set", 0.9m));
        RevaluedStudyItem[] input = [new("resource", app, FxConversionHash.Compute(app))];
        var candidate = new RevalueStudyCandidate("study", 0, 1, "set", input);
        input[0] = input[0] with { ResourceId = "replaced" };
        Assert.AreEqual("resource", candidate.Items[0].ResourceId);
    }

    private static T RoundTrip<T>(T value) =>
        JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, Options()), Options())!;

    private static FxRate Rate(string id, string set, decimal quote) =>
        new(new FxRateId(id), Usd, Eur, quote, BusinessDate.ParseIso("2026-10-05"), "audit", set);
}
