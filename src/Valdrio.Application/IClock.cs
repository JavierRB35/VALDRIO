using Valdrio.Domain;

namespace Valdrio.Application;

public interface IClock
{
    UtcInstant GetUtcNow();
}
