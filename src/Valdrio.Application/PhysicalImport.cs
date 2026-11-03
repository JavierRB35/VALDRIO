using System.Globalization;
using Valdrio.Domain;

namespace Valdrio.Application;

// Deliberately not the accounting import model. These rows are physical evidence,
// never monetary ledger entries, redistribution keys or object consumption.
public sealed record PhysicalImportLine(
    int RecordNumber, string NodeCode, EntityId? NodeId, EntityId? PoolId,
    string UnitCode, BusinessDate? PeriodStart, BusinessDate? PeriodEnd,
    decimal? NormalQuantity, decimal? RealQuantity,
    PhysicalEvidence? NormalEvidence, PhysicalEvidence? RealEvidence,
    string? ProblemKey, bool Duplicate, bool PartialPeriod)
{
    public bool CanChoose => ProblemKey is null && !PartialPeriod && PoolId.HasValue &&
                             (NormalQuantity.HasValue || RealQuantity.HasValue);
}
public sealed record PhysicalImportPreview(string FileName, string SourceHash,
    IReadOnlyList<PhysicalImportLine> Lines);

// Distinct decisions are required; defaults in the presentation are Keep.
// Importing records only DRAFT physical versions. Confirmation remains explicit.
public enum PhysicalImportChoice { Keep, Replace }
public sealed record PhysicalImportOperation(SavePhysicalObservation Draft, PhysicalImportChoice Choice);
public sealed record ApplyPhysicalImport(IReadOnlyList<PhysicalImportOperation> Operations);

public static class PhysicalImportMapper
{
    public static PhysicalImportPreview Preview(
        ImportSourceDocument document, ModelStructure model,
        IReadOnlyList<UnitDefinition> units, StudyDefinition study, CultureInfo numericCulture)
    {
        var result = PreviewResult(document, model, units, study, numericCulture);
        return result.Preview ?? throw new DomainRuleException(result.Diagnostic!);
    }
    public static (PhysicalImportPreview? Preview, DomainDiagnostic? Diagnostic) PreviewResult(
        ImportSourceDocument document, ModelStructure model,
        IReadOnlyList<UnitDefinition> units, StudyDefinition study, CultureInfo numericCulture)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(units);
        ArgumentNullException.ThrowIfNull(study);
        ArgumentNullException.ThrowIfNull(numericCulture);
        var map = document.Headers.Select((h, index) => (Header: h.Trim().ToLowerInvariant(), Index: index + 1))
            .GroupBy(x => x.Header).ToDictionary(g => g.Key, g => g.Select(x => x.Index).ToArray());
        foreach (var required in new[] { "node", "unit", "period_start", "period_end" })
            if (!map.TryGetValue(required, out var numbers) || numbers.Length != 1)
                return (null, new("ACT_IMPORT_HEADER", "Activity.ImportHeader", []));
        if (!map.ContainsKey("normal") && !map.ContainsKey("real"))
            return (null, new("ACT_IMPORT_HEADER", "Activity.ImportHeader", []));
        if (map.Any(kv => kv.Value.Length != 1))
            return (null, new("ACT_IMPORT_HEADER", "Activity.ImportHeader", []));

        string Read(SourceRow row, string key) =>
            map.TryGetValue(key, out var values)
                ? row.GetCell(values[0]).TextValue?.Trim() ?? "" : "";

