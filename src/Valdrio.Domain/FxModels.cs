namespace Valdrio.Domain;

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
        ArgumentException.ThrowIfNullOrWhiteSpace(versionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        if (rates.Count == 0)
        {
            throw new ArgumentException("A published RateSet requires at least one rate.", nameof(rates));
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
        rates.Values.OrderBy(rate => rate.Id.Value, StringComparer.Ordinal).ToArray();

    public FxRate GetRequired(FxRateId id) =>
        rates.TryGetValue(id, out var rate)
            ? rate
            : throw new DomainRuleException(
                new DomainDiagnostic(
                    "FX_RATE_MISSING",
                    "Fx.RateMissing",
                    [new("rateId", DiagnosticArgumentType.Identifier, id.Value)]));
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
    public bool IsIdentity => RateId is null;
}
