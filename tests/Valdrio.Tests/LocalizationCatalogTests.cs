using System.Globalization;
using System.Text.RegularExpressions;
using Valdrio.Localization;

namespace Valdrio.Tests;

[TestClass]
public sealed partial class LocalizationCatalogTests
{
    [TestMethod]
    public void EnglishAndSpanishPublishedCatalogs_HaveExactlySameKeysAndPlaceholderSignatures()
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
    public void PublishedCatalogs_AreCompleteAndNonEmpty()
    {
        var catalog = new UiTextCatalog();
        var languages = new LanguageCatalog();

        foreach (var language in languages.Published)
        {
            var resources = catalog.ReadAll(
                CultureInfo.GetCultureInfo(language.Tag));

            Assert.IsTrue(resources.Count >= 30);

            foreach (var pair in resources)
            {
                Assert.IsFalse(
                    string.IsNullOrWhiteSpace(pair.Value),
                    $"Resource '{pair.Key}' is empty for '{language.Tag}'.");
            }
        }
    }

    [TestMethod]
    public void AllocationResults_HaveLabelsForEveryResourceBehavior()
    {
        var catalog = new UiTextCatalog();
        foreach (var language in new[] { "en", "es" })
        foreach (var behavior in Enum.GetValues<Valdrio.Domain.ResourceBehavior>())
            Assert.IsFalse(string.IsNullOrWhiteSpace(catalog.Get(
                "AllocationUI.SecondaryBehavior" + behavior, CultureInfo.GetCultureInfo(language))));
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
