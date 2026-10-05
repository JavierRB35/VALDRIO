using Valdrio.Application;
using Valdrio.Calculation;
using Valdrio.Domain;

namespace Valdrio.Tests;

[TestClass]
public sealed class T02AFxTests
{
    private static readonly CurrencyCode Eur = new("EUR");
    private static readonly CurrencyCode Usd = new("USD");
    private static readonly CurrencyCode Jpy = new("JPY");

    [TestMethod]
    public void C33_UsdToEur_UsesExplicitDestinationPerSourceQuote()
    {
        var rate = Rate("r-090", Usd, Eur, 0.9m, "rs-1");
        var result = new FxConverter().Convert(
            "app-c33",
            new Money(100m, Usd),
            Eur,
            rate);

        Assert.AreEqual(100m, result.OriginalMoney.Amount);
        Assert.AreEqual("USD", result.OriginalMoney.Currency.Value);
        Assert.AreEqual(90m, result.ConvertedOriginalMoney.Amount);
        Assert.AreEqual("EUR", result.ConvertedOriginalMoney.Currency.Value);
        Assert.AreEqual(0m, result.ConversionRoundingAdjustment.Amount);
        Assert.AreEqual("r-090", result.RateId?.Value);
        Assert.AreEqual("rs-1", result.RateSetVersionId);
    }

    [TestMethod]
    public void C34_SameCurrency_IsIdentityWithoutRateSet()
    {
        var result = new FxConverter().Convert(
            "app-c34",
            new Money(100m, Eur),
            Eur,
            selectedRate: null);

        Assert.IsTrue(result.IsIdentity);
        Assert.AreEqual(100m, result.ConvertedOriginalMoney.Amount);
        Assert.AreEqual(1m, result.Quote);
        Assert.IsNull(result.RateId);
        Assert.IsNull(result.RateSetVersionId);
    }

    [TestMethod]
    public void C35_JpyToEur_DoesNotAssumeSourceMinorUnits()
    {
        var result = new FxConverter().Convert(
            "app-c35",
            new Money(1000m, Jpy),
            Eur,
            Rate("r-jpy", Jpy, Eur, 0.006m, "rs-1"));

        Assert.AreEqual(1000m, result.OriginalMoney.Amount);
        Assert.AreEqual(6m, result.ConvertedOriginalMoney.Amount);
    }

    [TestMethod]
    public void C36_MissingCrossCurrencyRate_Blocks()
    {
        var exception = CaptureDomainRule(
            () => new FxConverter().Convert(
                "app-c36",
                new Money(100m, Usd),
                Eur,
                selectedRate: null));

        Assert.AreEqual("FX_RATE_MISSING", exception.Diagnostic.Code);
    }

    [TestMethod]
    public void C37_NegativeOriginal_PreservesOriginAndSign()
    {
        var result = new FxConverter().Convert(
            "app-c37",
            new Money(-100m, Usd),
            Eur,
            Rate("r-neg", Usd, Eur, 0.9m, "rs-1"));

        Assert.AreEqual(-100m, result.OriginalMoney.Amount);
        Assert.AreEqual(-90m, result.ConvertedOriginalMoney.Amount);
        Assert.AreEqual(0m, result.ConversionRoundingAdjustment.Amount);
    }

    [TestMethod]
    public void Conversion_RoundsEachAmountToScaleEight_MidpointToEven_AndRecordsBridge()
    {
        var result = new FxConverter().Convert(
            "app-round",
            new Money(1m, Usd),
            Eur,
            Rate(
                "r-round",
                Usd,
                Eur,
                1.234567895m,
                "rs-round"));

        Assert.AreEqual(1.234567895m, result.ConvertedRaw);
        Assert.AreEqual(1.23456790m, result.ConvertedOriginalMoney.Amount);
        Assert.AreEqual(
            0.000000005m,
            result.ConversionRoundingAdjustment.Amount);
    }

    [TestMethod]
    public void RateMustBePositiveAndAtMostEighteenDecimals()
    {
        var zero = CaptureDomainRule(
            () => _ = Rate("zero", Usd, Eur, 0m, "rs"));
        Assert.AreEqual("FX_RATE_INVALID", zero.Diagnostic.Code);

        var tooPrecise = CaptureDomainRule(
            () => _ = Rate(
                "scale19",
                Usd,
                Eur,
                0.1234567890123456789m,
                "rs"));
        Assert.AreEqual("FX_RATE_INVALID", tooPrecise.Diagnostic.Code);
    }

    [TestMethod]
    public void ExplicitRateOrientationMustMatchOriginalAndFunctionalCurrencies()
    {
        var inverse = Rate("inverse", Eur, Usd, 1.1m, "rs");

        var exception = CaptureDomainRule(
            () => new FxConverter().Convert(
                "app-orientation",
                new Money(100m, Usd),
                Eur,
                inverse));

        Assert.AreEqual(
            "FX_RATE_ORIENTATION_INVALID",
            exception.Diagnostic.Code);
    }

