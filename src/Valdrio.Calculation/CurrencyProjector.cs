using Valdrio.Domain;

namespace Valdrio.Calculation;

public sealed class CurrencyProjector
{
    private readonly PrecisionPolicy precisionPolicy;

    public CurrencyProjector(PrecisionPolicy? precisionPolicy = null)
    {
        this.precisionPolicy =
            precisionPolicy ?? PrecisionPolicy.Pdec1Proposed;
    }

    public CurrencyProjectionResult Project(
        decimal sourceLedger,
        IReadOnlyList<CurrencyProjectionInputLine> ledgerLines,
        decimal quantum)
    {
        precisionPolicy.ValidateAmount(
            sourceLedger,
            nameof(sourceLedger));

        if (quantum <= 0m)
        {
            throw RoundingDiagnostics.InvalidQuantum(quantum);
        }

        if (ledgerLines.Count == 0)
        {
            throw new ArgumentException(
                "At least one ledger contribution is required.",
                nameof(ledgerLines));
        }

        var ids = new HashSet<StableRoundingId>();
        var ledgerTotal = 0m;

        foreach (var line in ledgerLines)
        {
            if (!ids.Add(line.DestinationId))
            {
                throw RoundingDiagnostics.DuplicateDestination(
                    line.DestinationId);
            }

            precisionPolicy.ValidateAmount(
                line.LedgerAmount,
                line.DestinationId.Value);

            ledgerTotal = checked(
                ledgerTotal + line.LedgerAmount);
        }

        if (ledgerTotal != sourceLedger)
        {
            throw RoundingDiagnostics.ConservationFailure(
                sourceLedger,
                ledgerTotal);
        }

        var target = RoundToQuantum(
            sourceLedger,
            quantum);

        var projected = LargestRemainderCore.Round(
            signReference: sourceLedger,
            target,
            lines: ledgerLines
                .Select(line =>
                    (line.DestinationId, line.LedgerAmount))
                .ToArray(),
            quantum,
            pendingMayEqualLineCount: true);

        var lines = ledgerLines
            .OrderBy(
                line => line.DestinationId.Value,
                StringComparer.Ordinal)
            .Select(line =>
            {
                var projectedAmount =
                    projected[line.DestinationId];

                return new CurrencyProjectionLine(
                    line.DestinationId,
                    line.LedgerAmount,
                    projectedAmount,
                    checked(
                        projectedAmount -
                        line.LedgerAmount));
            })
            .ToArray();

        var result = new CurrencyProjectionResult(
            sourceLedger,
            target,
            checked(target - sourceLedger),
            quantum,
            lines);

        if (result.ProjectedTotal != target)
        {
            throw RoundingDiagnostics.ConservationFailure(
                target,
                result.ProjectedTotal);
        }

        return result;
    }

    public static decimal RoundToQuantum(
        decimal value,
        decimal quantum)
    {
        if (quantum <= 0m)
        {
            throw RoundingDiagnostics.InvalidQuantum(quantum);
        }

        var units = value / quantum;
        var targetUnits = decimal.Round(
            units,
            0,
            MidpointRounding.ToEven);

        return checked(targetUnits * quantum);
    }
}
