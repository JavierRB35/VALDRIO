using System.Text.Json.Serialization;

namespace Valdrio.Domain;

[JsonConverter(typeof(FxRateIdJsonConverter))]
public readonly record struct FxRateId
{
    public FxRateId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException("FX rate ID cannot have outer whitespace.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }
    public bool IsValid => !string.IsNullOrWhiteSpace(Value) &&
        string.Equals(Value, Value.Trim(), StringComparison.Ordinal);
    public void EnsureValid()
    {
        if (!IsValid) throw new ArgumentException("FX_RATE_ID_INVALID");
    }

    public override string ToString() => Value;
}

public sealed record FxRate
{
    public FxRate(
        FxRateId id,
        CurrencyCode sourceCurrency,
        CurrencyCode targetCurrency,
        decimal quote,
        BusinessDate rateDate,
        string source,
        string rateSetVersionId)
    {
        id.EnsureValid();
        if (!sourceCurrency.IsValid || !targetCurrency.IsValid)
            throw new ArgumentException("FX_CURRENCY_INVALID");
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(rateSetVersionId);

        if (sourceCurrency == targetCurrency)
        {
            throw FxDiagnostic(
                "FX_RATE_ORIENTATION_INVALID",
                "Fx.RateOrientationInvalid",
                sourceCurrency,
                targetCurrency);
        }

        if (quote <= 0m || CanonicalDecimal.Scale(quote) > 18)
        {
            throw new DomainRuleException(
                new DomainDiagnostic(
                    "FX_RATE_INVALID",
                    "Fx.RateInvalid",
                    [
                        new("rateId", DiagnosticArgumentType.Identifier, id.Value),
                        new("quote", DiagnosticArgumentType.Decimal, CanonicalDecimal.Format(quote))
                    ]));
        }

        Id = id;
        SourceCurrency = sourceCurrency;
        TargetCurrency = targetCurrency;
        Quote = quote;
        RateDate = rateDate;
        Source = source;
        RateSetVersionId = rateSetVersionId;
    }

    public FxRateId Id { get; }
    public CurrencyCode SourceCurrency { get; }
    public CurrencyCode TargetCurrency { get; }
    public decimal Quote { get; }
    public BusinessDate RateDate { get; }
    public string Source { get; }
    public string RateSetVersionId { get; }

    private static DomainRuleException FxDiagnostic(
        string code,
        string messageKey,
        CurrencyCode source,
        CurrencyCode target) =>
        new(
            new DomainDiagnostic(
                code,
                messageKey,
                [
                    new("sourceCurrency", DiagnosticArgumentType.CurrencyCode, source.Value),
                    new("targetCurrency", DiagnosticArgumentType.CurrencyCode, target.Value)
                ]));
}

public sealed class FxRateSetVersion
{
    private readonly IReadOnlyDictionary<FxRateId, FxRate> rates;

    public FxRateSetVersion(
        string versionId,
        BusinessDate publishedDate,
        string source,
        IReadOnlyList<FxRate> rates)
    {
        ArgumentNullException.ThrowIfNull(rates);
        ArgumentException.ThrowIfNullOrWhiteSpace(versionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        if (rates.Count == 0)
        {
            throw new ArgumentException("A published RateSet requires at least one rate.", nameof(rates));
        }

        foreach (var rate in rates)
        {
            ArgumentNullException.ThrowIfNull(rate);
            rate.Id.EnsureValid();
        }

        if (rates.Any(rate => !string.Equals(
                rate.RateSetVersionId,
                versionId,
                StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                "Every rate must belong to the published RateSet version.",
                nameof(rates));
        }

        VersionId = versionId;
        PublishedDate = publishedDate;
        Source = source;
        this.rates = rates.ToDictionary(rate => rate.Id);
    }

    public string VersionId { get; }
    public BusinessDate PublishedDate { get; }
    public string Source { get; }

    public IReadOnlyList<FxRate> Rates =>
        FrozenList.Copy(rates.Values.OrderBy(rate => rate.Id.Value, StringComparer.Ordinal));

    public FxRate GetRequired(FxRateId id)
    {
        id.EnsureValid();
        return rates.TryGetValue(id, out var rate)
            ? rate
            : throw new DomainRuleException(
                new DomainDiagnostic(
                    "FX_RATE_MISSING",
                    "Fx.RateMissing",
                    [new("rateId", DiagnosticArgumentType.Identifier, id.Value)]));
    }
}

public sealed record ConversionRoundingAdjustment(
    decimal Amount,
    CurrencyCode CurrencyCode);

public sealed record FxRateApplication(
    string ApplicationId,
    Money OriginalMoney,
    Money ConvertedOriginalMoney,
    decimal ConvertedRaw,
    ConversionRoundingAdjustment ConversionRoundingAdjustment,
    FxRateId? RateId,
    string? RateSetVersionId,
    decimal Quote,
    BusinessDate? RateDate,
    string Source)
{
    public IReadOnlyList<DomainDiagnostic> Diagnostics =>
        OriginalMoney.Amount != 0m && ConvertedOriginalMoney.Amount == 0m
            ? Array.AsReadOnly(new[]
              {
                  new DomainDiagnostic("FX_AMOUNT_BELOW_QUANTUM", "Fx.AmountBelowQuantum",
                      [
                          new("originalAmount", DiagnosticArgumentType.Decimal, CanonicalDecimal.Format(OriginalMoney.Amount)),
                          new("sourceCurrency", DiagnosticArgumentType.CurrencyCode, OriginalMoney.Currency.Value),
                          new("targetCurrency", DiagnosticArgumentType.CurrencyCode, ConvertedOriginalMoney.Currency.Value)
                      ])
              })
            : Array.Empty<DomainDiagnostic>();

    public bool IsIdentity => RateId is null;

    public void EnsureValid()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ApplicationId);
        OriginalMoney.EnsureValid("originalMoney");
        ConvertedOriginalMoney.EnsureValid("convertedMoney");
        PrecisionPolicyCompatibility.EnsureSame(OriginalMoney.PrecisionPolicy,
            ConvertedOriginalMoney.PrecisionPolicy, "original", "converted");
        ArgumentNullException.ThrowIfNull(ConversionRoundingAdjustment);
        if (ConversionRoundingAdjustment.CurrencyCode != ConvertedOriginalMoney.Currency ||
            checked(ConvertedRaw + ConversionRoundingAdjustment.Amount) != ConvertedOriginalMoney.Amount)
            throw new ArgumentException("FX_BRIDGE_INVALID");
        ArgumentException.ThrowIfNullOrWhiteSpace(Source);
        if (IsIdentity)
        {
            if (RateSetVersionId is not null || RateDate.HasValue || Quote != 1m ||
                OriginalMoney != ConvertedOriginalMoney || ConvertedRaw != OriginalMoney.Amount ||
                ConversionRoundingAdjustment.Amount != 0m)
                throw new ArgumentException("FX_IDENTITY_INVALID");
        }
        else
        {
            RateId!.Value.EnsureValid();
            ArgumentException.ThrowIfNullOrWhiteSpace(RateSetVersionId);
            if (!RateDate.HasValue || Quote <= 0m || CanonicalDecimal.Scale(Quote) > 18 ||
                OriginalMoney.Currency == ConvertedOriginalMoney.Currency)
                throw new ArgumentException("FX_APPLICATION_INVALID");
        }
    }
}
