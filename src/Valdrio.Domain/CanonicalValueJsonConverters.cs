using System.Text.Json;
using System.Text.Json.Serialization;

namespace Valdrio.Domain;

// These converters are part of the value contract, including when a caller uses
// JsonSerializer directly. Snapshot envelopes/versioning belong to Application.
public abstract class CanonicalStringJsonConverter<T> : JsonConverter<T>
{
    protected abstract T Parse(string text);
    protected abstract string Format(T value);
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("A canonical value must be a non-null JSON string.");
        try { return Parse(reader.GetString()!); }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        { throw new JsonException("Invalid canonical value.", ex); }
    }
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
        writer.WriteStringValue(Format(value));
}

public sealed class BusinessDateJsonConverter : CanonicalStringJsonConverter<BusinessDate>
{
    protected override BusinessDate Parse(string text) => BusinessDate.ParseIso(text);
    protected override string Format(BusinessDate value) => value.ToCanonicalString();
}
public sealed class UtcInstantJsonConverter : CanonicalStringJsonConverter<UtcInstant>
{
    protected override UtcInstant Parse(string text) => UtcInstant.ParseIso(text);
    protected override string Format(UtcInstant value) => value.ToCanonicalString();
}
public sealed class EntityIdJsonConverter : CanonicalStringJsonConverter<EntityId>
{
    protected override EntityId Parse(string text) => EntityId.Parse(text);
    protected override string Format(EntityId value) { value.EnsureValid(); return value.ToString(); }
}
public sealed class UnitIdJsonConverter : CanonicalStringJsonConverter<UnitId>
{
    protected override UnitId Parse(string text) => UnitId.Parse(text);
    protected override string Format(UnitId value) { value.EnsureValid(); return value.ToString(); }
}
public sealed class FxRateIdJsonConverter : CanonicalStringJsonConverter<FxRateId>
{
    protected override FxRateId Parse(string text) => new(text);
    protected override string Format(FxRateId value) { value.EnsureValid(); return value.Value; }
}

public sealed class QuantityJsonConverter : JsonConverter<Quantity>
{
    public override Quantity Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            root.EnumerateObject().Count() != 3 ||
            !root.TryGetProperty("Value", out var value) ||
            !root.TryGetProperty("UnitId", out var unit) ||
            !root.TryGetProperty("PrecisionPolicy", out var policy) ||
            policy.ValueKind == JsonValueKind.Null)
            throw new JsonException("Quantity requires Value, UnitId and PrecisionPolicy.");
        try
        {
            return new Quantity(value.Deserialize<decimal>(options), unit.Deserialize<UnitId>(options),
                policy.Deserialize<PrecisionPolicy>(options) ?? throw new JsonException("Missing precision policy."));
        }
        catch (Exception ex) when (ex is ArgumentException or DomainRuleException)
        { throw new JsonException("Invalid quantity.", ex); }
    }
    public override void Write(Utf8JsonWriter writer, Quantity value, JsonSerializerOptions options)
    {
        value.EnsureValid();
        writer.WriteStartObject();
        writer.WritePropertyName("Value");
        JsonSerializer.Serialize(writer, value.Value, options);
        writer.WritePropertyName("UnitId");
        JsonSerializer.Serialize(writer, value.UnitId, options);
        writer.WritePropertyName("PrecisionPolicy");
        JsonSerializer.Serialize(writer, value.PrecisionPolicy, options);
        writer.WriteEndObject();
    }
}
