using Valdrio.Application;
using Valdrio.Calculation;
using Valdrio.Domain;
using Valdrio.Infrastructure;

namespace Valdrio.Runner;

internal static class T02ACurrencyVerifier
{
    public static IReadOnlyDictionary<string, string> Verify(
        string catalogPath)
    {
        var catalog = new JsonCurrencyCatalog(catalogPath);
        var snapshots = new CurrencyMetadataSnapshotService(catalog);

        var jpy = snapshots.Freeze(new CurrencyCode("JPY"));
        var kwd = snapshots.Freeze(new CurrencyCode("KWD"));

        var jpyResult = ProjectWeighted(
            new Money(101m, new CurrencyCode("JPY")),
            jpy);

        var kwdResult = ProjectWeighted(
            new Money(0.001m, new CurrencyCode("KWD")),
            kwd);

        if (Amount(jpyResult, "A") != 34m
            || Amount(jpyResult, "B") != 34m
            || Amount(jpyResult, "C") != 33m)
        {
            throw new InvalidOperationException(
                "I06/C38 JPY projection differs from reference.");
        }

        if (Amount(kwdResult, "A") != 0.001m
            || Amount(kwdResult, "B") != 0m
            || Amount(kwdResult, "C") != 0m)
        {
            throw new InvalidOperationException(
                "I07/C39 KWD projection differs from reference.");
        }

        var mismatchCode = VerifyCurrencyMismatch();

        return new Dictionary<string, string>
        {
            ["catalog_version"] = catalog.CatalogVersion,
            ["jpy_quantum"] = CanonicalDecimal.Format(jpy.ReportQuantum),
            ["jpy_projection"] = "34/34/33",
            ["kwd_quantum"] = CanonicalDecimal.Format(kwd.ReportQuantum),
            ["kwd_projection"] = "0.001/0/0",
            ["i13"] = mismatchCode
        };
    }

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

    private static string VerifyCurrencyMismatch()
    {
        try
        {
            _ =
                new Money(10m, new CurrencyCode("EUR")) +
                new Money(10m, new CurrencyCode("USD"));
        }
        catch (DomainRuleException exception)
        {
            if (exception.Diagnostic.Code ==
                "MONEY_CURRENCY_MISMATCH")
            {
                return exception.Diagnostic.Code;
            }

            throw;
        }

        throw new InvalidOperationException(
            "I13 did not reject nominal cross-currency addition.");
    }
}
