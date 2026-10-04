using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Valdrio.Domain;

public static class CanonicalDecimal
{
    private static readonly string FormatPattern = "0." + new string('#', 28);

    public static string Format(decimal value)
    {
        if (value == 0m)
        {
            return "0";
        }

        return value.ToString(FormatPattern, CultureInfo.InvariantCulture);
    }

    public static decimal Parse(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        const NumberStyles styles =
            NumberStyles.AllowLeadingSign |
            NumberStyles.AllowDecimalPoint;

        if (!decimal.TryParse(
                text,
                styles,
                CultureInfo.InvariantCulture,
                out var value))
        {
            throw new FormatException("CANONICAL_DECIMAL_INVALID");
        }

        if (!string.Equals(Format(value), text, StringComparison.Ordinal))
        {
            throw new FormatException("CANONICAL_DECIMAL_NON_CANONICAL");
        }

        return value;
    }

    public static int Scale(decimal value)
    {
        var bits = decimal.GetBits(value);
        return (bits[3] >> 16) & 0x7F;
    }
}

public sealed class CanonicalDecimalJsonConverter : JsonConverter<decimal>
{
    public override decimal Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("Economic decimal values must be JSON strings.");
        }

        var text = reader.GetString()
            ?? throw new JsonException("Economic decimal string is null.");

        try
        {
            return CanonicalDecimal.Parse(text);
        }
        catch (FormatException exception)
        {
            throw new JsonException(exception.Message, exception);
        }
    }

    public override void Write(
        Utf8JsonWriter writer,
        decimal value,
        JsonSerializerOptions options) =>
        writer.WriteStringValue(CanonicalDecimal.Format(value));
}
