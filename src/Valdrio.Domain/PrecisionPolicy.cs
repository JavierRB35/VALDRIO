namespace Valdrio.Domain;

public sealed record PrecisionPolicy(
    string Version,
    int AmountScale,
    int QuantityScale,
    int PercentageScale,
    decimal MaximumInitialMagnitude,
    decimal MaximumNumericCorrection)
{
    public static PrecisionPolicy Pdec1Proposed { get; } =
        new(
            Version: "PDEC-1-proposed",
            AmountScale: 8,
            QuantityScale: 12,
            PercentageScale: 18,
            MaximumInitialMagnitude: 1_000_000_000_000m,
            MaximumNumericCorrection: 0.00000001m);

    public decimal InternalLedgerQuantum =>
        DecimalQuantum(AmountScale);

    public void ValidateAmount(decimal value, string fieldName) =>
        Validate(value, AmountScale, fieldName);

    public void ValidateQuantity(decimal value, string fieldName) =>
        Validate(value, QuantityScale, fieldName);

    public void ValidatePercentage(decimal value, string fieldName)
    {
        Validate(value, PercentageScale, fieldName);

        if (value is < 0m or > 100m)
        {
            throw Diagnostic(
                "NUM_PERCENTAGE_OUT_OF_RANGE",
                "Precision.PercentageOutOfRange",
                new DiagnosticArgument(
                    fieldName,
                    DiagnosticArgumentType.Decimal,
                    CanonicalDecimal.Format(value)));
        }
    }

    public void ValidateMagnitude(decimal value, string fieldName)
    {
        if (value > MaximumInitialMagnitude || value < -MaximumInitialMagnitude)
        {
            throw Diagnostic(
                "NUM_MAGNITUDE_EXCEEDED",
                "Precision.MagnitudeExceeded",
                new DiagnosticArgument(
                    fieldName,
                    DiagnosticArgumentType.Decimal,
                    CanonicalDecimal.Format(value)),
                new DiagnosticArgument(
                    "limit",
                    DiagnosticArgumentType.Decimal,
                    CanonicalDecimal.Format(MaximumInitialMagnitude)));
        }
    }

    public static decimal DecimalQuantum(int scale)
    {
        if (scale is < 0 or > 28)
        {
            throw new ArgumentOutOfRangeException(nameof(scale));
        }

        var quantum = 1m;

        for (var index = 0; index < scale; index++)
        {
            quantum /= 10m;
        }

        return quantum;
    }

    private void Validate(decimal value, int maximumScale, string fieldName)
    {
        ValidateMagnitude(value, fieldName);

        var scale = CanonicalDecimal.Scale(value);
        if (scale > maximumScale)
        {
            throw Diagnostic(
                "NUM_SCALE_EXCEEDED",
                "Precision.ScaleExceeded",
                new DiagnosticArgument(
                    fieldName,
                    DiagnosticArgumentType.Decimal,
                    CanonicalDecimal.Format(value)),
                new DiagnosticArgument(
                    "maximumScale",
                    DiagnosticArgumentType.Integer,
                    maximumScale.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }
    }

    private static DomainRuleException Diagnostic(
        string code,
        string messageKey,
        params DiagnosticArgument[] arguments) =>
        new(new DomainDiagnostic(code, messageKey, arguments));
}
