using System.Globalization;
using Valdrio.Domain;

namespace Valdrio.Application;

// Nullable fields mean unavailable, never an invented calendar or measured activity.
public sealed record PhysicalCalendarDefaults(decimal? Days, decimal? Shifts, decimal? Hours,
    decimal? Resources, decimal? Stops, decimal? Factor, string? StopsBasis, decimal? StopsEntered);
public sealed record PhysicalSuggestionContext(EntityId CompanyId, EntityId ModelVersionId,
    EntityId NodeId, EntityId PoolId, UnitId UnitId, BusinessDate Start, BusinessDate End);
public sealed record PhysicalActivitySuggestions(PhysicalObservation? CalendarReference,
    IReadOnlyList<PhysicalObservation> ComparableHistory);
public sealed record PhysicalCalendarSegment(BusinessDate Start, BusinessDate End, PhysicalCalendarInput Input);

public static class PhysicalActivityInference
{
    public static PhysicalActivitySuggestions Suggest(PhysicalSuggestionContext context,
        IEnumerable<PhysicalObservation> observations)
    {
        var latest = observations.Where(o => o.CompanyId == context.CompanyId &&
                o.ModelVersionId == context.ModelVersionId && o.NodeId == context.NodeId &&
                o.PoolId == context.PoolId && o.UnitId == context.UnitId)
            .GroupBy(o => (o.StudyId, o.PeriodStart, o.PeriodEnd))
            .Select(g => g.MaxBy(o => o.Version)!)
            .Where(o => o.State == PhysicalObservationState.Confirmed && o.PeriodEnd.Value < context.Start.Value)
            .OrderByDescending(o => o.PeriodEnd.Value).ThenByDescending(o => o.RecordedAtUtc.ToString())
            .ToArray();
        // Alternative analyses of the same period must not weight that period twice.
        var duration = context.End.Value.DayNumber - context.Start.Value.DayNumber;
        var history = new List<PhysicalObservation>();
        foreach (var item in latest.Where(o => o.RealQuantity is >= 0 &&
                     o.PeriodEnd.Value.DayNumber - o.PeriodStart.Value.DayNumber == duration))
            if (!history.Any(h => item.PeriodStart.Value <= h.PeriodEnd.Value &&
                                  item.PeriodEnd.Value >= h.PeriodStart.Value)) history.Add(item);
        return new(latest.FirstOrDefault(o => o.NormalEvidence?.Quality == PhysicalEvidenceQuality.Calculated && ReadCalendar(o.NormalEvidence?.Description) is not null), history);
    }

    // Read the invariant calendar evidence already stored by released versions.
    // Keeping this format avoids rewriting immutable observations or their hashes.
    public static PhysicalCalendarDefaults? ReadCalendar(string? description)
    {
        if (description?.StartsWith("calendar: days=", StringComparison.Ordinal) != true) return null;
        var values = description.Split("; ").Select(p => p.Split('=', 2)).Where(p => p.Length == 2)
            .GroupBy(p => p[0]).ToDictionary(g => g.Key, g => g.First()[1]);
        decimal? Read(string key) => values.TryGetValue(key, out var s) &&
            decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? n : null;
        return new(Read("calendar: days"), Read("shifts"), Read("hours"), Read("resources"),
            Read("stops"), Read("factor"), values.GetValueOrDefault("stopsBasis"), Read("stopsEntered"));
    }

    public static PhysicalCalendarProposal CalendarSegments(IReadOnlyList<PhysicalCalendarSegment> segments)
    {
        if (segments.Count == 0) throw PhysicalActivityRules.Error("ACT_CALENDAR_INVALID", "Activity.CalendarInvalid");
        var ordered = segments.OrderBy(s => s.Start.Value).ToArray();
        decimal theoretical = 0, practical = 0, normal = 0;
        for (var i = 0; i < ordered.Length; i++)
        {
            var s = ordered[i];
            if (s.End.Value < s.Start.Value || s.Input.OperatingDays > s.End.Value.DayNumber - s.Start.Value.DayNumber + 1 ||
                i > 0 && ordered[i - 1].End.Value >= s.Start.Value)
                throw PhysicalActivityRules.Error("ACT_CALENDAR_INVALID", "Activity.CalendarInvalid");
            var value = PhysicalNormalCalculator.Calendar(s.Input);
            theoretical = checked(theoretical + value.Theoretical);
            practical = checked(practical + value.Practical);
            normal = checked(normal + value.Normal);
        }
        return new(theoretical, practical, normal);
    }
}
