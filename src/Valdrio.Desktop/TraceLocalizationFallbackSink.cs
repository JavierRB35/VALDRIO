using System.Diagnostics;
using Valdrio.Localization;

namespace Valdrio.Desktop;

public sealed class TraceLocalizationFallbackSink : ILocalizationFallbackSink
{
    public void MissingKey(string requestedLanguageTag, string key) =>
        Trace.TraceWarning(
            "Localization fallback to neutral English. Language={0}; Key={1}",
            requestedLanguageTag,
            key);
}
