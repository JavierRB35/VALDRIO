using System.Globalization;
using Valdrio.Calculation;
using Valdrio.Domain;

namespace Valdrio.Tests;

[TestClass]
public sealed class T02BalancedRoundTests
{
    private readonly BalancedRounder rounder = new();
    private readonly ProportionalAllocator allocator = new();
    private readonly CurrencyProjector projector = new();

    [TestMethod]
    public void C07_PositiveCentInThreeEqualParts_UsesStableIdTieBreak()
    {
        var result = RoundWeighted(
            0.01m,
            0.01m,
            ["C", "A", "B"]);

        AssertPosted(
            result,
            ("A", 0.01m),
            ("B", 0m),
            ("C", 0m));

        Assert.AreEqual(0.01m, result.PostedTotal);
    }

    [TestMethod]
    public void C08_NegativeCent_IsSignSymmetric()
    {
        var positive = RoundWeighted(
            0.01m,
            0.01m,
            ["A", "B", "C"]);

        var negative = RoundWeighted(
            -0.01m,
            0.01m,
            ["A", "B", "C"]);

        foreach (var positiveLine in positive.Lines)
        {
            var negativeLine = negative.Lines.Single(
                line => line.DestinationId == positiveLine.DestinationId);

            Assert.AreEqual(
                -positiveLine.PostedAmount,
                negativeLine.PostedAmount);
        }

        Assert.AreEqual(-0.01m, negative.PostedTotal);
    }

    [TestMethod]
    public void C21_SignedVariation_UsesFloorForNegativeContribution()
    {
        var result = rounder.Round(
            1m,
            [
                new(new StableRoundingId("O1"), 0.6033m),
                new(new StableRoundingId("O2"), 0.6033m),
                new(new StableRoundingId("VAR"), -0.2066m)
            ],
            0.01m);

        AssertPosted(
            result,
            ("O1", 0.60m),
            ("O2", 0.60m),
            ("VAR", -0.20m));

        Assert.AreEqual(1m, result.PostedTotal);
    }

    [TestMethod]
    public void Result_IsIndependentFromUiCultureAndInputOrder()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentCulture =
                CultureInfo.GetCultureInfo("es-ES");
            CultureInfo.CurrentUICulture =
                CultureInfo.GetCultureInfo("es-ES");

            var spanish = RoundWeighted(
                0.01m,
                0.01m,
                ["C", "B", "A"]);

            CultureInfo.CurrentCulture =
                CultureInfo.GetCultureInfo("en-US");
            CultureInfo.CurrentUICulture =
                CultureInfo.GetCultureInfo("en-US");

            var english = RoundWeighted(
                0.01m,
                0.01m,
                ["B", "A", "C"]);

            CollectionAssert.AreEqual(
                CanonicalLines(spanish),
                CanonicalLines(english));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [TestMethod]
    public void C38AndC39_QuantumIsNotHardcodedToTwoDecimals()
    {
        var jpy = RoundWeighted(
            101m,
            1m,
            ["A", "B", "C"]);

        AssertPosted(
            jpy,
            ("A", 34m),
            ("B", 34m),
            ("C", 33m));

        var kwd = RoundWeighted(
            0.001m,
            0.001m,
            ["A", "B", "C"]);

        AssertPosted(
            kwd,
            ("A", 0.001m),
            ("B", 0m),
            ("C", 0m));
    }

    [TestMethod]
    public void CurrencyProjection_0015Eur_ProjectsToTwoCentsWithoutChangingLedger()
    {
        var result = projector.Project(
            0.015m,
            [
                new(new StableRoundingId("A"), 0.0075m),
                new(new StableRoundingId("B"), 0.0075m)
            ],
            0.01m);

        Assert.AreEqual(0.02m, result.Target);
        Assert.AreEqual(0.005m, result.ProjectionDifference);
        Assert.AreEqual(0.015m, result.SourceLedger);
        Assert.AreEqual(0.01m, Amount(result, "A"));
        Assert.AreEqual(0.01m, Amount(result, "B"));
        Assert.AreEqual(0.02m, result.ProjectedTotal);

        Assert.AreEqual(
            0.0075m,
            result.Lines.Single(
                line => line.DestinationId.Value == "A")
            .LedgerAmount);
    }

