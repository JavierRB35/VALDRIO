using Valdrio.Domain;

namespace Valdrio.Calculation;

internal static class RoundingDiagnostics
{
    public static DomainRuleException ConservationFailure(
        decimal expected,
        decimal actual) =>
        new(
            new DomainDiagnostic(
                "NUM_CONSERVATION_FAILURE",
                "Numeric.ConservationFailure",
                [
                    new(
                        "expected",
                        DiagnosticArgumentType.Decimal,
                        CanonicalDecimal.Format(expected)),
                    new(
                        "actual",
                        DiagnosticArgumentType.Decimal,
                        CanonicalDecimal.Format(actual)),
                    new(
                        "difference",
                        DiagnosticArgumentType.Decimal,
                        CanonicalDecimal.Format(checked(expected - actual)))
                ]));

    public static DomainRuleException InvalidQuantum(decimal quantum) =>
        new(
            new DomainDiagnostic(
                "NUM_INVALID_QUANTUM",
                "Numeric.InvalidQuantum",
                [
                    new(
                        "quantum",
                        DiagnosticArgumentType.Decimal,
                        CanonicalDecimal.Format(quantum))
                ]));

    public static DomainRuleException DuplicateDestination(
        StableRoundingId destinationId) =>
        new(
            new DomainDiagnostic(
                "NUM_DUPLICATE_DESTINATION",
                "Numeric.DuplicateDestination",
                [
                    new(
                        "destinationId",
                        DiagnosticArgumentType.Identifier,
                        destinationId.Value)
                ]));
}
