namespace Valdrio.Domain;

public enum DiagnosticArgumentType
{
    Text,
    Integer,
    Decimal,
    Identifier,
    CurrencyCode,
    UnitCode
}

public sealed record DiagnosticArgument(
    string Name,
    DiagnosticArgumentType Type,
    string CanonicalValue);

public sealed record DomainDiagnostic(
    string Code,
    string MessageKey,
    IReadOnlyList<DiagnosticArgument> Arguments);

public sealed class DomainRuleException : Exception
{
    public DomainRuleException(DomainDiagnostic diagnostic)
        : base(diagnostic.Code)
    {
        Diagnostic = diagnostic;
    }

    public DomainDiagnostic Diagnostic { get; }
}
