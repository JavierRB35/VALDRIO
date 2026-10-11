using Valdrio.Application;
using Valdrio.Domain;

namespace Valdrio.Tests;

public sealed partial class T06ImportConfirmationTests
{
    [TestMethod]
    public async Task T13_RealDirectPoolAndExcludedPartsReconcileWithoutDoubleCounting()
    {
        using var file = Baseline();
        var resources = await ImportedResources(file, "Amount;Ignored;Account\n1000;x;6000");
        var companyId = resources.Study.CompanyId;
        var studyId = resources.Study.Id;
        var resource = resources.Resources.Single();
        var objectId = Id();
        file.Resources.ChangeResources(Mutation(resources, ResourceMutationKind.PutObject,
            obj: new ResourceCostObject(objectId, companyId, studyId,
                "CONTRACT-001", "Service contract", ResourceObjectKind.Product)));
        resources = file.Resources.ReadResources(companyId, studyId);
        var pool = resources.Model.Structure.Pools.First();
        var direct = new ResourcePartDraft(Id(), Eur(300m), ResourceRoutingKind.DirectObject,
            [new(objectId, Eur(300m))]);
        var shared = new ResourcePartDraft(Id(), Eur(500m), ResourceRoutingKind.AllocationPool,
            poolId: pool.Id);
        var excluded = new ResourcePartDraft(Id(), Eur(200m), ResourceRoutingKind.Excluded,
            exclusionReason: "Explicitly outside the cost study");
        file.Resources.ChangeResources(Mutation(resources, ResourceMutationKind.EditResource,
            resource.Id, [direct, shared, excluded]));
        resources = file.Resources.ReadResources(companyId, studyId);
        var coverage = resources.Preparation.Coverage.Single();
        Assert.AreEqual(1000m, coverage.Effective.Amount);
        Assert.AreEqual(300m, coverage.Direct.Amount);
        Assert.AreEqual(500m, coverage.Pools.Amount);
        Assert.AreEqual(200m, coverage.Excluded.Amount);
        Assert.AreEqual(0m, coverage.Pending.Amount);
        Assert.AreEqual(0m, coverage.Difference.Amount);

        var model = resources.Model.Structure;
        var principal = model.Nodes.First(node => node.IsActive && node.Role == NodeRole.Principal);
        var auxiliaries = model.Nodes.Where(node => node.IsActive && node.Role == NodeRole.Auxiliary).ToArray();
        var primaryKey = file.Allocations.SaveAllocationKey(
            KeyCommand(resources, lines: [new(principal.Id, 100m)])).After;
        var secondaryKey = file.Allocations.SaveAllocationKey(
            KeyCommand(resources, stage: AllocationStage.Secondary,
                lines: [new(principal.Id, 100m)])).After;
        var secondaryRules = auxiliaries.Select(auxiliary =>
            new SecondaryRule(Id(), companyId, model.Id, auxiliary.Id, "all", secondaryKey.Id, true)).ToArray();
        var configuration = new AllocationConfiguration([], secondaryRules,
            [new ExplicitPrimaryAssignment(shared.Id, primaryKey.Id)], [], []);
        file.Allocations.ChangeAllocations(AllocationCommand(resources, configuration));
        var live = file.Allocations.ReadAllocations(companyId, studyId);
        var primary = PrimaryAllocationService.Calculate(live);
        Assert.IsTrue(primary.IsComplete);
        Assert.AreEqual(500m, primary.Facts.Sum(fact => fact.PostedAmount.Amount));

        file.Allocations.ChangeAllocations(AllocationCommand(live.Resources,
            configuration.WithPrimaryClosed(true)));
        live = file.Allocations.ReadAllocations(companyId, studyId);
        var secondary = SecondaryAllocationService.Calculate(live);
        Assert.IsTrue(secondary.IsComplete);
        Assert.AreEqual(500m, secondary.Facts.Sum(fact => fact.PostedAmount.Amount));
        var bridge = secondary.ResourceBridges.Single();
        Assert.AreEqual(1000m, bridge.Effective.Amount);
        Assert.AreEqual(300m, bridge.Direct.Amount);
        Assert.AreEqual(200m, bridge.Excluded.Amount);
        Assert.AreEqual(500m, bridge.Terminal.Amount);
        Assert.AreEqual(0m, bridge.Difference.Amount);
        Assert.AreEqual(0m, secondary.Reconciliations.Sum(r => r.Difference.Amount));

        var stamp = SecondaryAllocationService.ResultFingerprint(secondary, live.Configuration);
        var published = file.Allocations.ChangeAllocations(AllocationCommand(live.Resources,
            live.Configuration.WithSecondaryClosed(true, stamp)));
        Assert.AreEqual(500m,
            published.SecondaryPublication!.Facts.Sum(fact => fact.PostedAmount.Amount));
    }

