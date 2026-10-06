using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Valdrio.Domain;

namespace Valdrio.Application;

public enum ImportConfirmMode { AppendBatch = 0 }
public sealed record ConfirmImport(EntityId CompanyId, EntityId StudyId, long ExpectedRevision,
    EntityId RequestId, string PreparedFingerprint, PreparedImport Prepared, ImportConfirmMode Mode, string Actor);
public interface IImportWorkspace
{
    PreparedImport PrepareImport(EntityId companyId, EntityId studyId, long expectedRevision,
        EntityId preparedId, ImportSourceDocument source, SourceParseProfileVersion profile, CancellationToken token = default);
    ImportedBatch ConfirmImport(ConfirmImport command, CancellationToken token = default);
    IReadOnlyList<ImportedBatch> ReadImports(EntityId companyId, EntityId studyId, CancellationToken token = default);
}

public sealed record PreparedAmountRow(int RecordNumber, int PhysicalLine, string? Account, Money? Original, bool Selected);
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
        if (profile.AmountMode != ImportAmountMode.PeriodAmount || profile.SignConvention != ImportSignConvention.SignedAmount ||
            profile.DuplicatePolicy != ImportDuplicatePolicy.Reject)
            Block("IMP_MODE_UNAVAILABLE", "Import.ModeUnavailable");
        if (profile.Approval.State != ImportProfileApprovalState.ApprovedBusiness || !profile.SignConventionConfirmed)
            Block("IMP_APPROVAL_REQUIRED", "Import.ApprovalRequired");
        if (profile.PeriodStart != studyContext.PeriodStart || profile.PeriodEnd != studyContext.PeriodEnd)
            Block("IMP_PERIOD_MISMATCH", "Import.PeriodMismatch");
        if (profile.FixedCurrency != studyContext.Currency.FunctionalCurrencyCode || profile.CurrencyColumn is not null)
            Block("IMP_FX_REQUIRED", "Import.FxRequired");
        if (studyContext.State != StudyState.Open) Block("STUDY_CLOSED", "Study.ClosedDiagnostic");
        var rows = new List<PreparedAmountRow>();
        if (profile.SignConvention == ImportSignConvention.SignedAmount)
        {
            var selection = ImportColumnSelection.Validate(source,
                new(profile.SourceKind, profile.WorksheetName, profile.HeaderRow, profile.FirstDataRow, profile.Csv),
                profile.Columns, profile.NumberFormat, profile.XlsxNumericAccountPolicy, token);
            diagnostics.AddRange(selection.Diagnostics);
            var blockedRows = selection.Diagnostics.Where(d => d.Severity == ImportDiagnosticSeverity.Blocking)
                .Select(d => d.Location.RecordNumber).ToHashSet();
            foreach (var row in selection.Rows)
            {
                token.ThrowIfCancellationRequested();
                var validAmount = row.Amount.HasValue;
                if (row.Amount is { } amount)
                {
                    try { precisionPolicy.ValidateAmount(amount, "amount"); }
                    catch (DomainRuleException error)
                    { validAmount = false; diagnostics.Add(new(error.Diagnostic, ImportDiagnosticSeverity.Blocking,
                        new(source.FileName, source.SheetName, row.RecordNumber, row.PhysicalLine, profile.Columns.Amount))); }
                }
                var invalid = !validAmount || blockedRows.Contains(row.RecordNumber);
                var selected = !invalid && row.Amount.HasValue &&
                    (profile.AccountSelection.Kind == ImportAccountSelectionKind.All ||
                     profile.AccountSelection.Accounts.Contains(row.Account!, StringComparer.Ordinal)) &&
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
        var selectedCount = rows.Count(r => r.Selected);
        if (selectedCount == 0) Block("IMP_NO_SELECTED_ROWS", "Import.NoSelectedRows");
        var invalidCount = diagnostics.Where(d => d.Severity == ImportDiagnosticSeverity.Blocking && d.Location.RecordNumber.HasValue)
            .Select(d => d.Location.RecordNumber).Distinct().Count();
        Rows = FrozenList.Copy(rows); Diagnostics = FrozenList.Copy(diagnostics);
        Reconciliation = new(source.Rows.Count, invalidCount, rows.Count - invalidCount - selectedCount,
            selectedCount, profile.FixedCurrency ?? studyContext.Currency.FunctionalCurrencyCode, positive, negative,
            rows.Count(r => r.Selected && r.Original?.Amount == 0m), checked(positive + negative));
    }
    public string SchemaVersion => Schema;
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
    [JsonIgnore] public string DuplicateFingerprint => PublishedModelStructure.Hash(CanonicalSnapshotJson.Serialize(new
    {
        Source.FileHash, Profile.SourceKind, Profile.WorksheetName, Profile.HeaderRow, Profile.FirstDataRow,
        Profile.Columns, Profile.Csv, Profile.NumberFormat, Profile.DatePattern, Profile.AmountMode, Profile.SignConvention,
        Profile.PeriodStart, Profile.PeriodEnd, Profile.FixedCurrency, Profile.CurrencyColumn, Profile.DuplicatePolicy,
        Profile.ZeroPolicy, Profile.AccountSelection, Profile.XlsxNumericAccountPolicy
    }));
}

