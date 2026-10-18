using Valdrio.Domain;

namespace Valdrio.Calculation;

public enum RateComponentKind { OwnFixed, Variable, Consumed, Period }
public sealed record RateSource(string Id, ResourceBehavior Behavior, RateComponentKind Kind, decimal Amount, string? Account = null)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public EntityId? CapacityOwnerPoolId { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? AssessmentId { get; init; }
}
public sealed record ActualObjectActivity(string Id, decimal Quantity);
public sealed record RateComponent(RateSource Source, decimal? Denominator, decimal? Rate, string? Diagnostic,
    IReadOnlyList<RoundedContribution> Destinations);
public sealed record PrincipalRateResult(IReadOnlyList<RateComponent> Components, decimal? CombinedRate,
    decimal? CapacityVariance, decimal? PerformedCost, decimal? AppliedCost, decimal? UnassignedCost,
    decimal PeriodCost, decimal SourceCost, bool AssignmentKnown, IReadOnlyList<string> Diagnostics)
{
    public IReadOnlyList<DomainDiagnostic> DiagnosticFacts => Diagnostics.Select(key => new DomainDiagnostic(key switch
    {
        "Rates.ZeroNormal" => "CAP_ZERO_DENOMINATOR",
        "Rates.ZeroDenominator" => "ACT_COST_WITH_ZERO_ACTIVITY",
        "Rates.ExcessConsumption" => "ACT_CONSUMPTION_EXCEEDS_OBSERVED",
        _ => "ACT_MISSING_ACTIVITY"
    }, key, [])).ToArray();
}

public static class PrincipalRateCalculator
{
    public const string Version = "normalized-management-t15-v1";
    public static PrincipalRateResult Calculate(IReadOnlyList<RateSource> sources, decimal? normal, decimal? real,
        IReadOnlyList<ActualObjectActivity>? objects = null, PrecisionPolicy? precision = null)
    {
        precision ??= PrecisionPolicy.Pdec1Proposed;
        foreach (var quantity in new[] { normal, real }.Concat(objects?.Select(o => (decimal?)o.Quantity) ?? []))
            if (quantity.HasValue) precision.ValidateQuantity(quantity.Value, "activity");
        if (normal < 0 || real < 0 || sources.Select(s => s.Id).Distinct().Count() != sources.Count ||
            sources.Any(s => string.IsNullOrWhiteSpace(s.Id) || !Enum.IsDefined(s.Kind) || !Enum.IsDefined(s.Behavior)) ||
            objects?.Any(o => o.Quantity < 0 || string.IsNullOrWhiteSpace(o.Id)) == true ||
            objects is not null && objects.Select(o => o.Id).Distinct().Count() != objects.Count)
            throw new ArgumentException("Invalid rate inputs");
        var assigned = objects?.Sum(o => o.Quantity);
        if (assigned > real) return new([], null, null, null, null, null, 0, sources.Sum(s => s.Amount), true, ["Rates.ExcessConsumption"]);
        var components = new List<RateComponent>();
        foreach (var source in sources)
        {
            precision.ValidateAmount(source.Amount, source.Id);
            var denominator = source.Kind == RateComponentKind.OwnFixed ? normal : real;
            if (source.Kind == RateComponentKind.Period)
            {
                components.Add(new(source, null, null, null, [new(new("period"), source.Amount, 0, 0, source.Amount)]));
                continue;
            }
            var error = denominator is null ? "Rates.MissingActivity" :
                denominator == 0 && source.Amount != 0 ? source.Kind == RateComponentKind.OwnFixed ? "Rates.ZeroNormal" : "Rates.ZeroDenominator" : null;
            decimal? rate = error is null && denominator > 0 ? source.Amount / denominator : null;
            if (error is not null || real is null)
            {
                components.Add(new(source, denominator, rate, error ?? "Rates.MissingActivity", []));
                continue;
            }
            var raw = new List<RoundingContribution>();
            var performed = source.Kind == RateComponentKind.OwnFixed ? (rate ?? 0m) * real.Value : source.Amount;
            raw.Add(new(new("capacity"), source.Amount - performed));
            if (objects is null) raw.Add(new(new("performed"), performed));
            else
            {
                foreach (var obj in objects) raw.Add(new(new("object:" + obj.Id), (rate ?? 0m) * obj.Quantity));
                raw.Add(new(new("unassigned"), performed - raw.Where(x => x.DestinationId.Value.StartsWith("object:", StringComparison.Ordinal)).Sum(x => x.RawAmount)));
            }
            var rounded = new BalancedRounder(precision).Round(source.Amount, raw, precision.InternalLedgerQuantum);
            components.Add(new(source, denominator, rate, null, rounded.Lines));
        }
        var diagnostics = components.Where(c => c.Diagnostic is not null).Select(c => c.Diagnostic!).Distinct().ToArray();
        bool valid = diagnostics.Length == 0;
        decimal Sum(string destination) => components.SelectMany(c => c.Destinations)
            .Where(d => d.DestinationId.Value == destination).Sum(d => d.PostedAmount);
        var applied = components.SelectMany(c => c.Destinations).Where(d => d.DestinationId.Value.StartsWith("object:", StringComparison.Ordinal)).Sum(d => d.PostedAmount);
        var rates = components.Where(c => c.Source.Kind != RateComponentKind.Period).ToArray();
        return new(FrozenList.Copy(components), rates.Length > 0 && rates.All(c => c.Rate.HasValue) ? rates.Sum(c => c.Rate!.Value) : null,
            valid ? Sum("capacity") : null, valid ? objects is null ? Sum("performed") : applied + Sum("unassigned") : null,
            valid && objects is not null ? applied : null, valid && objects is not null ? Sum("unassigned") : null,
            Sum("period"), sources.Sum(s => s.Amount), objects is not null, diagnostics);
    }
}
