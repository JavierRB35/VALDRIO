using Valdrio.Application;
using Valdrio.Calculation;
using Valdrio.Domain;

namespace Valdrio.Tests;

[TestClass]
public sealed class Audit20261005BoundaryTests
{
    [TestMethod]
    public void A01_SnapshotRejectsEconomicallyForgedFxBridge()
    {
        var eur = new CurrencyCode("EUR");
        var app = new FxConverter().Convert("audit", new Money(100m, new CurrencyCode("USD")), eur,
            new FxRate(new FxRateId("r"), new CurrencyCode("USD"), eur, 0.9m,
                BusinessDate.ParseIso("2026-10-05"), "audit", "set"));
        var forged = app with { ConvertedRaw = 999m, ConvertedOriginalMoney = new Money(999m, eur) };
        Assert.ThrowsExactly<ArgumentException>(() => CanonicalSnapshotJson.Serialize(forged));
    }

    [TestMethod]
    public void A04_RevaluationRejectsDuplicateOriginsAndDefaultSelectedRateBeforePorts()
    {
        var spy = new Ports();
        var service = new RevalueStudyService(spy, spy);
        var command = Command(1);
        Assert.ThrowsExactly<ArgumentException>(() => service.RevalueStudy(command with
        { Items = [command.Items[0], command.Items[0]] }));
        Assert.ThrowsExactly<ArgumentException>(() => service.RevalueStudy(command with
        { Items = [command.Items[0] with { SelectedRateId = default(FxRateId) }] }));
        Assert.AreEqual(0, spy.Calls);
    }

    [TestMethod]
    public void A07_RevisionIsLongAndOverflowDoesNotCallPorts()
    {
        var spy = new Ports();
        var service = new RevalueStudyService(spy, spy);
        Assert.AreEqual(2147483649L, service.RevalueStudy(Command(2147483648L)).Revision);
        spy.Calls = 0;
        Assert.ThrowsExactly<OverflowException>(() => service.RevalueStudy(Command(long.MaxValue)));
        Assert.AreEqual(0, spy.Calls);
    }

    private static RevalueStudyCommand Command(long revision)
    {
        var eur = new CurrencyCode("EUR");
        var usd = new CurrencyCode("USD");
        var rate = new FxRate(new FxRateId("r"), usd, eur, 0.9m,
            BusinessDate.ParseIso("2026-10-05"), "audit", "set");
        return new RevalueStudyCommand("study", revision, eur,
            new FxRateSetVersion("set", BusinessDate.ParseIso("2026-10-05"), "audit", [rate]),
            [new RevalueStudyItem("resource", new Money(100m, eur), null)]);
    }
    private sealed class Ports : IStudyRevaluationValidator, IStudyRevaluationRepository
    {
        public int Calls { get; set; }
        public void ValidatePartitionsAndConsumptions(RevalueStudyCandidate candidate) => Calls++;
        public RevalueStudyCommitResult CommitAtomically(RevalueStudyCandidate candidate)
        {
            Calls++;
            return new(candidate.StudyId, candidate.NewRevision, candidate.RateSetVersionId);
        }
    }
}
