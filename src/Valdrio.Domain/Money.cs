using System.Text.Json.Serialization;

namespace Valdrio.Domain;

public readonly record struct Money
{
    [JsonConstructor]
    public Money(
        decimal amount,
        CurrencyCode currency,
        PrecisionPolicy? precisionPolicy = null)
    {
        if (!currency.IsValid)
        {
            throw InvalidCurrency("currency");
        }

        var policy =
            precisionPolicy ?? PrecisionPolicy.Pdec1Proposed;

        policy.ValidateAmount(amount, nameof(amount));

        Amount = amount;
        Currency = currency;
        PrecisionPolicy = policy;
    }

    public decimal Amount { get; }
    public CurrencyCode Currency { get; }
    public PrecisionPolicy PrecisionPolicy { get; }

    public void EnsureValid(string role)
    {
        if (!Currency.IsValid)
        {
            throw InvalidCurrency(role);
        }

        if (PrecisionPolicy is null)
        {
            throw new DomainRuleException(
                new DomainDiagnostic(
                    "PRECISION_POLICY_MISSING",
                    "Precision.PolicyMissing",
                    [
                        new(
                            "role",
                            DiagnosticArgumentType.Text,
                            role)
                    ]));
        }

        PrecisionPolicy.ValidateAmount(
            Amount,
            role);
    }

    public static Money operator +(Money left, Money right)
    {
        EnsureCompatible(left, right);

        return new Money(
            checked(left.Amount + right.Amount),
            left.Currency,
            left.PrecisionPolicy);
    }

    public static Money operator -(Money left, Money right)
    {
        EnsureCompatible(left, right);

        return new Money(
            checked(left.Amount - right.Amount),
            left.Currency,
            left.PrecisionPolicy);
    }

    private static void EnsureCompatible(
        Money left,
        Money right)
    {
        left.EnsureValid("leftMoney");
        right.EnsureValid("rightMoney");
        EnsureSameCurrency(left, right);

        PrecisionPolicyCompatibility.EnsureSame(
            left.PrecisionPolicy,
            right.PrecisionPolicy,
            "leftMoney",
            "rightMoney");
    }

    private static void EnsureSameCurrency(
        Money left,
        Money right)
    {
        if (left.Currency != right.Currency)
        {
            throw new DomainRuleException(
                new DomainDiagnostic(
                    "MONEY_CURRENCY_MISMATCH",
                    "Money.CurrencyMismatch",
                    [
                        new(
                            "leftCurrency",
                            DiagnosticArgumentType.CurrencyCode,
                            left.Currency.Value),
                        new(
                            "rightCurrency",
                            DiagnosticArgumentType.CurrencyCode,
                            right.Currency.Value)
                    ]));
        }
    }

    private static DomainRuleException InvalidCurrency(
        string role) =>
        new(
            new DomainDiagnostic(
                "MONEY_CURRENCY_INVALID",
                "Money.CurrencyInvalid",
                [
                    new(
                        "role",
                        DiagnosticArgumentType.Text,
                        role)
                ]));
}
