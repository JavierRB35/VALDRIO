using System.IO;
using System.Text.Json;

namespace Valdrio.Desktop;

/// <summary>Versioned offline documentation keyed by logical screen, never by a UI control name.</summary>
internal sealed class ContextHelpCatalog
{
    private readonly string root;
    private readonly IReadOnlyDictionary<string, string> files;

    internal sealed record Article(string ViewId, string Markdown, bool SpanishFallback);

    public ContextHelpCatalog(string root)
    {
        this.root = root;
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "help-map.json")));
        files = manifest.RootElement.GetProperty("views").EnumerateArray()
            .ToDictionary(
                v => v.GetProperty("viewId").GetString()!,
                v => ValidateFileName(v.GetProperty("file").GetString()!),
                StringComparer.Ordinal);
    }

    private static string ValidateFileName(string file)
    {
        if (string.IsNullOrWhiteSpace(file) || Path.GetFileName(file) != file ||
            !file.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
            file.Contains("..", StringComparison.Ordinal))
            throw new InvalidDataException("Invalid contextual help filename.");
        return file;
    }

    public IReadOnlyCollection<string> ViewIds => files.Keys.ToArray();

    public Article Load(string viewId, string? languageTag)
    {
        if (!files.TryGetValue(viewId, out var file))
            throw new KeyNotFoundException("Unknown contextual help view: " + viewId);
        var english = string.Equals(languageTag?.Split('-')[0], "en", StringComparison.OrdinalIgnoreCase);
        var localized = english ? Path.Combine(root, "en", file) : Path.Combine(root, file);
        var fallback = english && !File.Exists(localized);
        var path = fallback ? Path.Combine(root, file) : localized;
        return new Article(viewId, File.ReadAllText(path), fallback);
    }

    /// <summary>A logical allocation stage is distinct from its shared XAML surface.</summary>
    public static string ViewFor(int workflowStep, string? integratedPage = null) => integratedPage switch
    {
        "ImportColumnMappingWindow" => "View_ImportBalances",
        "PhysicalActivityWindow" => "View_PhysicalActivity",
        "PhysicalCapacityAssistantWindow" => "View_NormalCapacityAssistant",
        "PhysicalImportWindow" => "View_PhysicalImport",
        "AuxiliaryCapacityWindow" => "View_AuxiliaryServices",
        "PrincipalRatesWindow" => "View_PrincipalRates",
        "EstimateWindow" => "View_Estimates",
        "CompositionWindow" => "View_Compositions",
        "AllocationReuseWindow" => "View_AllocationReuse",
        _ => workflowStep switch
        {
            0 => "View_Project",
            1 => "View_Structure",
            2 => "View_Study",
            3 => "View_ImportBalances",
            4 => "View_ResourcePreparation",
            5 => "View_PrimaryAllocation",
            6 => "View_Preferences",
            7 => "View_SecondaryAllocation",
            8 => "View_CostRuns",
            9 => "View_PhysicalActivity",
            10 => "View_PrincipalRates",
            _ => "View_Project"
        }
    };
}
