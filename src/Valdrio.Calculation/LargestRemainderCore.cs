namespace Valdrio.Calculation;

internal static class LargestRemainderCore
{
    public static IReadOnlyDictionary<StableRoundingId, decimal> Round(
        decimal signReference,
        decimal target,
        IReadOnlyList<(StableRoundingId Id, decimal Amount)> lines,
        decimal quantum,
        bool pendingMayEqualLineCount)
    {
        if (quantum <= 0m)
        {
            throw RoundingDiagnostics.InvalidQuantum(quantum);
        }

        if (lines.Count == 0)
        {
            throw new ArgumentException(
                "At least one contribution is required.",
                nameof(lines));
        }

        EnsureUniqueIds(lines.Select(line => line.Id));

        var sign = signReference < 0m ? -1m : 1m;
        var workingTarget = checked(target * sign);

        var targetUnits = workingTarget / quantum;
        if (targetUnits != decimal.Truncate(targetUnits))
        {
            throw RoundingDiagnostics.InvalidQuantum(quantum);
        }

        var work = new List<WorkLine>(lines.Count);
        var floorTotal = 0m;

        foreach (var line in lines)
        {
            var workingAmount = checked(line.Amount * sign);
            var scaled = workingAmount / quantum;
            var floorUnits = decimal.Floor(scaled);
            var fractional = checked(scaled - floorUnits);

            floorTotal = checked(floorTotal + floorUnits);
            work.Add(
                new WorkLine(
                    line.Id,
                    floorUnits,
                    fractional));
        }

        var pendingDecimal = checked(targetUnits - floorTotal);

        if (pendingDecimal != decimal.Truncate(pendingDecimal))
        {
            throw new InvalidOperationException(
                "Pending quantum units must be integral.");
        }

        var upperBound = pendingMayEqualLineCount
            ? lines.Count
            : lines.Count - 1;

        if (pendingDecimal < 0m || pendingDecimal > upperBound)
        {
            throw new InvalidOperationException(
                $"Pending quantum units {pendingDecimal} are outside the allowed range 0..{upperBound}.");
        }

        var pending = decimal.ToInt32(pendingDecimal);

        var ranked = work
            .OrderByDescending(line => line.Fractional)
            .ThenBy(line => line.Id.Value, StringComparer.Ordinal)
            .ToArray();

        var awarded = ranked
            .Take(pending)
            .Select(line => line.Id)
            .ToHashSet();

        return work
            .OrderBy(line => line.Id.Value, StringComparer.Ordinal)
            .ToDictionary(
                line => line.Id,
                line =>
                {
                    var units = line.FloorUnits +
                        (awarded.Contains(line.Id) ? 1m : 0m);
                    return checked(units * quantum * sign);
                });
    }

    private static void EnsureUniqueIds(
        IEnumerable<StableRoundingId> ids)
    {
        var seen = new HashSet<StableRoundingId>();

        foreach (var id in ids)
        {
            if (!seen.Add(id))
            {
                throw RoundingDiagnostics.DuplicateDestination(id);
            }
        }
    }

    private sealed record WorkLine(
        StableRoundingId Id,
        decimal FloorUnits,
        decimal Fractional);
}
