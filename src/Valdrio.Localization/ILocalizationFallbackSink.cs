namespace Valdrio.Localization;

public interface ILocalizationFallbackSink
{
    void MissingKey(string requestedLanguageTag, string key);
}

public sealed class NullLocalizationFallbackSink : ILocalizationFallbackSink
{
    public static NullLocalizationFallbackSink Instance { get; } = new();

    public void MissingKey(string requestedLanguageTag, string key)
    {
    }
}
