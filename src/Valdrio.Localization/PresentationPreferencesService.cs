using System.Globalization;

namespace Valdrio.Localization;

public sealed record PresentationPreferencesChangedEventArgs(
    UserPresentationPreferences Previous,
    UserPresentationPreferences Current);

public sealed class PresentationPreferencesService
{
    private readonly LanguageCatalog languages;

    public PresentationPreferencesService(
        LanguageCatalog languages,
        UserPresentationPreferences initial)
    {
        this.languages = languages;
        Validate(initial);
        Current = initial;
    }

    public UserPresentationPreferences Current { get; private set; }

    public event EventHandler<PresentationPreferencesChangedEventArgs>? Changed;

    public void Update(UserPresentationPreferences next)
    {
        Validate(next);

        if (next == Current)
        {
            return;
        }

        var previous = Current;
        Current = next;
        Changed?.Invoke(this, new PresentationPreferencesChangedEventArgs(previous, next));
    }

    private void Validate(UserPresentationPreferences preferences)
    {
        languages.GetRequired(preferences.UiLanguageTag);
        _ = CultureInfo.GetCultureInfo(preferences.FormatCultureTag);

        if (string.IsNullOrWhiteSpace(preferences.DisplayTimeZoneId))
        {
            throw new ArgumentException("Display time zone is required.", nameof(preferences));
        }

        if (preferences.NumberDisplayOptions.DecimalDigits is < 0 or > 8)
        {
            throw new ArgumentOutOfRangeException(
                nameof(preferences),
                "Display decimal digits must be between 0 and 8.");
        }
    }
}
