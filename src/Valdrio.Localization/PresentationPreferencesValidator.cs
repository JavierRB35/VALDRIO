using System.Globalization;

namespace Valdrio.Localization;

public sealed class PresentationPreferencesValidationException
    : ArgumentException
{
    public PresentationPreferencesValidationException(
        string code,
        string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}

public sealed class PresentationPreferencesValidator
{
    private readonly LanguageCatalog languages;
    private readonly DisplayTimeZoneResolver timeZones;

    public PresentationPreferencesValidator(
        LanguageCatalog languages,
        DisplayTimeZoneResolver timeZones)
    {
        this.languages = languages;
        this.timeZones = timeZones;
    }

    public void Validate(
        UserPresentationPreferences preferences)
    {
        if (preferences is null)
        {
            throw Invalid(
                "PREFERENCES_DOCUMENT_NULL",
                "Presentation preferences document is null.");
        }

        if (string.IsNullOrWhiteSpace(
                preferences.UiLanguageTag))
        {
            throw Invalid(
                "PREFERENCES_UI_LANGUAGE_INVALID",
                "UI language is required.");
        }

        try
        {
            languages.GetRequired(
                preferences.UiLanguageTag);
        }
        catch (ArgumentException exception)
        {
            throw Invalid(
                "PREFERENCES_UI_LANGUAGE_INVALID",
                exception.Message);
        }

        if (string.IsNullOrWhiteSpace(
                preferences.FormatCultureTag))
        {
            throw Invalid(
                "PREFERENCES_FORMAT_CULTURE_INVALID",
                "Format culture is required.");
        }

        try
        {
            _ = CultureInfo.GetCultureInfo(
                preferences.FormatCultureTag);
        }
        catch (CultureNotFoundException exception)
        {
            throw Invalid(
                "PREFERENCES_FORMAT_CULTURE_INVALID",
                exception.Message);
        }

        if (!Enum.IsDefined(
                preferences.DateDisplayPreset))
        {
            throw Invalid(
                "PREFERENCES_DATE_PRESET_INVALID",
                "Date display preset is unknown.");
        }

        if (preferences.NumberDisplayOptions is null)
        {
            throw Invalid(
                "PREFERENCES_NUMBER_OPTIONS_MISSING",
                "Number display options are required.");
        }

        if (preferences.NumberDisplayOptions.DecimalDigits
            is < 0 or > 8)
        {
            throw Invalid(
                "PREFERENCES_DECIMAL_DIGITS_INVALID",
                "Display decimal digits must be between 0 and 8.");
        }

        if (string.IsNullOrWhiteSpace(
                preferences.DisplayTimeZoneId))
        {
            throw Invalid(
                "PREFERENCES_TIME_ZONE_INVALID",
                "Display time zone is required.");
        }

        try
        {
            _ = timeZones.Resolve(
                preferences.DisplayTimeZoneId);
        }
        catch (Exception exception)
            when (exception is TimeZoneNotFoundException
                  or InvalidTimeZoneException
                  or ArgumentException)
        {
            throw Invalid(
                "PREFERENCES_TIME_ZONE_INVALID",
                exception.Message);
        }
    }

    public bool TryValidate(
        UserPresentationPreferences? preferences,
        out string? failureCode)
    {
        try
        {
            Validate(preferences!);
            failureCode = null;
            return true;
        }
        catch (PresentationPreferencesValidationException exception)
        {
            failureCode = exception.Code;
            return false;
        }
    }

    private static PresentationPreferencesValidationException Invalid(
        string code,
        string message) =>
        new(code, message);
}
