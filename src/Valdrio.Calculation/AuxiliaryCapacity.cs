using Valdrio.Domain;

namespace Valdrio.Calculation;

public sealed record AuxiliaryCapacitySplit(decimal Applied, decimal Variance);
public static class AuxiliaryCapacityCalculator
{
    public static AuxiliaryCapacitySplit Assess(decimal cost, decimal normal, decimal real, PrecisionPolicy precision)
    {
        precision.ValidateAmount(cost, "auxiliary");
        precision.ValidateQuantity(normal, "normal"); precision.ValidateQuantity(real, "real");
        if (normal < 0 || real < 0 || normal == 0 && cost != 0)
            throw new DomainRuleException(new("CAP_ZERO_DENOMINATOR", "Aux.ZeroNormal", []));
        var applied = normal == 0 ? 0 : checked(cost / normal * real);
        var rounded = new BalancedRounder(precision).Round(cost,
            [new(new("service"), applied), new(new("variance"), checked(cost - applied))], precision.InternalLedgerQuantum);
        return new(rounded.Lines.Single(l => l.DestinationId.Value == "service").PostedAmount,
            rounded.Lines.Single(l => l.DestinationId.Value == "variance").PostedAmount);
    }
}
