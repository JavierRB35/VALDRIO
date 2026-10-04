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

public sealed record UnitDefinition
{
    public UnitDefinition(
        UnitId id,
        string code,
        string dimension,
        string symbol,
        int decimalScale,
        bool isDiscrete)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("UNIT_CODE_REQUIRED", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(dimension))
        {
            throw new ArgumentException(
                "UNIT_DIMENSION_REQUIRED",
                nameof(dimension));
        }

        if (decimalScale is < 0 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(decimalScale));
        }

        Id = id;
        Code = code;
        Dimension = dimension;
        Symbol = symbol;
        DecimalScale = decimalScale;
        IsDiscrete = isDiscrete;
    }

    public UnitId Id { get; }
    public string Code { get; }
    public string Dimension { get; }
    public string Symbol { get; }
    public int DecimalScale { get; }
    public bool IsDiscrete { get; }
}
