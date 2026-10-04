using System.Globalization;
using Valdrio.Domain;

namespace Valdrio.Localization;

public sealed record LocalizedDiagnosticArgument(
    string Name,
    DiagnosticArgumentType Type,
    string DisplayValue);

public sealed record LocalizedDiagnostic(
    string Code,
    string MessageKey,
    string Message,
    IReadOnlyList<LocalizedDiagnosticArgument> Arguments);

public sealed class DomainDiagnosticFormatter
{
    private readonly UiTextCatalog texts;

    public DomainDiagnosticFormatter(UiTextCatalog texts)
    {
        this.texts = texts;
    }

    public LocalizedDiagnostic Format(
        DomainDiagnostic diagnostic,
        CultureInfo uiCulture,
        CultureInfo formatCulture)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        ArgumentNullException.ThrowIfNull(uiCulture);
        ArgumentNullException.ThrowIfNull(formatCulture);

        var arguments = diagnostic.Arguments
            .Select(argument =>
                new LocalizedDiagnosticArgument(
                    argument.Name,
                    argument.Type,
                    FormatArgument(
                        argument,
                        formatCulture)))
            .ToArray();

        var message = string.Format(
            uiCulture,
            texts.Get(
                diagnostic.MessageKey,
                uiCulture),
            arguments
                .Select(argument => (object?)argument.DisplayValue)
                .ToArray());

        return new LocalizedDiagnostic(
            diagnostic.Code,
            diagnostic.MessageKey,
            message,
            arguments);
    }

    private static string FormatArgument(
        DiagnosticArgument argument,
        CultureInfo formatCulture) =>
        argument.Type switch
        {
            DiagnosticArgumentType.Integer =>
                long.Parse(
                        argument.CanonicalValue,
                        NumberStyles.AllowLeadingSign,
                        CultureInfo.InvariantCulture)
                    .ToString("N0", formatCulture),

            DiagnosticArgumentType.Decimal =>
                CanonicalDecimal.Parse(
                        argument.CanonicalValue)
                    .ToString(
                        "0.############################",
                        formatCulture),

            _ => argument.CanonicalValue
        };
}