public sealed record ImportedResource(EntityId Id, EntityId BatchId, int RecordNumber, string Account,
    Money Original, Money Functional, BusinessDate PeriodStart, BusinessDate PeriodEnd);

public sealed class ImportedBatch
{
    public ImportedBatch(EntityId id, PreparedImport prepared, string preparedFingerprint,
        StudyDefinition confirmedStudy, UtcInstant confirmedAtUtc, string actor)
    {
        id.EnsureValid(); ArgumentNullException.ThrowIfNull(prepared); ArgumentNullException.ThrowIfNull(confirmedStudy);
        if (!prepared.CanConfirm || preparedFingerprint != prepared.Fingerprint || string.IsNullOrWhiteSpace(actor) ||
            !ImportConfirmationRules.IsImportRevision(prepared.StudyContext, confirmedStudy))
            throw ImportRules.Error("IMP_PREPARED_INVALID", "Import.PreparedInvalid");
        Id = id; Prepared = prepared; PreparedFingerprint = preparedFingerprint; ConfirmedStudy = confirmedStudy;
        ConfirmedAtUtc = confirmedAtUtc; Actor = actor;
        Resources = FrozenList.Copy(prepared.Rows.Where(r => r.Selected).Select(r => new ImportedResource(
            ResourceId(id, r.RecordNumber), id, r.RecordNumber, r.Account!, r.Original!.Value, r.Original.Value,
            prepared.Profile.PeriodStart, prepared.Profile.PeriodEnd)).OrderBy(r => r.Id));
    }
    public EntityId Id { get; }
    public PreparedImport Prepared { get; }
    public string PreparedFingerprint { get; }
    public StudyDefinition ConfirmedStudy { get; }
    public UtcInstant ConfirmedAtUtc { get; }
    public string Actor { get; }
    public IReadOnlyList<ImportedResource> Resources { get; }
    private static EntityId ResourceId(EntityId batch, int record)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("valdrio-import-resource-v1|" + batch + "|" +
            record.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        return new(new Guid(hash.AsSpan(0, 16)));
    }
}

public static class ImportConfirmationRules
{
    public static bool IsImportRevision(StudyDefinition before, StudyDefinition after) =>
        before.State == StudyState.Open && after.Revision == before.Revision + 1 &&
        CanonicalSnapshotJson.Serialize(new StudyDefinition(before.Id, before.CompanyId, before.Name, before.PeriodStart,
            before.PeriodEnd, before.Purpose, before.ScenarioKind, null, before.SelectedModelVersionId, before.Currency,
            before.CurrencyValidity, after.Revision, before.State, null, null, before.CreatedAtUtc)) == CanonicalSnapshotJson.Serialize(after);
}
