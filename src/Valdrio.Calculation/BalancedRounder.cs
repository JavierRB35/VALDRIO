using Valdrio.Domain;

namespace Valdrio.Calculation;

public sealed class BalancedRounder
{
    private readonly PrecisionPolicy precisionPolicy;

    public BalancedRounder(PrecisionPolicy? precisionPolicy = null)
    {
        this.precisionPolicy =
            precisionPolicy ?? PrecisionPolicy.Pdec1Proposed;
    }

    public BalancedRoundResult Round(
        decimal sourceTotal,
        IReadOnlyList<RoundingContribution> contributions,
        decimal quantum)
    {
        precisionPolicy.ValidateAmount(sourceTotal, nameof(sourceTotal));

        if (quantum <= 0m)
        {
            throw RoundingDiagnostics.InvalidQuantum(quantum);
        }

        if (CanonicalDecimal.Scale(quantum) >
            precisionPolicy.AmountScale)
        {
            throw RoundingDiagnostics.InvalidQuantum(quantum);
        }

        var sourceUnits = sourceTotal / quantum;
        if (sourceUnits != decimal.Truncate(sourceUnits))
        {
            throw RoundingDiagnostics.InvalidQuantum(quantum);
        }

        if (contributions.Count == 0)
        {
            throw new ArgumentException(
                "At least one contribution is required.",
                nameof(contributions));
        }

        EnsureUnique(contributions);

        foreach (var line in contributions)
        {
            precisionPolicy.ValidateMagnitude(
                line.RawAmount,
                line.DestinationId.Value);
        }

        var rawTotal = Sum(
            contributions.Select(line => line.RawAmount));

        var difference = checked(sourceTotal - rawTotal);

        var numericAdjustments = contributions
            .ToDictionary(
                line => line.DestinationId,
                _ => 0m);

        var corrected = contributions
            .ToDictionary(
                line => line.DestinationId,
                line => line.RawAmount);

        if (difference != 0m)
        {
            if (Absolute(difference) >
                precisionPolicy.MaximumNumericCorrection)
            {
                throw RoundingDiagnostics.ConservationFailure(
                    sourceTotal,
                    rawTotal);
            }

            var correctionTarget = contributions
                .OrderByDescending(line => Absolute(line.RawAmount))
                .ThenBy(
                    line => line.DestinationId.Value,
                    StringComparer.Ordinal)
                .First();

            corrected[correctionTarget.DestinationId] =
                checked(
                    corrected[correctionTarget.DestinationId] +
                    difference);

            numericAdjustments[correctionTarget.DestinationId] =
                difference;
        }

        var correctedTotal = Sum(corrected.Values);
        if (correctedTotal != sourceTotal)
        {
            throw RoundingDiagnostics.ConservationFailure(
                sourceTotal,
                correctedTotal);
        }

        var posted = LargestRemainderCore.Round(
            signReference: sourceTotal,
            target: sourceTotal,
            lines: corrected
                .Select(pair => (pair.Key, pair.Value))
                .ToArray(),
            quantum,
            pendingMayEqualLineCount: false);

        var resultLines = contributions
            .OrderBy(
                line => line.DestinationId.Value,
                StringComparer.Ordinal)
            .Select(line =>
            {
                var numeric =
                    numericAdjustments[line.DestinationId];
                var postedAmount =
                    posted[line.DestinationId];
                var rounding = checked(
                    postedAmount -
                    line.RawAmount -
                    numeric);

                precisionPolicy.ValidateAmount(
                    postedAmount,
                    line.DestinationId.Value);

                return new RoundedContribution(
                    line.DestinationId,
                    line.RawAmount,
                    numeric,
                    rounding,
                    postedAmount);
            })
            .ToArray();

        var result = new BalancedRoundResult(
            sourceTotal,
            quantum,
            resultLines);

        if (result.PostedTotal != sourceTotal)
        {
            throw RoundingDiagnostics.ConservationFailure(
                sourceTotal,
                result.PostedTotal);
        }

        return result;
    }

    private static decimal Sum(IEnumerable<decimal> values)
    {
        var total = 0m;

        foreach (var value in values)
        {
            total = checked(total + value);
        }

        return total;
    }

    private static decimal Absolute(decimal value) =>
        value < 0m ? checked(-value) : value;

    private static void EnsureUnique(
        IEnumerable<RoundingContribution> lines)
    {
        var ids = new HashSet<StableRoundingId>();

        foreach (var line in lines)
        {
            if (!ids.Add(line.DestinationId))
            {
                throw RoundingDiagnostics.DuplicateDestination(
                    line.DestinationId);
            }
        }
    }
}
