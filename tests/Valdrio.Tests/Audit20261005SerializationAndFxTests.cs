using System.Globalization;
using System.Text.Json;
using Valdrio.Application;
using Valdrio.Calculation;
using Valdrio.Domain;
using Valdrio.Localization;

namespace Valdrio.Tests;

[TestClass]
public sealed class Audit20261005SerializationAndFxTests
{
    private static readonly CurrencyCode Usd = new("USD");
    private static readonly CurrencyCode Eur = new("EUR");

    [TestMethod]
    public void A01_VersionedValuesRoundTripAcrossCultures()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            string? canonical = null;
            foreach (var culture in new[] { "es-ES", "en-US", "de-DE" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                var app = Convert(1234.5m, 0.912345678912345678m);
                var json = CanonicalSnapshotJson.Serialize(app);
                canonical ??= json;
                Assert.AreEqual(canonical, json);
                var restored = CanonicalSnapshotJson.Deserialize<FxRateApplication>(json);
                Assert.AreEqual(app.OriginalMoney, restored.OriginalMoney);
                Assert.AreEqual(app.ConvertedOriginalMoney, restored.ConvertedOriginalMoney);
                Assert.AreEqual(app.RateDate, restored.RateDate);
                Assert.AreEqual(FxConversionHash.Compute(app), FxConversionHash.Compute(restored));
            }
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [TestMethod]
    public void A01_RateSetRoundTripsAllRatesInStableOrder()
    {
        var set = new FxRateSetVersion("set", BusinessDate.ParseIso("2026-10-05"), "audit",
            [Rate("z", 0.9m), Rate("a", 0.95m)]);
        var restored = CanonicalSnapshotJson.Deserialize<FxRateSetVersion>(CanonicalSnapshotJson.Serialize(set));
        Assert.AreEqual("a", restored.Rates[0].Id.Value);
        Assert.AreEqual(set.GetRequired(new FxRateId("z")), restored.GetRequired(new FxRateId("z")));
        Assert.AreEqual(CanonicalSnapshotJson.Serialize(set), CanonicalSnapshotJson.Serialize(restored));
    }

    [TestMethod]
    public void A01_QuantityAndAllIdentifiersRoundTrip()
    {
        var id = EntityId.Parse("11111111-1111-1111-1111-111111111111");
        Assert.AreEqual(id, RoundTrip(id));
        Assert.AreEqual(new UnitId(id), RoundTrip(new UnitId(id)));
        Assert.AreEqual(new FxRateId("r|á"), RoundTrip(new FxRateId("r|á")));
        Assert.AreEqual(new StableRoundingId("stable|á"), RoundTrip(new StableRoundingId("stable|á")));
        var quantity = new Quantity(2.5m, new UnitId(id),
            PrecisionPolicy.Pdec1Proposed with { Version = "custom" });
        Assert.AreEqual(quantity, RoundTrip(quantity));
    }

    [TestMethod]
    public void A01_IncompleteDuplicateUnknownAndFutureSnapshotsAreRejected()
    {
        foreach (var value in new[]
        {
            "{}",
            "{\"Amount\":\"1\",\"Currency\":{\"Value\":\"EUR\",\"IsValid\":true}}",
            "{\"Amount\":\"1\",\"Amount\":\"2\"}",
            "{\"Extra\":\"ignored\"}"
        })
            Assert.ThrowsExactly<JsonException>(() => CanonicalSnapshotJson.Deserialize<Money>(
                "{\"SchemaVersion\":\"valdrio-values-v1\",\"Value\":" + value + "}"));
        Assert.ThrowsExactly<JsonException>(() => CanonicalSnapshotJson.Deserialize<Money>(
            "{\"SchemaVersion\":\"future\",\"Value\":{}}"));
    }

    [TestMethod]
    public void A01_InvalidJsonPrimitivesDoNotBecomeDefaults()
    {
        Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize<BusinessDate>("\"\""));
        Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize<UtcInstant>("\"2026-10-05\""));
        Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize<UnitId>(
            "\"00000000-0000-0000-0000-000000000000\""));
        Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize<FxRateId>("\"\""));
        Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize<Quantity>("{}"));
    }

    [TestMethod]
    public void A02_UnicodeNullAndPolicyChangesAreUnambiguous()
    {
        var original = Convert(100m, 0.9m);
        var changed = original with { Source = "audit|á" };
        Assert.AreNotEqual(FxConversionHash.Compute(original), FxConversionHash.Compute(changed));
        var customPolicy = PrecisionPolicy.Pdec1Proposed with { MaximumNumericCorrection = 0m };
        var changedPolicy = original with
        {
            OriginalMoney = new Money(100m, Usd, customPolicy),
            ConvertedOriginalMoney = new Money(90m, Eur, customPolicy)
        };
        Assert.AreNotEqual(FxConversionHash.Compute(original), FxConversionHash.Compute(changedPolicy));
        Assert.AreEqual("fx-v2", FxConversionHash.SchemaVersion);
        var identity = new FxConverter().Convert("identity", new Money(100m, Eur), Eur, null);
        Assert.IsTrue(RoundTrip(identity).IsIdentity);
        Assert.AreEqual(FxConversionHash.Compute(identity), FxConversionHash.Compute(RoundTrip(identity)));
    }

    [TestMethod]
    public void A04_InvalidCurrencyAndDefaultLookupAreRejected()
    {
        Assert.ThrowsExactly<ArgumentException>(() => _ = new FxRate(new FxRateId("r"), default, Eur,
            0.9m, BusinessDate.ParseIso("2026-10-05"), "audit", "set"));
        Assert.ThrowsExactly<ArgumentException>(() => new FxRateSetVersion("set",
            BusinessDate.ParseIso("2026-10-05"), "audit", [Rate("r", 0.9m)]).GetRequired(default));
        Assert.ThrowsExactly<ArgumentException>(() => _ = new UnitId(default));
        Assert.ThrowsExactly<ArgumentException>(() => _ = new UnitDefinition(default, "H", "time", "h", 2, false));
        Assert.ThrowsExactly<ArgumentException>(() => new CurrencyProjector().Project(1m,
            [new CurrencyProjectionInputLine(default, 1m)], 0.01m));
    }

    [TestMethod]
    public void A05_ExactTieAndAdjacentQuotesPreserveBothSigns()
    {
        foreach (var sign in new[] { 1m, -1m })
        {
            Assert.AreEqual(sign * 1.23456789m, Convert(sign, 1.234567894999999999m).ConvertedOriginalMoney.Amount);
            Assert.AreEqual(sign * 1.23456790m, Convert(sign, 1.234567895m).ConvertedOriginalMoney.Amount);
            Assert.AreEqual(sign * 1.23456790m, Convert(sign, 1.234567895000000001m).ConvertedOriginalMoney.Amount);
            Assert.AreEqual(sign * 1.23456788m, Convert(sign, 1.234567885m).ConvertedOriginalMoney.Amount);
        }
    }

    [TestMethod]
    public void A05_BelowQuantumReportsWarningAndPreservesBridge()
    {
        foreach (var sign in new[] { 1m, -1m })
        {
            var app = Convert(sign * 0.00000001m, 0.1m);
            Assert.AreEqual(0m, app.ConvertedOriginalMoney.Amount);
            Assert.AreEqual(sign * 0.000000001m, app.ConvertedRaw);
            Assert.AreEqual(-sign * 0.000000001m, app.ConversionRoundingAdjustment.Amount);
            Assert.AreEqual("FX_AMOUNT_BELOW_QUANTUM", app.Diagnostics.Single().Code);
            app.EnsureValid();
            foreach (var language in new[] { "en", "es" })
                Assert.IsFalse(string.IsNullOrWhiteSpace(new DomainDiagnosticFormatter(new UiTextCatalog())
                    .Format(app.Diagnostics.Single(), CultureInfo.GetCultureInfo(language),
                        CultureInfo.GetCultureInfo("es-ES")).Message));
        }
        Assert.AreEqual(0, Convert(0m, 0.1m).Diagnostics.Count);
    }

    [TestMethod]
    public void A05_OverflowIsDiagnosedAndApproximateRawBridgeRemainsExact()
    {
        var app = Convert(999999999999.99999999m, 0.500000000000000001m);
        Assert.AreEqual(app.ConvertedOriginalMoney.Amount,
            app.ConvertedRaw + app.ConversionRoundingAdjustment.Amount);
        var exception = Assert.ThrowsExactly<DomainRuleException>(() => Convert(1000000000000m, decimal.MaxValue));
        Assert.AreEqual("FX_AMOUNT_OUT_OF_RANGE", exception.Diagnostic.Code);
    }

    [TestMethod]
    public void A06_MoneyProjectionAndDiagnosticsCopyTheirInputs()
    {
        var money = new Money(1m, Eur);
        var metadata = new CurrencyMetadataSnapshot(Eur, "978", 2, true, "v1", "audit",
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1));
        MoneyCurrencyProjectionLine[] lines = [new(new StableRoundingId("a"), money, money, new Money(0m, Eur))];
        var result = new MoneyCurrencyProjectionResult(money, money, new Money(0m, Eur), metadata, lines);
        lines[0] = lines[0] with { ProjectedMoney = new Money(999m, Eur) };
        Assert.AreEqual(1m, result.ProjectedTotal.Amount);
        Assert.ThrowsExactly<NotSupportedException>(() =>
            ((IList<MoneyCurrencyProjectionLine>)result.Lines)[0] = lines[0]);
        DiagnosticArgument[] args = [new("value", DiagnosticArgumentType.Decimal, "1")];
        var diagnostic = new DomainDiagnostic("AUDIT", "Audit.Test", args);
        args[0] = args[0] with { CanonicalValue = "999" };
        Assert.AreEqual("1", diagnostic.Arguments[0].CanonicalValue);
    }

    private static T RoundTrip<T>(T value) => CanonicalSnapshotJson.Deserialize<T>(CanonicalSnapshotJson.Serialize(value));
    private static FxRate Rate(string id, decimal quote) => new(new FxRateId(id), Usd, Eur,
        quote, BusinessDate.ParseIso("2026-10-05"), "audit", "set");
    private static FxRateApplication Convert(decimal amount, decimal quote) =>
        new FxConverter().Convert("audit", new Money(amount, Usd), Eur, Rate("r", quote));
}
