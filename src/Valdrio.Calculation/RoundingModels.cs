using System.Text.Json.Serialization;
using Valdrio.Domain;

namespace Valdrio.Calculation;

[JsonConverter(typeof(StableRoundingIdJsonConverter))]
public readonly record struct StableRoundingId : IComparable<StableRoundingId>
{
    public StableRoundingId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Stable rounding ID cannot contain leading or trailing whitespace.",
                nameof(value));
        }

        Value = value;
    }

    public string Value { get; }
    public void EnsureValid()
    {
        if (string.IsNullOrWhiteSpace(Value) || Value != Value.Trim())
            throw new ArgumentException("ROUNDING_ID_INVALID");
    }

    public int CompareTo(StableRoundingId other) =>
        StringComparer.Ordinal.Compare(Value, other.Value);

    public override string ToString() =>
        Value;
}

public sealed record RoundingContribution(
    StableRoundingId DestinationId,
    decimal RawAmount);

public sealed record RoundedContribution(
    StableRoundingId DestinationId,
    decimal RawAmount,
    decimal NumericAdjustment,
    decimal RoundingAdjustment,
    decimal PostedAmount);

public sealed record BalancedRoundResult
{
    [JsonConstructor]
    public BalancedRoundResult(decimal SourceTotal, decimal Quantum, IReadOnlyList<RoundedContribution> Lines)
    {
        this.SourceTotal = SourceTotal;
        this.Quantum = Quantum;
        this.Lines = FrozenList.Copy(Lines);
    }
    public decimal SourceTotal { get; }
    public decimal Quantum { get; }
    public IReadOnlyList<RoundedContribution> Lines { get; }
    public decimal PostedTotal =>
        Lines.Aggregate(
            0m,
            (total, line) => checked(total + line.PostedAmount));
}

public sealed record WeightedDestination(
    StableRoundingId DestinationId,
    decimal Weight);

public sealed record CurrencyProjectionInputLine(
    StableRoundingId DestinationId,
    decimal LedgerAmount);

public sealed record CurrencyProjectionLine(
    StableRoundingId DestinationId,
    decimal LedgerAmount,
    decimal ProjectedAmount,
    decimal ProjectionAdjustment);

public sealed record CurrencyProjectionResult
{
    [JsonConstructor]
    public CurrencyProjectionResult(decimal SourceLedger, decimal Target, decimal ProjectionDifference,
        decimal Quantum, IReadOnlyList<CurrencyProjectionLine> Lines)
    {
        this.SourceLedger = SourceLedger;
        this.Target = Target;
        this.ProjectionDifference = ProjectionDifference;
        this.Quantum = Quantum;
        this.Lines = FrozenList.Copy(Lines);
    }
    public decimal SourceLedger { get; }
    public decimal Target { get; }
    public decimal ProjectionDifference { get; }
    public decimal Quantum { get; }
    public IReadOnlyList<CurrencyProjectionLine> Lines { get; }
    public decimal ProjectedTotal =>
        Lines.Aggregate(
            0m,
            (total, line) => checked(total + line.ProjectedAmount));
}

public sealed record AllocationAmountDto(
    string DestinationId,
    string RawAmount,
    string NumericAdjustment,
    string RoundingAdjustment,
    string PostedAmount)
{
    public static AllocationAmountDto From(RoundedContribution line) =>
        new(
            line.DestinationId.Value,
            CanonicalDecimal.Format(line.RawAmount),
            CanonicalDecimal.Format(line.NumericAdjustment),
            CanonicalDecimal.Format(line.RoundingAdjustment),
            CanonicalDecimal.Format(line.PostedAmount));
}