        bool Number(SourceRow row, string key, out decimal? value)
        {
            value = null;
            var cell = map.TryGetValue(key, out var columns) ? row.GetCell(columns[0]) : SourceCell.Missing(0);
            var raw = cell.TextValue?.Trim() ?? "";
            var kind = cell.Kind == SourceValueKind.FormulaWithCachedValue ? cell.CachedValueKind : cell.Kind;
            if (kind is not (SourceValueKind.Missing or SourceValueKind.Empty or SourceValueKind.Text or SourceValueKind.Number))
                return false;
            if (raw.Length == 0) return true;
            // No group separators or scientific notation: lexical and culture are explicit.
            if (!decimal.TryParse(raw, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                document.SourceKind == ImportSourceKind.Xlsx && kind == SourceValueKind.Number
                    ? CultureInfo.InvariantCulture : numericCulture, out var val) ||
                val < 0) return false;
            value = val; return true;
        }
        var nodes = model.Nodes.Where(n => n.Role is NodeRole.Principal or NodeRole.Auxiliary && n.IsActive)
            .GroupBy(n => n.Code, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Single(), StringComparer.Ordinal);
        var pools = model.Pools.ToDictionary(p => p.NodeId);
        var lines = new List<PhysicalImportLine>();
        foreach (var record in document.Rows)
        {
            var code = Read(record, "node");
            if (code.Length == 0 && record.Cells.All(c => string.IsNullOrWhiteSpace(c.TextValue))) continue;
            var unit = Read(record, "unit");
            var issue = (string?)null;
            var validNode = nodes.TryGetValue(code, out var node);
            CostPoolDefinition? pool = null;
            var validPool = validNode && pools.TryGetValue(node!.Id, out pool);
            if (!validNode || !validPool) issue = "Activity.ImportNode";
            else if (!units.Any(u => u.Id == pool!.DriverUnitId && u.Code == unit))
                issue = "Activity.ImportUnit";
            BusinessDate? start = null, end = null;
            if (DateOnly.TryParseExact(Read(record, "period_start"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var startDate) &&
                DateOnly.TryParseExact(Read(record, "period_end"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var endDate))
            { start = new(startDate); end = new(endDate); if (startDate > endDate) issue ??= "Activity.ImportPeriod"; }
            else issue ??= "Activity.ImportPeriod";
            var normalValid = Number(record, "normal", out var normal);
            var realValid = Number(record, "real", out var real);
            if (!normalValid || !realValid) issue ??= "Activity.ImportNumber";
            if (!normal.HasValue && !real.HasValue) issue ??= "Activity.ImportEmpty";

            var partial = start.HasValue && end.HasValue &&
                (start != study.PeriodStart || end != study.PeriodEnd);
            var note = "file="+document.FileName+"; SHA-256="+document.FileHash+"; row="+record.RecordNumber;
            PhysicalEvidence? Evidence(decimal? amount, string source, string quality)
            {
                if (!amount.HasValue) return null;
                var classification = quality.Trim().ToLowerInvariant() switch
                {
                    "recorded" or "registrado" => PhysicalEvidenceQuality.Recorded,
                    "calculated" or "calculado" => PhysicalEvidenceQuality.Calculated,
                    _ => PhysicalEvidenceQuality.Declared
                };
                var identifier = source.Length > 0 ? source + " · " + document.FileName : document.FileName;
                if (identifier.Length > 500 || note.Length > 4000) { issue ??= "Activity.ImportSource"; return null; }
                return new(classification, identifier, note);
            }
            var normalEv = Evidence(normal, Read(record, "normal_source"), Read(record, "normal_quality"));
            var realEv = Evidence(real, Read(record, "real_source"), Read(record, "real_quality"));
            lines.Add(new(record.RecordNumber, code, validNode ? node!.Id : null,
                validPool ? pool!.Id : null, unit, start, end, normal, real, normalEv, realEv,
                issue, false, partial));
        }
        // Flag all duplicates, never summing duplicate totals; the analyst selects at most one.
        var duplicates = lines.Where(x => x.PoolId.HasValue && x.ProblemKey is null)
            .GroupBy(x => x.PoolId!.Value).Where(g => g.Count() > 1)
            .SelectMany(g => g).Select(g => g.RecordNumber).ToHashSet();
        return (new(document.FileName, document.FileHash, FrozenList.Copy(lines
            .Select(line => line with { Duplicate = duplicates.Contains(line.RecordNumber) }))), null);
    }
}
