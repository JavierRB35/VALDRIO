using Microsoft.VisualStudio.TestTools.UnitTesting;
using Valdrio.Domain;

namespace Valdrio.Tests;

[TestClass]
public sealed class ProcessRouteGraphTests
{
    private static EntityId Id(int value) =>
        new(Guid.Parse($"00000000-0000-4000-8000-{value:000000000000}"));

    private static CalculationNodeDefinition Node(int id, NodeRole role, NodeEconomicFunction function) =>
        new(Id(id), Id(80), Id(81), "N" + id, role,
            NodeCostClassification.WithFunction([], function), true);

    [TestMethod]
    public void OperationalSequencesRemainDistinctFromAuxiliaryServiceProposals()
    {
        var cut = Node(1, NodeRole.Principal, NodeEconomicFunction.Production);
        var assembly = Node(2, NodeRole.Principal, NodeEconomicFunction.Production);
        var maintenance = Node(3, NodeRole.Auxiliary, NodeEconomicFunction.Structure);
        cut = ProcessRouteGraph.WithLink(cut,
            new(cut.Id, assembly.Id, ProcessRouteKind.Sequence), true);
        maintenance = ProcessRouteGraph.WithLink(maintenance,
            new(maintenance.Id, cut.Id, ProcessRouteKind.AuxiliaryService), true);
        maintenance = ProcessRouteGraph.WithLink(maintenance,
            new(maintenance.Id, assembly.Id, ProcessRouteKind.AuxiliaryService), true);
        // The reciprocal solver, not the process-route validator, evaluates
        // whether auxiliary services (including self service) are resolvable.
        maintenance = ProcessRouteGraph.WithLink(maintenance,
            new(maintenance.Id, maintenance.Id, ProcessRouteKind.AuxiliaryService), true);

        var nodes = new[] { cut, assembly, maintenance };
        var links = ProcessRouteGraph.Read(nodes);
        Assert.AreEqual(1, links.Count(l => l.Kind == ProcessRouteKind.Sequence));
        Assert.AreEqual(3, links.Count(l => l.Kind == ProcessRouteKind.AuxiliaryService));
        Assert.IsTrue(ProcessRouteGraph.Check(nodes).IsValid);
        var validated = ProcessRouteGraph.MarkValidated(nodes);
        Assert.IsTrue(ProcessRouteGraph.IsValidated(validated));
        Assert.AreEqual(3, validated.Count);

        var changed = validated.ToArray();
        changed[0] = ProcessRouteGraph.WithLink(changed[0],
            new(cut.Id, assembly.Id, ProcessRouteKind.Sequence), false);
        Assert.IsFalse(ProcessRouteGraph.IsValidated(changed));
    }

    [TestMethod]
    public void DestinationCheckboxEligibilityAllowsOnlyAuxiliarySelfServiceAndAcyclicSequences()
    {
        var cutting = Node(31, NodeRole.Principal, NodeEconomicFunction.Production);
        var assembly = Node(32, NodeRole.Principal, NodeEconomicFunction.Production);
        var finishing = Node(33, NodeRole.Principal, NodeEconomicFunction.Commercial);
        var services = Node(34, NodeRole.Auxiliary, NodeEconomicFunction.Structure);
        var nodes = new[] { cutting, assembly, finishing, services };

        Assert.AreEqual(ProcessRouteRestriction.PrincipalSelfLoop,
            ProcessRouteGraph.Restriction(nodes,
                new(cutting.Id, cutting.Id, ProcessRouteKind.Sequence)));
        Assert.AreEqual(ProcessRouteRestriction.SequenceNeedsPrincipals,
            ProcessRouteGraph.Restriction(nodes,
                new(cutting.Id, services.Id, ProcessRouteKind.Sequence)));
        Assert.AreEqual(ProcessRouteRestriction.None,
            ProcessRouteGraph.Restriction(nodes,
                new(services.Id, services.Id, ProcessRouteKind.AuxiliaryService)),
            "Self service from an auxiliary is permitted.");
        Assert.AreEqual(ProcessRouteRestriction.None,
            ProcessRouteGraph.Restriction(nodes,
                new(services.Id, cutting.Id, ProcessRouteKind.AuxiliaryService)),
            "A structure auxiliary may service production.");
        Assert.AreEqual(ProcessRouteRestriction.ServiceNeedsAuxiliary,
            ProcessRouteGraph.Restriction(nodes,
                new(cutting.Id, assembly.Id, ProcessRouteKind.AuxiliaryService)));
        Assert.AreEqual(ProcessRouteRestriction.None,
            ProcessRouteGraph.Restriction(nodes,
                new(cutting.Id, assembly.Id, ProcessRouteKind.Sequence)));

        cutting = ProcessRouteGraph.WithLink(cutting,
            new(cutting.Id, assembly.Id, ProcessRouteKind.Sequence), true);
        assembly = ProcessRouteGraph.WithLink(assembly,
            new(assembly.Id, finishing.Id, ProcessRouteKind.Sequence), true);
        nodes = [cutting, assembly, finishing, services];
        Assert.AreEqual(ProcessRouteRestriction.SequenceCycle,
            ProcessRouteGraph.Restriction(nodes,
                new(finishing.Id, cutting.Id, ProcessRouteKind.Sequence)),
            "The destination panel must not enable a production cycle.");
        Assert.AreEqual(ProcessRouteRestriction.None,
            ProcessRouteGraph.Restriction(nodes,
                new(services.Id, finishing.Id, ProcessRouteKind.AuxiliaryService)));
    }

