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

public sealed record DomainDiagnostic
{
    public DomainDiagnostic(string Code, string MessageKey, IReadOnlyList<DiagnosticArgument> Arguments)
    {
        this.Code = Code;
        this.MessageKey = MessageKey;
        this.Arguments = FrozenList.Copy(Arguments);
    }
    public string Code { get; }
    public string MessageKey { get; }
    public IReadOnlyList<DiagnosticArgument> Arguments { get; }
}

public sealed class DomainRuleException : Exception
{
    public DomainRuleException(DomainDiagnostic diagnostic)
        : base(diagnostic.Code)
    {
        Diagnostic = diagnostic;
    }

    public DomainDiagnostic Diagnostic { get; }
}
