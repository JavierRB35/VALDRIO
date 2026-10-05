using Valdrio.Domain;

namespace Valdrio.Calculation;

public sealed class FxConverter
{
    private const int ConversionScale = 8;

    public FxRateApplication Convert(
        string applicationId,
        Money original,
        CurrencyCode functionalCurrency,
        FxRate? selectedRate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationId);
        original.EnsureValid("originalMoney");
        if (!functionalCurrency.IsValid) throw new ArgumentException("FX_CURRENCY_INVALID");

        if (original.Currency == functionalCurrency)
        {
            return new FxRateApplication(
                applicationId,
                original,
                new Money(original.Amount, functionalCurrency, original.PrecisionPolicy),
                original.Amount,
                new ConversionRoundingAdjustment(0m, functionalCurrency),
                RateId: null,
                RateSetVersionId: null,
                Quote: 1m,
                RateDate: null,
                Source: "IDENTITY");
        }

        if (selectedRate is null)
        {
            throw new DomainRuleException(
                new DomainDiagnostic(
                    "FX_RATE_MISSING",
                    "Fx.RateMissing",
                    [
                        new("sourceCurrency", DiagnosticArgumentType.CurrencyCode, original.Currency.Value),
                        new("targetCurrency", DiagnosticArgumentType.CurrencyCode, functionalCurrency.Value)
                    ]));
        }

        if (selectedRate.SourceCurrency != original.Currency ||
            selectedRate.TargetCurrency != functionalCurrency)
        {
            throw new DomainRuleException(
                new DomainDiagnostic(
                    "FX_RATE_ORIENTATION_INVALID",
                    "Fx.RateOrientationInvalid",
                    [
                        new("rateId", DiagnosticArgumentType.Identifier, selectedRate.Id.Value),
                        new("expectedSource", DiagnosticArgumentType.CurrencyCode, original.Currency.Value),
                        new("expectedTarget", DiagnosticArgumentType.CurrencyCode, functionalCurrency.Value),
                        new("actualSource", DiagnosticArgumentType.CurrencyCode, selectedRate.SourceCurrency.Value),
                        new("actualTarget", DiagnosticArgumentType.CurrencyCode, selectedRate.TargetCurrency.Value)
                    ]));
        }

        decimal raw;
        decimal converted;
        try
        {
            raw = checked(original.Amount * selectedRate.Quote);
            converted = ExactDecimalProduct.Quantize(original.Amount, selectedRate.Quote, ConversionScale);
        }
        catch (OverflowException)
        {
            throw new DomainRuleException(
                new DomainDiagnostic(
                    "FX_AMOUNT_OUT_OF_RANGE",
                    "Fx.AmountOutOfRange",
                    [
                        new("originalAmount", DiagnosticArgumentType.Decimal, CanonicalDecimal.Format(original.Amount)),
                        new("quote", DiagnosticArgumentType.Decimal, CanonicalDecimal.Format(selectedRate.Quote))
                    ]));
        }

        // ConvertedRaw is a decimal approximation when the exact product needs
        // more than 96 coefficient bits. The posted amount is quantized from the
        // exact integer product; origin + quote retain its exact reconstruction.

        original.PrecisionPolicy.ValidateAmount(
            converted,
            "convertedAmount");

        var adjustment = checked(converted - raw);

        return new FxRateApplication(
            applicationId,
            original,
            new Money(converted, functionalCurrency, original.PrecisionPolicy),
            raw,
            new ConversionRoundingAdjustment(adjustment, functionalCurrency),
            selectedRate.Id,
            selectedRate.RateSetVersionId,
            selectedRate.Quote,
            selectedRate.RateDate,
            selectedRate.Source);
    }
}
