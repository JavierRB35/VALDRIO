using Valdrio.Application;
using Valdrio.Calculation;
using Valdrio.Domain;
using Valdrio.Infrastructure;

namespace Valdrio.Tests;

[TestClass]
public sealed class T02ACurrencyMetadataTests
{
    [TestMethod]
    public void ExampleCatalog_MatchesPackageFixtureExactly()
    {
        var catalog = LoadCatalog();

        Assert.AreEqual(
            "fixture-six-list-one-2026-09-17",
            catalog.CatalogVersion);
        Assert.AreEqual("SIX List One XML", catalog.Source);
        Assert.AreEqual(
            new DateOnly(2026, 9, 17),
            catalog.SourcePublicationDate);
        Assert.AreEqual(
            new DateOnly(2026, 10, 4),
            catalog.CheckedOn);
        Assert.AreEqual(4, catalog.GetAll().Count);

        AssertCurrency(catalog, "EUR", "978", 2);
        AssertCurrency(catalog, "USD", "840", 2);
        AssertCurrency(catalog, "JPY", "392", 0);
        AssertCurrency(catalog, "KWD", "414", 3);
    }

    [TestMethod]
    public void I06_JpyProjection_UsesFrozenZeroDigitQuantum()
    {
        var catalog = LoadCatalog();
        var snapshot = new CurrencyMetadataSnapshotService(catalog)
            .Freeze(new CurrencyCode("JPY"));

        Assert.AreEqual(0, snapshot.MinorUnitDigits);
        Assert.AreEqual(1m, snapshot.ReportQuantum);

        var result = ProjectWeighted(
            new Money(101m, new CurrencyCode("JPY")),
            snapshot);

        Assert.AreEqual(34m, Amount(result, "A"));
        Assert.AreEqual(34m, Amount(result, "B"));
        Assert.AreEqual(33m, Amount(result, "C"));
        Assert.AreEqual(101m, result.ProjectedTotal.Amount);
        Assert.AreEqual("JPY", result.ProjectedTotal.Currency.Value);
    }

    [TestMethod]
    public void I07_KwdProjection_UsesFrozenThreeDigitQuantum()
    {
        var catalog = LoadCatalog();
        var snapshot = new CurrencyMetadataSnapshotService(catalog)
            .Freeze(new CurrencyCode("KWD"));

        Assert.AreEqual(3, snapshot.MinorUnitDigits);
        Assert.AreEqual(0.001m, snapshot.ReportQuantum);

        var result = ProjectWeighted(
            new Money(0.001m, new CurrencyCode("KWD")),
            snapshot);

        Assert.AreEqual(0.001m, Amount(result, "A"));
        Assert.AreEqual(0m, Amount(result, "B"));
        Assert.AreEqual(0m, Amount(result, "C"));
        Assert.AreEqual(0.001m, result.ProjectedTotal.Amount);
    }

    [TestMethod]
    public void I13_MoneyDoesNotAddDifferentCurrencies()
    {
        var exception = CaptureDomainRule(
            () => _ =
                new Money(10m, new CurrencyCode("EUR")) +
                new Money(10m, new CurrencyCode("USD")));

        Assert.AreEqual(
            "MONEY_CURRENCY_MISMATCH",
            exception.Diagnostic.Code);
    }

    [TestMethod]
    public void FrozenMetadata_DoesNotChangeWhenAnotherDefinitionExists()
    {
        var jpyV1 = new CurrencyDefinition(
            new CurrencyCode("JPY"),
            "392",
            0,
            true,
            new CurrencyValidity(null, null),
            "fixture-v1",
            "fixture",
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 1));

        var frozen = jpyV1.Freeze();

        _ = new CurrencyDefinition(
            new CurrencyCode("JPY"),
            "392",
            2,
            true,
            new CurrencyValidity(null, null),
            "fixture-v2",
            "fixture",
            new DateOnly(2026, 2, 1),
            new DateOnly(2026, 2, 1));

        Assert.AreEqual("fixture-v1", frozen.CatalogVersion);
        Assert.AreEqual(0, frozen.MinorUnitDigits);
        Assert.AreEqual(1m, frozen.ReportQuantum);
    }

    [TestMethod]
    public void StudyCurrencyContext_RequiresSnapshotForSameFunctionalCurrency()
    {
        var catalog = LoadCatalog();
        var snapshot = new CurrencyMetadataSnapshotService(catalog)
            .Freeze(new CurrencyCode("EUR"));

        var context = new StudyCurrencyContext(
            new CurrencyCode("EUR"),
            snapshot);

        Assert.AreEqual(
            "EUR",
            context.FunctionalCurrencyCode.Value);
        Assert.AreEqual(
            catalog.CatalogVersion,
            context.CurrencyCatalogVersion);

        var mismatch = CaptureDomainRule(
            () => _ = new StudyCurrencyContext(
                new CurrencyCode("USD"),
                snapshot));

        Assert.AreEqual(
            "MONEY_CURRENCY_MISMATCH",
            mismatch.Diagnostic.Code);
    }

    [TestMethod]
    public void ProjectionRejectsMetadataFromAnotherCurrency()
    {
        var catalog = LoadCatalog();
        var eur = new CurrencyMetadataSnapshotService(catalog)
            .Freeze(new CurrencyCode("EUR"));

        var projector = new MoneyCurrencyProjector();

        var exception = CaptureDomainRule(
            () => projector.Project(
                new Money(1m, new CurrencyCode("USD")),
                [
                    new(
                        new StableRoundingId("A"),
                        new Money(1m, new CurrencyCode("USD")))
                ],
                eur));

        Assert.AreEqual(
            "MONEY_CURRENCY_MISMATCH",
            exception.Diagnostic.Code);
    }

    private static JsonCurrencyCatalog LoadCatalog() =>
        new(
            Path.Combine(
                AppContext.BaseDirectory,
                "reference",
                "currency-catalog-example.json"));

    private static MoneyCurrencyProjectionResult ProjectWeighted(
        Money source,
        CurrencyMetadataSnapshot metadata)
    {
        var allocator = new ProportionalAllocator();
        var rounder = new BalancedRounder();
        var projector = new MoneyCurrencyProjector();

        var raw = allocator.FromWeights(
            source.Amount,
            [
                new(new StableRoundingId("A"), 1m),
                new(new StableRoundingId("B"), 1m),
                new(new StableRoundingId("C"), 1m)
            ]);

        var ledger = rounder.Round(
            source.Amount,
            raw,
            PrecisionPolicy.Pdec1Proposed.InternalLedgerQuantum);

        return projector.Project(
            source,
            ledger.Lines
                .Select(line =>
                    new MoneyCurrencyProjectionInputLine(
                        line.DestinationId,
                        new Money(
                            line.PostedAmount,
                            source.Currency)))
                .ToArray(),
            metadata);
    }

    private static decimal Amount(
        MoneyCurrencyProjectionResult result,
        string id) =>
        result.Lines.Single(
            line => line.DestinationId.Value == id)
        .ProjectedMoney.Amount;

    private static void AssertCurrency(
        JsonCurrencyCatalog catalog,
        string alpha,
        string numeric,
        int minorDigits)
    {
        var definition =
            catalog.GetRequired(new CurrencyCode(alpha));

        Assert.AreEqual(numeric, definition.NumericCode);
        Assert.AreEqual(
            minorDigits,
            definition.MinorUnitDigits);
        Assert.IsTrue(definition.IsMonetary);
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
