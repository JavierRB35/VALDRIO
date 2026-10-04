namespace Valdrio.Domain;

public readonly record struct Quantity
{
    public Quantity(
        decimal value,
        UnitId unitId,
        PrecisionPolicy? precisionPolicy = null)
    {
        var policy =
            precisionPolicy ?? PrecisionPolicy.Pdec1Proposed;

        policy.ValidateQuantity(value, nameof(value));

        Value = value;
        UnitId = unitId;
        PrecisionPolicy = policy;
    }

    public decimal Value { get; }
    public UnitId UnitId { get; }
    public PrecisionPolicy PrecisionPolicy { get; }

    public static Quantity operator +(Quantity left, Quantity right)
    {
        EnsureSameUnit(left, right);

        PrecisionPolicyCompatibility.EnsureSame(
            left.PrecisionPolicy,
            right.PrecisionPolicy,
            "leftQuantity",
            "rightQuantity");

        return new Quantity(
            checked(left.Value + right.Value),
            left.UnitId,
            left.PrecisionPolicy);
    }

    private static void EnsureSameUnit(
        Quantity left,
        Quantity right)
    {
        if (left.UnitId != right.UnitId)
        {
            throw new DomainRuleException(
                new DomainDiagnostic(
                    "QUANTITY_UNIT_MISMATCH",
                    "Quantity.UnitMismatch",
                    [
                        new(
                            "leftUnitId",
                            DiagnosticArgumentType.Identifier,
                            left.UnitId.ToString()),
                        new(
                            "rightUnitId",
                            DiagnosticArgumentType.Identifier,
                            right.UnitId.ToString())
                    ]));
        }
    }
}
