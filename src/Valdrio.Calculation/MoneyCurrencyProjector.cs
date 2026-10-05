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

public sealed record MoneyCurrencyProjectionResult
{
    public MoneyCurrencyProjectionResult(Money SourceLedger, Money Target, Money ProjectionDifference,
        CurrencyMetadataSnapshot CurrencyMetadata, IReadOnlyList<MoneyCurrencyProjectionLine> Lines)
    {
        this.SourceLedger = SourceLedger;
        this.Target = Target;
        this.ProjectionDifference = ProjectionDifference;
        this.CurrencyMetadata = CurrencyMetadata;
        this.Lines = FrozenList.Copy(Lines);
    }
    public Money SourceLedger { get; }
    public Money Target { get; }
    public Money ProjectionDifference { get; }
    public CurrencyMetadataSnapshot CurrencyMetadata { get; }
    public IReadOnlyList<MoneyCurrencyProjectionLine> Lines { get; }
    public Money ProjectedTotal =>
        Lines.Aggregate(
            new Money(
                0m,
                SourceLedger.Currency,
                SourceLedger.PrecisionPolicy),
            (total, line) => total + line.ProjectedMoney);
}

public sealed class MoneyCurrencyProjector
{
    private readonly PrecisionPolicy precisionPolicy;
    private readonly CurrencyProjector projector;

    public MoneyCurrencyProjector(
        PrecisionPolicy? precisionPolicy = null)
    {
        this.precisionPolicy =
            precisionPolicy ?? PrecisionPolicy.Pdec1Proposed;
        projector = new CurrencyProjector(this.precisionPolicy);
    }

    public MoneyCurrencyProjectionResult Project(
        Money sourceLedger,
        IReadOnlyList<MoneyCurrencyProjectionInputLine> ledgerLines,
        CurrencyMetadataSnapshot currencyMetadata)
    {
        sourceLedger.EnsureValid("sourceLedger");

        PrecisionPolicyCompatibility.EnsureSame(
            sourceLedger.PrecisionPolicy,
            precisionPolicy,
            "sourceLedger",
            "projector");

        EnsureCurrency(
            sourceLedger.Currency,
            currencyMetadata.AlphaCode,
            "sourceLedger",
            "currencyMetadata");

        foreach (var line in ledgerLines)
        {
            line.LedgerMoney.EnsureValid(
                line.DestinationId.Value);

            EnsureCurrency(
                line.LedgerMoney.Currency,
                sourceLedger.Currency,
                line.DestinationId.Value,
                "sourceLedger");

            PrecisionPolicyCompatibility.EnsureSame(
                line.LedgerMoney.PrecisionPolicy,
                precisionPolicy,
                line.DestinationId.Value,
                "projector");
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
                        sourceLedger.Currency,
                        precisionPolicy),
                    new Money(
                        line.ProjectedAmount,
                        sourceLedger.Currency,
                        precisionPolicy),
                    new Money(
                        line.ProjectionAdjustment,
                        sourceLedger.Currency,
                        precisionPolicy)))
            .ToArray();

        return new MoneyCurrencyProjectionResult(
            sourceLedger,
            new Money(
                result.Target,
                sourceLedger.Currency,
                precisionPolicy),
            new Money(
                result.ProjectionDifference,
                sourceLedger.Currency,
                precisionPolicy),
            currencyMetadata,
            wrappedLines);
    }

    private static void EnsureCurrency(
        CurrencyCode left,
        CurrencyCode right,
        string leftName,
        string rightName)
    {
        if (!left.IsValid || !right.IsValid)
        {
            throw new DomainRuleException(
                new DomainDiagnostic(
                    "MONEY_CURRENCY_INVALID",
                    "Money.CurrencyInvalid",
                    [
                        new(
                            "role",
                            DiagnosticArgumentType.Text,
                            !left.IsValid ? leftName : rightName)
                    ]));
        }

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
