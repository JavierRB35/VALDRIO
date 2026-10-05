using System.Text;
using System.Text.Json;
using Valdrio.Domain;

namespace Valdrio.Application;

// Foundation for persistence. The economic/full RunInput schemas are delivered in T05.
public static class CanonicalSnapshotJson
{
    public const string SchemaVersion = "valdrio-values-v1";
    private static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            RespectRequiredConstructorParameters = true,
            PropertyNameCaseInsensitive = false
        };
        options.Converters.Add(new CanonicalDecimalJsonConverter());
        return options;
    }

    public static string Serialize<T>(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Validate(value);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("SchemaVersion", SchemaVersion);
            writer.WritePropertyName("Value");
            WriteCanonical(writer, JsonSerializer.SerializeToElement(value, Options));
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static T Deserialize<T>(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 2 ||
            !root.TryGetProperty("SchemaVersion", out var schema) ||
            schema.ValueKind != JsonValueKind.String || schema.GetString() != SchemaVersion ||
            !root.TryGetProperty("Value", out var payload) || payload.ValueKind == JsonValueKind.Null)
            throw new JsonException("Unsupported or incomplete snapshot envelope.");

        try
        {
            var value = payload.Deserialize<T>(Options)
                ?? throw new JsonException("Null snapshot value.");
            Validate(value);
            // Reject missing/ignored/duplicate fields and silently synthesized defaults.
            // Property order is irrelevant; arrays preserve their contractual order.
            using var restored = JsonDocument.Parse(Serialize(value));
            if (CanonicalText(payload) != CanonicalText(restored.RootElement.GetProperty("Value")))
                throw new JsonException("Snapshot fields are missing, unknown or inconsistent.");
            return value;
        }
        catch (Exception ex) when (ex is ArgumentException or DomainRuleException or FormatException)
        { throw new JsonException("Invalid snapshot value.", ex); }
    }

    private static void Validate<T>(T value)
    {
        switch (value)
        {
            case Money money: money.EnsureValid("snapshot"); break;
            case Quantity quantity: quantity.EnsureValid(); break;
            case EntityId id: id.EnsureValid(); break;
            case UnitId id: id.EnsureValid(); break;
            case FxRateId id: id.EnsureValid(); break;
            case FxRateApplication app:
                app.EnsureValid();
                var rate = app.IsIdentity ? null : new FxRate(app.RateId!.Value,
                    app.OriginalMoney.Currency, app.ConvertedOriginalMoney.Currency, app.Quote,
                    app.RateDate!.Value, app.Source, app.RateSetVersionId!);
                var expected = new Valdrio.Calculation.FxConverter().Convert(app.ApplicationId,
                    app.OriginalMoney, app.ConvertedOriginalMoney.Currency, rate);
                if (app != expected) throw new ArgumentException("FX_SNAPSHOT_CONVERSION_INVALID");
                break;
            case CurrencyCode currency when !currency.IsValid:
                throw new ArgumentException("CURRENCY_CODE_INVALID");
            case CurrencyMetadataSnapshot metadata: metadata.EnsureValid(); break;
            case PrecisionPolicy policy: policy.EnsureValid(); break;
        }
    }

    private static string CanonicalText(JsonElement element)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) WriteCanonical(writer, element);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var properties = element.EnumerateObject().ToArray();
            if (properties.Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() != properties.Length)
                throw new JsonException("Duplicate snapshot property.");
            writer.WriteStartObject();
            foreach (var property in properties.OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                writer.WritePropertyName(property.Name);
                WriteCanonical(writer, property.Value);
            }
            writer.WriteEndObject();
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (var item in element.EnumerateArray()) WriteCanonical(writer, item);
            writer.WriteEndArray();
        }
        else element.WriteTo(writer);
    }
}
