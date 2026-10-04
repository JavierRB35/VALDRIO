namespace Valdrio.Domain;

public readonly record struct CurrencyCode
{
    public CurrencyCode(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var normalized = value.ToUpperInvariant();

        if (normalized.Length != 3 ||
            normalized.Any(character => character is < 'A' or > 'Z'))
        {
            throw new ArgumentException(
                "Currency code must contain exactly three ASCII letters.",
                nameof(value));
        }

        Value = normalized;
    }

    public string Value { get; }

    public override string ToString() =>
        Value;
}
