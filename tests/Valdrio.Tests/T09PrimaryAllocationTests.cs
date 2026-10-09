using Valdrio.Application;
using Valdrio.Calculation;
using Valdrio.Domain;
using Valdrio.Infrastructure.CompanyFiles;

namespace Valdrio.Tests;

public sealed partial class T06ImportConfirmationTests
{
    [TestMethod]
    public async Task T09_PreparedAccountAllocatesWithoutPriorPoolAndReopensWithFrozenPolicy()
    {
        EntityId studyId;
        string expected;
        using (var file = Baseline())
        {
            var w = await ImportedResources(file);
            studyId = w.Study.Id;
            var correction = ResourceCorrectionCommand.Prepare(w, w.Resources.Single().Id, w.Study.Revision,
                Eur(-200m), Catalog.GetRequired(new("EUR")).Freeze(), Id(), Id(), Id(), "test")!;
            file.Resources.ChangeResources(correction);
            w = file.Resources.ReadResources(w.Study.CompanyId, studyId);
            Assert.IsTrue(w.Resources.Single().Parts.All(p => p.PoolId is null && p.Routing == ResourceRoutingKind.Pending));
            var nodes = w.Model.Structure.Nodes.Take(2).ToArray();
            var key = file.Allocations.SaveAllocationKey(KeyCommand(w, lines: [new(nodes[0].Id, 25m), new(nodes[1].Id, 75m)])).After;
            var rule = new PrimaryRule(Id(), w.Study.CompanyId, w.Model.Structure.Id, AccountSelectorKind.Exact,
                w.Origins.Single().Code, 0, key.Id, true);
            file.Allocations.ChangeAllocations(AllocationCommand(w, new([rule], [], [], [], [])));
            var live = file.Allocations.ReadAllocations(w.Study.CompanyId, studyId);
            var result = PrimaryAllocationService.Calculate(live);
            Assert.IsTrue(result.IsComplete);
            Assert.AreEqual(800m, result.Facts.Sum(f => f.PostedAmount.Amount));
            Assert.AreEqual(200m, result.ByNode(nodes[0].Id).Sum(f => f.PostedAmount.Amount));
            Assert.AreEqual(600m, result.ByNode(nodes[1].Id).Sum(f => f.PostedAmount.Amount));
            Assert.AreEqual(0m, result.Resources.Single().Pending.Amount);
            Assert.AreEqual(800m, result.Resources.Single().Pools.Amount);
            expected = CanonicalSnapshotJson.Serialize(result);
            var snapshot = file.Studies.Capture(new(w.Study.CompanyId, studyId, live.Resources.Study.Revision, Id(), "test"));
            snapshot.EnsureValid();
            var input = (AllocationStudySnapshotInput)snapshot.Input;
            Assert.IsTrue(input.IncludeUnroutedAccounts);
            Assert.AreEqual(expected, CanonicalSnapshotJson.Serialize(PrimaryAllocationService.Calculate(input)));
            Assert.IsFalse(StudyPreparationService.Evaluate(input).Diagnostics.Any(d => d.Code == "STUDY_PARTITIONS_PENDING"));

            // Existing v7 snapshots omit the new flag and keep the original pending-route semantics.
            var historical = new AllocationStudySnapshotInput(input.StudyContext, input.PrecisionPolicy, input.Model,
                input.Provenance, input.Imports, input.History, input.AllocationHistory);
            var historicalJson = AllocationStudySnapshotJson.Serialize(historical);
            Assert.IsFalse(historicalJson.Contains("IncludeUnroutedAccounts", StringComparison.Ordinal));
            var replay = AllocationStudySnapshotJson.Deserialize(historicalJson);
            Assert.AreEqual(historicalJson, AllocationStudySnapshotJson.Serialize(replay));
            Assert.AreEqual(AllocationStudySnapshotJson.EconomicJson(historical), AllocationStudySnapshotJson.EconomicJson(replay));
            Assert.IsTrue(PrimaryAllocationService.Calculate(replay).Gaps.All(g => g.Diagnostic.Code == "KEY_PART_PENDING"));
        }
        using var reopened = Service().Open(FileOf("current.valdrio"));
        Assert.AreEqual(expected, CanonicalSnapshotJson.Serialize(PrimaryAllocationService.Calculate(reopened.Allocations.ReadAllocations(reopened.Read().Id, studyId))));
        reopened.Studies.ReadSnapshots(reopened.Read().Id, studyId).Single().EnsureValid();
    }

