using Microsoft.VisualStudio.TestTools.UnitTesting;
using Valdrio.Desktop;

namespace Valdrio.Tests;

[TestClass]
public sealed class FinancialDisplayProjectionTests
{
    [TestMethod]
    public void Thirds_ReconcileDeterministicallyToCents()
    {
        decimal[] thirds = [1833.33333334m,1833.33333332m,1833.33333334m];
        var shown=FinancialDisplayProjection.Project(thirds,0.01m);
        CollectionAssert.AreEqual(new[] {1833.34m,1833.33m,1833.33m},shown);
        Assert.AreEqual(5500.00m,shown.Sum());
    }

    [TestMethod]
    public void UserSeenTotals_ConserveEveryCentWithoutChangingRawLedger()
    {
        decimal[] ledger=[1833.33333334m,916.66666668m,1833.33333332m,916.66666666m];
        var before=ledger.ToArray();
        var projected=FinancialDisplayProjection.Project(ledger,0.01m);
        Assert.AreEqual(5500m,projected.Sum());
        Assert.IsTrue(projected.All(v=>decimal.Round(v,2)==v));
        CollectionAssert.AreEqual(before,ledger);
        CollectionAssert.AreEqual(projected,FinancialDisplayProjection.Project(ledger,0.01m));
    }

    [TestMethod]
    public void CurrencyMinorUnitsAreNotHardcodedAsCents()
    {
        CollectionAssert.AreEqual(new[]{1m,2m}, FinancialDisplayProjection.Project([1.4m,1.6m],1m));
        Assert.AreEqual(3m,FinancialDisplayProjection.Project([1.004m,1.996m],0.001m).Sum());
    }

    [TestMethod]
    public void DisplaySupportsSpanishEnglishAndNegativeBalances()
    {
        decimal[] ledger=[-2.345m,1.235m,2.110m];
        var shown=FinancialDisplayProjection.Project(ledger,0.01m);
        Assert.AreEqual(1m,shown.Sum());
        Assert.AreEqual("-2,35 EUR",FinancialDisplayProjection.Format(-2.35m,"EUR",0.01m,
            System.Globalization.CultureInfo.GetCultureInfo("es-ES")));
        Assert.AreEqual("2,110 KWD", FinancialDisplayProjection.Format(2.110m,"KWD",0.001m,
            System.Globalization.CultureInfo.GetCultureInfo("es-ES")));
    }
}
