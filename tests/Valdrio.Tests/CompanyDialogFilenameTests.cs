using Valdrio.Desktop;

namespace Valdrio.Tests;

[TestClass]
public sealed class CompanyDialogFilenameTests
{
    [TestMethod]
    public void SaveDialogSuggestionUsesCompanyNameWithoutReentry()
    {
        Assert.AreEqual("Talleres del Norte", CompanyFileDialogs.SuggestedFileName("Talleres del Norte"));
        Assert.AreEqual("Café - Taller- Norte", CompanyFileDialogs.SuggestedFileName(" Café / Taller: Norte "));
        Assert.AreEqual("_CON", CompanyFileDialogs.SuggestedFileName("CON"));
        Assert.AreEqual("_AUX.txt", CompanyFileDialogs.SuggestedFileName("AUX.txt"));
        Assert.AreEqual("Empresa", CompanyFileDialogs.SuggestedFileName("..."));
        var longName = CompanyFileDialogs.SuggestedFileName(new string('a', 200));
        Assert.AreEqual(100, longName.Length);
        Assert.IsFalse(longName.Contains(".valdrio"), "The save dialog, not the suggestion, appends the file extension.");
    }
}
