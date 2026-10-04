using Valdrio.Domain;

namespace Valdrio.Calculation;

public sealed class ProportionalAllocator
{
    private readonly PrecisionPolicy precisionPolicy;

    public ProportionalAllocator(PrecisionPolicy? precisionPolicy = null)
    {
        this.precisionPolicy =
            precisionPolicy ?? PrecisionPolicy.Pdec1Proposed;
    }

    public IReadOnlyList<RoundingContribution> FromWeights(
        decimal sourceTotal,
        IReadOnlyList<WeightedDestination> destinations)
    {
        precisionPolicy.ValidateAmount(
            sourceTotal,
            nameof(sourceTotal));

        if (destinations.Count == 0)
        {
            throw new ArgumentException(
                "At least one destination is required.",
                nameof(destinations));
        }

        var ids = new HashSet<StableRoundingId>();
        var weightTotal = 0m;

        foreach (var destination in destinations)
        {
            if (!ids.Add(destination.DestinationId))
            {
                throw RoundingDiagnostics.DuplicateDestination(
                    destination.DestinationId);
            }

            precisionPolicy.ValidateQuantity(
                destination.Weight,
                destination.DestinationId.Value);

            if (destination.Weight < 0m)
            {
                throw new DomainRuleException(
                    new DomainDiagnostic(
                        "KEY_NEGATIVE_DRIVER_WEIGHT",
                        "AllocationKey.NegativeWeight",
                        [
                            new(
                                "destinationId",
                                DiagnosticArgumentType.Identifier,
                                destination.DestinationId.Value),
                            new(
                                "weight",
                                DiagnosticArgumentType.Decimal,
                                CanonicalDecimal.Format(
                                    destination.Weight))
                        ]));
            }

            weightTotal = checked(
                weightTotal + destination.Weight);
        }

        if (weightTotal <= 0m)
        {
            throw new DomainRuleException(
                new DomainDiagnostic(
                    "KEY_ZERO_DRIVER_TOTAL",
                    "AllocationKey.ZeroDriverTotal",
                    []));
        }

        return destinations
            .Select(destination =>
                new RoundingContribution(
                    destination.DestinationId,
                    checked(
                        sourceTotal *
                        destination.Weight /
                        weightTotal)))
            .ToArray();
    }
}
