using Valdrio.Domain;
namespace Valdrio.Application;

public interface ICompositionWorkspace
{
    CompositionCatalog ReadCompositions(CancellationToken token = default);
    CompositionCatalog SaveCompositions(CompositionCatalog proposal, long expectedRevision,
        string actor, CancellationToken token = default);
}
