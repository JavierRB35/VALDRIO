using Valdrio.Domain;

namespace Valdrio.Calculation;

public sealed record CompositionCostEstimate(decimal? PricedComponents, decimal? ImportedOutputReference,
    IReadOnlyList<string> UnpricedComponents, bool HasActivity, CurrencyCode Currency);

/// <summary>Management estimate from reference unit costs; this is neither actual consumption nor inventory valuation.</summary>
public static class CompositionCostEstimator
{
    public static CompositionCostEstimate Estimate(CompositionCatalog catalog, CompositionPreview preview,
        EntityId outputItemId, CurrencyCode currency)
    {
        var unpriced = new List<string>();
        decimal total = 0;
        foreach (var totalLine in preview.Totals)
        {
            if (totalLine.Activity) continue;
            var item = catalog.Items.Single(i => i.Id == totalLine.TargetId);
            if (item.ReferenceUnitCost is not { } price)
            {
                unpriced.Add(item.Code + " · " + item.Name);
                continue;
            }
            total = checked(total + checked(totalLine.Quantity * price));
        }
        var hasActivity = preview.Totals.Any(x => x.Activity);
        var output = catalog.Items.Single(x => x.Id == outputItemId);
        var outputReference = output.ReferenceUnitCost is { } unitCost
            ? checked(unitCost * preview.OutputQuantity)
            : (decimal?)null;
        return new(total, outputReference, unpriced, hasActivity, currency);
    }
}
