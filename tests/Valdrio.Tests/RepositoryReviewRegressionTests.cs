using System.Globalization;
using Valdrio.Domain;
using Valdrio.Localization;

namespace Valdrio.Tests;

[TestClass]
public sealed class RepositoryReviewRegressionTests
{
    [TestMethod]
    public void R01_UtcInstantRejectsIncompleteOrZoneLessText()
    {
        var invalid = new[]
        {
            "2026-10-04T00:30:00",
            "00:30",
            "2026-10-04"
        };

        foreach (var input in invalid)
        {
            Assert.ThrowsExactly<FormatException>(
                () => UtcInstant.ParseIso(input),
                $"Expected '{input}' to be rejected.");
        }
    }

    [TestMethod]
    public void R01_UtcInstantNormalizesExplicitOffsetAndRoundTrips()
    {
        var fromOffset = UtcInstant.ParseIso(
            "2026-10-04T02:30:00+02:00");

        Assert.AreEqual(
            "2026-10-04T00:30:00.0000000Z",
            fromOffset.ToCanonicalString());

        var roundTrip = UtcInstant.ParseIso(
            fromOffset.ToCanonicalString());

        Assert.AreEqual(fromOffset, roundTrip);
    }

    [TestMethod]
    public void R02_EnUsRejectsMalformedGrouping()
    {
        var edit = new DecimalEditSession(
            CultureInfo.GetCultureInfo("en-US"));

        Assert.IsFalse(edit.TryCommit("1,2,3", out _));
        Assert.IsFalse(edit.TryCommit("12,34.50", out _));
    }

    [TestMethod]
    public void R02_EsEsRejectsMalformedGrouping()
    {
        var edit = new DecimalEditSession(
            CultureInfo.GetCultureInfo("es-ES"));

        Assert.IsFalse(edit.TryCommit("1.23", out _));
        Assert.IsFalse(edit.TryCommit("12.34,50", out _));
    }

    [TestMethod]
    public void R02_ValidRegionalNumbersKeepTheirValue()
    {
        var en = new DecimalEditSession(
            CultureInfo.GetCultureInfo("en-US"));
        var es = new DecimalEditSession(
            CultureInfo.GetCultureInfo("es-ES"));

        Assert.IsTrue(en.TryCommit("1,234.50", out var enValue));
        Assert.IsTrue(es.TryCommit("1.234,50", out var esValue));
        Assert.IsTrue(en.TryCommit("-1,234.50", out var negative));
        Assert.IsTrue(es.TryCommit(" 1.234,50 ", out var spaced));

        Assert.AreEqual(1234.50m, enValue);
        Assert.AreEqual(1234.50m, esValue);
        Assert.AreEqual(-1234.50m, negative);
        Assert.AreEqual(1234.50m, spaced);
    }

    [TestMethod]
    public void R02_EmptyInputIsRejected()
    {
        var edit = new DecimalEditSession(
            CultureInfo.GetCultureInfo("en-US"));

        Assert.IsFalse(edit.TryCommit("", out _));
        Assert.IsFalse(edit.TryCommit("   ", out _));
    }
}
