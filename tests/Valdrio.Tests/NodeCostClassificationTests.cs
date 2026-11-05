using Microsoft.VisualStudio.TestTools.UnitTesting;
using Valdrio.Domain;

namespace Valdrio.Tests;

[TestClass]
public sealed class NodeCostClassificationTests
{
    private static CalculationNodeDefinition Node(IReadOnlyList<string> tags, NodeRole role = NodeRole.Principal) =>
        new(new(Guid.Parse("00000000-0000-4000-8000-000000000001")),
            new(Guid.Parse("00000000-0000-4000-8000-000000000002")),
            new(Guid.Parse("00000000-0000-4000-8000-000000000003")),
            "MOD-MONTAJE", role, tags, true);

    [TestMethod]
    public void ExplicitFunctionAndPurposeRemainIndependentOfRoleAndLegacyTags()
    {
        var tags = NodeCostClassification.WithClassification(
            ["maintenance", "production"], NodeEconomicFunction.Production, NodeTariffPurpose.DirectLabor);
        var node = Node(tags);

        Assert.AreEqual(NodeRole.Principal, node.Role);
        Assert.AreEqual(NodeEconomicFunction.Production, NodeCostClassification.Function(node));
        Assert.AreEqual(NodeTariffPurpose.DirectLabor, NodeCostClassification.Purpose(node));
        Assert.IsTrue(NodeCostClassification.IsDirectFactor(node));
        Assert.IsTrue(NodeCostClassification.HasValidTags(node.FunctionTags));
        CollectionAssert.Contains(node.FunctionTags.ToArray(), "maintenance");
        CollectionAssert.Contains(node.FunctionTags.ToArray(), "production");
    }

    [TestMethod]
    public void HistoricalFreeTagsAreNotSilentlyReclassified()
    {
        var legacy = Node(["production", "maintenance"], NodeRole.Auxiliary);
        Assert.AreEqual(NodeEconomicFunction.Unspecified, NodeCostClassification.Function(legacy));
        Assert.AreEqual(NodeTariffPurpose.General, NodeCostClassification.Purpose(legacy));
        Assert.IsFalse(NodeCostClassification.IsDirectFactor(legacy));
    }

    [TestMethod]
    public void EditingFunctionDoesNotChangePurposeOrOtherTags()
    {
        var source = NodeCostClassification.WithClassification(
            ["maintenance"], NodeEconomicFunction.Production, NodeTariffPurpose.Procurement);
        var changed = NodeCostClassification.WithFunction(source, NodeEconomicFunction.Commercial);
        Assert.AreEqual(NodeEconomicFunction.Commercial, NodeCostClassification.Function(changed));
        Assert.AreEqual(NodeTariffPurpose.Procurement, NodeCostClassification.Purpose(changed));
        CollectionAssert.Contains(changed.ToArray(), "maintenance");

        var general = NodeCostClassification.WithPurpose(changed, NodeTariffPurpose.General);
        Assert.AreEqual(NodeTariffPurpose.General, NodeCostClassification.Purpose(general));
        Assert.AreEqual(NodeEconomicFunction.Commercial, NodeCostClassification.Function(general));
    }

    [TestMethod]
    public void ConflictingOrUnknownReservedTagsDoNotPassValidation()
    {
        Assert.IsFalse(NodeCostClassification.HasValidTags([
            NodeCostClassification.FunctionPrefix + "production",
            NodeCostClassification.FunctionPrefix + "commercial"]));
        Assert.IsFalse(NodeCostClassification.HasValidTags([
            NodeCostClassification.PurposePrefix + "unexpected"]));
        Assert.ThrowsExactly<ArgumentException>(() => NodeCostClassification.WithClassification(
            [], NodeEconomicFunction.Unspecified, NodeTariffPurpose.DirectLabor));
    }
}
