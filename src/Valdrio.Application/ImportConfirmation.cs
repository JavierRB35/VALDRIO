using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Valdrio.Domain;

namespace Valdrio.Application;

public enum ImportConfirmMode { AppendBatch = 0, ReplaceIdentifiedBatch = 1 }
public sealed record ConfirmImport(EntityId CompanyId, EntityId StudyId, long ExpectedRevision,
    EntityId RequestId, string PreparedFingerprint, PreparedImport Prepared, ImportConfirmMode Mode, string Actor,
    EntityId? ReplacesBatchId = null);
public interface IImportWorkspace
{
    PreparedImport PrepareImport(EntityId companyId, EntityId studyId, long expectedRevision,
        EntityId preparedId, ImportSourceDocument source, SourceParseProfileVersion profile, CancellationToken token = default);
    ImportedBatch ConfirmImport(ConfirmImport command, CancellationToken token = default);
    IReadOnlyList<ImportedBatch> ReadImports(EntityId companyId, EntityId studyId, CancellationToken token = default);
}

public sealed record PreparedAmountRow(int RecordNumber, int PhysicalLine, string? Account, Money? Original, bool Selected,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] BusinessDate? ParsedDate = null);
public sealed record ImportReconciliation(int ReadRows, int InvalidRows, int IgnoredRows, int SelectedRows,
    CurrencyCode Currency, decimal PositiveAmount, decimal NegativeAmount, int ZeroRows, decimal NetAmount);