    [TestMethod]
    public async Task T09_UnroutedAccountSupportsManualAndAbsoluteAssignmentsButMissingRuleStaysPending()
    {
        using var file = Baseline(); var w = await ImportedResources(file);
        var part = w.Resources.Single().Parts.Single();
        var missing = PrimaryAllocationService.Calculate(file.Allocations.ReadAllocations(w.Study.CompanyId, w.Study.Id));
        Assert.IsFalse(missing.IsComplete);
        Assert.AreEqual("KEY_RULE_MISSING", missing.Gaps.Single().Diagnostic.Code);
        Assert.AreEqual(1000m, missing.Resources.Single().Pending.Amount);
        var node = w.Model.Structure.Nodes.First();
        var key = file.Allocations.SaveAllocationKey(KeyCommand(w, lines: [new(node.Id, 100m)])).After;
        file.Allocations.ChangeAllocations(AllocationCommand(w, new([], [], [new(part.Id, key.Id)], [], [])));
        var live = file.Allocations.ReadAllocations(w.Study.CompanyId, w.Study.Id);
        Assert.IsTrue(PrimaryAllocationService.Calculate(live).IsComplete);
        var absolute = new AbsolutePartAllocation(part.Id, w.EconomicRevision, part.Amount, [new(node.Id, part.Amount)]);
        file.Allocations.ChangeAllocations(AllocationCommand(live.Resources, new([], [], [], [absolute], [])));
        live = file.Allocations.ReadAllocations(w.Study.CompanyId, w.Study.Id);
        Assert.AreEqual("absolute", PrimaryAllocationService.Calculate(live).Facts.Single().Selection);
        Assert.IsNull(live.Coverage.Single(c => c.SourceId == part.Id).Diagnostic);
    }

    [TestMethod]
    public async Task T09_RealCompanyAndFrozenSnapshotProduceIdenticalBalancedFacts()
    {
        using var file = Baseline();
        var resources = RouteForKey(file, await ImportedResources(file));
        var source = resources.Resources.Single();
        var part = source.Parts.Single();
        var nodes = resources.Model.Structure.Nodes.Take(3).ToArray();
        var key = file.Allocations.SaveAllocationKey(KeyCommand(resources, AllocationKeyMode.EqualShares,
            lines: nodes.Select(n => new AllocationKeyLine(n.Id, 1m)).Reverse().ToArray())).After;
        var config = new AllocationConfiguration([], [], [new(part.Id, key.Id)], [], []);
        var command = AllocationCommand(resources, config);
        var changed = file.Allocations.ChangeAllocations(command);
        var workspace = file.Allocations.ReadAllocations(resources.Study.CompanyId, resources.Study.Id);
        var result = PrimaryAllocationService.Calculate(workspace);
        Assert.IsTrue(result.IsComplete);
        Assert.AreEqual(3, result.Facts.Count);
        Assert.AreEqual(part.Amount.Amount, result.Facts.Sum(f => f.PostedAmount.Amount));
        Assert.AreEqual(part.Amount.Amount, result.Parts.Single().Output.Amount);
        Assert.IsTrue(result.Facts.All(f => f.KeyVersionId == key.Id && f.Selection == "manual"));
        Assert.IsTrue(result.Facts.All(f => f.PostedAmount.Currency == resources.Study.Currency.FunctionalCurrencyCode));
        Assert.AreEqual(0m, result.Resources.Single().Difference.Amount);
        var photo = file.Studies.Capture(new(resources.Study.CompanyId, resources.Study.Id,
            changed.ConfirmedStudy.Revision, Id(), "test"));
        photo.EnsureValid();
        Assert.IsInstanceOfType<AllocationStudySnapshotInput>(photo.Input);
        var fromSnapshot = PrimaryAllocationService.Calculate((AllocationStudySnapshotInput)photo.Input);
        Assert.AreEqual(CanonicalSnapshotJson.Serialize(result), CanonicalSnapshotJson.Serialize(fromSnapshot));
        Assert.AreEqual(CanonicalSnapshotJson.Serialize(result),
            CanonicalSnapshotJson.Serialize(PrimaryAllocationService.Calculate(workspace)));
    }

