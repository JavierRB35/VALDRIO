using System.Globalization;
using Valdrio.Domain;

namespace Valdrio.Application;

public sealed record TemporalContractExample(
    string BusinessDateIso,
    string UtcInstant,
    string DisplayTimeZoneId,
    string LocalInstant);

public sealed class TemporalContractExampleService
{
    private readonly TimeZoneResolver timeZones;

    public TemporalContractExampleService(
        TimeZoneResolver timeZones)
    {
        this.timeZones = timeZones;
    }

    public TemporalContractExample Build(
        string displayTimeZoneId)
    {
        var businessDate =
            new BusinessDate(
                new DateOnly(2026, 10, 4));

        var instant = UtcInstant.ParseIso(
            "2026-10-04T00:30:00Z");

        var local = timeZones.ToLocal(
            instant,
            displayTimeZoneId);

        var localText = string.Create(
            CultureInfo.InvariantCulture,
            $"{local.LocalDateTime:yyyy-MM-dd HH:mm:ss} {local.OffsetText}");

        return new TemporalContractExample(
            businessDate.ToCanonicalString(),
            instant.ToCanonicalString(),
            displayTimeZoneId,
            localText);
    }
}
