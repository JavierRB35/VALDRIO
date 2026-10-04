using Valdrio.Application;
using Valdrio.Domain;

namespace Valdrio.Runner;

internal static class T02ATimeVerifier
{
    public static IReadOnlyDictionary<string, object> Verify()
    {
        var resolver = new TimeZoneResolver();
        var businessDate =
            BusinessDate.ParseIso("2026-10-04");
        var instant =
            UtcInstant.ParseIso("2026-10-04T00:30:00Z");

        var madrid = resolver.ToLocal(
            instant,
            "Europe/Madrid");
        var losAngeles = resolver.ToLocal(
            instant,
            "America/Los_Angeles");

        var ambiguousLocal =
            new DateTime(2026, 10, 25, 2, 30, 0);

        var ambiguousCode = CaptureCode(
            () => resolver.InterpretLocal(
                ambiguousLocal,
                "Europe/Madrid"));

        var offsets = resolver.GetValidOffsets(
            ambiguousLocal,
            "Europe/Madrid");

        if (offsets.Count != 2)
        {
            throw new InvalidOperationException(
                "I09 fixture did not expose two valid offsets.");
        }

        var first = resolver.InterpretLocal(
            ambiguousLocal,
            "Europe/Madrid",
            offsets[0]);

        var second = resolver.InterpretLocal(
            ambiguousLocal,
            "Europe/Madrid",
            offsets[1]);

        if (first == second)
        {
            throw new InvalidOperationException(
                "I09 explicit choices resolved to the same instant.");
        }

        if (businessDate.ToCanonicalString() !=
                "2026-10-04"
            || instant.ToCanonicalString() !=
                "2026-10-04T00:30:00.0000000Z")
        {
            throw new InvalidOperationException(
                "I08 canonical identities changed.");
        }

        if (madrid.LocalDateTime.Date ==
            losAngeles.LocalDateTime.Date)
        {
            throw new InvalidOperationException(
                "I08 fixture did not demonstrate a local date difference.");
        }

        return new Dictionary<string, object>
        {
            ["business_date"] =
                businessDate.ToCanonicalString(),
            ["instant_utc"] =
                instant.ToCanonicalString(),
            ["madrid_local"] =
                $"{madrid.LocalDateTime:yyyy-MM-dd HH:mm:ss} {madrid.OffsetText}",
            ["los_angeles_local"] =
                $"{losAngeles.LocalDateTime:yyyy-MM-dd HH:mm:ss} {losAngeles.OffsetText}",
            ["i09"] = ambiguousCode,
            ["ambiguous_offsets"] =
                offsets.Select(
                    TimeZoneResolver.FormatOffset)
                .ToArray(),
            ["explicit_choice_distinct"] = true
        };
    }

    private static string CaptureCode(Action action)
    {
        try
        {
            action();
        }
        catch (DomainRuleException exception)
        {
            return exception.Diagnostic.Code;
        }

        throw new InvalidOperationException(
            "Expected a temporal diagnostic.");
    }
}
