using System.Text;
using System.Text.Json;

namespace Valdrio.Localization;

public sealed class JsonUserPresentationPreferencesStore
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

    public bool TryLoad(out UserPresentationPreferences? preferences)
    {
        if (!File.Exists(path))
        {
            preferences = null;
            return false;
        }

        try
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            preferences = JsonSerializer.Deserialize<UserPresentationPreferences>(
                json,
                JsonOptions);
            return preferences is not null;
        }
        catch (JsonException)
        {
            preferences = null;
            return false;
        }
    }

    public void Save(UserPresentationPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException(
                "Presentation preferences path must have a directory.");

        Directory.CreateDirectory(directory);

        var tempPath = path + ".tmp";
        var json = JsonSerializer.Serialize(preferences, JsonOptions);

        File.WriteAllText(
            tempPath,
            json,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        File.Move(tempPath, path, overwrite: true);
    }
}