    [TestMethod]
    public void CycleDetectionIsLimitedToOperationalRoutes()
    {
        var first = Node(5, NodeRole.Principal, NodeEconomicFunction.Production);
        var second = Node(6, NodeRole.Principal, NodeEconomicFunction.Production);
        first = ProcessRouteGraph.WithLink(first, new(first.Id, second.Id, ProcessRouteKind.Sequence), true);
        second = ProcessRouteGraph.WithLink(second, new(second.Id, first.Id, ProcessRouteKind.Sequence), true);
        var issues = ProcessRouteGraph.Check([first, second]);
        Assert.IsFalse(issues.IsValid);
        Assert.IsTrue(issues.Errors.Any(x => x.Contains("cycle", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void CustomPositionsAndCloneMappingPreserveConnectionsWithoutReusingValidation()
    {
        var source = Node(11, NodeRole.Auxiliary, NodeEconomicFunction.Structure);
        var destination = Node(12, NodeRole.Principal, NodeEconomicFunction.Commercial);
        source = ProcessRouteGraph.WithLink(source,
            new(source.Id, destination.Id, ProcessRouteKind.AuxiliaryService), true);
        source = ProcessRouteGraph.WithPosition(source, new(123, 456));
        // The editable node form uses commas between function tags.
        Assert.IsFalse(source.FunctionTags.Any(tag => tag.StartsWith(ProcessRouteGraph.PositionPrefix)
            && tag.Contains(',')));
        Assert.AreEqual(new ProcessRoutePoint(123, 456), ProcessRouteGraph.ReadPosition(source));

        var validated = ProcessRouteGraph.MarkValidated([source, destination]);
        var remap = new Dictionary<EntityId, EntityId>
        {
            [source.Id] = Id(13),
            [destination.Id] = Id(14)
        };
        var copiedTags = ProcessRouteGraph.RemapTags(validated[0].FunctionTags, remap);
        Assert.IsFalse(copiedTags.Any(t => t.StartsWith(ProcessRouteGraph.ValidationPrefix)));
        var clone = new CalculationNodeDefinition(Id(13), Id(90), Id(91),
            source.Code, source.Role, copiedTags, true);
        Assert.AreEqual(Id(14), ProcessRouteGraph.Read([clone])
            .Single().Destination);
        Assert.AreEqual(new ProcessRoutePoint(123, 456), ProcessRouteGraph.ReadPosition(clone));
    }

    [TestMethod]
    public void MissingEndpointsAndMisclassifiedSourcesCannotBeValidated()
    {
        var principal = Node(21, NodeRole.Principal, NodeEconomicFunction.Production);
        principal = ProcessRouteGraph.WithLink(principal,
            new(principal.Id, Id(25), ProcessRouteKind.AuxiliaryService), true);
        var check = ProcessRouteGraph.Check([principal]);
        Assert.IsFalse(check.IsValid);
        Assert.IsTrue(check.Errors.Count > 0);
        Assert.IsFalse(ProcessRouteGraph.IsValidated([principal]));
    }
}
