using Valdrio.Calculation;
using Valdrio.Domain;

namespace Valdrio.Application;

public sealed record CurrencyMetadataExampleRow(
    string AlphaCode,
    string NumericCode,
    int MinorUnitDigits,
    string Quantum,
    string LedgerAmount,
    string ProjectedDistribution,
    string CatalogVersion);

public sealed class CurrencyMetadataExampleService
{
    private readonly ICurrencyCatalog currencyCatalog;

    public CurrencyMetadataExampleService(
        ICurrencyCatalog currencyCatalog)
    {
        this.currencyCatalog = currencyCatalog;
    }

    public IReadOnlyList<CurrencyMetadataExampleRow> Build()
    {
        return
        [
            BuildProjection(
                new CurrencyCode("JPY"),
                101m),
            BuildProjection(
                new CurrencyCode("KWD"),
                0.001m)
        ];
    }

    private CurrencyMetadataExampleRow BuildProjection(
        CurrencyCode currency,
        decimal sourceAmount)
    {
        var metadata = currencyCatalog
            .GetRequired(currency)
            .Freeze();

        var allocator = new ProportionalAllocator();
        var rounder = new BalancedRounder();
        var projector = new MoneyCurrencyProjector();

        var raw = allocator.FromWeights(
            sourceAmount,
            [
                new(new StableRoundingId("A"), 1m),
                new(new StableRoundingId("B"), 1m),
                new(new StableRoundingId("C"), 1m)
            ]);

        var ledger = rounder.Round(
            sourceAmount,
            raw,
            PrecisionPolicy.Pdec1Proposed.InternalLedgerQuantum);

        var projection = projector.Project(
            new Money(sourceAmount, currency),
            ledger.Lines
                .Select(line =>
                    new MoneyCurrencyProjectionInputLine(
                        line.DestinationId,
                        new Money(
                            line.PostedAmount,
                            currency)))
                .ToArray(),
            metadata);

        var distribution = string.Join(
            "/",
            projection.Lines
                .OrderBy(
                    line => line.DestinationId.Value,
                    StringComparer.Ordinal)
                .Select(line =>
                    CanonicalDecimal.Format(
                        line.ProjectedMoney.Amount)));

        return new CurrencyMetadataExampleRow(
            metadata.AlphaCode.Value,
            metadata.NumericCode,
            metadata.MinorUnitDigits,
            CanonicalDecimal.Format(metadata.ReportQuantum),
            CanonicalDecimal.Format(sourceAmount),
            distribution,
            metadata.CatalogVersion);
    }
}
