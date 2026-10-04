namespace Valdrio.Localization;

public enum MessageArgumentType
{
    Text,
    Integer,
    Decimal,
    CurrencyCode,
    Date,
    Instant,
    Identifier
}

public sealed record MessageArgument(
    string Name,
    MessageArgumentType Type,
    object? Value);

public sealed record MessageDescriptor(
    string Code,
    string MessageKey,
    IReadOnlyList<MessageArgument> Arguments);