    [TestMethod]
    public async Task T09_MissingRuleIsNotHiddenByAnOtherwiseBalancedResource()
    {
        using var file = Baseline();
        var resources = RouteForKey(file, await ImportedResources(file));
        var result = PrimaryAllocationService.Calculate(
            file.Allocations.ReadAllocations(resources.Study.CompanyId, resources.Study.Id));
        Assert.IsFalse(result.IsComplete);
        Assert.AreEqual(0, result.Facts.Count);
        Assert.AreEqual("KEY_RULE_MISSING", result.Gaps.Single().Diagnostic.Code);
        Assert.IsFalse(result.Parts.Single().Covered);
        Assert.AreEqual(resources.Resources.Single().Parts.Single().Amount.Amount,
            result.Parts.Single().Difference.Amount);
        Assert.AreEqual(0m, result.Resources.Single().Difference.Amount);
    }

    [TestMethod]
    public async Task T09_MultiPartSignAndEightDigitRemaindersBalanceIndependently()
    {
        using var file = Baseline();
        var resources = RouteForKey(file, await ImportedResources(file));
        var source = resources.Resources.Single();
        var nodes = resources.Model.Structure.Nodes.Take(3).ToArray();
        var original = source.Parts.Single();
        var tiny = new ResourcePartDraft(Id(), Eur(-0.00000001m), ResourceRoutingKind.AllocationPool,
            poolId: original.PoolId);
        var rest = new ResourcePartDraft(Id(), Eur(original.Amount.Amount + 0.00000001m),
            ResourceRoutingKind.AllocationPool, poolId: original.PoolId);
        file.Resources.ChangeResources(Mutation(resources, ResourceMutationKind.EditResource,
            source.Id, [tiny, rest]));
        resources = file.Resources.ReadResources(resources.Study.CompanyId, resources.Study.Id);
        var key = file.Allocations.SaveAllocationKey(KeyCommand(resources, AllocationKeyMode.EqualShares,
            lines: nodes.Select(n => new AllocationKeyLine(n.Id, 1m)).ToArray())).After;
        file.Allocations.ChangeAllocations(AllocationCommand(resources,
            new AllocationConfiguration([], [], [new(tiny.Id, key.Id), new(rest.Id, key.Id)], [], [])));
        var result = PrimaryAllocationService.Calculate(file.Allocations.ReadAllocations(
            resources.Study.CompanyId, resources.Study.Id));
        Assert.IsTrue(result.IsComplete);
        Assert.AreEqual(-0.00000001m, result.ByResource(source.Id).Where(f => f.PartId == tiny.Id)
            .Sum(f => f.PostedAmount.Amount));
        Assert.AreEqual(original.Amount.Amount, result.Facts.Sum(f => f.PostedAmount.Amount));
        Assert.IsTrue(result.Parts.All(p => p.Difference.Amount == 0m));
        var negative = result.Facts.Where(f => f.PartId == tiny.Id).OrderBy(f => f.NodeId).ToArray();
        Assert.AreEqual(1, negative.Count(f => f.PostedAmount.Amount == -0.00000001m));
        Assert.AreEqual(2, negative.Count(f => f.PostedAmount.Amount == 0m));
    }

