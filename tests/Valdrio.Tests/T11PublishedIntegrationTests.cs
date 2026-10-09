using Valdrio.Application;
using Valdrio.Domain;

namespace Valdrio.Tests;

public sealed partial class T06ImportConfirmationTests
{
    [TestMethod]
    public async Task T11_ClosedSecondaryPublishesReopenableAndReplayableTerminalLedger()
    {
        EntityId companyId, studyId, principalId;
        string posted;
        using (var file = Baseline())
        {
            var w = await ImportedResources(file);
            companyId = w.Study.CompanyId;
            studyId = w.Study.Id;
            var model = w.Model.Structure;
            var auxiliaries = model.Nodes.Where(n => n.IsActive && n.Role == NodeRole.Auxiliary).ToArray();
            Assert.IsTrue(auxiliaries.Length > 0);
            principalId = model.Nodes.First(n => n.IsActive && n.Role == NodeRole.Principal).Id;

            // T09: manually assign the imported resource to an auxiliary.
            var primary = file.Allocations.SaveAllocationKey(KeyCommand(w,
                lines: [new(auxiliaries[0].Id, 100m)])).After;
            var secondary = file.Allocations.SaveAllocationKey(KeyCommand(w, stage: AllocationStage.Secondary,
                lines: [new(principalId, 100m)])).After;
            var rules = auxiliaries.Select(node =>
                new SecondaryRule(Id(), companyId, model.Id, node.Id, "all", secondary.Id, true)).ToArray();
            var part = w.Resources.Single().Parts.Single();
            var configuration = new AllocationConfiguration([], rules,
                [new ExplicitPrimaryAssignment(part.Id, primary.Id)], [], []);
            file.Allocations.ChangeAllocations(AllocationCommand(w, configuration));
            var current = file.Allocations.ReadAllocations(companyId, studyId);
            var expectedAmount = part.Amount.Amount;
            Assert.IsTrue(PrimaryAllocationService.Calculate(current).IsComplete);

            // Close primary first; secondary routing remains editable.
            file.Allocations.ChangeAllocations(AllocationCommand(current.Resources,
                current.Configuration.WithPrimaryClosed(true)));
            current = file.Allocations.ReadAllocations(companyId, studyId);
            Assert.IsTrue(current.Configuration.PrimaryClosed);
            Assert.IsFalse(current.Configuration.SecondaryClosed);
            var calculated = SecondaryAllocationService.Calculate(current);
            Assert.IsTrue(calculated.IsComplete);
            Assert.AreEqual(expectedAmount, calculated.ByTerminal(principalId).Sum(f => f.PostedAmount.Amount));
            var fingerprint = SecondaryAllocationService.ResultFingerprint(calculated, current.Configuration);

            var closed = file.Allocations.ChangeAllocations(AllocationCommand(current.Resources,
                current.Configuration.WithSecondaryClosed(true, fingerprint)));
            Assert.IsNotNull(closed.SecondaryPublication);
            Assert.AreEqual(fingerprint, closed.SecondaryPublication.CalculationFingerprint);
            Assert.AreEqual(expectedAmount, closed.SecondaryPublication.Facts.Sum(f => f.PostedAmount.Amount));
            Assert.IsTrue(closed.SecondaryPublication.Reconciliations.All(r => r.Difference.Amount == 0m));
            posted = CanonicalSnapshotJson.Serialize(closed.SecondaryPublication);
            current = file.Allocations.ReadAllocations(companyId, studyId);
            Assert.IsNotNull(current.SecondaryPublication);
            Assert.AreEqual(posted, CanonicalSnapshotJson.Serialize(current.SecondaryPublication));

            // A closed secondary phase cannot silently reopen primary or mutate routes.
            var forbidden = Assert.ThrowsExactly<DomainRuleException>(() =>
                file.Allocations.ChangeAllocations(AllocationCommand(current.Resources,
                    current.Configuration.WithPrimaryClosed(false))));
            Assert.AreEqual("SECONDARY_CLOSED", forbidden.Diagnostic.Code);

            // User-approved sequence: reopen secondary (primary remains locked),
            // then reopen primary (secondary publication is no longer current).
            file.Allocations.ChangeAllocations(AllocationCommand(current.Resources,
                current.Configuration.WithSecondaryClosed(false)));
            current = file.Allocations.ReadAllocations(companyId, studyId);
            Assert.IsTrue(current.Configuration.PrimaryClosed);
            Assert.IsFalse(current.Configuration.SecondaryClosed);
            Assert.IsNull(current.SecondaryPublication);
            file.Allocations.ChangeAllocations(AllocationCommand(current.Resources,
                current.Configuration.WithPrimaryClosed(false)));
            current = file.Allocations.ReadAllocations(companyId, studyId);
            Assert.IsFalse(current.Configuration.PrimaryClosed);
            Assert.IsFalse(current.Configuration.SecondaryClosed);
            Assert.IsNull(current.Configuration.SecondaryCalculationHash);

            // Historical receipt remains available for audit after reopening.
            Assert.AreEqual(posted, CanonicalSnapshotJson.Serialize(
                file.Allocations.ReadAllocationHistory(companyId, studyId)
                    .Single(c => c.SecondaryPublication is not null).SecondaryPublication!));
        }
        using var reopened = Service().Open(FileOf("current.valdrio"));
        var latest = reopened.Allocations.ReadAllocations(companyId, studyId);
        Assert.IsFalse(latest.Configuration.SecondaryClosed);
        Assert.IsNull(latest.SecondaryPublication);
        Assert.AreEqual(posted, CanonicalSnapshotJson.Serialize(
            reopened.Allocations.ReadAllocationHistory(companyId, studyId)
                .Single(c => c.SecondaryPublication is not null).SecondaryPublication!));
    }
}
