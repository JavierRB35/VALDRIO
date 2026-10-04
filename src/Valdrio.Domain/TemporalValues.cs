using System.Globalization;

namespace Valdrio.Domain;

public readonly record struct BusinessDate
{
    public BusinessDate(DateOnly value)
    {
        Value = value;
    }

    public DateOnly Value { get; }

    public static BusinessDate ParseIso(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        return new BusinessDate(
            DateOnly.ParseExact(
                text,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture));
    }

    public string ToCanonicalString() =>
        Value.ToString(
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture);

    public override string ToString() =>
        ToCanonicalString();
}

public readonly record struct UtcInstant
{
    private UtcInstant(DateTimeOffset utcValue)
    {
        if (utcValue.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "UtcInstant requires offset zero.",
                nameof(utcValue));
        }

        Value = utcValue;
    }

    public DateTimeOffset Value { get; }

    public static UtcInstant From(
        DateTimeOffset instant) =>
        new(instant.ToUniversalTime());

    public static UtcInstant ParseIso(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var parsed = DateTimeOffset.Parse(
            text,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind);

        return From(parsed);
    }

    public string ToCanonicalString() =>
        Value.ToString(
            "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'",
            CultureInfo.InvariantCulture);

    public override string ToString() =>
        ToCanonicalString();
}
