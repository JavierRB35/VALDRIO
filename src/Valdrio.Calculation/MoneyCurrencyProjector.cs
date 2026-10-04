using Valdrio.Domain;

namespace Valdrio.Calculation;

public sealed record MoneyCurrencyProjectionInputLine(
    StableRoundingId DestinationId,
    Money LedgerMoney);

public sealed record MoneyCurrencyProjectionLine(
    StableRoundingId DestinationId,
    Money LedgerMoney,
    Money ProjectedMoney,
    Money ProjectionAdjustment);

public sealed record MoneyCurrencyProjectionResult(
    Money SourceLedger,
    Money Target,
    Money ProjectionDifference,
    CurrencyMetadataSnapshot CurrencyMetadata,
    IReadOnlyList<MoneyCurrencyProjectionLine> Lines)
{
    public Money ProjectedTotal =>
        Lines.Aggregate(
            new Money(0m, SourceLedger.Currency),
            (total, line) => total + line.ProjectedMoney);
}

public sealed class MoneyCurrencyProjector
{
    private readonly CurrencyProjector projector;

    public MoneyCurrencyProjector(
        PrecisionPolicy? precisionPolicy = null)
    {
        projector = new CurrencyProjector(precisionPolicy);
    }

    public MoneyCurrencyProjectionResult Project(
        Money sourceLedger,
        IReadOnlyList<MoneyCurrencyProjectionInputLine> ledgerLines,
        CurrencyMetadataSnapshot currencyMetadata)
    {
        EnsureCurrency(
            sourceLedger.Currency,
            currencyMetadata.AlphaCode,
            "sourceLedger",
            "currencyMetadata");

        foreach (var line in ledgerLines)
        {
            EnsureCurrency(
                line.LedgerMoney.Currency,
                sourceLedger.Currency,
                line.DestinationId.Value,
                "sourceLedger");
        }

        var result = projector.Project(
            sourceLedger.Amount,
            ledgerLines
                .Select(line =>
                    new CurrencyProjectionInputLine(
                        line.DestinationId,
                        line.LedgerMoney.Amount))
                .ToArray(),
            currencyMetadata.ReportQuantum);

        var wrappedLines = result.Lines
            .Select(line =>
                new MoneyCurrencyProjectionLine(
                    line.DestinationId,
                    new Money(
                        line.LedgerAmount,
                        sourceLedger.Currency),
                    new Money(
                        line.ProjectedAmount,
                        sourceLedger.Currency),
                    new Money(
                        line.ProjectionAdjustment,
                        sourceLedger.Currency)))
            .ToArray();

        return new MoneyCurrencyProjectionResult(
            sourceLedger,
            new Money(
                result.Target,
                sourceLedger.Currency),
            new Money(
                result.ProjectionDifference,
                sourceLedger.Currency),
            currencyMetadata,
            wrappedLines);
    }

    private static void EnsureCurrency(
        CurrencyCode left,
        CurrencyCode right,
        string leftName,
        string rightName)
    {
        if (left == right)
        {
            return;
        }

        throw new DomainRuleException(
            new DomainDiagnostic(
                "MONEY_CURRENCY_MISMATCH",
                "Money.CurrencyMismatch",
                [
                    new(
                        leftName,
                        DiagnosticArgumentType.CurrencyCode,
                        left.Value),
                    new(
                        rightName,
                        DiagnosticArgumentType.CurrencyCode,
                        right.Value)
                ]));
    }
}
