using System.Globalization;
using System.Text.Json;
using Valdrio.Application;
using Valdrio.Domain;

namespace Valdrio.Infrastructure;

public sealed class JsonCurrencyCatalog : ICurrencyCatalog
{
    private readonly IReadOnlyDictionary<CurrencyCode, CurrencyDefinition> definitions;

    public JsonCurrencyCatalog(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var document = JsonDocument.Parse(
            File.ReadAllText(path));

        var root = document.RootElement;

        Source = RequiredString(root, "source");
        SourcePublicationDate = DateOnly.ParseExact(
            RequiredString(root, "source_publication"),
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture);
        CheckedOn = DateOnly.ParseExact(
            RequiredString(root, "checked_on"),
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture);

        CatalogVersion =
            $"fixture-six-list-one-{SourcePublicationDate:yyyy-MM-dd}";

        SourceUrl = RequiredString(root, "source_url");
        Scope = RequiredString(root, "scope");

        definitions = root.GetProperty("currencies")
            .EnumerateArray()
            .Select(item =>
            {
                var definition = new CurrencyDefinition(
                    new CurrencyCode(RequiredString(item, "code")),
                    RequiredString(item, "numeric"),
                    item.GetProperty("minor_unit_digits").GetInt32(),
                    isMonetary: true,
                    new CurrencyValidity(null, null),
                    CatalogVersion,
                    Source,
                    SourcePublicationDate,
                    CheckedOn);

                return KeyValuePair.Create(
                    definition.AlphaCode,
                    definition);
            })
            .ToDictionary(
                pair => pair.Key,
                pair => pair.Value);
    }

    public string CatalogVersion { get; }
    public string Source { get; }
    public DateOnly SourcePublicationDate { get; }
    public DateOnly CheckedOn { get; }
    public string SourceUrl { get; }
    public string Scope { get; }

    public IReadOnlyList<CurrencyDefinition> GetAll() =>
        definitions.Values
            .OrderBy(
                definition => definition.AlphaCode.Value,
                StringComparer.Ordinal)
            .ToArray();

    public CurrencyDefinition GetRequired(
        CurrencyCode currencyCode) =>
        definitions.TryGetValue(currencyCode, out var definition)
            ? definition
            : throw new DomainRuleException(
                new DomainDiagnostic(
                    "CURRENCY_NOT_IN_CATALOG",
                    "Currency.NotInCatalog",
                    [
                        new(
                            "currency",
                            DiagnosticArgumentType.CurrencyCode,
                            currencyCode.Value),
                        new(
                            "catalogVersion",
                            DiagnosticArgumentType.Text,
                            CatalogVersion)
                    ]));

    private static string RequiredString(
        JsonElement element,
        string propertyName) =>
        element.GetProperty(propertyName).GetString()
        ?? throw new InvalidDataException(
            $"Currency catalog property '{propertyName}' is null.");
}
