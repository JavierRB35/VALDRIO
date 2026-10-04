using System.Collections;
using System.Globalization;
using System.Resources;

namespace Valdrio.Localization;

public sealed class UiTextCatalog
{
    private readonly ResourceManager resourceManager =
        new("Valdrio.Localization.Resources.Strings", typeof(UiTextCatalog).Assembly);

    public static IReadOnlyList<CultureInfo> SupportedUiCultures { get; } =
    [
        CultureInfo.GetCultureInfo("en"),
        CultureInfo.GetCultureInfo("es")
    ];

    public string Get(string key, CultureInfo culture)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(culture);

        return resourceManager.GetString(key, culture)
            ?? throw new MissingManifestResourceException(
                $"Missing UI resource '{key}' for culture '{culture.Name}'.");
    }

    public string Format(string key, CultureInfo culture, params object?[] arguments) =>
        string.Format(culture, Get(key, culture), arguments);

    public IReadOnlyDictionary<string, string> ReadAll(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        var set = resourceManager.GetResourceSet(culture, createIfNotExists: true, tryParents: true)
            ?? throw new MissingManifestResourceException(
                $"Missing UI resource set for culture '{culture.Name}'.");

        return set.Cast<DictionaryEntry>()
            .ToDictionary(
                entry => (string)entry.Key,
                entry => entry.Value?.ToString() ?? string.Empty,
                StringComparer.Ordinal);
    }
}
