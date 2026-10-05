namespace Valdrio.Localization;

public sealed record PresentationPreferencesPersistenceFailure(
    string Code,
    string MessageKey);

public sealed class PresentationPreferencesPersistence
{
    private readonly IUserPresentationPreferencesStore store;

    public PresentationPreferencesPersistence(
        IUserPresentationPreferencesStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    public bool TrySave(
        UserPresentationPreferences preferences,
        out PresentationPreferencesPersistenceFailure? failure)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        try
        {
            store.Save(preferences);
            failure = null;
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            failure = new(
                "PREFERENCES_SAVE_ACCESS_DENIED",
                "Preferences.SaveFailed");
            return false;
        }
        catch (IOException)
        {
            failure = new(
                "PREFERENCES_SAVE_IO_FAILED",
                "Preferences.SaveFailed");
            return false;
        }
    }
}
