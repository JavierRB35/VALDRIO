using System.Globalization;
using Valdrio.Application;
using Valdrio.Calculation;
using Valdrio.Domain;
using Valdrio.Infrastructure.CompanyFiles;
namespace Valdrio.Tests;
public sealed partial class T06ImportConfirmationTests
{
    private static SaveAllocationKey KeyCommand(ResourceWorkspaceSnapshot w, AllocationKeyMode mode = AllocationKeyMode.Percentage,
        AllocationStage stage = AllocationStage.Primary, bool publish = true, IReadOnlyList<AllocationKeyLine>? lines = null) =>
        new(w.Study.CompanyId, w.Model.Structure.Id, Id(), null, -1, "Reparto comprobado", stage, mode,
            mode == AllocationKeyMode.DriverUnits ? w.Model.DriverUnits.First().Id : null, SelfAllocationPolicy.Forbidden,
            lines ?? [new(w.Model.Structure.Nodes.First(n => n.Role == NodeRole.Principal).Id, mode == AllocationKeyMode.Percentage ? 100 : 1)], publish, Id(), "test");
    private static ChangeAllocations AllocationCommand(ResourceWorkspaceSnapshot w, AllocationConfiguration config) =>
        new(w.Study.CompanyId, w.Study.Id, w.Study.Revision, Id(), "test", config);
    private static ResourceWorkspaceSnapshot RouteForKey(ICompanyFileSession file, ResourceWorkspaceSnapshot w)
    {
        var part = new ResourcePartDraft(Id(), w.Preparation.Coverage.Single().Effective, ResourceRoutingKind.AllocationPool,
            poolId: w.Model.Structure.Pools.First().Id);
        file.Resources.ChangeResources(Mutation(w, ResourceMutationKind.EditResource, w.Resources.Single().Id, [part]));
        return file.Resources.ReadResources(w.Study.CompanyId, w.Study.Id);
    }

