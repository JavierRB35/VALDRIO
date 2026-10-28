using System.Windows.Documents;
using Valdrio.Desktop;

namespace Valdrio.Tests;

[TestClass]
public sealed class ContextHelpRenderingTests
{
    private static void OnSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() => { try { action(); } catch (Exception e) { error = e; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(30)), "Help rendering timed out.");
        if (error is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }

    [TestMethod]
    public void EveryPackagedGuide_RendersTablesAndTrailingWorkflow_InBothLanguages() => OnSta(() =>
    {
        var catalog = new ContextHelpCatalog(Path.Combine(AppContext.BaseDirectory, "help"));
        foreach (var language in new[] { "es", "en" })
        foreach (var view in catalog.ViewIds)
        {
            var document = ContextHelpMarkdown.Render(catalog.Load(view, language).Markdown);
            var text = new TextRange(document.ContentStart, document.ContentEnd).Text;
            Assert.IsTrue(document.Blocks.OfType<Table>().Any(), view + ": missing table");
            StringAssert.Contains(text, language == "es" ? "Recorrido recomendado" : "Recommended workflow");
            Assert.IsFalse(text.Contains("**Last updated:**") || text.Contains("Last updated:") ||
                text.Contains("Última actualización:") || text.Contains("ID Vista UI:") ||
                text.Contains("src/Valdrio."), view + ": technical metadata leaked");
        }
    });

    [TestMethod]
    public void TechnicalReferences_DoNotHideFollowingBusinessContent() => OnSta(() =>
    {
        foreach (var heading in new[] { "### Referencias al código", "### Code references" })
        {
            var document = ContextHelpMarkdown.Render("# Guide\n" + heading +
                "\n- internal.cs\n\n#### Decision\nKeep this explanation.\n");
            var text = new TextRange(document.ContentStart, document.ContentEnd).Text;
            Assert.IsFalse(text.Contains("internal.cs"));
            StringAssert.Contains(text, "Keep this explanation.");
        }
    });
}
