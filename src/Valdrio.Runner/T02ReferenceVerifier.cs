using System.Globalization;
using System.Text.Json;
using Valdrio.Calculation;

namespace Valdrio.Runner;

internal static class T02ReferenceVerifier
{
    public static IReadOnlyList<string> Verify(string path)
    {
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
            var quantum = Parse(
                input.GetProperty("quantum").GetString());

            BalancedRoundResult result;

            if (input.TryGetProperty("weights", out var weights))
            {
                var amount = Parse(
                    input.GetProperty("amount").GetString());

                result = rounder.Round(
                    amount,
                    allocator.FromWeights(
                        amount,
                        weights.EnumerateObject()
                            .Select(property =>
                                new WeightedDestination(
                                    new StableRoundingId(property.Name),
                                    Parse(property.Value.GetString())))
                            .ToArray()),
                    quantum);
            }
            else
            {
                var source = Parse(
                    input.GetProperty("source").GetString());

                result = rounder.Round(
                    source,
                    input.GetProperty("raw")
                        .EnumerateObject()
                        .Select(property =>
                            new RoundingContribution(
                                new StableRoundingId(property.Name),
                                Parse(property.Value.GetString())))
                        .ToArray(),
                    quantum);
            }

            foreach (var property in item
                         .GetProperty("expected")
                         .GetProperty("posted")
                         .EnumerateObject())
            {
                var actual = result.Lines.Single(
                    line => line.DestinationId.Value == property.Name);

                if (actual.PostedAmount != Parse(property.Value.GetString()))
                {
                    throw new InvalidOperationException(
                        $"{id}/{property.Name} differs from reference.");
                }
            }

            if (result.PostedTotal != Parse(
                    item.GetProperty("expected")
                        .GetProperty("total")
                        .GetString()))
            {
                throw new InvalidOperationException(
                    $"{id}/total differs from reference.");
            }

            verified.Add(id);
        }

        return verified;
    }

    private static decimal Parse(string? text)
    {
        if (text is null)
        {
            throw new InvalidDataException("Reference decimal missing.");
        }

        return decimal.Parse(
            text,
            NumberStyles.AllowLeadingSign |
            NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture);
    }
}
