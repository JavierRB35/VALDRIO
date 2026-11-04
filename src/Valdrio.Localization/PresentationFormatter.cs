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

        // Currency rounding is a view concern. Do not change the source Money,
        // project a new ledger quantum, or force trailing zeroes on whole values.
        var decimals = Math.Clamp(options.DecimalDigits, 0, 2);
        var pattern = (options.UseGrouping ? "#,##0" : "0") +
            (decimals > 0 ? "." + new string('#', decimals) : "");
        var rounded = decimal.Round(amount, decimals, MidpointRounding.AwayFromZero);
        var marker = rounded == 0m && amount != 0m ? "≈ " : "";
        return $"{marker}{rounded.ToString(pattern, culture)} {currencyCode.ToUpperInvariant()}";
    }

    public string FormatCurrency(Money money, CurrencyMetadataSnapshot metadata,
        CultureInfo culture, NumberDisplayOptions options)
    {
        money.EnsureValid("displayMoney");
        metadata.EnsureValid();
        if (money.Currency != metadata.AlphaCode)
            throw new ArgumentException("DISPLAY_CURRENCY_MISMATCH");
        // A detailed/audit view retains frozen currency metadata and full ledger
        // precision. The compact business view never exceeds two decimals.
        return FormatCurrency(money.Amount, money.Currency.Value, culture,
            options with { DecimalDigits = Math.Min(2, Math.Max(options.DecimalDigits, metadata.MinorUnitDigits)) });
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
