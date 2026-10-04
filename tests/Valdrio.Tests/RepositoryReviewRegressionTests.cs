using System.Globalization;
using System.Text.Json;
using Valdrio.Calculation;
using Valdrio.Domain;
using Valdrio.Localization;

namespace Valdrio.Tests;

[TestClass]
public sealed class RepositoryReviewRegressionTests
{
    [TestMethod]
    public void R01_UtcInstantRejectsIncompleteOrZoneLessText()
    {
        var invalid = new[]
        {
            "2026-10-04T00:30:00",
            "00:30",
            "2026-10-04"
        };

        foreach (var input in invalid)
        {
            Assert.ThrowsExactly<FormatException>(
                () => UtcInstant.ParseIso(input),
                $"Expected '{input}' to be rejected.");
        }
    }

    [TestMethod]
    public void R01_UtcInstantNormalizesExplicitOffsetAndRoundTrips()
    {
        var fromOffset = UtcInstant.ParseIso(
            "2026-10-04T02:30:00+02:00");

        Assert.AreEqual(
            "2026-10-04T00:30:00.0000000Z",
            fromOffset.ToCanonicalString());

        var roundTrip = UtcInstant.ParseIso(
            fromOffset.ToCanonicalString());

        Assert.AreEqual(fromOffset, roundTrip);
    }

    [TestMethod]
    public void R02_EnUsRejectsMalformedGrouping()
    {
        var edit = new DecimalEditSession(
            CultureInfo.GetCultureInfo("en-US"));

        Assert.IsFalse(edit.TryCommit("1,2,3", out _));
        Assert.IsFalse(edit.TryCommit("12,34.50", out _));
    }

    [TestMethod]
    public void R02_EsEsRejectsMalformedGrouping()
    {
        var edit = new DecimalEditSession(
            CultureInfo.GetCultureInfo("es-ES"));

        Assert.IsFalse(edit.TryCommit("1.23", out _));
        Assert.IsFalse(edit.TryCommit("12.34,50", out _));
    }

    [TestMethod]
    public void R02_ValidRegionalNumbersKeepTheirValue()
    {
        var en = new DecimalEditSession(
            CultureInfo.GetCultureInfo("en-US"));
        var es = new DecimalEditSession(
            CultureInfo.GetCultureInfo("es-ES"));

        Assert.IsTrue(en.TryCommit("1,234.50", out var enValue));
        Assert.IsTrue(es.TryCommit("1.234,50", out var esValue));
        Assert.IsTrue(en.TryCommit("-1,234.50", out var negative));
        Assert.IsTrue(es.TryCommit(" 1.234,50 ", out var spaced));

        Assert.AreEqual(1234.50m, enValue);
        Assert.AreEqual(1234.50m, esValue);
        Assert.AreEqual(-1234.50m, negative);
        Assert.AreEqual(1234.50m, spaced);
    }

    [TestMethod]
    public void R02_EmptyInputIsRejected()
    {
        var edit = new DecimalEditSession(
            CultureInfo.GetCultureInfo("en-US"));

        Assert.IsFalse(edit.TryCommit("", out _));
        Assert.IsFalse(edit.TryCommit("   ", out _));
    }
    [TestMethod]
    public void R03_MoneyRejectsDefaultCurrency()
    {
        var exception = CaptureDomainRule(
            () => _ = new Money(
                10m,
                default(CurrencyCode)));

        Assert.AreEqual(
            "MONEY_CURRENCY_INVALID",
            exception.Diagnostic.Code);
    }

    [TestMethod]
    public void R03_DefaultMoneyCannotParticipateInArithmetic()
    {
        var exception = CaptureDomainRule(
            () => _ = default(Money) + default(Money));

        Assert.AreEqual(
            "MONEY_CURRENCY_INVALID",
            exception.Diagnostic.Code);
    }

    [TestMethod]
    public void R03_IncompleteMoneyJsonIsRejected()
    {
        try
        {
            _ = JsonSerializer.Deserialize<Money>(
                "{\"Amount\":10}");
            Assert.Fail("Incomplete Money JSON must be rejected.");
        }
        catch (Exception exception)
            when (exception is JsonException
                  or DomainRuleException
                  or ArgumentException)
        {
        }
    }

    [TestMethod]
    public void R04_MoneyArithmeticKeepsSelectedPrecisionPolicy()
    {
        var policy = PrecisionPolicy.Pdec1Proposed with
        {
            Version = "REVIEW-scale9",
            AmountScale = 9
        };

        var eur = new CurrencyCode("EUR");
        var amount = new Money(
            0.123456789m,
            eur,
            policy);
        var zero = new Money(
            0m,
            eur,
            policy);

        var sum = amount + zero;
        var difference = sum - zero;

        Assert.AreEqual(0.123456789m, sum.Amount);
        Assert.AreEqual(0.123456789m, difference.Amount);
    }