// Computed values are reconstructed from immutable source/profile/context, including when reading canonical JSON.
public sealed class PreparedImport
{
    public const string Schema = "valdrio-prepared-import-v1";
    public PreparedImport(EntityId id, StudyDefinition studyContext, PrecisionPolicy precisionPolicy,
        ImportSourceDocument source, SourceParseProfileVersion profile)
        : this(id, studyContext, precisionPolicy, source, profile, CancellationToken.None) { }
    public static PreparedImport Create(EntityId id, StudyDefinition studyContext, PrecisionPolicy precisionPolicy,
        ImportSourceDocument source, SourceParseProfileVersion profile, CancellationToken token = default) =>
        new(id, studyContext, precisionPolicy, source, profile, token);
    private PreparedImport(EntityId id, StudyDefinition studyContext, PrecisionPolicy precisionPolicy,
        ImportSourceDocument source, SourceParseProfileVersion profile, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        id.EnsureValid(); ArgumentNullException.ThrowIfNull(studyContext); ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(profile); ArgumentNullException.ThrowIfNull(precisionPolicy); precisionPolicy.EnsureValid();
        if (source.Rows.Select(r => r.RecordNumber).Distinct().Count() != source.Rows.Count ||
            source.Rows.Any(r => r.Cells.Any(c => c.Column < 1)) ||
            !source.Rows.Select(r => r.RecordNumber).SequenceEqual(source.Rows.Select(r => r.RecordNumber).Order()))
            throw ImportRules.Error("IMP_SOURCE_INVALID", "Import.InvalidSource");
        Id = id; StudyContext = studyContext; PrecisionPolicy = precisionPolicy; Source = source; Profile = profile;
        var diagnostics = new List<ImportDiagnostic>();
        void Block(string code, string key) => diagnostics.Add(new(new(code, key, []), ImportDiagnosticSeverity.Blocking,
            new(source.FileName, source.SheetName, null, null, null)));
        if ((profile.AmountMode != ImportAmountMode.PeriodAmount && profile.AmountMode != ImportAmountMode.ClosingBalanceAsStudyAmount && profile.Comparison is null) ||
            profile.DuplicatePolicy != ImportDuplicatePolicy.Reject && profile.Grouping is null)
            Block("IMP_MODE_UNAVAILABLE", "Import.ModeUnavailable");
        if (profile.Approval.State != ImportProfileApprovalState.ApprovedBusiness || !profile.SignConventionConfirmed)
            Block("IMP_APPROVAL_REQUIRED", "Import.ApprovalRequired");
        if (profile.PeriodStart != studyContext.PeriodStart || profile.PeriodEnd != studyContext.PeriodEnd)
            Block("IMP_PERIOD_MISMATCH", "Import.PeriodMismatch");
        if (profile.Conversion is null && (profile.FixedCurrency != studyContext.Currency.FunctionalCurrencyCode || profile.CurrencyColumn is not null))
            Block("IMP_FX_REQUIRED", "Import.FxRequired");
        if (studyContext.State != StudyState.Open) Block("STUDY_CLOSED", "Study.ClosedDiagnostic");
        if (profile.Conversion is not null)
        {
            var converted = ImportConversion.Prepare(studyContext, precisionPolicy, source, profile, diagnostics, token);
            Rows = converted.Rows; ConversionBridge = converted.Bridge;
            if (!Rows.Any(r => r.Selected)) Block("IMP_NO_SELECTED_ROWS", "Import.NoSelectedRows");
            Groups = [];
            try
            {
                Groups = FrozenList.Copy(ConversionBridge.Groups.GroupBy(g => g.Code, StringComparer.Ordinal)
                    .Select(g => new PreparedImportGroup(g.Key,
                        new(g.Sum(r => r.Functional.Amount), studyContext.Currency.FunctionalCurrencyCode, precisionPolicy),
                        FrozenList.Copy(g.SelectMany(r => r.Records).Order()))));
            }
            catch (OverflowException) { Block("IMP_TOTAL_OVERFLOW", "Import.TotalOverflow"); }
            catch (DomainRuleException ex) { diagnostics.Add(new(ex.Diagnostic, ImportDiagnosticSeverity.Blocking,
                new(source.FileName, source.SheetName, null, null, null))); }
            var invalidRows = diagnostics.Where(d => d.Severity == ImportDiagnosticSeverity.Blocking && d.Location.RecordNumber.HasValue)
                .Select(d => d.Location.RecordNumber).Distinct().Count();
            decimal positiveTotal = 0m, negativeTotal = 0m;
            try
            {
                positiveTotal = ConversionBridge.Groups.Where(g => g.Functional.Amount > 0).Sum(g => g.Functional.Amount);
                negativeTotal = ConversionBridge.Groups.Where(g => g.Functional.Amount < 0).Sum(g => g.Functional.Amount);
            }
            catch (OverflowException) { Block("IMP_TOTAL_OVERFLOW", "Import.TotalOverflow"); }
            Diagnostics = FrozenList.Copy(diagnostics);
            Reconciliation = new(source.Rows.Count, invalidRows, Rows.Count(r => !r.Selected && r.Original.HasValue),
                Rows.Count(r => r.Selected), studyContext.Currency.FunctionalCurrencyCode, positiveTotal, negativeTotal,
                ConversionBridge.Groups.Count(g => g.Functional.Amount == 0), ConversionBridge.FunctionalTotal);
            return;
        }
        var selectedAccounts = profile.AccountSelection.Accounts.ToHashSet(StringComparer.Ordinal);
        var selectedRecords = profile.Grouping?.Records.Select(r => r.RecordNumber).ToHashSet();
        var rows = new List<PreparedAmountRow>();
        if (profile.SignConvention is ImportSignConvention.SignedAmount or ImportSignConvention.DebitMinusCredit)
        {
            var selection = ImportColumnSelection.Validate(source,
                new(profile.SourceKind, profile.WorksheetName, profile.HeaderRow, profile.FirstDataRow, profile.Csv),
                profile.Columns, profile.NumberFormat, profile.XlsxNumericAccountPolicy, token, profile.DuplicatePolicy);
            diagnostics.AddRange(selection.Diagnostics);
            var blockedRows = selection.Diagnostics.Where(d => d.Severity == ImportDiagnosticSeverity.Blocking)
                .Select(d => d.Location.RecordNumber).ToHashSet();
            foreach (var row in selection.Rows)
            {
                token.ThrowIfCancellationRequested();
                var validAmount = row.Amount.HasValue;
                void Validate(decimal value, int? column, string field)
                {
                    try { precisionPolicy.ValidateAmount(value, field); }
                    catch (DomainRuleException error)
                    { validAmount = false; diagnostics.Add(new(error.Diagnostic, ImportDiagnosticSeverity.Blocking,
                        new(source.FileName, source.SheetName, row.RecordNumber, row.PhysicalLine, column))); }
                }
                // Validate each operand too: cancellation must not hide excessive source precision/magnitude.
                if (row.Debit is { } debit) Validate(debit, profile.Columns.Debit, "amount");
                if (row.Credit is { } credit) Validate(credit, profile.Columns.Credit, "amount");
                if (row.Amount is { } amount) Validate(amount, profile.Columns.Amount, "amount");
                var invalid = !validAmount || blockedRows.Contains(row.RecordNumber);
                var selected = !invalid && row.Amount.HasValue &&
                    (selectedRecords is null || selectedRecords.Contains(row.RecordNumber)) &&
                    (profile.AccountSelection.Kind == ImportAccountSelectionKind.All ||
                     selectedAccounts.Contains(row.Account!)) &&
                    (profile.ZeroPolicy == ImportZeroPolicy.Keep || row.Amount != 0m);
                // Foreign amounts retain their declared currency; they are never silently converted.
                rows.Add(new(row.RecordNumber, row.PhysicalLine, row.Account,
                    validAmount && row.Amount is { } value && profile.FixedCurrency is { } currency ? new(value, currency, precisionPolicy) : null, selected));
            }
        }
        decimal positive = 0m, negative = 0m;
        try
        {
            foreach (var row in rows.Where(r => r.Selected && r.Original.HasValue))
                if (row.Original!.Value.Amount >= 0m) positive = checked(positive + row.Original.Value.Amount);
                else negative = checked(negative + row.Original.Value.Amount);
        }
        catch (OverflowException) { Block("IMP_TOTAL_OVERFLOW", "Import.TotalOverflow"); }
        // Group-level errors must not count a valid selected source row as invalid as well.
        var invalidCount = diagnostics.Where(d => d.Severity == ImportDiagnosticSeverity.Blocking && d.Location.RecordNumber.HasValue)
            .Select(d => d.Location.RecordNumber).Distinct().Count();
        if (profile.Grouping is not null)
            Groups = ImportGrouping.Prepare(source, profile, rows, precisionPolicy, diagnostics, token);
        if (profile.Comparison is { } comparison)
        {
            Groups = comparison.Difference(studyContext, precisionPolicy, profile, Groups!, diagnostics, token, out var details);
            ComparisonRows = details;
            positive = 0m; negative = 0m;
            try
            {
                foreach (var group in Groups)
                    if (group.Amount.Amount >= 0) positive = checked(positive + group.Amount.Amount);
                    else negative = checked(negative + group.Amount.Amount);
            }
            catch (OverflowException) { Block("IMP_TOTAL_OVERFLOW", "Import.TotalOverflow"); }
        }
        var selectedCount = rows.Count(r => r.Selected);
        if (selectedCount == 0) Block("IMP_NO_SELECTED_ROWS", "Import.NoSelectedRows");
        Rows = FrozenList.Copy(rows); Diagnostics = FrozenList.Copy(diagnostics);
        Reconciliation = new(source.Rows.Count, invalidCount, rows.Count - invalidCount - selectedCount,
            selectedCount, profile.FixedCurrency ?? studyContext.Currency.FunctionalCurrencyCode, positive, negative,
            profile.Comparison is null ? rows.Count(r => r.Selected && r.Original?.Amount == 0m) : Groups!.Count(g => g.Amount.Amount == 0m), checked(positive + negative));
    }
    public string SchemaVersion => Profile.Conversion is not null ? "valdrio-prepared-import-v4" : Profile.Comparison is not null ? "valdrio-prepared-import-v3" : Profile.Grouping is null ? Schema : "valdrio-prepared-import-v2";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public ImportConversionBridge? ConversionBridge { get; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public IReadOnlyList<PreparedImportGroup>? Groups { get; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public IReadOnlyList<ImportComparisonLine>? ComparisonRows { get; }
    public EntityId Id { get; }
    public StudyDefinition StudyContext { get; }
    public PrecisionPolicy PrecisionPolicy { get; }
    public ImportSourceDocument Source { get; }
    public SourceParseProfileVersion Profile { get; }
    public IReadOnlyList<PreparedAmountRow> Rows { get; }
    public IReadOnlyList<ImportDiagnostic> Diagnostics { get; }
    public ImportReconciliation Reconciliation { get; }
    public bool CanConfirm => Rows.Any(r => r.Selected) && !Diagnostics.Any(d => d.Severity == ImportDiagnosticSeverity.Blocking);
    [JsonIgnore] public string Fingerprint => PublishedModelStructure.Hash(CanonicalSnapshotJson.Serialize(this));
    [JsonIgnore] public string DuplicateFingerprint => Profile.Conversion is not null ?
        PublishedModelStructure.Hash(CanonicalSnapshotJson.Serialize(new { LegacyDuplicateFingerprint, Profile.Grouping,
            Profile.Conversion.DateColumn, Profile.Conversion.RateSet, Profile.Conversion.Selections,
            Comparison = Profile.Comparison?.DuplicateFingerprint })) : Profile.Comparison is not null ?
        PublishedModelStructure.Hash(CanonicalSnapshotJson.Serialize(new { LegacyDuplicateFingerprint, Profile.Grouping, Comparison = Profile.Comparison.DuplicateFingerprint })) : Profile.Grouping is null ? LegacyDuplicateFingerprint :
        PublishedModelStructure.Hash(CanonicalSnapshotJson.Serialize(new { LegacyDuplicateFingerprint, Profile.Grouping }));
    [JsonIgnore] private string LegacyDuplicateFingerprint => PublishedModelStructure.Hash(CanonicalSnapshotJson.Serialize(new
    {
        Source.FileHash, Profile.SourceKind, Profile.WorksheetName, Profile.HeaderRow, Profile.FirstDataRow,
        Profile.Columns, Profile.Csv, Profile.NumberFormat, Profile.DatePattern, Profile.AmountMode, Profile.SignConvention,
        Profile.PeriodStart, Profile.PeriodEnd, Profile.FixedCurrency, Profile.CurrencyColumn, Profile.DuplicatePolicy,
        Profile.ZeroPolicy, Profile.AccountSelection, Profile.XlsxNumericAccountPolicy
    }));
}

public sealed record ImportedResource(EntityId Id, EntityId BatchId, int RecordNumber, string Account,
    Money Original, Money Functional, BusinessDate PeriodStart, BusinessDate PeriodEnd,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<int>? SourceRecords = null);

public sealed class ImportedBatch
{
    public ImportedBatch(EntityId id, PreparedImport prepared, string preparedFingerprint,
        StudyDefinition confirmedStudy, UtcInstant confirmedAtUtc, string actor, EntityId? replacesBatchId = null)
    {
        id.EnsureValid(); ArgumentNullException.ThrowIfNull(prepared); ArgumentNullException.ThrowIfNull(confirmedStudy);
        if (!prepared.CanConfirm || preparedFingerprint != prepared.Fingerprint || string.IsNullOrWhiteSpace(actor) ||
            !ImportConfirmationRules.IsImportRevision(prepared.StudyContext, confirmedStudy))
            throw ImportRules.Error("IMP_PREPARED_INVALID", "Import.PreparedInvalid");
        Id = id; Prepared = prepared; PreparedFingerprint = preparedFingerprint; ConfirmedStudy = confirmedStudy;
        replacesBatchId?.EnsureValid();
        if (replacesBatchId == id) throw ImportRules.Error("IMP_REPLACEMENT_INVALID", "Import.ReplacementInvalid");
        ReplacesBatchId = replacesBatchId;
        ConfirmedAtUtc = confirmedAtUtc; Actor = actor;
        Resources = prepared.ConversionBridge is { } bridge ? FrozenList.Copy(bridge.Groups.Select(g => new ImportedResource(
            InternationalResourceId(id, g.Code, g.Original.Currency), id, g.Records[0], g.Code, g.Original, g.Functional,
            prepared.Profile.PeriodStart, prepared.Profile.PeriodEnd, g.Records)).OrderBy(r => r.Id)) :
            prepared.Groups is { } groups ? FrozenList.Copy(groups.Select(g => new ImportedResource(
            GroupResourceId(id, g.Code), id, g.Records[0], g.Code, g.Amount, g.Amount,
            prepared.Profile.PeriodStart, prepared.Profile.PeriodEnd, g.Records)).OrderBy(r => r.Id)) :
            FrozenList.Copy(prepared.Rows.Where(r => r.Selected).Select(r => new ImportedResource(
            ResourceId(id, r.RecordNumber), id, r.RecordNumber, r.Account!, r.Original!.Value, r.Original.Value,
            prepared.Profile.PeriodStart, prepared.Profile.PeriodEnd)).OrderBy(r => r.Id));
    }
    public EntityId Id { get; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public EntityId? ReplacesBatchId { get; }
    public PreparedImport Prepared { get; }
    public string PreparedFingerprint { get; }
    public StudyDefinition ConfirmedStudy { get; }
    public UtcInstant ConfirmedAtUtc { get; }
    public string Actor { get; }
    public IReadOnlyList<ImportedResource> Resources { get; }
    private static EntityId GroupResourceId(EntityId batch, string code) =>
        new(new Guid(SHA256.HashData(Encoding.UTF8.GetBytes("valdrio-import-resource-v2|" + batch + "|" + code)).AsSpan(0, 16)));
    private static EntityId InternationalResourceId(EntityId batch, string code, CurrencyCode currency) =>
        new(new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(CanonicalSnapshotJson.Serialize(new { batch, code, currency }))).AsSpan(0, 16)));
    private static EntityId ResourceId(EntityId batch, int record)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("valdrio-import-resource-v1|" + batch + "|" +
            record.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        return new(new Guid(hash.AsSpan(0, 16)));
    }
}

public static class ImportConfirmationRules
{
    // Ledger is append-only. A replacement retires exactly one active batch in the same study.
    public static IReadOnlyList<ImportedBatch> ActiveBatches(IEnumerable<ImportedBatch> batches)
    {
        var active = new Dictionary<EntityId, ImportedBatch>();
        foreach (var batch in batches.OrderBy(b => b.ConfirmedStudy.Revision))
        {
            if (batch.ReplacesBatchId is { } replaced)
            {
                if (!active.TryGetValue(replaced, out var previous) ||
                    previous.ConfirmedStudy.Id != batch.ConfirmedStudy.Id ||
                    previous.ConfirmedStudy.CompanyId != batch.ConfirmedStudy.CompanyId ||
                    previous.ConfirmedStudy.Revision >= batch.ConfirmedStudy.Revision)
                    throw ImportRules.Error("IMP_REPLACEMENT_INVALID", "Import.ReplacementInvalid");
                active.Remove(replaced);
            }
            if (!active.TryAdd(batch.Id, batch)) throw ImportRules.Error("IMP_REPLACEMENT_INVALID", "Import.ReplacementInvalid");
        }
        return FrozenList.Copy(active.Values.OrderBy(b => b.ConfirmedStudy.Revision));
    }
    public static bool IsImportRevision(StudyDefinition before, StudyDefinition after) =>
        before.State == StudyState.Open && after.Revision == before.Revision + 1 &&
        CanonicalSnapshotJson.Serialize(new StudyDefinition(before.Id, before.CompanyId, before.Name, before.PeriodStart,
            before.PeriodEnd, before.Purpose, before.ScenarioKind, null, before.SelectedModelVersionId, before.Currency,
            before.CurrencyValidity, after.Revision, before.State, null, null, before.CreatedAtUtc)) == CanonicalSnapshotJson.Serialize(after);
}
