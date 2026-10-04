using System.Globalization;
using System.Text.RegularExpressions;

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
    private static readonly Regex ExplicitIsoInstant =
        new(
            @"^d{4}-d{2}-d{2}Td{2}:d{2}:d{2}(?:.d{1,7})?(?:Z|[+-]d{2}:d{2})$",
            RegexOptions.CultureInvariant);

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

        if (!ExplicitIsoInstant.IsMatch(text))
        {
            throw new FormatException(
                "UTC instant must contain full ISO date, time and an explicit Z or numeric offset.");
        }

        var normalized = text.EndsWith(
            "Z",
            StringComparison.Ordinal)
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
