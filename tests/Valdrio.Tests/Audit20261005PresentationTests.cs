using System.Globalization;
using Valdrio.Application;
using Valdrio.Domain;
using Valdrio.Localization;

namespace Valdrio.Tests;

[TestClass]
public sealed class Audit20261005PresentationTests
{
    [TestMethod]
    public void A03_RegionalEditsProduceTheSameTypedFxInput()
    {
        foreach (var (culture, input) in new[]
        {
            ("es-ES", "0,90"), ("en-US", "0.90"), ("de-DE", "0,90")
        })
        {
            var session = new DecimalEditSession(CultureInfo.GetCultureInfo(culture));
            Assert.IsTrue(session.TryCommit(input, out var quote));
            var result = new FxConversionExampleService().Build(quote, BusinessDate.ParseIso("2026-10-05"), "audit");
            Assert.AreEqual(0.9m, result.Quote);
            Assert.AreEqual(90m, result.ConvertedOriginalMoney.Amount);
        }
    }

    [TestMethod]
    public void A03_CapturedCultureSurvivesAmbientRegionChange()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            var edit = new DecimalEditSession(CultureInfo.GetCultureInfo("es-ES"));
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            Assert.IsTrue(edit.TryCommit("0,90", out var quote));
            Assert.AreEqual(0.9m, quote);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [TestMethod]
    public void A03_MonetaryViewRetainsMinorUnitsFromFrozenMetadata()
    {
        var formatter = new PresentationFormatter(new DisplayTimeZoneResolver());
        var date = new DateOnly(2026, 10, 5);
        var kwd = new CurrencyCode("KWD");
        var metadata = new CurrencyMetadataSnapshot(kwd, "414", 3, true, "catalog", "fixture", date, date);
        Assert.AreEqual("0,001 KWD", formatter.FormatCurrency(new Money(0.001m, kwd), metadata,
            CultureInfo.GetCultureInfo("es-ES"), new NumberDisplayOptions(2, false)));
        var jpy = new CurrencyCode("JPY");
        var jpyMetadata = new CurrencyMetadataSnapshot(jpy, "392", 0, true, "catalog", "fixture", date, date);
        Assert.AreEqual("1 JPY", formatter.FormatCurrency(new Money(1m, jpy), jpyMetadata,
            CultureInfo.GetCultureInfo("en-US"), new NumberDisplayOptions(0, false)));
    }
}
