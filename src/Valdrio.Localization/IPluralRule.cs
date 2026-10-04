namespace Valdrio.Localization;

public enum PluralCategory
{
    One,
    Other
}

public interface IPluralRule
{
    PluralCategory Select(decimal value);
}

public sealed class EnglishPluralRule : IPluralRule
{
    public PluralCategory Select(decimal value) =>
        value == 1m ? PluralCategory.One : PluralCategory.Other;
}

public sealed class SpanishPluralRule : IPluralRule
{
    public PluralCategory Select(decimal value) =>
        value == 1m ? PluralCategory.One : PluralCategory.Other;
}

public sealed class PluralRuleRegistry
{
    private readonly IReadOnlyDictionary<string, IPluralRule> rules =
        new Dictionary<string, IPluralRule>(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = new EnglishPluralRule(),
            ["es"] = new SpanishPluralRule()
        };

    public IPluralRule GetRequired(string languageTag) =>
        rules.TryGetValue(languageTag, out var rule)
            ? rule
            : throw new ArgumentOutOfRangeException(
                nameof(languageTag),
                languageTag,
                "No plural rule is registered for the language.");
}
