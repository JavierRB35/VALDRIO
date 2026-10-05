using Valdrio.Domain;

namespace Valdrio.Application;

public sealed record FxRateProviderRequest(
    string RequestedVersionLabel,
    IReadOnlyList<(CurrencyCode Source, CurrencyCode Target)> Pairs,
    BusinessDate RateDate);

public sealed record FxRateSetDraft(
    string Source,
    BusinessDate RateDate,
    IReadOnlyList<FxRate> Rates);

public interface IFxRateProvider
{
    FxRateSetDraft Prepare(FxRateProviderRequest request);
}