    [TestMethod]
    public async Task T13_GroupedClosingBalancePreservesTwoDescriptionsAndRoutesOneAccount()
    {
        using var file = Baseline();
        var study = Study(file);
        var profile = GroupProfile(study, new(2, "6005"), new(3, "6005"));
        var document = await Source(profile,
            "Amount;Description;Account\n280;Base expense;6005\n78,35;Different source description;6005");
        var prepared = Prepare(file, study, document, profile);
        Assert.IsTrue(prepared.CanConfirm);
        file.Imports.ConfirmImport(Command(prepared));
        var resources = file.Resources.ReadResources(study.CompanyId, study.Id);
        var origin = resources.Origins.Single();
        Assert.AreEqual("6005", origin.Code);
        Assert.IsTrue(origin.HasDescriptionConflict);
        Assert.AreEqual(2, origin.Descriptions.Count);
        Assert.AreEqual(358.35m, resources.Preparation.Coverage.Single().Effective.Amount);

        var companyId = study.CompanyId;
        var model = resources.Model.Structure;
        var principal = model.Nodes.First(node => node.IsActive && node.Role == NodeRole.Principal);
        var primaryKey = file.Allocations.SaveAllocationKey(
            KeyCommand(resources, lines: [new(principal.Id, 100m)])).After;
        var secondaryKey = file.Allocations.SaveAllocationKey(
            KeyCommand(resources, stage: AllocationStage.Secondary,
                lines: [new(principal.Id, 100m)])).After;
        var primaryRule = new PrimaryRule(Id(), companyId, model.Id,
            AccountSelectorKind.Exact, "6005", 0, primaryKey.Id, true);
        var secondaryRules = model.Nodes
            .Where(node => node.IsActive && node.Role == NodeRole.Auxiliary)
            .Select(node => new SecondaryRule(Id(), companyId, model.Id,
                node.Id, "all", secondaryKey.Id, true)).ToArray();
        var configuration = new AllocationConfiguration([primaryRule], secondaryRules,
            [], [], []);
        file.Allocations.ChangeAllocations(AllocationCommand(resources, configuration));
        var live = file.Allocations.ReadAllocations(companyId, study.Id);
        Assert.IsTrue(PrimaryAllocationService.Calculate(live).IsComplete);
        file.Allocations.ChangeAllocations(AllocationCommand(live.Resources,
            live.Configuration.WithPrimaryClosed(true)));
        live = file.Allocations.ReadAllocations(companyId, study.Id);
        var secondary = SecondaryAllocationService.Calculate(live);
        Assert.IsTrue(secondary.IsComplete);
        Assert.AreEqual(358.35m, secondary.Facts.Sum(fact => fact.PostedAmount.Amount));
        Assert.AreEqual(0m, secondary.Reconciliations.Sum(r => r.Difference.Amount));
        var fingerprint = SecondaryAllocationService.ResultFingerprint(secondary, live.Configuration);
        file.Allocations.ChangeAllocations(AllocationCommand(live.Resources,
            live.Configuration.WithSecondaryClosed(true, fingerprint)));

        using var reopened = Service().Open(file.Path, CompanyFileOpenMode.ReadOnly);
        Assert.AreEqual(2, reopened.Resources.ReadResources(companyId, study.Id)
            .Origins.Single().Descriptions.Count);
        Assert.AreEqual(358.35m,
            reopened.Allocations.ReadAllocations(companyId, study.Id)
                .SecondaryPublication!.Facts.Sum(fact => fact.PostedAmount.Amount));
    }
}
