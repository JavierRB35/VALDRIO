namespace Valdrio.Domain;

public static class PrecisionPolicyCompatibility
{
    public static void EnsureSame(
        PrecisionPolicy? left,
        PrecisionPolicy? right,
        string leftRole,
        string rightRole)
    {
        if (left is null || right is null)
        {
            throw new DomainRuleException(
                new DomainDiagnostic(
                    "PRECISION_POLICY_MISSING",
                    "Precision.PolicyMissing",
                    [
                        new(
                            "leftRole",
                            DiagnosticArgumentType.Text,
                            leftRole),
                        new(
                            "rightRole",
                            DiagnosticArgumentType.Text,
                            rightRole)
                    ]));
        }

        if (left == right)
        {
            return;
        }

        throw new DomainRuleException(
            new DomainDiagnostic(
                "PRECISION_POLICY_MISMATCH",
                "Precision.PolicyMismatch",
                [
                    new(
                        "leftPolicy",
                        DiagnosticArgumentType.Text,
                        left.Version),
                    new(
                        "rightPolicy",
                        DiagnosticArgumentType.Text,
                        right.Version)
                ]));
    }
}
