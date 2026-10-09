using Valdrio.Application;
using Valdrio.Domain;

namespace Valdrio.Tests;

public sealed partial class T06ImportConfirmationTests
{
    [TestMethod]
    public async Task T11_BulkClassificationPersistsAndCanBeEditedAgain()
    {
        using var file = Baseline();
        var w = await ImportedResources(file, "Amount;Ignored;Account\n100;x;00001\n200;y;00002");
        var company = w.Study.CompanyId; var study = w.Study.Id;
        var original = w.Resources.ToDictionary(r => r.Id, r => r.Parts.Single());
        var economicRevision = w.EconomicRevision;
        var fixedDefault = new ResourceClassification(ResourceBehavior.AssumedFixed, null,
            ResourceCostLayer.Unclassified, ResourceCapacityTreatment.None, null);
        var replacements = w.Resources.Select(r => new ResourcePartReplacement(r.Id,
            [r.Parts.Single().WithClassification(fixedDefault)])).ToArray();
        var receipt = file.Resources.ChangeResources(Mutation(w, ResourceMutationKind.ClassifyBatch,
            replacements: replacements));
        Assert.AreEqual(1, file.Resources.ReadResourceHistory(company, study).Count);
        w = file.Resources.ReadResources(company, study);
        Assert.IsTrue(w.Resources.All(r => r.Parts.Single().Classification.Behavior == ResourceBehavior.AssumedFixed));
        Assert.AreEqual(300m, w.Preparation.Coverage.Sum(c => c.Effective.Amount));
        Assert.AreEqual(economicRevision, w.EconomicRevision);
        Assert.IsTrue(w.Preparation.Coverage.All(c => c.Difference.Amount == 0m));
        foreach (var r in w.Resources)
        {
            Assert.AreEqual(original[r.Id].Id, r.Parts.Single().Id);
            Assert.AreEqual(original[r.Id].Amount, r.Parts.Single().Amount);
            Assert.AreEqual(original[r.Id].Routing, r.Parts.Single().Routing);
        }

        // Second edit changes the very same account, without a new part or a second amount.
        var target = w.Resources.Single(r => r.Sources.Single().Conversion.OriginalMoney.Amount == 100m);
        var unit = w.Model.DriverUnits.First().Id;
        var context = new ResourceBehaviorContext(unit, w.Study.PeriodStart.Value,
            w.Study.PeriodEnd.Value, new Quantity(0m, unit, w.Precision), new Quantity(1000m, unit, w.Precision));
        var variable = fixedDefault with { Behavior = ResourceBehavior.Variable, Context = context };
        file.Resources.ChangeResources(Mutation(w, ResourceMutationKind.ClassifyBatch,
            replacements: [new(target.Id, [target.Parts.Single().WithClassification(variable)])]));
        w = file.Resources.ReadResources(company, study);
        Assert.AreEqual(ResourceBehavior.Variable, w.Resources.Single(r => r.Id == target.Id).Parts.Single().Classification.Behavior);
        Assert.AreEqual(ResourceBehavior.AssumedFixed, w.Resources.Single(r => r.Id != target.Id).Parts.Single().Classification.Behavior);

        target = w.Resources.Single(r => r.Id == target.Id);
        file.Resources.ChangeResources(Mutation(w, ResourceMutationKind.ClassifyBatch,
            replacements: [new(target.Id, [target.Parts.Single().WithClassification(fixedDefault)])]));
        w = file.Resources.ReadResources(company, study);
        Assert.IsTrue(w.Resources.All(r => r.Parts.Single().Classification.Behavior == ResourceBehavior.AssumedFixed));
        Assert.AreEqual(3, file.Resources.ReadResourceHistory(company, study).Count);
        Assert.AreEqual(economicRevision, w.EconomicRevision);
        Assert.AreEqual(300m, w.Preparation.Coverage.Sum(c => c.Effective.Amount));
    }

    [TestMethod]
    public async Task T11_BatchRejectsAmountOrRoutingTamperingWithoutWriting()
    {
        using var file = Baseline();
        var w = await ImportedResources(file, "Amount;Ignored;Account\n100;x;00001\n200;y;00002");
        var original = w.Resources[0].Parts.Single();
        var changed = new ResourcePartDraft(original.Id, new(original.Amount.Amount + 1m, original.Amount.Currency,
            original.Amount.PrecisionPolicy), original.Routing,
            original.DirectAssignments, original.PoolId, original.ExclusionReason,
            original.Classification with { Behavior = ResourceBehavior.AssumedFixed });
        var before = Hash(file.Path);
        var error = Assert.ThrowsExactly<DomainRuleException>(() => file.Resources.ChangeResources(
            Mutation(w, ResourceMutationKind.ClassifyBatch, replacements: [new(w.Resources[0].Id, [changed])])));
        Assert.AreEqual("RES_COMMAND_INVALID", error.Diagnostic.Code);
        Assert.AreEqual(before, Hash(file.Path));

        var duplicate = new ResourcePartReplacement(w.Resources[0].Id,
            [original.WithClassification(original.Classification with { Behavior = ResourceBehavior.AssumedFixed })]);
        Assert.AreEqual("RES_COMMAND_INVALID", Assert.ThrowsExactly<DomainRuleException>(() =>
            file.Resources.ChangeResources(Mutation(w, ResourceMutationKind.ClassifyBatch,
                replacements: [duplicate, duplicate]))).Diagnostic.Code);
        Assert.AreEqual(before, Hash(file.Path));
    }
}
