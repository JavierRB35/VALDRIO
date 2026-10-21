using Valdrio.Desktop;

namespace Valdrio.Tests;

[TestClass]
public sealed class ContextHelpCatalogTests
{
    private static ContextHelpCatalog Open() =>
        new(Path.Combine(AppContext.BaseDirectory, "help"));

    [TestMethod]
    public void AllSixteenScreenArticles_ArePackagedAndReadable()
    {
        var catalog = Open();
        Assert.AreEqual(16, catalog.ViewIds.Count);
        foreach (var id in catalog.ViewIds)
        {
            var article = catalog.Load(id, "es");
            Assert.AreEqual(id, article.ViewId);
            Assert.IsFalse(article.SpanishFallback);
            StringAssert.Contains(article.Markdown, "## 1.");
            StringAssert.Contains(article.Markdown, "## 4.");
        }
    }

    [TestMethod]
    public void SharedPrimarySecondaryPanel_UsesDifferentLogicalHelp()
    {
        Assert.AreEqual("View_PrimaryAllocation", ContextHelpCatalog.ViewFor(5));
        Assert.AreEqual("View_SecondaryAllocation", ContextHelpCatalog.ViewFor(7));
        Assert.AreEqual("View_AllocationReuse", ContextHelpCatalog.ViewFor(5, "AllocationReuseWindow"));
        Assert.AreEqual("View_NormalCapacityAssistant", ContextHelpCatalog.ViewFor(9, "PhysicalCapacityAssistantWindow"));
        Assert.AreEqual("View_PhysicalImport", ContextHelpCatalog.ViewFor(9, "PhysicalImportWindow"));
        Assert.AreEqual("View_ImportBalances", ContextHelpCatalog.ViewFor(3, "ImportColumnMappingWindow"));
    }

    [TestMethod]
    public void EveryNavigationStage_HasRegisteredHelp()
    {
        var catalog = Open();
        for (var step = 0; step <= 10; step++)
            Assert.IsTrue(catalog.ViewIds.Contains(ContextHelpCatalog.ViewFor(step)), $"Stage {step} is unmapped.");
    }

    [TestMethod]
    public void EnglishFallback_IsExplicitAndNeverSilentlyReportedAsEnglish()
    {
        var catalog = Open();
        foreach (var id in catalog.ViewIds)
        {
            var article = catalog.Load(id, "en");
            Assert.IsFalse(string.IsNullOrWhiteSpace(article.Markdown));
            if (article.SpanishFallback)
                StringAssert.Contains(article.Markdown, "## 1. Objetivo");
        }
    }
}
