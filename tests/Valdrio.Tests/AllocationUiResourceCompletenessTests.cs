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
