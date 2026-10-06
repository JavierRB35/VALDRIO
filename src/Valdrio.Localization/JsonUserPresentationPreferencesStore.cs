using System.Text;
using System.Text.Json;

namespace Valdrio.Localization;

public interface IUserPresentationPreferencesStore
{
    bool TryLoad(out UserPresentationPreferences? preferences);
    bool TryLoad(out UserPresentationPreferences? preferences, out string? failureCode);
    void Save(UserPresentationPreferences preferences);
}

public sealed class JsonUserPresentationPreferencesStore : IUserPresentationPreferencesStore
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.General)
        {
            WriteIndented = true
        };

    private readonly string path;

    public JsonUserPresentationPreferencesStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        this.path = path;
    }

    public bool TryLoad(
        out UserPresentationPreferences? preferences) =>
        TryLoad(
            out preferences,
            out _);

    public bool TryLoad(
        out UserPresentationPreferences? preferences,
        out string? failureCode)
    {
        if (!File.Exists(path))
        {
            preferences = null;
            failureCode = "PREFERENCES_NOT_FOUND";
            return false;
        }

        try
        {
            var json = File.ReadAllText(
                path,
                Encoding.UTF8);

            preferences =
                JsonSerializer.Deserialize<UserPresentationPreferences>(
                    json,
                    JsonOptions);

            if (preferences is null)
            {
                failureCode =
                    "PREFERENCES_DOCUMENT_NULL";
                return false;
            }

            failureCode = null;
            return true;
        }
        catch (JsonException)
        {
            preferences = null;
            failureCode =
                "PREFERENCES_JSON_INVALID";
            return false;
        }
        catch (IOException)
        {
            preferences = null;
            failureCode =
                "PREFERENCES_IO_FAILED";
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            preferences = null;
            failureCode =
                "PREFERENCES_ACCESS_DENIED";
            return false;
        }
    }

    public void Save(
        UserPresentationPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException(
                "Presentation preferences path must have a directory.");

        Directory.CreateDirectory(directory);

        var tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var json = JsonSerializer.Serialize(
                preferences,
                JsonOptions);

            File.WriteAllText(
                tempPath,
                json,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false));

            File.Move(
                tempPath,
                path,
                overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}