    [TestMethod]
    public void CurrencyProjection_0005Eur_UsesMidpointToEvenAndShowsDifference()
    {
        var result = projector.Project(
            0.005m,
            [
                new(new StableRoundingId("A"), 0.0025m),
                new(new StableRoundingId("B"), 0.0025m)
            ],
            0.01m);

        Assert.AreEqual(0m, result.Target);
        Assert.AreEqual(-0.005m, result.ProjectionDifference);
        Assert.AreEqual(0m, Amount(result, "A"));
        Assert.AreEqual(0m, Amount(result, "B"));
        Assert.AreEqual(0m, result.ProjectedTotal);
    }

    [TestMethod]
    public void CurrencyProjection_NegativesAreSignSymmetric()
    {
        var positive = projector.Project(
            0.015m,
            [
                new(new StableRoundingId("A"), 0.0075m),
                new(new StableRoundingId("B"), 0.0075m)
            ],
            0.01m);

        var negative = projector.Project(
            -0.015m,
            [
                new(new StableRoundingId("A"), -0.0075m),
                new(new StableRoundingId("B"), -0.0075m)
            ],
            0.01m);

        Assert.AreEqual(-positive.Target, negative.Target);
        Assert.AreEqual(
            -positive.ProjectionDifference,
            negative.ProjectionDifference);

        foreach (var line in positive.Lines)
        {
            Assert.AreEqual(
                -line.ProjectedAmount,
                Amount(negative, line.DestinationId.Value));
        }
    }

    [TestMethod]
    public void ConservationDifferenceAboveOneInternalQuantum_IsRejected()
    {
        var exception = CaptureDomainRule(
            () => rounder.Round(
                1m,
                [
                    new(new StableRoundingId("A"), 0.50m),
                    new(new StableRoundingId("B"), 0.49m)
                ],
                0.01m));

        Assert.AreEqual(
            "NUM_CONSERVATION_FAILURE",
            exception.Diagnostic.Code);
    }

    private BalancedRoundResult RoundWeighted(
        decimal amount,
        decimal quantum,
        IReadOnlyList<string> ids)
    {
        var contributions = allocator.FromWeights(
            amount,
            ids.Select(
                    id => new WeightedDestination(
                        new StableRoundingId(id),
                        1m))
                .ToArray());

        return rounder.Round(
            amount,
            contributions,
            quantum);
    }

    private static string[] CanonicalLines(
        BalancedRoundResult result) =>
        result.Lines
            .OrderBy(
                line => line.DestinationId.Value,
                StringComparer.Ordinal)
            .Select(
                line =>
                    $"{line.DestinationId.Value}:{CanonicalDecimal.Format(line.PostedAmount)}")
            .ToArray();

    private static void AssertPosted(
        BalancedRoundResult result,
        params (string Id, decimal Amount)[] expected)
    {
        foreach (var item in expected)
        {
            var actual = result.Lines.Single(
                line => line.DestinationId.Value == item.Id);

            Assert.AreEqual(
                item.Amount,
                actual.PostedAmount,
                $"Unexpected posted amount for {item.Id}.");
        }
    }

    private static decimal Amount(
        CurrencyProjectionResult result,
        string id) =>
        result.Lines.Single(
            line => line.DestinationId.Value == id)
        .ProjectedAmount;

    private static DomainRuleException CaptureDomainRule(Action action)
    {
        try
        {
            action();
        }
        catch (DomainRuleException exception)
        {
            return exception;
        }

        Assert.Fail("Expected DomainRuleException.");
        throw new InvalidOperationException();
    }
}
