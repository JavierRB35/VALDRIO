namespace Valdrio.Domain;

public sealed record CurrencyValidity(
    DateOnly? ValidFrom,
    DateOnly? ValidTo)
{
    public bool Contains(DateOnly date) =>
        (!ValidFrom.HasValue || date >= ValidFrom.Value) &&
        (!ValidTo.HasValue || date <= ValidTo.Value);
}

public sealed record CurrencyDefinition
{
    public CurrencyDefinition(
        CurrencyCode alphaCode,
        string numericCode,
        int? minorUnitDigits,
        bool isMonetary,
        CurrencyValidity validity,
        string catalogVersion,
        string source,
        DateOnly sourcePublicationDate,
        DateOnly checkedOn)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(numericCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        if (numericCode.Length != 3 ||
            numericCode.Any(character => character is < '0' or > '9'))
        {
            throw new ArgumentException(
                "Numeric currency code must contain exactly three ASCII digits.",
                nameof(numericCode));
        }

        if (minorUnitDigits is < 0 or > 8)
        {
            throw new ArgumentOutOfRangeException(nameof(minorUnitDigits));
        }

        AlphaCode = alphaCode;
        NumericCode = numericCode;
        MinorUnitDigits = minorUnitDigits;
        IsMonetary = isMonetary;
        Validity = validity;
        CatalogVersion = catalogVersion;
        Source = source;
        SourcePublicationDate = sourcePublicationDate;
        CheckedOn = checkedOn;
    }

    public CurrencyCode AlphaCode { get; }
    public string NumericCode { get; }
    public int? MinorUnitDigits { get; }
    public bool IsMonetary { get; }
    public CurrencyValidity Validity { get; }
    public string CatalogVersion { get; }
    public string Source { get; }
    public DateOnly SourcePublicationDate { get; }
    public DateOnly CheckedOn { get; }

    public CurrencyMetadataSnapshot Freeze()
    {
        if (!MinorUnitDigits.HasValue)
        {
            throw new DomainRuleException(
                new DomainDiagnostic(
                    "CURRENCY_MINOR_UNIT_UNDEFINED",
                    "Currency.MinorUnitUndefined",
                    [
                        new(
                            "currency",
                            DiagnosticArgumentType.CurrencyCode,
                            AlphaCode.Value),
                        new(
                            "catalogVersion",
                            DiagnosticArgumentType.Text,
                            CatalogVersion)
                    ]));
        }

        return new CurrencyMetadataSnapshot(
            AlphaCode,
            NumericCode,
            MinorUnitDigits.Value,
            IsMonetary,
            CatalogVersion,
            Source,
            SourcePublicationDate,
            CheckedOn);
    }
}

public sealed record CurrencyMetadataSnapshot(
    CurrencyCode AlphaCode,
    string NumericCode,
    int MinorUnitDigits,
    bool IsMonetary,
    string CatalogVersion,
    string Source,
    DateOnly SourcePublicationDate,
    DateOnly CheckedOn)
{
    public decimal ReportQuantum =>
        PrecisionPolicy.DecimalQuantum(MinorUnitDigits);
}
