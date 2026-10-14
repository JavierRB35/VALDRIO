using System.Globalization;
using Valdrio.Localization;

namespace Valdrio.Tests;

[TestClass]
public sealed class AllocationUiResourceCompletenessTests
{
    [TestMethod]
    public void AllocationUiKeysAreCompleteInNeutralEnglishAndSpanish()
    {
        var texts = new UiTextCatalog();
        var en = texts.ReadAll(CultureInfo.GetCultureInfo("en"));
        var es = texts.ReadAll(CultureInfo.GetCultureInfo("es"));

        var englishKeys = en.Keys.Where(k => k.StartsWith("AllocationUI.", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal).ToArray();
        var spanishKeys = es.Keys.Where(k => k.StartsWith("AllocationUI.", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal).ToArray();

        CollectionAssert.AreEqual(englishKeys, spanishKeys,
            "All allocation UI labels must exist in both the neutral English and Spanish resources.");

        foreach (var key in englishKeys)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(texts.Get(key, CultureInfo.GetCultureInfo("en"))),
                "Empty neutral English resource: " + key);
            Assert.IsFalse(string.IsNullOrWhiteSpace(texts.Get(key, CultureInfo.GetCultureInfo("es"))),
                "Empty Spanish resource: " + key);
        }
    }

    [TestMethod]
    public void SecondaryExplanationControlsHaveEnglishAndSpanishLabels()
    {
        var texts = new UiTextCatalog();
        foreach (var culture in UiTextCatalog.SupportedUiCultures)
        foreach (var key in new[] { "AllocationUI.ShowSource", "AllocationUI.BackToAmounts" })
            Assert.IsFalse(string.IsNullOrWhiteSpace(texts.Get(key, culture)),
                "Missing allocation explanation label: " + culture.Name + " / " + key);
    }

    [TestMethod]
    public void GlobalBusyIndicatorHasAccessibleLabelsInBothLanguages()
    {
        var catalog = new UiTextCatalog();
        foreach (var culture in UiTextCatalog.SupportedUiCultures)
        {
            var label = catalog.Get("GlobalProcessing", culture);
            Assert.IsFalse(string.IsNullOrWhiteSpace(label),
                "Global busy indicator must have an accessible label in " + culture.Name);
        }
    }

    [TestMethod]
    public void PrincipalOriginSeveralKeysCanBeFormattedWithoutException()
    {
        var catalog = new UiTextCatalog();
        foreach (var culture in UiTextCatalog.SupportedUiCultures)
        {
            var label = catalog.Format("AllocationUI.PrincipalOriginMoreKeys", culture, 3);
            StringAssert.Contains(label, "3");
        }
    }
}
