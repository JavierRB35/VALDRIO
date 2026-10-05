namespace Valdrio.Domain;

public sealed record StudyCurrencyContext
{
    public StudyCurrencyContext(
        CurrencyCode functionalCurrencyCode,
        CurrencyMetadataSnapshot currencyMetadataSnapshot)
    {
        ArgumentNullException.ThrowIfNull(currencyMetadataSnapshot);
        currencyMetadataSnapshot.EnsureValid();
        if (!functionalCurrencyCode.IsValid) throw new ArgumentException("CURRENCY_CODE_INVALID");

        if (functionalCurrencyCode !=
            currencyMetadataSnapshot.AlphaCode)
        {
            throw new DomainRuleException(
                new DomainDiagnostic(
                    "MONEY_CURRENCY_MISMATCH",
                    "Money.CurrencyMismatch",
                    [
                        new(
                            "functionalCurrency",
                            DiagnosticArgumentType.CurrencyCode,
                            functionalCurrencyCode.Value),
                        new(
                            "metadataCurrency",
                            DiagnosticArgumentType.CurrencyCode,
                            currencyMetadataSnapshot.AlphaCode.Value)
                    ]));
        }

        FunctionalCurrencyCode = functionalCurrencyCode;
        CurrencyCatalogVersion =
            currencyMetadataSnapshot.CatalogVersion;
        CurrencyMetadataSnapshot =
            currencyMetadataSnapshot;
    }

    public CurrencyCode FunctionalCurrencyCode { get; }
    public string CurrencyCatalogVersion { get; }
    public CurrencyMetadataSnapshot CurrencyMetadataSnapshot { get; }
}
