using System.Globalization;
using Valdrio.Domain;

namespace Valdrio.Application;

public sealed record LocalInstantView(
    string TimeZoneId,
    DateTime LocalDateTime,
    TimeSpan Offset)
{
    public string OffsetText =>
        TimeZoneResolver.FormatOffset(Offset);
}

public sealed class TimeZoneResolver
{
    public TimeZoneInfo Resolve(string timeZoneId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            if (TimeZoneInfo.TryConvertIanaIdToWindowsId(
                    timeZoneId,
                    out var windowsId))
            {
                return TimeZoneInfo.FindSystemTimeZoneById(
                    windowsId);
            }

            throw;
        }
    }

    public LocalInstantView ToLocal(
        UtcInstant instant,
        string timeZoneId)
    {
        var zone = Resolve(timeZoneId);
        var local = TimeZoneInfo.ConvertTime(
            instant.Value,
            zone);

        return new LocalInstantView(
            timeZoneId,
            local.DateTime,
            local.Offset);
    }

    public IReadOnlyList<TimeSpan> GetValidOffsets(
        DateTime localDateTime,
        string timeZoneId)
    {
        var local = AsUnspecified(localDateTime);
        var zone = Resolve(timeZoneId);

        if (zone.IsInvalidTime(local))
        {
            return [];
        }

        if (zone.IsAmbiguousTime(local))
        {
            return zone
                .GetAmbiguousTimeOffsets(local)
                .OrderByDescending(offset => offset)
                .ToArray();
        }

        return [zone.GetUtcOffset(local)];
    }

    public UtcInstant InterpretLocal(
        DateTime localDateTime,
        string timeZoneId,
        TimeSpan? explicitOffset = null)
    {
        var local = AsUnspecified(localDateTime);
        var zone = Resolve(timeZoneId);

        if (zone.IsInvalidTime(local))
        {
            throw TemporalDiagnostic(
                "TIME_LOCAL_INVALID",
                "Time.LocalInvalid",
                local,
                timeZoneId,
                []);
        }

        if (zone.IsAmbiguousTime(local))
        {
            var offsets = zone.GetAmbiguousTimeOffsets(local);

            if (!explicitOffset.HasValue)
            {
                throw TemporalDiagnostic(
                    "TIME_LOCAL_AMBIGUOUS",
                    "Time.LocalAmbiguous",
                    local,
                    timeZoneId,
                    offsets);
            }

            if (!offsets.Contains(explicitOffset.Value))
            {
                throw TemporalDiagnostic(
                    "TIME_LOCAL_OFFSET_INVALID",
                    "Time.LocalOffsetInvalid",
                    local,
                    timeZoneId,
                    offsets);
            }

            return UtcInstant.From(
                new DateTimeOffset(
                    local,
                    explicitOffset.Value));
        }

        var resolvedOffset = zone.GetUtcOffset(local);

        if (explicitOffset.HasValue &&
            explicitOffset.Value != resolvedOffset)
        {
            throw TemporalDiagnostic(
                "TIME_LOCAL_OFFSET_INVALID",
                "Time.LocalOffsetInvalid",
                local,
                timeZoneId,
                [resolvedOffset]);
        }

        return UtcInstant.From(
            new DateTimeOffset(
                local,
                resolvedOffset));
    }

    public static string FormatOffset(TimeSpan offset)
    {
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        var absolute = offset.Duration();

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{sign}{absolute.Hours:00}:{absolute.Minutes:00}");
    }

    private static DateTime AsUnspecified(
        DateTime value) =>
        DateTime.SpecifyKind(
            value,
            DateTimeKind.Unspecified);

    private static DomainRuleException TemporalDiagnostic(
        string code,
        string messageKey,
        DateTime local,
        string timeZoneId,
        IReadOnlyList<TimeSpan> validOffsets)
    {
        var offsets = validOffsets.Count == 0
            ? "(none)"
            : string.Join(
                "|",
                validOffsets.Select(FormatOffset));

        return new DomainRuleException(
            new DomainDiagnostic(
                code,
                messageKey,
                [
                    new(
                        "localTime",
                        DiagnosticArgumentType.Text,
                        local.ToString(
                            "yyyy-MM-dd'T'HH:mm:ss",
                            CultureInfo.InvariantCulture)),
                    new(
                        "timeZoneId",
                        DiagnosticArgumentType.Text,
                        timeZoneId),
                    new(
                        "validOffsets",
                        DiagnosticArgumentType.Text,
                        offsets)
                ]));
    }
}
