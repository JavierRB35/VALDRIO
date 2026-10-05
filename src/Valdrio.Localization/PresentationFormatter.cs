using System.Globalization;
using Valdrio.Domain;

namespace Valdrio.Localization;

public sealed class DisplayTimeZoneResolver
{
    public TimeZoneInfo Resolve(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException)
        {
            if (TimeZoneInfo.TryConvertIanaIdToWindowsId(id, out var windowsId))
            {
                return TimeZoneInfo.FindSystemTimeZoneById(windowsId);
            }

            throw;
        }
    }
}

public sealed class PresentationFormatter
{
    private readonly DisplayTimeZoneResolver timeZones;

    public PresentationFormatter(DisplayTimeZoneResolver timeZones)
    {
        this.timeZones = timeZones;
    }

    public string FormatCurrency(
        decimal amount,
        string currencyCode,
        CultureInfo culture,
        NumberDisplayOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currencyCode);
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentNullException.ThrowIfNull(options);

        var pattern = options.UseGrouping
            ? $"N{options.DecimalDigits}"
            : $"F{options.DecimalDigits}";

        return $"{amount.ToString(pattern, culture)} {currencyCode.ToUpperInvariant()}";
    }

    public string FormatCurrency(Money money, CurrencyMetadataSnapshot metadata,
        CultureInfo culture, NumberDisplayOptions options)
    {
        money.EnsureValid("displayMoney");
        metadata.EnsureValid();
        if (money.Currency != metadata.AlphaCode)
            throw new ArgumentException("DISPLAY_CURRENCY_MISMATCH");
        // A normal monetary display retains at least the currency's minor units.
        // Extra ledger precision is an explicit view; display never changes the ledger.
        return FormatCurrency(money.Amount, money.Currency.Value, culture,
            options with { DecimalDigits = Math.Max(options.DecimalDigits, metadata.MinorUnitDigits) });
    }

    public string FormatDate(
        DateOnly value,
        CultureInfo culture,
        DateDisplayPreset preset) =>
        preset switch
        {
            DateDisplayPreset.Short => value.ToString("d", culture),
            DateDisplayPreset.Long => value.ToString("D", culture),
            DateDisplayPreset.Iso => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            _ => throw new ArgumentOutOfRangeException(nameof(preset))
        };

    public string FormatInstant(
        DateTimeOffset utcInstant,
        string timeZoneId,
        CultureInfo culture)
    {
        var zone = timeZones.Resolve(timeZoneId);
        var local = TimeZoneInfo.ConvertTime(utcInstant, zone);
        var localDateTime = local.ToString("g", culture);
        var offset = local.ToString("zzz", CultureInfo.InvariantCulture);

        return $"{localDateTime} {offset}";
    }
}
