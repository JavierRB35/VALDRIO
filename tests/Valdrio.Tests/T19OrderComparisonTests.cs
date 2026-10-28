using Valdrio.Calculation;
using Valdrio.Domain;

namespace Valdrio.Tests;

[TestClass]
public sealed class T19OrderComparisonTests
{
    private static EntityId Id() => new(Guid.NewGuid());

    private static (EstimatedCost estimate, UnitId unit) Baseline()
    {
        UnitId unit = new(Id());
        var names = new[] { "Material A", "Material B", "Setup", "Process" };
        var costs = new[] { 200m, 150m, 100m, 250m };
        var quantities = new[] { 20m, 15m, 5m, 25m };
        var lines = names.Select((name, i) =>
        {
            var quantity = new CompositionQuantity(name, Id(), Id(), i > 1,
                unit, 1m, quantities[i], quantities[i], quantities[i], quantities[i], name);
            return new EstimatedCostLine(quantity, 10m, "reference", costs[i], costs[i]);
        }).ToArray();
        return (new EstimatedCost(100m, 1m, lines, 700m, 7m), unit);
    }

    [TestMethod]
    public void ReportedTotalComparesWithoutInventingMaterialDetailsOrLedgerFacts()
    {
        var (estimate, _) = Baseline();
        var result = OrderCostComparisonCalculator.CompareTotal(
            estimate, 100m, 7.8m, "EUR", "controller: production cost report",
            true, true, PrecisionPolicy.Pdec1Proposed);
        Assert.AreEqual(OrderComparisonMode.TotalReported, result.Mode);
        Assert.AreEqual(780m, result.RealTotal);
        Assert.AreEqual(80m, result.TotalDifference);
        Assert.AreEqual(7.8m, result.RealUnitCost);
        Assert.AreEqual(0, result.Components.Count);
        Assert.IsTrue(result.Comparable);
        Assert.IsFalse(result.HasPendingCosts);
        Assert.IsTrue(result.ControllerConfirmed);
    }

    [TestMethod]
    public void DetailedComparesMaterialsAndProcessesAgainstFrozenEstimate()
    {
        var (estimate, unit) = Baseline();
        ActualOrderComponent[] actual =
        [
            new(0, "Material A", false, unit, 22m, 220m, "issue A"),
            new(1, "Material B", false, unit, 16.5m, 165m, "issue B"),
            new(2, "Setup", true, unit, 5.5m, 110m, "work report 1"),
            new(3, "Process", true, unit, 28.5m, 285m, "work report 2")
        ];
        var result = OrderCostComparisonCalculator.CompareDetailed(
            estimate, 100m, actual, "EUR", true, true, PrecisionPolicy.Pdec1Proposed);
        Assert.AreEqual(OrderComparisonMode.MaterialsAndProcesses, result.Mode);
        Assert.AreEqual(780m, result.RealTotal);
        Assert.AreEqual(80m, result.TotalDifference);
        Assert.AreEqual(20m, result.Components[0].Difference);
        Assert.AreEqual(15m, result.Components[1].Difference);
        Assert.AreEqual(10m, result.Components[2].Difference);
        Assert.AreEqual(35m, result.Components[3].Difference);
        Assert.IsFalse(result.HasPendingCosts);
        Assert.AreEqual(700m, estimate.Total, "T18 remains unchanged");
    }

    [TestMethod]
    public void MissingCostDoesNotBecomeZeroOrProduceMisleadingTotalDifference()
    {
        var (estimate, unit) = Baseline();
        ActualOrderComponent[] input =
        [
            new(0, "Material A", false, unit, 22m, 220m, "issue A"),
            new(1, "Material B", false, unit, 16.5m, null, "issue B")
        ];
        var result = OrderCostComparisonCalculator.CompareDetailed(
            estimate, 100m, input, "EUR", true, false, PrecisionPolicy.Pdec1Proposed);
        Assert.IsTrue(result.HasPendingCosts);
        Assert.IsNull(result.RealTotal);
        Assert.IsNull(result.TotalDifference);
        Assert.AreEqual(220m, result.ValuedSubtotal);
        Assert.AreEqual(4, result.Components.Count);
        Assert.IsNull(result.Components[1].RealCost);
        Assert.IsNull(result.Components[2].RealQuantity, "Missing is not zero");
    }

