using Valdrio.Application;
using Valdrio.Domain;
using Valdrio.Infrastructure;

namespace Valdrio.Tests;

[TestClass]
public sealed class T02ATemporalContractTests
{
    [TestMethod]
    public void BusinessDate_UsesCanonicalIsoAndHasNoTimeZone()
    {
        var date = BusinessDate.ParseIso("2026-10-04");

        Assert.AreEqual(
            new DateOnly(2026, 10, 4),
            date.Value);
        Assert.AreEqual(
            "2026-10-04",
            date.ToCanonicalString());
    }

    [TestMethod]
    public void UtcInstant_NormalizesToCanonicalUtc()
    {
        var instant = UtcInstant.ParseIso(
            "2026-10-04T02:30:00+02:00");

        Assert.AreEqual(
            TimeSpan.Zero,
            instant.Value.Offset);
        Assert.AreEqual(
            "2026-10-04T00:30:00.0000000Z",
            instant.ToCanonicalString());
    }

    [TestMethod]
    public void I08_ChangingDisplayZone_DoesNotMoveBusinessDateOrInstantIdentity()
    {
        var resolver = new TimeZoneResolver();
        var businessDate =
            BusinessDate.ParseIso("2026-10-04");
        var instant =
            UtcInstant.ParseIso("2026-10-04T00:30:00Z");

        var madrid = resolver.ToLocal(
            instant,
            "Europe/Madrid");
        var losAngeles = resolver.ToLocal(
            instant,
            "America/Los_Angeles");

        Assert.AreEqual(
            "2026-10-04",
            businessDate.ToCanonicalString());
        Assert.AreEqual(
            "2026-10-04T00:30:00.0000000Z",
            instant.ToCanonicalString());

        Assert.AreEqual(
            new DateTime(2026, 10, 4, 2, 30, 0),
            madrid.LocalDateTime);
        Assert.AreEqual(
            new DateTime(2026, 10, 3, 17, 30, 0),
            losAngeles.LocalDateTime);

        Assert.AreNotEqual(
            madrid.LocalDateTime.Date,
            losAngeles.LocalDateTime.Date);
    }

    [TestMethod]
    public void I09_AmbiguousLocalTime_RequiresExplicitOffset()
    {
        var resolver = new TimeZoneResolver();
        var local =
            new DateTime(2026, 10, 25, 2, 30, 0);

        var exception = CaptureDomainRule(
            () => resolver.InterpretLocal(
                local,
                "Europe/Madrid"));

        Assert.AreEqual(
            "TIME_LOCAL_AMBIGUOUS",
            exception.Diagnostic.Code);

        var offsets = resolver.GetValidOffsets(
            local,
            "Europe/Madrid");

        Assert.AreEqual(2, offsets.Count);

        var first = resolver.InterpretLocal(
            local,
            "Europe/Madrid",
            offsets[0]);

        var second = resolver.InterpretLocal(
            local,
            "Europe/Madrid",
            offsets[1]);

        Assert.AreNotEqual(
            first.ToCanonicalString(),
            second.ToCanonicalString());
    }

    [TestMethod]
    public void InvalidLocalTime_IsRejectedRatherThanShifted()
    {
        var resolver = new TimeZoneResolver();
        var missingLocalTime =
            new DateTime(2026, 3, 29, 2, 30, 0);

        var exception = CaptureDomainRule(
            () => resolver.InterpretLocal(
                missingLocalTime,
                "Europe/Madrid"));

        Assert.AreEqual(
            "TIME_LOCAL_INVALID",
            exception.Diagnostic.Code);
    }

    [TestMethod]
    public void ExplicitOffsetMustBeOneOfAmbiguousCandidates()
    {
        var resolver = new TimeZoneResolver();

        var exception = CaptureDomainRule(
            () => resolver.InterpretLocal(
                new DateTime(2026, 10, 25, 2, 30, 0),
                "Europe/Madrid",
                TimeSpan.Zero));

        Assert.AreEqual(
            "TIME_LOCAL_OFFSET_INVALID",
            exception.Diagnostic.Code);
    }

    [TestMethod]
    public void SystemClock_ReturnsUtcInstant()
    {
        IClock clock = new SystemClock();

        var now = clock.GetUtcNow();

        Assert.AreEqual(
            TimeSpan.Zero,
            now.Value.Offset);
        Assert.IsTrue(
            now.ToCanonicalString().EndsWith(
                "Z",
                StringComparison.Ordinal));
    }

    [TestMethod]
    public void TemporalExample_OnlyLocalViewChangesWithZone()
    {
        var service = new TemporalContractExampleService(
            new TimeZoneResolver());

        var madrid = service.Build("Europe/Madrid");
        var losAngeles =
            service.Build("America/Los_Angeles");

        Assert.AreEqual(
            madrid.BusinessDateIso,
            losAngeles.BusinessDateIso);
        Assert.AreEqual(
            madrid.UtcInstant,
            losAngeles.UtcInstant);
        Assert.AreNotEqual(
            madrid.LocalInstant,
            losAngeles.LocalInstant);
    }

    private static DomainRuleException CaptureDomainRule(
        Action action)
    {
        try
        {
            action();
        }
        catch (DomainRuleException exception)
        {
            return exception;
        }

        Assert.Fail("Expected DomainRuleException.");
        throw new InvalidOperationException();
    }
}
