namespace Valdrio.Localization;

public sealed record PresentationPreferencesChangedEventArgs(
    UserPresentationPreferences Previous,
    UserPresentationPreferences Current);

public sealed class PresentationPreferencesService
{
    private readonly PresentationPreferencesValidator validator;

    public PresentationPreferencesService(
        LanguageCatalog languages,
        UserPresentationPreferences initial)
        : this(
            new PresentationPreferencesValidator(
                languages,
                new DisplayTimeZoneResolver()),
            initial)
    {
    }

    public PresentationPreferencesService(
        PresentationPreferencesValidator validator,
        UserPresentationPreferences initial)
    {
        this.validator = validator;
        validator.Validate(initial);
        Current = initial;
    }

    public UserPresentationPreferences Current { get; private set; }

    public event EventHandler<PresentationPreferencesChangedEventArgs>? Changed;

    public void Update(
        UserPresentationPreferences next)
    {
        validator.Validate(next);

        if (next == Current)
        {
            return;
        }

        var previous = Current;
        Current = next;

        Changed?.Invoke(
            this,
            new PresentationPreferencesChangedEventArgs(
                previous,
                next));
    }
}
