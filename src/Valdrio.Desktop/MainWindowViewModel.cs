using System.Globalization;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using Valdrio.Localization;

namespace Valdrio.Desktop;

public partial class MainWindowViewModel : ObservableObject
{
    private const string ExpectedKind = "valdrio-development-smoke-v1";

    private readonly UiTextCatalog texts;
    private readonly CultureInfo culture;

    [ObservableProperty]
    private string status;

    [ObservableProperty]
    private string sampleName = "—";

    [ObservableProperty]
    private string samplePurpose = "—";

    [ObservableProperty]
    private string sourcePath = "—";

    public MainWindowViewModel(UiTextCatalog texts, CultureInfo culture)
    {
        this.texts = texts;
        this.culture = culture;
        status = texts.Get("StatusNoSample", culture);
    }

    public string AppTitle => texts.Get("AppTitle", culture);
    public string WindowTitle => texts.Get("WindowTitle", culture);
    public string SpecificationVersion => texts.Get("SpecificationVersion", culture);
    public string HeaderSubtitle => texts.Get("HeaderSubtitle", culture);
    public string LabelStatus => texts.Get("LabelStatus", culture);
    public string LabelSample => texts.Get("LabelSample", culture);
    public string LabelPurpose => texts.Get("LabelPurpose", culture);
    public string LabelSource => texts.Get("LabelSource", culture);
    public string FooterDevelopmentOnly => texts.Get("FooterDevelopmentOnly", culture);

    public bool TryLoadDevelopmentSample(string path, out string? diagnosticCode)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;

            var kind = root.GetProperty("kind").GetString();
            if (!string.Equals(kind, ExpectedKind, StringComparison.Ordinal))
            {
                Status = texts.Get("StatusSampleOpenFailed", culture);
                diagnosticCode = "T01_SAMPLE_KIND_INVALID";
                return false;
            }

            SampleName = root.GetProperty("name").GetString()
                ?? throw new InvalidDataException();
            SamplePurpose = root.GetProperty("purpose").GetString()
                ?? throw new InvalidDataException();
            SourcePath = Path.GetFullPath(path);
            Status = texts.Get("StatusSampleOpened", culture);

            diagnosticCode = null;
            return true;
        }
        catch (Exception exception) when (
            exception is IOException
            or UnauthorizedAccessException
            or JsonException
            or InvalidDataException
            or KeyNotFoundException)
        {
            Status = texts.Get("StatusSampleOpenFailed", culture);
            diagnosticCode = "T01_SAMPLE_OPEN_FAILED";
            return false;
        }
    }
}
