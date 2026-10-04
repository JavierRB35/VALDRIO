namespace Valdrio.Domain;

public readonly record struct Money
{
    public Money(
        decimal amount,
        CurrencyCode currency,
        PrecisionPolicy? precisionPolicy = null)
    {
        var policy = precisionPolicy ?? PrecisionPolicy.Pdec1Proposed;
        policy.ValidateAmount(amount, nameof(amount));

        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }
    public CurrencyCode Currency { get; }

    public static Money operator +(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(checked(left.Amount + right.Amount), left.Currency);
    }

    public static Money operator -(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(checked(left.Amount - right.Amount), left.Currency);
    }

    private static void EnsureSameCurrency(Money left, Money right)
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
}
