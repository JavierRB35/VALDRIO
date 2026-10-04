using Valdrio.Domain;

namespace Valdrio.Application;

public interface ICurrencyCatalog
{
    string CatalogVersion { get; }

    IReadOnlyList<CurrencyDefinition> GetAll();

    CurrencyDefinition GetRequired(CurrencyCode currencyCode);
}

public sealed class CurrencyMetadataSnapshotService
{
    private readonly ICurrencyCatalog currencyCatalog;

    public CurrencyMetadataSnapshotService(
        ICurrencyCatalog currencyCatalog)
    {
        this.currencyCatalog = currencyCatalog;
    }

    public CurrencyMetadataSnapshot Freeze(
        CurrencyCode functionalCurrencyCode) =>
        currencyCatalog
            .GetRequired(functionalCurrencyCode)
            .Freeze();
}
