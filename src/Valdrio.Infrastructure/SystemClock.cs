using Valdrio.Application;
using Valdrio.Domain;

namespace Valdrio.Infrastructure;

public sealed class SystemClock : IClock
{
    public UtcInstant GetUtcNow() =>
        UtcInstant.From(DateTimeOffset.UtcNow);
}
