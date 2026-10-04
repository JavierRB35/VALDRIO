using System.Globalization;
using System.Text.RegularExpressions;
using Valdrio.Localization;

namespace Valdrio.Tests;

[TestClass]
public sealed partial class LocalizationCatalogTests
{
    [TestMethod]
    public void EnglishAndSpanishCatalogs_HaveSameKeysAndPlaceholderSignatures()
    {
        var catalog = new UiTextCatalog();
        var english = catalog.ReadAll(CultureInfo.GetCultureInfo("en"));
        var spanish = catalog.ReadAll(CultureInfo.GetCultureInfo("es"));

        CollectionAssert.AreEquivalent(english.Keys.ToArray(), spanish.Keys.ToArray());

        foreach (var key in english.Keys)
        {
            CollectionAssert.AreEquivalent(
                PlaceholderIndexes(english[key]),
                PlaceholderIndexes(spanish[key]),
                $"Placeholder signature differs for resource '{key}'.");
        }
    }

    [TestMethod]
    public void RequiredShellResources_AreAvailableInEnglishAndSpanish()
    {
        var catalog = new UiTextCatalog();
        var requiredKeys = new[]
        {
            "AppTitle",
            "WindowTitle",
            "SpecificationVersion",
            "HeaderSubtitle",
            "LabelStatus",
            "LabelSample",
            "LabelPurpose",
            "LabelSource",
            "FooterDevelopmentOnly",
            "StatusNoSample",
            "StatusSampleOpened",
            "StatusSampleOpenFailed",
            "ErrorSampleOpenTitle",
            "ErrorSampleOpenBody"
        };

        foreach (var culture in UiTextCatalog.SupportedUiCultures)
        {
            foreach (var key in requiredKeys)
            {
                Assert.IsFalse(
                    string.IsNullOrWhiteSpace(catalog.Get(key, culture)),
                    $"Resource '{key}' is empty for '{culture.Name}'.");
            }
        }
    }

    private static string[] PlaceholderIndexes(string value) =>
        PlaceholderRegex()
            .Matches(value)
            .Select(match => match.Groups[1].Value)
            .OrderBy(index => index, StringComparer.Ordinal)
            .ToArray();

    [GeneratedRegex(@"\{(\d+)(?:[^}]*)\}")]
    private static partial Regex PlaceholderRegex();
}