    [TestMethod]
    public void R04_IncompatiblePrecisionPoliciesAreRejected()
    {
        var scale9 = PrecisionPolicy.Pdec1Proposed with
        {
            Version = "REVIEW-scale9",
            AmountScale = 9
        };

        var eur = new CurrencyCode("EUR");
        var left = new Money(1m, eur, scale9);
        var right = new Money(
            1m,
            eur,
            PrecisionPolicy.Pdec1Proposed);

        var exception = CaptureDomainRule(
            () => _ = left + right);

        Assert.AreEqual(
            "PRECISION_POLICY_MISMATCH",
            exception.Diagnostic.Code);
    }

    [TestMethod]
    public void R04_MoneyProjectionKeepsSelectedPrecisionPolicy()
    {
        var policy = PrecisionPolicy.Pdec1Proposed with
        {
            Version = "REVIEW-scale9",
            AmountScale = 9
        };

        var eur = new CurrencyCode("EUR");
        var metadata = new CurrencyMetadataSnapshot(
            eur,
            "978",
            9,
            true,
            "review",
            "review",
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 1));

        var projector = new MoneyCurrencyProjector(policy);

        var result = projector.Project(
            new Money(0.123456789m, eur, policy),
            [
                new(
                    new StableRoundingId("A"),
                    new Money(0.123456789m, eur, policy))
            ],
            metadata);

        Assert.AreEqual(
            0.123456789m,
            result.Target.Amount);
    }

    [TestMethod]
    public void R05_AllProductionDiagnosticKeysExistInBothPublishedCatalogs()
    {
        var keys = new[]
        {
            "AllocationKey.NegativeWeight",
            "AllocationKey.ZeroDriverTotal",
            "Currency.MinorUnitUndefined",
            "Currency.NotInCatalog",
            "Money.CurrencyMismatch",
            "Money.CurrencyInvalid",
            "Numeric.ConservationFailure",
            "Numeric.DuplicateDestination",
            "Numeric.InvalidQuantum",
            "Precision.MagnitudeExceeded",
            "Precision.PercentageOutOfRange",
            "Precision.ScaleExceeded",
            "Precision.PolicyMissing",
            "Precision.PolicyMismatch",
            "Quantity.UnitMismatch",
            "Time.LocalAmbiguous",
            "Time.LocalInvalid",
            "Time.LocalOffsetInvalid"
        };

        var catalog = new UiTextCatalog();

        foreach (var key in keys)
        {
            Assert.IsFalse(
                string.IsNullOrWhiteSpace(
                    catalog.Get(
                        key,
                        CultureInfo.GetCultureInfo("en"))),
                $"Missing English resource: {key}");

            Assert.IsFalse(
                string.IsNullOrWhiteSpace(
                    catalog.Get(
                        key,
                        CultureInfo.GetCultureInfo("es"))),
                $"Missing Spanish resource: {key}");
        }
    }

    [TestMethod]
    public void R06_NullNumberOptionsAreRejectedWithoutNullReference()
    {
        var invalid = UserPresentationPreferences.Default with
        {
            NumberDisplayOptions = null!
        };

        try
        {
            _ = new PresentationPreferencesService(
                new LanguageCatalog(),
                invalid);
            Assert.Fail("Null number options must be rejected.");
        }
        catch (Exception exception)
        {
            Assert.IsFalse(
                exception is NullReferenceException,
                "Validation must not fail with NullReferenceException.");
        }
    }

    [TestMethod]
    public void R06_UnknownDatePresetIsRejected()
    {
        var invalid = UserPresentationPreferences.Default with
        {
            DateDisplayPreset = (DateDisplayPreset)999
        };

        Assert.ThrowsExactly<PresentationPreferencesValidationException>(
            () => _ = new PresentationPreferencesService(
                new LanguageCatalog(),
                invalid));
    }

    [TestMethod]
    public void R06_UnknownTimeZoneIsRejected()
    {
        var invalid = UserPresentationPreferences.Default with
        {
            DisplayTimeZoneId = "Invalid/ReviewZone"
        };

        Assert.ThrowsExactly<PresentationPreferencesValidationException>(
            () => _ = new PresentationPreferencesService(
                new LanguageCatalog(),
                invalid));
    }

    private static DomainRuleException CaptureDomainRule(
        Action action)
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

}
