namespace Valdrio.Localization;

public sealed record LanguageDefinition(
    string Tag,
    string EnglishName,
    string NativeName,
    bool IsRightToLeft,
    string Revision,
    bool IsPublished);

public sealed class LanguageCatalog
{
    private static readonly IReadOnlyList<LanguageDefinition> Definitions =
    [
        new("en", "English", "English", false, "0.4", true),
        new("es", "Spanish", "Español", false, "0.4", true)
    ];

    public IReadOnlyList<LanguageDefinition> Published =>
        Definitions.Where(definition => definition.IsPublished).ToArray();

    public LanguageDefinition GetRequired(string tag) =>
        Definitions.FirstOrDefault(definition =>
            string.Equals(definition.Tag, tag, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentOutOfRangeException(nameof(tag), tag, "Unsupported UI language.");
}
