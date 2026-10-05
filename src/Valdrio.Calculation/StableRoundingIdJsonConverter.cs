using Valdrio.Domain;

namespace Valdrio.Calculation;

public sealed class StableRoundingIdJsonConverter : CanonicalStringJsonConverter<StableRoundingId>
{
    protected override StableRoundingId Parse(string text) => new(text);
    protected override string Format(StableRoundingId value) { value.EnsureValid(); return value.Value; }
}
