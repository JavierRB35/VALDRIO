namespace Valdrio.Localization;

public enum DateDisplayPreset
{
    Short,
    Long,
    Iso
}

public sealed record NumberDisplayOptions(
    int DecimalDigits = 2,
    bool UseGrouping = true);

public sealed record UserPresentationPreferences(
    string UiLanguageTag,
    string FormatCultureTag,
    string DisplayTimeZoneId,
    DateDisplayPreset DateDisplayPreset,
    NumberDisplayOptions NumberDisplayOptions)
{
    public static UserPresentationPreferences Default { get; } =
        new(
            UiLanguageTag: "en",
            FormatCultureTag: "es-ES",
            DisplayTimeZoneId: "Europe/Madrid",
            DateDisplayPreset.Short,
            new NumberDisplayOptions());
}
