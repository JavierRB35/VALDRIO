using Valdrio.Application;
using Valdrio.Domain;

namespace Valdrio.Desktop;

internal sealed record SecondaryPresentedContribution(SecondaryAllocationFact Fact,
    EntityId PrimaryNodeId, IReadOnlyList<SecondaryExplanationLink> Links, decimal PostedAmount);

internal static class SecondaryResultPresentation
{
    internal static IReadOnlyList<SecondaryPresentedContribution> Build(
        IReadOnlyList<SecondaryAllocationFact> facts, IReadOnlyList<SecondaryExplanationLink> links,
        IReadOnlyDictionary<EntityId, CalculationNodeDefinition> nodes) =>
        Valdrio.Reporting.SecondaryContributionProjector.Build(facts, links, nodes)
            .Select(row => new SecondaryPresentedContribution(row.Fact, row.PrimaryNodeId,
                row.Links, row.PostedAmount)).ToArray();
}