    [TestMethod]
    public void UnplannedMaterialIsNotHiddenOrCountedTwice()
    {
        var (estimate, unit) = Baseline();
        var input = Enumerable.Range(0, 4).Select(i =>
            new ActualOrderComponent(i, estimate.Lines[i].Quantity.Name,
                estimate.Lines[i].Quantity.Activity, unit, 1m, estimate.Lines[i].Amount, "report " + i)).ToList();
        input.Add(new(null, "Additional material", false, unit, 1m, 30m, "extra issue"));
        var result = OrderCostComparisonCalculator.CompareDetailed(
            estimate, 100m, input, "EUR", true, true, PrecisionPolicy.Pdec1Proposed);
        Assert.AreEqual(730m, result.RealTotal);
        Assert.AreEqual(30m, result.TotalDifference);
        Assert.IsTrue(result.Components.Last().Unplanned);
        Assert.IsNull(result.Components.Last().EstimatedCost);
    }

    [TestMethod]
    public void IncompatibleOutputQuantityOrCostPerimeterSuppressesDifference()
    {
        var (estimate, _) = Baseline();
        var differentQuantity = OrderCostComparisonCalculator.CompareTotal(
            estimate, 120m, 7.8m, "EUR", "controller", true, true, PrecisionPolicy.Pdec1Proposed);
        Assert.AreEqual(936m, differentQuantity.RealTotal);
        Assert.IsNull(differentQuantity.TotalDifference);
        Assert.IsFalse(differentQuantity.Comparable);
        var differentScope = OrderCostComparisonCalculator.CompareTotal(
            estimate, 100m, 7.8m, "EUR", "controller", false, true, PrecisionPolicy.Pdec1Proposed);
        Assert.IsNull(differentScope.TotalDifference);
    }

    [TestMethod]
    public void DuplicateOrIncompatibleDetailedComponentIsRejected()
    {
        var (estimate, unit) = Baseline();
        ActualOrderComponent[] duplicate =
        [
            new(0, "Material A", false, unit, 2m, 20m, "report 1"),
            new(0, "Material A", false, unit, 3m, 30m, "report 2")
        ];
        Assert.ThrowsExactly<DomainRuleException>(() => OrderCostComparisonCalculator.CompareDetailed(
            estimate, 100m, duplicate, "EUR", true, true, PrecisionPolicy.Pdec1Proposed));

        var incompatible = new[] { new ActualOrderComponent(0, "Material A", true, unit, 2m, 20m, "report") };
        Assert.ThrowsExactly<DomainRuleException>(() => OrderCostComparisonCalculator.CompareDetailed(
            estimate, 100m, incompatible, "EUR", true, true, PrecisionPolicy.Pdec1Proposed));
    }

    [TestMethod]
    public void ExplicitZeroIsAllowedButNegativeOrMissingEvidenceIsNot()
    {
        var (estimate, unit) = Baseline();
        var input = Enumerable.Range(0, 4).Select(i =>
            new ActualOrderComponent(i, estimate.Lines[i].Quantity.Name,
                estimate.Lines[i].Quantity.Activity, unit, 0m, 0m, "zero consumption verified")).ToArray();
        var result = OrderCostComparisonCalculator.CompareDetailed(
            estimate, 100m, input, "EUR", true, true, PrecisionPolicy.Pdec1Proposed);
        Assert.AreEqual(0m, result.RealTotal);
        Assert.AreEqual(-700m, result.TotalDifference);
        Assert.IsFalse(result.HasPendingCosts);

        Assert.ThrowsExactly<DomainRuleException>(() => OrderCostComparisonCalculator.CompareTotal(
            estimate, 100m, -1m, "EUR", "controller", true, true, PrecisionPolicy.Pdec1Proposed));
        Assert.ThrowsExactly<DomainRuleException>(() => OrderCostComparisonCalculator.CompareTotal(
            estimate, 100m, 7.8m, "EUR", "", true, true, PrecisionPolicy.Pdec1Proposed));
    }
}