    [TestMethod]
    public async Task T08_C06_Draft80CannotPublishAndPublicationRequiresNewVersion()
    {
        using var file = Baseline(); var w = await ImportedResources(file);
        var command = KeyCommand(w, publish: false, lines: [new(w.Model.Structure.Nodes[0].Id, 80)]);
        var draft = file.Allocations.SaveAllocationKey(command); var hash = Hash(file.Path);
        var publish = command with { ExpectedRevision = 0, Publish = true, RequestId = Id() };
        Assert.AreEqual("KEY_INCOMPLETE", Assert.ThrowsExactly<DomainRuleException>(() => file.Allocations.SaveAllocationKey(publish)).Diagnostic.Code);
        Assert.AreEqual(hash, Hash(file.Path)); Assert.AreEqual(80m, draft.After.Lines.Single().Value);
        publish = publish with { Lines = [new(w.Model.Structure.Nodes[0].Id, 100)] };
        var saved = file.Allocations.SaveAllocationKey(publish);
        Assert.AreEqual(CanonicalSnapshotJson.Serialize(saved), CanonicalSnapshotJson.Serialize(file.Allocations.SaveAllocationKey(publish)));
        var renamed = file.Allocations.SaveAllocationKey(publish with { ExpectedRevision = 1, RequestId = Id(), Name = "Changed" });
        Assert.AreEqual("Changed", renamed.After.Name);
        Assert.AreEqual(saved.After.Lines.Single(), renamed.After.Lines.Single());
        var next = file.Allocations.SaveAllocationKey(publish with { KeyId = Id(), ParentVersionId = saved.After.Id, ExpectedRevision = -1, RequestId = Id() });
        Assert.AreEqual(2, next.After.VersionNumber); Assert.AreEqual(saved.After.FamilyId, next.After.FamilyId);
        Assert.AreEqual(2, file.Allocations.ReadAllocationKeys(w.Study.CompanyId, w.Model.Structure.Id).Count);
    }
    [TestMethod]
    public async Task T08_C07_C08_EqualSharesNegativeAmountsAndDestinationOrderPreserveLedger()
    {
        using var file = Baseline(); var w = await ImportedResources(file); var model = w.Model.Structure;
        Assert.IsTrue(model.Nodes.Count >= 3);
        var lines = model.Nodes.Take(3).Select(n => new AllocationKeyLine(n.Id, 1m)).ToArray();
        var key = file.Allocations.SaveAllocationKey(KeyCommand(w, AllocationKeyMode.EqualShares, lines: lines)).After;
        var reverse = new AllocationKeyVersion(key.Id, key.CompanyId, key.ModelVersionId, key.FamilyId, key.VersionNumber,
            key.ParentVersionId, key.Revision, key.Name, key.Stage, key.Mode, key.UnitId, key.SelfAllocation, key.Status, key.PublishedAtUtc, lines.Reverse().ToArray());
        var positive = AllocationKeyRules.Preview(key, model, w.Model.DriverUnits, Eur(0.01m), w.Precision);
        var negative = AllocationKeyRules.Preview(reverse, model, w.Model.DriverUnits, Eur(-0.01m), w.Precision);
        Assert.AreEqual(0.01m, positive.PostedTotal); Assert.AreEqual(-0.01m, negative.PostedTotal);
        foreach (var line in positive.Lines) Assert.AreEqual(-line.PostedAmount, negative.Lines.Single(n => n.DestinationId == line.DestinationId).PostedAmount);
        Assert.AreEqual(CanonicalSnapshotJson.Serialize(positive), CanonicalSnapshotJson.Serialize(AllocationKeyRules.Preview(reverse, model, w.Model.DriverUnits, Eur(0.01m), w.Precision)));
        // The methodological cent projection is separate from the internal 8-place ledger.
        var weights = new ProportionalAllocator().FromWeights(0.01m, [new(new("A"),1), new(new("B"),1), new(new("C"),1)]);
        var cents = new BalancedRounder().Round(0.01m, weights, 0.01m);
        Assert.AreEqual(0.01m, cents.Lines.Single(l => l.DestinationId.Value == "A").PostedAmount);
        Assert.AreEqual(0m, cents.Lines.Single(l => l.DestinationId.Value == "B").PostedAmount);
    }
    [TestMethod]
    public async Task T08ZeroDriverDuplicateDestinationAndForeignScopeNeverWrite()
    {
        using var file = Baseline(); var w = await ImportedResources(file); var hash = Hash(file.Path);
        var command = KeyCommand(w, AllocationKeyMode.DriverUnits, lines: [new(w.Model.Structure.Nodes[0].Id, 0)]);
        Assert.AreEqual("KEY_ZERO_DRIVER_TOTAL", Assert.ThrowsExactly<DomainRuleException>(() => file.Allocations.SaveAllocationKey(command)).Diagnostic.Code);
        Assert.ThrowsExactly<DomainRuleException>(() => file.Allocations.SaveAllocationKey(command with { Lines = [new(Id(), 10)] }));
        Assert.ThrowsExactly<DomainRuleException>(() => file.Allocations.SaveAllocationKey(command with { Lines = [new(w.Model.Structure.Nodes[0].Id, 1), new(w.Model.Structure.Nodes[0].Id, 1)] }));
        Assert.AreEqual(hash, Hash(file.Path));
    }
    [TestMethod]
    public async Task T08ManualExactAndLongestPrefixResolveDeterministicallyAndTiesFail()
    {
        using var file = Baseline(); var w = await ImportedResources(file); var model = w.Model.Structure;
        var keys = Enumerable.Range(0, 3).Select(_ => file.Allocations.SaveAllocationKey(KeyCommand(w)).After).ToArray();
        PrimaryRule Rule(string value, AccountSelectorKind kind, int index, int priority = 0) => new(Id(), w.Study.CompanyId, model.Id, kind, value, priority, keys[index].Id, true);
        var rules = new[] { Rule("6", AccountSelectorKind.Prefix, 0, 999), Rule("600", AccountSelectorKind.Prefix, 1), Rule("6005", AccountSelectorKind.Exact, 2) };
        Assert.AreEqual(keys[0].Id, AllocationKeyRules.ResolvePrimary("6005", keys[0].Id, rules, keys, model).Id);
        Assert.AreEqual(keys[2].Id, AllocationKeyRules.ResolvePrimary("6005", null, rules, keys, model).Id);
        Assert.AreEqual(keys[1].Id, AllocationKeyRules.ResolvePrimary("6006", null, rules.Reverse().ToArray(), keys, model).Id);
        Assert.AreEqual("KEY_RULE_MISSING", Assert.ThrowsExactly<DomainRuleException>(() => AllocationKeyRules.ResolvePrimary("06005", null, rules, keys, model)).Diagnostic.Code);
        var tied = rules.Append(Rule("6005", AccountSelectorKind.Exact, 1)).ToArray();
        Assert.AreEqual("KEY_RULE_AMBIGUOUS", Assert.ThrowsExactly<DomainRuleException>(() => AllocationKeyRules.ResolvePrimary("6005", null, tied, keys, model)).Diagnostic.Code);
    }
    [TestMethod]
    public async Task T08PrimarySecondaryAndChosenNamesSurviveReopenAndSnapshotsKeepAppliedVersions()
    {
        StudySnapshotReceipt photo; EntityId studyId, companyId; AllocationKeyVersion first;
        using (var file = Baseline())
        {
            var study = Study(file); studyId = study.Id; companyId = study.CompanyId;
            var profile = DescriptionProfile(study, grouping: new([new(2,"00123"),new(3,"00123")]));
            file.Imports.ConfirmImport(Command(Prepare(file, study, await Source(profile, "Amount;Name;Account\n100;Papelería;00123\n60;Material de oficina;00123"), profile)));
            var w = RouteForKey(file, file.Resources.ReadResources(companyId, studyId));
            first = file.Allocations.SaveAllocationKey(KeyCommand(w)).After;
            var secondary = file.Allocations.SaveAllocationKey(KeyCommand(w, stage: AllocationStage.Secondary)).After;
            var rules = w.Model.Structure.Nodes.Where(n => n.Role == NodeRole.Auxiliary).Select(n => new SecondaryRule(Id(), companyId, w.Model.Structure.Id, n.Id, "all", secondary.Id, true)).ToArray();
            var config = new AllocationConfiguration([new(Id(), companyId, w.Model.Structure.Id, AccountSelectorKind.Exact, "00123", 0, first.Id, true)], rules, [], [], [new("00123", "Papelería")]);
            var command = AllocationCommand(w, config); var change = file.Allocations.ChangeAllocations(command);
            Assert.AreEqual(CanonicalSnapshotJson.Serialize(change), CanonicalSnapshotJson.Serialize(file.Allocations.ChangeAllocations(command)));
            var saved = file.Allocations.ReadAllocations(companyId, studyId);
            Assert.IsTrue(saved.Coverage.All(c => c.Diagnostic is null));
            photo = file.Studies.Capture(new(companyId, studyId, change.ConfirmedStudy.Revision, Id(), "test"));
            Assert.AreEqual(AllocationStudySnapshotInput.Schema, photo.PayloadSchema); photo.EnsureValid();
            Assert.AreEqual(first.Id, ((AllocationStudySnapshotInput)photo.Input).AppliedKeys.Single(a => a.ComponentGroup == "").Key.Id);
            var renamed = file.Allocations.ChangeAllocations(AllocationCommand(saved.Resources, new(config.PrimaryRules, config.SecondaryRules, [], [], [new("00123", "Material de oficina")])));
            var next = file.Studies.Capture(new(companyId, studyId, renamed.ConfirmedStudy.Revision, Id(), "test"));
            Assert.AreEqual(photo.EconomicInputHash, next.EconomicInputHash);
            Assert.AreNotEqual(photo.FullSnapshotHash, next.FullSnapshotHash);
            file.Allocations.SaveAllocationKey(KeyCommand(w) with { ParentVersionId = first.Id });
        }
        using var reopened = Service().Open(FileOf("current.valdrio"));
        var current = reopened.Allocations.ReadAllocations(companyId, studyId);
        Assert.AreEqual("Material de oficina", current.Configuration.Descriptions.Single().Description);
        Assert.AreEqual(2, current.Resources.Origins.Single().Descriptions.Count);
        Assert.AreEqual(160m, current.Resources.Preparation.Coverage.Single().Effective.Amount);
        Assert.AreEqual(first.Id, current.Coverage.Single(c => c.ComponentGroup == "").KeyId);
        Assert.AreEqual(photo.CanonicalJson, reopened.Studies.ReadSnapshots(companyId, studyId).Single(s => s.Id == photo.Id).CanonicalJson);
    }
    [TestMethod]
    public async Task T08AbsoluteAllocationIsBoundToPartAndRevisionAndCannotBecomeTemplate()
    {
        using var file = Baseline(); var w = RouteForKey(file, await ImportedResources(file)); var part = w.Resources.Single().Parts.Single();
        var absolute = new AbsolutePartAllocation(part.Id, w.Study.Revision, part.Amount, [new(w.Model.Structure.Nodes[0].Id, Eur(1000))]);
        file.Allocations.ChangeAllocations(AllocationCommand(w, new([], [], [], [absolute], [])));
        var saved = file.Allocations.ReadAllocations(w.Study.CompanyId, w.Study.Id);
        Assert.IsNull(saved.Coverage.Single(c => c.SourceId == part.Id).Diagnostic);
        file.Resources.ChangeResources(Mutation(saved.Resources, ResourceMutationKind.EditResource, w.Resources.Single().Id,
            [new(part.Id, Eur(900), ResourceRoutingKind.AllocationPool, poolId: part.PoolId)], [Delta(-100)]));
        var stale = file.Allocations.ReadAllocations(w.Study.CompanyId, w.Study.Id);
        Assert.AreEqual("KEY_ABSOLUTE_STALE", stale.Coverage.Single(c => c.SourceId == part.Id).Diagnostic!.Code);
    }
}
