using System.Collections;
using System.Globalization;
using System.Resources;

namespace Valdrio.Localization;

public sealed class UiTextCatalog
{
    private readonly ResourceManager resourceManager =
        new("Valdrio.Localization.Resources.Strings", typeof(UiTextCatalog).Assembly);

    private readonly ILocalizationFallbackSink fallbackSink;

    public UiTextCatalog(ILocalizationFallbackSink? fallbackSink = null)
    {
        this.fallbackSink = fallbackSink ?? NullLocalizationFallbackSink.Instance;
    }

    public static IReadOnlyList<CultureInfo> SupportedUiCultures { get; } =
    [
        CultureInfo.GetCultureInfo("en"),
        CultureInfo.GetCultureInfo("es")
    ];

    public string Get(string key, CultureInfo culture)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(culture);

        if (string.Equals(culture.TwoLetterISOLanguageName, "en", StringComparison.OrdinalIgnoreCase))
        {
            return GetNeutralRequired(key);
        }

        var specific = resourceManager
            .GetResourceSet(culture, createIfNotExists: true, tryParents: false)?
            .GetString(key);

        if (specific is not null)
        {
            return specific;
        }

        fallbackSink.MissingKey(culture.Name, key);
        return GetNeutralRequired(key);
    }

    public string Format(string key, CultureInfo culture, params object?[] arguments) =>
        string.Format(culture, Get(key, culture), arguments);

    public IReadOnlyDictionary<string, string> ReadAll(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        ResourceSet? set;

        if (string.Equals(culture.TwoLetterISOLanguageName, "en", StringComparison.OrdinalIgnoreCase))
        {
            set = resourceManager.GetResourceSet(
                CultureInfo.InvariantCulture,
                createIfNotExists: true,
                tryParents: false);
        }
        else
        {
            set = resourceManager.GetResourceSet(
                culture,
                createIfNotExists: true,
                tryParents: false);
        }

        if (set is null)
        {
            throw new MissingManifestResourceException(
                $"Missing UI resource set for culture '{culture.Name}'.");
        }

        return set.Cast<DictionaryEntry>()
            .ToDictionary(
                entry => (string)entry.Key,
                entry => entry.Value?.ToString() ?? string.Empty,
                StringComparer.Ordinal);
    }

    private string GetNeutralRequired(string key) =>
        resourceManager.GetString(key, CultureInfo.InvariantCulture)
        ?? throw new MissingManifestResourceException(
            $"Missing neutral English UI resource '{key}'.");
}
