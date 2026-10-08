using Valdrio.Application;
using Valdrio.Calculation;
using Valdrio.Domain;
using Valdrio.Infrastructure.CompanyFiles;

namespace Valdrio.Tests;

public sealed partial class T06ImportConfirmationTests
{
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
        Assert.AreEqual(-0.00000001m, negative[0].PostedAmount.Amount);
        Assert.IsTrue(negative.Skip(1).All(f => f.PostedAmount.Amount == 0m));
    }
}
