using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Valdrio.Desktop;

public partial class MainWindowViewModel : ObservableObject
{
    private const string ExpectedKind = "valdrio-development-smoke-v1";

    [ObservableProperty]
    private string status = "No development sample loaded.";

    [ObservableProperty]
    private string sampleName = "—";

    [ObservableProperty]
    private string samplePurpose = "—";

    [ObservableProperty]
    private string sourcePath = "—";

    public bool TryLoadDevelopmentSample(string path, out string? error)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;

            var kind = root.GetProperty("kind").GetString();
            if (!string.Equals(kind, ExpectedKind, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Unexpected development fixture kind '{kind ?? "(null)"}'.");
            }

            SampleName = root.GetProperty("name").GetString() ?? "(unnamed)";
            SamplePurpose = root.GetProperty("purpose").GetString() ?? "(no purpose)";
            SourcePath = Path.GetFullPath(path);
            Status = "Development sample opened.";

            error = null;
            return true;
        }
        catch (Exception exception) when (
            exception is IOException
            or UnauthorizedAccessException
            or JsonException
            or InvalidDataException
            or KeyNotFoundException)
        {
            Status = "Development sample could not be opened.";
            error = exception.Message;
            return false;
        }
    }
}