    [TestMethod]
    public void I14_NewRateSetProducesNewConversionAndHashWithoutMutatingOldApplication()
    {
        var oldSet = RateSet(
            "rs-old",
            Rate("old", Usd, Eur, 0.9m, "rs-old"));
        var newSet = RateSet(
            "rs-new",
            Rate("new", Usd, Eur, 0.95m, "rs-new"));

        var converter = new FxConverter();
        var original = new Money(100m, Usd);

        var oldApplication = converter.Convert(
            "old-run",
            original,
            Eur,
            oldSet.GetRequired(new FxRateId("old")));
        var oldHash = FxConversionHash.Compute(oldApplication);

        var newApplication = converter.Convert(
            "new-run",
            original,
            Eur,
            newSet.GetRequired(new FxRateId("new")));
        var newHash = FxConversionHash.Compute(newApplication);

        Assert.AreEqual(90m, oldApplication.ConvertedOriginalMoney.Amount);
        Assert.AreEqual(95m, newApplication.ConvertedOriginalMoney.Amount);
        Assert.AreEqual(100m, oldApplication.OriginalMoney.Amount);
        Assert.AreEqual(100m, newApplication.OriginalMoney.Amount);
        Assert.AreEqual(0.9m, oldApplication.Quote);
        Assert.AreEqual("rs-old", oldApplication.RateSetVersionId);
        Assert.AreNotEqual(oldHash, newHash);
    }

    [TestMethod]
    public void RevalueStudy_ValidatesBeforeAtomicCommitAndCreatesNewRevision()
    {
        var repository = new RecordingRepository();
        var validator = new RecordingValidator();
        var service = new RevalueStudyService(validator, repository);

        var result = service.RevalueStudy(
            new RevalueStudyCommand(
                "study-1",
                ExpectedRevision: 7,
                Eur,
                RateSet(
                    "rs-95",
                    Rate("usd-eur-95", Usd, Eur, 0.95m, "rs-95")),
                [
                    new(
                        "res-1",
                        new Money(100m, Usd),
                        new FxRateId("usd-eur-95"))
                ]));

        Assert.IsTrue(validator.Called);
        Assert.AreEqual(1, repository.CommitCount);
        Assert.AreEqual(8L, result.Revision);
        Assert.AreEqual("rs-95", result.RateSetVersionId);
        Assert.AreEqual(
            95m,
            repository.LastCandidate!.Items[0]
                .RateApplication.ConvertedOriginalMoney.Amount);
    }

    [TestMethod]
    public void RevalueStudy_ValidationFailureLeavesRepositoryUnchanged()
    {
        var repository = new RecordingRepository();
        var validator = new FailingValidator();
        var service = new RevalueStudyService(validator, repository);

        Assert.ThrowsExactly<InvalidOperationException>(
            () => service.RevalueStudy(
                new RevalueStudyCommand(
                    "study-1",
                    7,
                    Eur,
                    RateSet(
                        "rs-95",
                        Rate("usd-eur-95", Usd, Eur, 0.95m, "rs-95")),
                    [
                        new(
                            "res-1",
                            new Money(100m, Usd),
                            new FxRateId("usd-eur-95"))
                    ])));

        Assert.AreEqual(0, repository.CommitCount);
    }

    [TestMethod]
    public void FxConversion_PreservesOriginalPrecisionPolicy()
    {
        var policy = PrecisionPolicy.Pdec1Proposed with
        {
            Version = "PDEC-FX-TEST"
        };

        var original = new Money(100m, Usd, policy);
        var result = new FxConverter().Convert(
            "precision",
            original,
            Eur,
            Rate("precision-rate", Usd, Eur, 0.9m, "rs-precision"));

        Assert.AreEqual(
            "PDEC-FX-TEST",
            result.ConvertedOriginalMoney.PrecisionPolicy.Version);
        Assert.AreSame(
            policy,
            result.ConvertedOriginalMoney.PrecisionPolicy);
    }

    private static FxRate Rate(
        string id,
        CurrencyCode source,
        CurrencyCode target,
        decimal quote,
        string rateSetVersion) =>
        new(
            new FxRateId(id),
            source,
            target,
            quote,
            BusinessDate.ParseIso("2026-10-04"),
            "manual-test",
            rateSetVersion);

    private static FxRateSetVersion RateSet(
        string version,
        params FxRate[] rates) =>
        new(
            version,
            BusinessDate.ParseIso("2026-10-04"),
            "manual-test",
            rates);

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

    private sealed class RecordingValidator : IStudyRevaluationValidator
    {
        public bool Called { get; private set; }

        public void ValidatePartitionsAndConsumptions(
            RevalueStudyCandidate candidate)
        {
            Called = true;
        }
    }

    private sealed class FailingValidator : IStudyRevaluationValidator
    {
        public void ValidatePartitionsAndConsumptions(
            RevalueStudyCandidate candidate) =>
            throw new InvalidOperationException(
                "Partitions require user review.");
    }

    private sealed class RecordingRepository : IStudyRevaluationRepository
    {
        public int CommitCount { get; private set; }
        public RevalueStudyCandidate? LastCandidate { get; private set; }

        public RevalueStudyCommitResult CommitAtomically(
            RevalueStudyCandidate candidate)
        {
            CommitCount++;
            LastCandidate = candidate;
            return new RevalueStudyCommitResult(
                candidate.StudyId,
                candidate.NewRevision,
                candidate.RateSetVersionId);
        }
    }
}
