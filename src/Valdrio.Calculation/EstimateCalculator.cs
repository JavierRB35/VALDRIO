using Valdrio.Domain;
namespace Valdrio.Calculation;

public sealed record EstimatePrice(EntityId TargetId, bool Activity, UnitId UnitId, decimal? UnitCost, string Source);
public sealed record EstimatedCostLine(CompositionQuantity Quantity, decimal UnitCost, string Source, decimal RawAmount, decimal Amount);
public sealed record EstimatedCost(decimal OutputQuantity, decimal StartedBatches, IReadOnlyList<EstimatedCostLine> Lines,
    decimal Total, decimal UnitCost);

public static class EstimateCalculator
{
    public const string Version = "management-estimate-t18-v1";
    public static DomainRuleException Error(string key) => new(new("ESTIMATE_" + key.ToUpperInvariant(), "Estimate." + key, []));
    public static EstimatedCost Calculate(CompositionPreview quantities, IReadOnlyList<EstimatePrice> prices, PrecisionPolicy precision)
    {
        if (quantities.OutputQuantity <= 0 || prices.GroupBy(x => (x.TargetId, x.Activity)).Any(g => g.Count() != 1))
            throw Error("InvalidInput");
        try
        {
            var rows = quantities.Lines.Where(x => !x.Expanded).Select(q =>
            {
                var price = prices.SingleOrDefault(p => p.TargetId == q.TargetId && p.Activity == q.Activity);
                if (price?.UnitCost is null) throw Error(q.Activity ? "MissingRate" : "MissingPrice");
                if (price.UnitId != q.UnitId || q.RequiredQuantity < 0 || string.IsNullOrWhiteSpace(price.Source)) throw Error("InvalidInput");
                var raw = checked(q.RequiredQuantity * price.UnitCost.Value);
                precision.ValidateMagnitude(raw, q.Name);
                return new EstimatedCostLine(q, price.UnitCost.Value, price.Source, raw, 0);
            }).ToArray();
            if (rows.Length == 0) throw Error("InvalidInput");
            var rawTotal = rows.Sum(x => x.RawAmount);
            var postedTotal = decimal.Round(rawTotal, precision.AmountScale, MidpointRounding.ToEven);
            var rounded = new BalancedRounder(precision).Round(postedTotal,
                rows.Select((r, i) => new RoundingContribution(new("estimate:" + i.ToString(System.Globalization.CultureInfo.InvariantCulture)), r.RawAmount)).ToArray(),
                precision.InternalLedgerQuantum);
            var amounts = rounded.Lines.ToDictionary(x => x.DestinationId.Value, x => x.PostedAmount);
            var result = rows.Select((r, i) => r with { Amount = amounts["estimate:" + i.ToString(System.Globalization.CultureInfo.InvariantCulture)] }).ToArray();
            return new(quantities.OutputQuantity, quantities.StartedBatches, FrozenList.Copy(result), postedTotal, postedTotal / quantities.OutputQuantity);
        }
        catch (OverflowException) { throw Error("Overflow"); }
    }
}
