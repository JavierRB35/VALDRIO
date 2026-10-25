using System.Globalization;
using System.IO;
using System.Text;
using Valdrio.Domain;

namespace Valdrio.Desktop;

public sealed record ProductCostImportRow(string Reference, string Name, string Unit, decimal UnitCost, int Record);

internal static class ProductCostImport
{
    public static IReadOnlyList<ProductCostImportRow> Read(string path, CultureInfo culture)
    {
        var source = File.ReadAllText(path, Encoding.UTF8).TrimStart('\uFEFF');
        var delimiter = DetectDelimiter(source);
        var records = Parse(source, delimiter).Where(row => row.Any(cell => !string.IsNullOrWhiteSpace(cell))).ToArray();
        if (records.Length < 2) throw new InvalidDataException("Empty");
        var header = records[0].Select(Normalize).ToArray();
        var reference = Find("referencia", "reference", "ref", "code", "codigo", "sku");
        var name = Find("nombre", "name", "description");
        var unit = Find("unidad de medida", "unit of measure", "unit", "unidad", "uom", "symbol");
        var cost = Find("coste unitario", "costo unitario", "unit cost", "unitcost", "coste", "costo");
        if (new[] { reference, name, unit, cost }.Distinct().Count() != 4) throw new InvalidDataException("Headers");
        var output = new List<ProductCostImportRow>();
        var refs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 1; i < records.Length; i++)
        {
            var row = records[i];
            if (row.Count <= Math.Max(Math.Max(reference, name), Math.Max(unit, cost))) throw new InvalidDataException("Columns:" + (i + 1));
            var refValue = row[reference].Trim(); var nameValue = row[name].Trim(); var unitValue = row[unit].Trim();
            if (refValue.Length == 0 || nameValue.Length == 0 || unitValue.Length == 0 || !refs.Add(refValue))
                throw new InvalidDataException("Data:" + (i + 1));
            var costText = row[cost].Trim();
            if (!decimal.TryParse(costText, NumberStyles.Number, culture, out var unitCost) &&
                !decimal.TryParse(costText, NumberStyles.Number, CultureInfo.InvariantCulture, out unitCost) || unitCost < 0)
                throw new InvalidDataException("Cost:" + (i + 1));
            output.Add(new(refValue, nameValue, unitValue, unitCost, i + 1));
        }
        if (output.Count == 0) throw new InvalidDataException("Empty");
        return output;

        int Find(params string[] aliases)
        {
            var matches = header.Select((value, index) => (value, index)).Where(x => aliases.Contains(x.value, StringComparer.Ordinal)).ToArray();
            if (matches.Length != 1) throw new InvalidDataException("Headers");
            return matches[0].index;
        }
    }

    public static IReadOnlyList<CompositionItem> Merge(IReadOnlyList<CompositionItem> current,
        IReadOnlyList<ProductCostImportRow> imported, IReadOnlyList<UnitDefinition> units,
        CompositionItemKind newItemKind, string source)
    {
        if (newItemKind is not (CompositionItemKind.Product or CompositionItemKind.Material or CompositionItemKind.Service) ||
            string.IsNullOrWhiteSpace(source)) throw new InvalidDataException("ImportContext");
        var byReference = new Dictionary<string, CompositionItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in current)
            if (!byReference.TryAdd(item.Code, item)) throw new InvalidDataException("ReferenceAmbiguous");
        var incomingReferences = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var prepared = new List<(ProductCostImportRow Row, UnitId Unit, CompositionItem? Existing)>();
        foreach (var record in imported)
        {
            if (!incomingReferences.Add(record.Reference)) throw new InvalidDataException("Data:" + record.Record);
            var matchingUnits = units.Where(u => string.Equals(u.Code, record.Unit, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(u.Symbol, record.Unit, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matchingUnits.Length != 1) throw new InvalidDataException("Unit:" + record.Record);
            byReference.TryGetValue(record.Reference, out var existing);
            if (existing is not null && existing.UnitId != matchingUnits[0].Id) throw new InvalidDataException("UnitMismatch:" + record.Record);
            prepared.Add((record, matchingUnits[0].Id, existing));
        }
        var updated = prepared.Where(x => x.Existing is not null).ToDictionary(x => x.Existing!.Id,
            x => x.Existing! with { Name = x.Row.Name, ReferenceUnitCost = x.Row.UnitCost, ReferenceCostSource = source });
        var additions = prepared.Where(x => x.Existing is null).Select(x => new CompositionItem(new(Guid.NewGuid()),
            x.Row.Reference, x.Row.Name, x.Unit, newItemKind, true)
        { ReferenceUnitCost = x.Row.UnitCost, ReferenceCostSource = source });
        return current.Select(item => updated.GetValueOrDefault(item.Id, item)).Concat(additions).ToArray();
    }

    private static string Normalize(string value)
    {
        var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var result = new StringBuilder();
        foreach (var ch in decomposed) if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) result.Append(ch);
        return string.Join(" ", result.ToString().Normalize(NormalizationForm.FormC).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static char DetectDelimiter(string source)
    {
        var first = source.Split('\n', 2)[0];
        var candidates = new[] { '\t', ';', ',' };
        return candidates.Select((delimiter, order) => (delimiter, order, count: Count(first, delimiter)))
            .OrderByDescending(x => x.count).ThenBy(x => x.order).First().delimiter;
    }
    private static int Count(string value, char delimiter)
    {
        var quoted = false; var count = 0;
        for (var i = 0; i < value.Length; i++)
            if (value[i] == '"' && quoted && i + 1 < value.Length && value[i + 1] == '"') i++;
            else if (value[i] == '"') quoted = !quoted;
            else if (!quoted && value[i] == delimiter) count++;
        return count;
    }
    private static IReadOnlyList<IReadOnlyList<string>> Parse(string source, char delimiter)
    {
        var rows = new List<IReadOnlyList<string>>(); var row = new List<string>(); var cell = new StringBuilder(); var quoted = false;
        for (var i = 0; i < source.Length; i++)
        {
            var ch = source[i];
            if (ch == '"' && quoted && i + 1 < source.Length && source[i + 1] == '"') { cell.Append('"'); i++; }
            else if (ch == '"') quoted = !quoted;
            else if (!quoted && ch == delimiter) { row.Add(cell.ToString()); cell.Clear(); }
            else if (!quoted && (ch == '\r' || ch == '\n'))
            {
                if (ch == '\r' && i + 1 < source.Length && source[i + 1] == '\n') i++;
                row.Add(cell.ToString()); cell.Clear(); rows.Add(row); row = [];
            }
            else cell.Append(ch);
        }
        if (quoted) throw new InvalidDataException("Quotes");
        if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add(row); }
        return rows;
    }
}
