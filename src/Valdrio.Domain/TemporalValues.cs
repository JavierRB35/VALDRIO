using System.Globalization;
using System.Text.Json.Serialization;

namespace Valdrio.Domain;

[JsonConverter(typeof(BusinessDateJsonConverter))]
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

[JsonConverter(typeof(UtcInstantJsonConverter))]
public readonly record struct UtcInstant
{
    private static readonly string[] ExplicitOffsetFormats =
    [
        "yyyy-MM-dd'T'HH:mm:sszzz",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz"
    ];

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

        var hasZulu = text.EndsWith(
            "Z",
            StringComparison.Ordinal);

        var hasNumericOffset =
            text.Length >= 6 &&
            (text[^6] == '+' || text[^6] == '-') &&
            text[^3] == ':' &&
            char.IsAsciiDigit(text[^5]) &&
            char.IsAsciiDigit(text[^4]) &&
            char.IsAsciiDigit(text[^2]) &&
            char.IsAsciiDigit(text[^1]);

        if (!hasZulu && !hasNumericOffset)
        {
            throw new FormatException(
                "UTC instant must contain full ISO date, time and an explicit Z or numeric offset.");
        }

        var normalized = hasZulu
            ? text[..^1] + "+00:00"
            : text;

        if (!DateTimeOffset.TryParseExact(
                normalized,
                ExplicitOffsetFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            throw new FormatException(
                "UTC instant is not a valid explicit-offset ISO value.");
        }

        return From(parsed);
    }

    public string ToCanonicalString() =>
        Value.ToString(
            "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'",
            CultureInfo.InvariantCulture);

    public override string ToString() =>
        ToCanonicalString();
}
