using System.Globalization;
using System.Text.Json;
using Valdrio.Calculation;

namespace Valdrio.Tests;

[TestClass]
public sealed class T02ReferenceCorpusTests
{
    [TestMethod]
    public void RelevantRoundingCorpusCases_MatchExactly()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "reference",
            "t02-rounding-cases.json");

        using var document = JsonDocument.Parse(
            File.ReadAllText(path));

        var rounder = new BalancedRounder();
        var allocator = new ProportionalAllocator();

        var verified = new List<string>();

        foreach (var item in document.RootElement
                     .GetProperty("cases")
                     .EnumerateArray())
        {
            var id = item.GetProperty("id").GetString()
                ?? throw new InvalidDataException("Case ID missing.");

            var input = item.GetProperty("input");
            var quantum = ParseReference(
                input.GetProperty("quantum").GetString());

            BalancedRoundResult result;

            if (input.TryGetProperty("weights", out var weights))
            {
                var amount = ParseReference(
                    input.GetProperty("amount").GetString());

                var destinations = weights
                    .EnumerateObject()
                    .Select(property =>
                        new WeightedDestination(
                            new StableRoundingId(property.Name),
                            ParseReference(property.Value.GetString())))
                    .ToArray();

                result = rounder.Round(
                    amount,
                    allocator.FromWeights(
                        amount,
                        destinations),
                    quantum);
            }
            else
            {
                var source = ParseReference(
                    input.GetProperty("source").GetString());

                var raw = input.GetProperty("raw")
                    .EnumerateObject()
                    .Select(property =>
                        new RoundingContribution(
                            new StableRoundingId(property.Name),
                            ParseReference(property.Value.GetString())))
                    .ToArray();

                result = rounder.Round(
                    source,
                    raw,
                    quantum);
            }

            var expected = item
                .GetProperty("expected")
                .GetProperty("posted");

            foreach (var property in expected.EnumerateObject())
            {
                var expectedAmount =
                    ParseReference(property.Value.GetString());

                var actual = result.Lines.Single(
                    line =>
                        line.DestinationId.Value ==
                        property.Name);

                Assert.AreEqual(
                    expectedAmount,
                    actual.PostedAmount,
                    $"{id}/{property.Name}");
            }

            var expectedTotal = ParseReference(
                item.GetProperty("expected")
                    .GetProperty("total")
                    .GetString());

            Assert.AreEqual(
                expectedTotal,
                result.PostedTotal,
                $"{id}/total");

            verified.Add(id);
        }

        CollectionAssert.AreEqual(
            new[] { "C07", "C08", "C21", "C38", "C39" },
            verified.ToArray());
    }

    private static decimal ParseReference(string? text)
    {
        if (text is null)
        {
            throw new InvalidDataException(
                "Reference decimal is null.");
        }

        return decimal.Parse(
            text,
            NumberStyles.AllowLeadingSign |
            NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture);
    }
}
