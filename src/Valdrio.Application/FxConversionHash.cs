using System.Security.Cryptography;
using System.Text.Json;
using Valdrio.Domain;

namespace Valdrio.Application;

public static class FxConversionHash
{
    public const string SchemaVersion = "fx-v2";

    public static string Compute(FxRateApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);
        application.EnsureValid();
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartArray();
            writer.WriteStringValue(SchemaVersion);
            writer.WriteStringValue(CanonicalDecimal.Format(application.OriginalMoney.Amount));
            writer.WriteStringValue(application.OriginalMoney.Currency.Value);
            writer.WriteStringValue(application.ConvertedOriginalMoney.Currency.Value);
            WritePolicy(writer, application.OriginalMoney.PrecisionPolicy);
            WritePolicy(writer, application.ConvertedOriginalMoney.PrecisionPolicy);
            writer.WriteStringValue(CanonicalDecimal.Format(application.ConvertedOriginalMoney.Amount));
            writer.WriteStringValue(CanonicalDecimal.Format(application.ConvertedRaw));
            writer.WriteStringValue(CanonicalDecimal.Format(application.ConversionRoundingAdjustment.Amount));
            writer.WriteStringValue(application.ConversionRoundingAdjustment.CurrencyCode.Value);
            writer.WriteStringValue(application.RateSetVersionId);
            writer.WriteStringValue(application.RateId?.Value);
            writer.WriteStringValue(CanonicalDecimal.Format(application.Quote));
            writer.WriteStringValue(application.RateDate?.ToCanonicalString());
            writer.WriteStringValue(application.Source);
            writer.WriteEndArray();
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WritePolicy(Utf8JsonWriter writer, PrecisionPolicy policy)
    {
        writer.WriteStartArray();
        writer.WriteStringValue(policy.Version);
        writer.WriteNumberValue(policy.AmountScale);
        writer.WriteNumberValue(policy.QuantityScale);
        writer.WriteNumberValue(policy.PercentageScale);
        writer.WriteStringValue(CanonicalDecimal.Format(policy.MaximumInitialMagnitude));
        writer.WriteStringValue(CanonicalDecimal.Format(policy.MaximumNumericCorrection));
        writer.WriteEndArray();
    }
}
