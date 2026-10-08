using Valdrio.Domain;

namespace Valdrio.Application;

/// <summary>Detects repeated selected source cells independently of grouping and FX versions.</summary>
public static class ImportOriginOverlap
{
    public static void EnsureNoOverlap(PreparedImport candidate, IEnumerable<ImportedBatch> active,
        EntityId? replacesBatchId)
    {
        foreach (var batch in active.Where(b => b.Id != replacesBatchId))
        foreach (var left in Selections(candidate))
        foreach (var right in Selections(batch.Prepared))
        {
            if (left.Key != right.Key) continue;
            var repeated = left.Records.Intersect(right.Records).Order().ToArray();
            if (repeated.Length == 0) continue;
            throw ImportRules.Error("IMP_SOURCE_ROWS_REUSED", "Import.SourceRowsReused",
                new("file", DiagnosticArgumentType.Text, left.FileName),
                new("records", DiagnosticArgumentType.Text, string.Join(", ", repeated)));
        }
    }

    private sealed record Selection(string Key, string FileName, IReadOnlyList<int> Records);

    private static IEnumerable<Selection> Selections(PreparedImport prepared)
    {
        yield return Select(prepared.Source, prepared.Profile, false,
            prepared.Rows.Where(r => r.Selected).Select(r => r.RecordNumber));
        if (prepared.Profile.Comparison is not { } comparison) yield break;
        var earlierRecords = prepared.ConversionBridge is { } bridge
            ? bridge.EarlierRows.Select(r => r.RecordNumber)
            : prepared.ComparisonRows?.SelectMany(r => r.EarlierRecords) ?? [];
        yield return Select(comparison.Source, comparison.Profile, true, earlierRecords);
    }

    private static Selection Select(ImportSourceDocument source, SourceParseProfileVersion profile,
        bool earlier, IEnumerable<int> records)
    {
        // Financial column coordinates distinguish separate values on the same physical row.
        // Description, selection, approval text, parse format and FX are not new source values.
        // Current and comparative periods retain their separate signed provenance.
        var key = PublishedModelStructure.Hash(CanonicalSnapshotJson.Serialize(new
        {
            source.FileHash, source.SheetName, profile.SourceKind,
            Csv = profile.Csv is { } csv ? new { csv.EncodingName, csv.Delimiter, csv.Quote } : null,
            profile.Columns.Account, profile.Columns.Amount, profile.Columns.Debit, profile.Columns.Credit,
            profile.PeriodStart, profile.PeriodEnd, Earlier = earlier
        }));
        return new(key, source.FileName, FrozenList.Copy(records.Distinct().Order()));
    }
}
