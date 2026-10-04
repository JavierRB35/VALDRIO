namespace Valdrio.Domain;

public static class PrecisionPolicyCompatibility
{
    public static void EnsureSame(
        PrecisionPolicy? left,
        PrecisionPolicy? right,
        string leftRole,
        string rightRole)
    {
        if (left is null)
        {
            throw Missing(leftRole);
        }

        if (right is null)
        {
            throw Missing(rightRole);
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

    private static DomainRuleException Missing(
        string role) =>
        new(
            new DomainDiagnostic(
                "PRECISION_POLICY_MISSING",
                "Precision.PolicyMissing",
                [
                    new(
                        "role",
                        DiagnosticArgumentType.Text,
                        role)
                ]));
}
