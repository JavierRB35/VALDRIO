using System.Globalization;
using Valdrio.Desktop;
using Valdrio.Localization;

namespace Valdrio.Tests;

[TestClass]
public sealed class UiCompactNumbersTests
{
    [TestMethod]
    public void MonetaryAndPercentageDisplay_UsesAtMostTwoDecimals()
    {
        var culture = CultureInfo.GetCultureInfo("es-ES");
        const decimal exact = 6.73154907m;
        Assert.AreEqual("3.000", UiCompactNumbers.Number(3000m, culture));
        Assert.AreEqual("3.000,25 EUR", UiCompactNumbers.Money(3000.25m, "EUR", culture));
        Assert.AreEqual("125,5 EUR", UiCompactNumbers.Money(125.5000m, "EUR", culture));
        Assert.AreEqual("6,73 %", UiCompactNumbers.Percent(exact, culture));
    }

    [TestMethod]
    public void GlobalCurrencyFormatter_DoesNotAddInsignificantZeros()
    {
        var formatter = new PresentationFormatter(new DisplayTimeZoneResolver());
        var culture = CultureInfo.GetCultureInfo("en-US");
        var options = new NumberDisplayOptions(5, true);
        Assert.AreEqual("1,000 EUR", formatter.FormatCurrency(1000m, "EUR", culture, options));
        Assert.AreEqual("1,000.25 EUR", formatter.FormatCurrency(1000.2500m, "EUR", culture, options));
        Assert.AreEqual("≈ 0 EUR", formatter.FormatCurrency(0.001m, "EUR", culture, options));
    }
}
