using Valdrio.Domain;

namespace Valdrio.Application;

public sealed record ImportRecordTarget(int RecordNumber, string Code);

public sealed class ImportGroupingPlan
{
    public ImportGroupingPlan(IReadOnlyList<ImportRecordTarget> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        if (records.Count == 0 || records.Any(r => r is null || r.RecordNumber < 1 || string.IsNullOrWhiteSpace(r.Code)) ||
            records.Select(r => r.RecordNumber).Distinct().Count() != records.Count)
            throw ImportRules.Error("IMP_GROUPING_INVALID", "Import.GroupingInvalid");
        Records = FrozenList.Copy(records.OrderBy(r => r.RecordNumber));
    }
    public IReadOnlyList<ImportRecordTarget> Records { get; }
}

public sealed record PreparedImportGroup(string Code, Money Amount, IReadOnlyList<int> Records);

internal static class ImportGrouping
{
    internal static IReadOnlyList<PreparedImportGroup> Prepare(ImportSourceDocument source,
        SourceParseProfileVersion profile, IReadOnlyList<PreparedAmountRow> rows, PrecisionPolicy precision,
        List<ImportDiagnostic> diagnostics, CancellationToken token)
    {
        var plan = profile.Grouping!;
        var sourceRows = source.Rows.ToDictionary(r => r.RecordNumber);
        var amounts = rows.Where(r => r.Selected && r.Original.HasValue).ToDictionary(r => r.RecordNumber);
        void Block(string code, string key, int? record = null) => diagnostics.Add(new(new(code, key, []),
            ImportDiagnosticSeverity.Blocking, new(source.FileName, source.SheetName, record,
                record.HasValue && sourceRows.TryGetValue(record.Value, out var row) ? row.PhysicalStartLine : null,
                profile.Columns.Description)));
        if (plan.Records.Any(r => !sourceRows.ContainsKey(r.RecordNumber)))
            Block("IMP_GROUPING_INVALID", "Import.GroupingInvalid");
        var result = new List<PreparedImportGroup>();
        foreach (var group in plan.Records.GroupBy(r => r.Code, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();
            var members = group.OrderBy(r => r.RecordNumber).ToArray();
            // An invalid/missing selected record must never silently disappear from an aggregate.
            if (members.Any(r => !amounts.ContainsKey(r.RecordNumber) || amounts[r.RecordNumber].Account != r.Code))
            { Block("IMP_GROUPING_INVALID", "Import.GroupingInvalid"); continue; }
            try
            {
                var currency = amounts[members[0].RecordNumber].Original!.Value.Currency;
                decimal total = 0m;
                foreach (var member in members)
                {
                    token.ThrowIfCancellationRequested();
                    var money = amounts[member.RecordNumber].Original!.Value;
                    if (money.Currency != currency) throw ImportRules.Error("IMP_FX_REQUIRED", "Import.FxRequired");
                    total = checked(total + money.Amount);
                }
                precision.ValidateAmount(total, "amount");
                result.Add(new(group.Key, new(total, currency, precision),
                    FrozenList.Copy(members.Select(r => r.RecordNumber))));
            }
            catch (OverflowException) { Block("IMP_TOTAL_OVERFLOW", "Import.TotalOverflow", members[0].RecordNumber); }
            catch (DomainRuleException error)
            { diagnostics.Add(new(error.Diagnostic, ImportDiagnosticSeverity.Blocking,
                new(source.FileName, source.SheetName, members[0].RecordNumber, null, null))); }
        }
        return FrozenList.Copy(result);
    }
}
