using System.Security.Cryptography;
using System.Text;
using Valdrio.Domain;

namespace Valdrio.Application;

public static class FxConversionHash
{
    public static string Compute(FxRateApplication application)
    {
        var canonical = string.Join(
            "|",
            "fx-v1",
            CanonicalDecimal.Format(application.OriginalMoney.Amount),
            application.OriginalMoney.Currency.Value,
            application.ConvertedOriginalMoney.Currency.Value,
            application.OriginalMoney.PrecisionPolicy.Version,
            CanonicalDecimal.Format(application.ConvertedOriginalMoney.Amount),
            CanonicalDecimal.Format(application.ConversionRoundingAdjustment.Amount),
            application.RateSetVersionId ?? "IDENTITY",
            application.RateId?.Value ?? "IDENTITY",
            CanonicalDecimal.Format(application.Quote),
            application.RateDate?.ToCanonicalString() ?? "IDENTITY",
            application.Source);

        return Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(canonical)));
    }
}