    [TestMethod]
    public async Task T09_ExplicitRuleWinsOverExactAccountAndPrefixWithStableProvenance()
    {
        using var file = Baseline();
        var resources = RouteForKey(file, await ImportedResources(file));
        var model = resources.Model.Structure;
        var account = resources.Origins.Single().Code;
        var part = resources.Resources.Single().Parts.Single();
        var nodes = model.Nodes.Take(2).ToArray();
        var prefixKey = file.Allocations.SaveAllocationKey(KeyCommand(resources,
            lines: [new(nodes[0].Id, 100m)])).After;
        var exactKey = file.Allocations.SaveAllocationKey(KeyCommand(resources,
            lines: [new(nodes[1].Id, 100m)])).After;
        var prefixRule = new PrimaryRule(Id(), resources.Study.CompanyId, model.Id,
            AccountSelectorKind.Prefix, account[..1], 900, prefixKey.Id, true);
        var exactRule = new PrimaryRule(Id(), resources.Study.CompanyId, model.Id,
            AccountSelectorKind.Exact, account, 0, exactKey.Id, true);
        var rules = new[] { prefixRule, exactRule };
        file.Allocations.ChangeAllocations(AllocationCommand(resources,
            new AllocationConfiguration(rules.Reverse().ToArray(), [], [], [], [])));
        var automatic = file.Allocations.ReadAllocations(resources.Study.CompanyId, resources.Study.Id);
        var result = PrimaryAllocationService.Calculate(automatic);
        Assert.IsTrue(result.IsComplete);
        Assert.AreEqual(exactRule.Id, result.Facts.Single().RuleId);
        Assert.AreEqual(exactKey.Id, result.Facts.Single().KeyVersionId);
        Assert.AreEqual("rule", result.Facts.Single().Selection);
        file.Allocations.ChangeAllocations(AllocationCommand(automatic.Resources,
            new AllocationConfiguration(rules, [], [new(part.Id, prefixKey.Id)], [], [])));
        var manual = PrimaryAllocationService.Calculate(file.Allocations.ReadAllocations(
            resources.Study.CompanyId, resources.Study.Id));
        Assert.IsTrue(manual.IsComplete);
        Assert.AreEqual(prefixKey.Id, manual.Facts.Single().KeyVersionId);
        Assert.AreEqual("manual", manual.Facts.Single().Selection);
        Assert.IsNull(manual.Facts.Single().RuleId);
    }

    [TestMethod]
    public async Task T09_AbsoluteAmountsAreNotRoundedAgainAndPoolTotalsComeFromFacts()
    {
        using var file = Baseline();
        var resources = RouteForKey(file, await ImportedResources(file));
        var part = resources.Resources.Single().Parts.Single();
        var nodes = resources.Model.Structure.Nodes.Take(2).ToArray();
        var assigned = new AbsolutePartAllocation(part.Id, resources.EconomicRevision, part.Amount,
            [new(nodes[0].Id, Eur(0.00000001m)),
             new(nodes[1].Id, Eur(part.Amount.Amount - 0.00000001m))]);
        file.Allocations.ChangeAllocations(AllocationCommand(resources,
            new AllocationConfiguration([], [], [], [assigned], [])));
        var result = PrimaryAllocationService.Calculate(file.Allocations.ReadAllocations(
            resources.Study.CompanyId, resources.Study.Id));
        Assert.IsTrue(result.IsComplete);
        Assert.AreEqual(2, result.Facts.Count);
        Assert.IsTrue(result.Facts.All(f => f.Selection == "absolute" && f.KeyVersionId is null &&
            f.RuleId is null && f.NumericAdjustment == 0m && f.RoundingAdjustment == 0m));
        Assert.AreEqual(0.00000001m, result.Facts.Single(f => f.NodeId == nodes[0].Id).PostedAmount.Amount);
        Assert.AreEqual(part.Amount.Amount, result.Pools.Sum(p => p.PostedAmount.Amount));
        Assert.AreEqual(part.Amount.Amount, result.Facts.Sum(f => f.PostedAmount.Amount));
        Assert.AreEqual(0m, result.Parts.Single().Difference.Amount);
    }
}
