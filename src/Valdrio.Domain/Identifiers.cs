namespace Valdrio.Domain;

public readonly record struct EntityId : IComparable<EntityId>
{
    public EntityId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("ID_EMPTY", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static EntityId Parse(string text) =>
        new(Guid.ParseExact(text, "D"));

    public int CompareTo(EntityId other) =>
        StringComparer.Ordinal.Compare(ToString(), other.ToString());

    public override string ToString() =>
        Value.ToString("D");
}

public readonly record struct UnitId(EntityId Value) : IComparable<UnitId>
{
    public static UnitId Parse(string text) =>
        new(EntityId.Parse(text));

    public int CompareTo(UnitId other) =>
        Value.CompareTo(other.Value);

    public override string ToString() =>
        Value.ToString();
}

public sealed record UnitDefinition(
    UnitId Id,
    string Code,
    string Dimension,
    string Symbol,
    int DecimalScale,
    bool IsDiscrete)
{
    public UnitDefinition
    {
        if (string.IsNullOrWhiteSpace(Code))
        {
            throw new ArgumentException("UNIT_CODE_REQUIRED", nameof(Code));
        }

        if (string.IsNullOrWhiteSpace(Dimension))
        {
            throw new ArgumentException("UNIT_DIMENSION_REQUIRED", nameof(Dimension));
        }

        if (DecimalScale is < 0 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(DecimalScale));
        }
    }
}
