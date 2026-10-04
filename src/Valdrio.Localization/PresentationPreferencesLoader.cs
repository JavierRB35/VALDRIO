using System.Diagnostics;

namespace Valdrio.Localization;

public interface IPresentationPreferencesRecoverySink
{
    void Recovered(string reasonCode);
}

public sealed class NullPresentationPreferencesRecoverySink
    : IPresentationPreferencesRecoverySink
{
    public static NullPresentationPreferencesRecoverySink Instance { get; } =
        new();

    public void Recovered(string reasonCode)
    {
    }
}

public sealed class TracePresentationPreferencesRecoverySink
    : IPresentationPreferencesRecoverySink
{
    public void Recovered(string reasonCode) =>
        Trace.TraceWarning(
            $"VALDRIO recovered presentation preferences: {reasonCode}");
}

public sealed class PresentationPreferencesLoader
{
    private readonly JsonUserPresentationPreferencesStore store;
    private readonly PresentationPreferencesValidator validator;
    private readonly IPresentationPreferencesRecoverySink recoverySink;

    public PresentationPreferencesLoader(
        JsonUserPresentationPreferencesStore store,
        PresentationPreferencesValidator validator,
        IPresentationPreferencesRecoverySink? recoverySink = null)
    {
        this.store = store;
        this.validator = validator;
        this.recoverySink =
            recoverySink ?? NullPresentationPreferencesRecoverySink.Instance;
    }

    public UserPresentationPreferences LoadOrDefault(
        bool allowStoredPreferences = true)
    {
        if (!allowStoredPreferences)
        {
            return UserPresentationPreferences.Default;
        }

        if (!store.TryLoad(
                out var loaded,
                out var loadFailure))
        {
            if (!string.Equals(
                    loadFailure,
                    "PREFERENCES_NOT_FOUND",
                    StringComparison.Ordinal))
            {
                recoverySink.Recovered(
                    loadFailure ?? "PREFERENCES_LOAD_FAILED");
            }

            return UserPresentationPreferences.Default;
        }

        if (!validator.TryValidate(
                loaded,
                out var validationFailure))
        {
            recoverySink.Recovered(
                validationFailure ?? "PREFERENCES_VALIDATION_FAILED");
            return UserPresentationPreferences.Default;
        }

        return loaded!;
    }
}
