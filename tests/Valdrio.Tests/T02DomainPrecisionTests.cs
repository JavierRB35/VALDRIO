using System.Text.Json;
using Valdrio.Domain;

namespace Valdrio.Tests;

[TestClass]
public sealed class T02DomainPrecisionTests
{
    private static readonly PrecisionPolicy Policy =
        PrecisionPolicy.Pdec1Proposed;

    [TestMethod]
    public void Pdec1_EnforcesAmountQuantityPercentageScalesAndMagnitude()
    {
        Policy.ValidateAmount(1_000_000_000_000m, "amount");
        Policy.ValidateAmount(0.00000001m, "amount");
        Policy.ValidateQuantity(0.000000000001m, "quantity");
        Policy.ValidatePercentage(33.333333333333333333m, "percentage");

        Assert.AreEqual(
            "NUM_SCALE_EXCEEDED",
            CaptureDomainRule(
                () => Policy.ValidateAmount(0.000000001m, "amount"))
            .Diagnostic.Code);

        Assert.AreEqual(
            "NUM_SCALE_EXCEEDED",
            CaptureDomainRule(
                () => Policy.ValidateQuantity(0.0000000000001m, "quantity"))
            .Diagnostic.Code);

        Assert.AreEqual(
            "NUM_MAGNITUDE_EXCEEDED",
            CaptureDomainRule(
                () => Policy.ValidateAmount(
                    1_000_000_000_000.00000001m,
                    "amount"))
            .Diagnostic.Code);

        Assert.AreEqual(
            "NUM_PERCENTAGE_OUT_OF_RANGE",
            CaptureDomainRule(
                () => Policy.ValidatePercentage(100.000000000000000001m, "percentage"))
            .Diagnostic.Code);
    }

    [TestMethod]
    public void CanonicalDecimal_IsInvariantTextWithoutGroupingExponentOrNegativeZero()
    {
        Assert.AreEqual("1234.5", CanonicalDecimal.Format(1234.50000000m));
        Assert.AreEqual("0", CanonicalDecimal.Format(decimal.Negate(0m)));
        Assert.AreEqual("-12.00000001", CanonicalDecimal.Format(-12.00000001m));

        Assert.AreEqual(1234.5m, CanonicalDecimal.Parse("1234.5"));

        Assert.ThrowsExactly<FormatException>(
            () => CanonicalDecimal.Parse("1.234,5"));
        Assert.ThrowsExactly<FormatException>(
            () => CanonicalDecimal.Parse("1.0"));
        Assert.ThrowsExactly<FormatException>(
            () => CanonicalDecimal.Parse("1e2"));
        Assert.ThrowsExactly<FormatException>(
            () => CanonicalDecimal.Parse("-0"));
    }

    [TestMethod]
    public void JsonConverter_WritesEconomicDecimalsAsCanonicalStrings()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new CanonicalDecimalJsonConverter());

        var json = JsonSerializer.Serialize(
            new DecimalEnvelope(1234.50000000m),
            options);

        Assert.AreEqual("{"Amount":"1234.5"}", json);

        var roundTrip = JsonSerializer.Deserialize<DecimalEnvelope>(
            json,
            options);

        Assert.IsNotNull(roundTrip);
        Assert.AreEqual(1234.5m, roundTrip.Amount);
    }

    [TestMethod]
    public void Money_RequiresSameCurrencyAndProtectsMagnitude()
    {
        var eur = new CurrencyCode("eur");
        var usd = new CurrencyCode("USD");

        Assert.AreEqual("EUR", eur.Value);

        var mismatch = CaptureDomainRule(
            () => _ = new Money(10m, eur) + new Money(10m, usd));

        Assert.AreEqual(
            "MONEY_CURRENCY_MISMATCH",
            mismatch.Diagnostic.Code);

        var overflow = CaptureDomainRule(
            () => _ =
                new Money(1_000_000_000_000m, eur) +
                new Money(0.00000001m, eur));

        Assert.AreEqual(
            "NUM_MAGNITUDE_EXCEEDED",
            overflow.Diagnostic.Code);
    }

    [TestMethod]
    public void Quantity_UsesExplicitStableUnitId()
    {
        var unitA = new UnitId(
            new EntityId(
                Guid.Parse("11111111-1111-1111-1111-111111111111")));
        var unitB = new UnitId(
            new EntityId(
                Guid.Parse("22222222-2222-2222-2222-222222222222")));

        var total =
            new Quantity(1.25m, unitA) +
            new Quantity(2.75m, unitA);

        Assert.AreEqual(4m, total.Value);
        Assert.AreEqual(
            "11111111-1111-1111-1111-111111111111",
            total.UnitId.ToString());

        var mismatch = CaptureDomainRule(
            () => _ =
                new Quantity(1m, unitA) +
                new Quantity(1m, unitB));

        Assert.AreEqual(
            "QUANTITY_UNIT_MISMATCH",
            mismatch.Diagnostic.Code);
    }

    private static DomainRuleException CaptureDomainRule(Action action)
    {
        try
        {
            action();
        }
        catch (DomainRuleException exception)
        {
            return exception;
        }

        Assert.Fail("Expected DomainRuleException.");
        throw new InvalidOperationException();
    }

    private sealed record DecimalEnvelope(decimal Amount);
}
