using Valdrio.Domain;

namespace Valdrio.Calculation;

// T19 comparison inputs are observations, not extra ledger sources.
// Closing and economic reconciliation belong to the application use case, not this projection.
public enum OrderComparisonMode
{
    TotalReported,
    MaterialsAndProcesses
}

public sealed record ActualOrderComponent(
    int? EstimateLineIndex,
    string Name,
    bool Activity,
    UnitId UnitId,
    decimal Quantity,
    decimal? Cost,
    string Evidence);

public sealed record OrderComparedComponent(
    int? EstimateLineIndex,
    string Name,
    bool Activity,
    UnitId UnitId,
    decimal? EstimatedQuantity,
    decimal? RealQuantity,
    decimal? EstimatedCost,
    decimal? RealCost,
    decimal? Difference,
    bool Unplanned);

public sealed record OrderCostComparison(
    OrderComparisonMode Mode,
    decimal ProducedQuantity,
    decimal EstimatedTotal,
    decimal EstimatedUnitCost,
    decimal? RealTotal,
    decimal? RealUnitCost,
    decimal ValuedSubtotal,
    decimal? TotalDifference,
    IReadOnlyList<OrderComparedComponent> Components,
    bool HasPendingCosts,
    bool Comparable,
    bool ControllerConfirmed,
    string CurrencyCode,
    string Evidence);

public static class OrderCostComparisonCalculator
{
    public const string Version = "t19-order-comparison-foundation-v1";

    private static DomainRuleException Invalid(string reason) =>
        new(new("ORDER_COMPARE_" + reason, "Estimate.InvalidInput", []));

    private static bool Validate(EstimatedCost estimate, decimal produced, string currency, PrecisionPolicy precision)
    {
        if (estimate is null || precision is null || produced <= 0 ||
            estimate.OutputQuantity <= 0 || estimate.Lines is null || estimate.Lines.Count == 0 ||
            estimate.Lines.Sum(x => x.Amount) != estimate.Total ||
            currency.Length != 3 || currency.Any(c => c < 'A' || c > 'Z'))
            throw Invalid("INVALID_INPUT");

        precision.ValidateQuantity(produced, "ProducedQuantity");
        return produced == estimate.OutputQuantity;
    }

    // Controller-reported total production cost. Never treated as a source of extra expenses.
    public static OrderCostComparison CompareTotal(
        EstimatedCost estimate,
        decimal producedQuantity,
        decimal realUnitCost,
        string currencyCode,
        string evidence,
        bool sameCostPerimeter,
        bool controllerConfirmed,
        PrecisionPolicy precision)
    {
        if (string.IsNullOrWhiteSpace(currencyCode) || string.IsNullOrWhiteSpace(evidence) || realUnitCost < 0)
            throw Invalid("INVALID_INPUT");

        try
        {
            var sameQuantity = Validate(estimate, producedQuantity, currencyCode, precision);
            precision.ValidateMagnitude(realUnitCost, "RealUnitCost");
            var actual = checked(realUnitCost * producedQuantity);
            precision.ValidateMagnitude(actual, "RealTotal");
            var comparable = sameQuantity && sameCostPerimeter;
            return new(OrderComparisonMode.TotalReported, producedQuantity, estimate.Total, estimate.UnitCost,
                actual, realUnitCost, actual, comparable ? actual - estimate.Total : null,
                FrozenList.Copy(Array.Empty<OrderComparedComponent>()), false, comparable,
                controllerConfirmed, currencyCode, evidence);
        }
        catch (OverflowException) { throw Invalid("OVERFLOW"); }
    }

    // A row with null Cost is physically observed but not economically valued.
    // An absent planned row is Missing, NOT an actual quantity or amount of zero.
    // EstimateLineIndex refers to the frozen estimate's line order; null is an unplanned component.
    public static OrderCostComparison CompareDetailed(
        EstimatedCost estimate,
        decimal producedQuantity,
        IReadOnlyList<ActualOrderComponent> actual,
        string currencyCode,
        bool sameCostPerimeter,
        bool controllerConfirmed,
        PrecisionPolicy precision)
    {
        if (actual is null || string.IsNullOrWhiteSpace(currencyCode))
            throw Invalid("INVALID_INPUT");

        try
        {
            var sameQuantity = Validate(estimate, producedQuantity, currencyCode, precision);
            var seen = new HashSet<int>();
            foreach (var line in actual)
            {
                if (line is null || line.Quantity < 0 || line.Cost < 0 ||
                    string.IsNullOrWhiteSpace(line.Name) || string.IsNullOrWhiteSpace(line.Evidence))
                    throw Invalid("INVALID_COMPONENT");

                precision.ValidateQuantity(line.Quantity, line.Name);
                if (line.Cost.HasValue) precision.ValidateMagnitude(line.Cost.Value, line.Name);
                if (line.EstimateLineIndex is int index)
                {
                    if (index < 0 || index >= estimate.Lines.Count || !seen.Add(index))
                        throw Invalid("DUPLICATE_OR_UNKNOWN_COMPONENT");
                    var expected = estimate.Lines[index].Quantity;
                    if (line.Activity != expected.Activity || line.UnitId != expected.UnitId)
                        throw Invalid("INCOMPATIBLE_UNIT_OR_KIND");
                }
            }

            var comparable = sameQuantity && sameCostPerimeter;
            var rows = new List<OrderComparedComponent>();
            for (var i = 0; i < estimate.Lines.Count; i++)
            {
                var expected = estimate.Lines[i];
                var matched = actual.SingleOrDefault(x => x.EstimateLineIndex == i);
                var amount = matched?.Cost;
                rows.Add(new(i, expected.Quantity.Name, expected.Quantity.Activity, expected.Quantity.UnitId,
                    expected.Quantity.RequiredQuantity, matched?.Quantity, expected.Amount, amount,
                    comparable && amount.HasValue ? amount.Value - expected.Amount : null, false));
            }

            foreach (var extra in actual.Where(x => x.EstimateLineIndex is null))
                rows.Add(new(null, extra.Name, extra.Activity, extra.UnitId,
                    null, extra.Quantity, null, extra.Cost,
                    comparable ? extra.Cost : null, true));

            var hasPending = rows.Any(x => !x.RealCost.HasValue);
            var valuedSubtotal = rows.Where(x => x.RealCost.HasValue).Sum(x => x.RealCost!.Value);
            precision.ValidateMagnitude(valuedSubtotal, "ValuedSubtotal");
            decimal? total = hasPending ? null : valuedSubtotal;
            return new(OrderComparisonMode.MaterialsAndProcesses, producedQuantity, estimate.Total, estimate.UnitCost,
                total, total / producedQuantity, valuedSubtotal,
                comparable && total.HasValue ? total.Value - estimate.Total : null,
                FrozenList.Copy(rows), hasPending, comparable, controllerConfirmed, currencyCode,
                "DetailedConsumption");
        }
        catch (OverflowException) { throw Invalid("OVERFLOW"); }
    }
}
