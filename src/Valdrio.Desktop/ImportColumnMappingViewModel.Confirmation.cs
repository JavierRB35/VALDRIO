using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using Valdrio.Application;
using Valdrio.Domain;

namespace Valdrio.Desktop;

public sealed record ImportGroupPreview(string Code, string Records, string Amount, string Description = "")
{
    public decimal NumericAmount { get; init; }
}
public sealed record ImportBatchOption(EntityId Id, string Label);
public sealed record ImportComparisonPreview(string Code, string Earlier, string Current, string Difference, string EarlierRecords, string Description = "");

public sealed partial class ImportColumnMappingViewModel
{
    private string amountMode = "period";
    private readonly ImportMappingOption[] amountModes = [new("period", ""), new("closing-full", ""), new("cumulative", "")];
    private ImportBatchOption? replacement;
    private bool replaceBatch;
    private ImportComparison? comparison;
    private string? comparisonPath;
    private DateTime? fiscalStart, fiscalEnd, previousEnd;
    private string comparisonScope = "";
    public string DateLanguageTag => preferences.Current.FormatCultureTag;
    public bool HasComparisonRows => preparedImport?.ComparisonRows is { Count: > 0 };
    public IReadOnlyList<ImportComparisonPreview> ComparisonRows => preparedImport?.ComparisonRows?.Select(r =>
        new ImportComparisonPreview(r.Code, r.Earlier.Amount.ToString(CultureInfo.GetCultureInfo(DateLanguageTag)),
            r.Current.Amount.ToString(CultureInfo.GetCultureInfo(DateLanguageTag)), r.Difference.Amount.ToString(CultureInfo.GetCultureInfo(DateLanguageTag)),
            string.Join(", ", r.EarlierRecords), DescriptionForCode(r.Code))).ToArray() ?? [];
    public IReadOnlyList<ImportBatchOption> ExistingBatches { get; private set; } = [];
    public void SetExistingBatches(IReadOnlyList<ImportedBatch> batches)
    {
        ExistingBatches = ImportConfirmationRules.ActiveBatches(batches).Select(b => new ImportBatchOption(b.Id,
            b.Prepared.Source.FileName + " · " + b.ConfirmedAtUtc + " · " + b.Id)).ToArray();
        replacement = null; replaceBatch = false; InvalidatePreparation(true); Refresh();
    }
    public bool ReplaceBatch { get => replaceBatch; set { if (SetProperty(ref replaceBatch, value)) { InvalidatePreparation(true); Refresh(); } } }
    public ImportBatchOption? Replacement { get => replacement; set { if (SetProperty(ref replacement, value)) { InvalidatePreparation(true); Refresh(); } } }
    public DateTime? FiscalStart { get => fiscalStart; set { if (SetProperty(ref fiscalStart, value)) ClearComparison(); } }
    public DateTime? FiscalEnd { get => fiscalEnd; set { if (SetProperty(ref fiscalEnd, value)) ClearComparison(); } }
    public DateTime? PreviousEnd { get => previousEnd; set { if (SetProperty(ref previousEnd, value)) ClearComparison(); } }
    public string ComparisonScope { get => comparisonScope; set { if (SetProperty(ref comparisonScope, value)) ClearComparison(); } }
    public string ComparisonStatus => comparison is null ? this["ComparisonEmpty"] : string.Format(Ui, this["ComparisonReady"], comparison.Source.FileName, comparison.Profile.PeriodEnd);
    public IRelayCommand FreezeComparisonCommand { get; private set; } = null!;
    public IRelayCommand ClearComparisonCommand { get; private set; } = null!;
    private void ClearComparison() { comparison = null; comparisonPath = null; InvalidatePreparation(true); Refresh(); }
    private void FreezeComparison()
    {
        try
        {
            var selected = validation!;
            var prior = new SourceParseProfileVersion(international ? SourceParseProfileVersion.InternationalSchema : SourceParseProfileVersion.GroupingSchema, new(Guid.NewGuid()), 1,
                selected.Source.FileName, selected.Settings.SourceKind, selected.Settings.WorksheetName,
                selected.Settings.HeaderRow, selected.Settings.FirstDataRow, selected.Columns, selected.Settings.Csv,
                selected.NumberFormat, international ? datePattern : "yyyy-MM-dd", ImportAmountMode.ClosingBalanceAsStudyAmount,
                selected.Columns.Debit.HasValue ? ImportSignConvention.DebitMinusCredit : ImportSignConvention.SignedAmount,
                new(DateOnly.FromDateTime(fiscalStart!.Value)), new(DateOnly.FromDateTime(previousEnd!.Value)), international && currencyFromColumn ? null : international ? new CurrencyCode(sourceCurrency) : targetStudy!.Currency.FunctionalCurrencyCode, international && currencyFromColumn ? currencyColumn : null,
                ImportDuplicatePolicy.AggregateWithProvenance, ImportZeroPolicy.Keep, ImportAccountSelection.All,
                new(ImportProfileApprovalState.ApprovedBusiness, "Earlier source retained explicitly: " + comparisonScope), true,
                xlsxNumericAccountPolicy: selected.NumericAccountPolicy, grouping: SelectedRecords(), conversion: international ? BuildConversion() : null);
            comparison = new(selected.Source, prior, new(DateOnly.FromDateTime(fiscalStart.Value)),
                new(DateOnly.FromDateTime(fiscalEnd!.Value)), comparisonScope);
            comparisonPath = sourcePath; InvalidatePreparation(true); diagnostic = null;
        }
        catch (DomainRuleException ex) { diagnostic = ex.Diagnostic; }
        catch (ArgumentException) { diagnostic = ImportRules.Error("IMP_COMPARISON_INVALID", "Import.ComparisonInvalid").Diagnostic; }
        Refresh();
    }
    public IReadOnlyList<ImportMappingOption> AmountModes => amountModes;
    public bool IsCumulativeMode => amountMode == "cumulative";
    public string AmountMode
    {
        get => amountMode;
        set
        {
            if (!SetProperty(ref amountMode, value)) return;
            OnPropertyChanged(nameof(IsCumulativeMode));
            InvalidatePreparation(true);
            Refresh();
        }
    }
    public string AmountBasis => amountMode == "cumulative" ? this["CumulativeBasis"] : amountMode == "closing-full"
        ? string.Format(Ui, this["ClosingBasis"], targetStudy?.PeriodEnd.ToString() ?? "—")
        : this["PeriodBasis"];
    private IImportWorkspace? importPort;
    private StudyDefinition? targetStudy;
    private Func<ImportedBatch, Task>? afterConfirmed;
    private PreparedImport? preparedImport;
    private ImportedBatch? confirmedImport;
    private EntityId requestId = new(Guid.NewGuid());
    private string sourceCurrency = "", approvalReference = "";
    private string? sourcePath;
    private bool interpretationApproved, writable, primaryClosed;
    public IAsyncRelayCommand PrepareCommand { get; }
    public IAsyncRelayCommand ConfirmCommand { get; }
    public PreparedImport? Preparation => preparedImport;
    public ImportedBatch? ConfirmedImport => confirmedImport;
    public bool HasConfirmableReview => preparedImport is { } p && p.Rows.Any(r => r.Selected) &&
        !p.Diagnostics.Any(d => d.Severity == ImportDiagnosticSeverity.Blocking && d.Diagnostic.Code != "IMP_APPROVAL_REQUIRED");
    public bool HasClosedPrimaryDestination => primaryClosed;
    public string PrimaryClosedNotice => primaryClosed ? Text("Allocation.PrimaryClosed") : "";
    public bool CanSaveReviewedImport => writable && !primaryClosed && confirmedImport is null && HasConfirmableReview && interpretationApproved;
    public string ReviewSaveRequirement => primaryClosed ? Text("Allocation.PrimaryClosed") : confirmedImport is not null ? Guide("SavedImport") : !HasConfirmableReview ? Guide("EditReview") : !interpretationApproved ? Guide("ApproveReview") : Guide("SaveReady");
    public bool ShowReviewApproval => HasConfirmableReview && confirmedImport is null;
    public bool HasPreparedReview => preparedImport is not null || confirmedImport is not null;
    public bool HasPreparedGroups => preparedImport?.Groups is { Count: > 0 };
    public IReadOnlyList<ImportGroupPreview> PreparedGroups => preparedImport?.Groups?.Select(g => new ImportGroupPreview(
        g.Code, string.Join(", ", g.Records), g.Amount.Amount.ToString(CultureInfo.GetCultureInfo(preferences.Current.FormatCultureTag)) + (international ? " " + g.Amount.Currency : ""), DescriptionForCode(g.Code))
        { NumericAmount = g.Amount.Amount })
        .ToArray() ?? [];
    public string SourceCurrency
    {
        get => !international && targetStudy is { } study ? study.Currency.FunctionalCurrencyCode.ToString() : sourceCurrency;
        set
        {
            // Legacy/programmatic FX support is retained for historical regression checks.
            // In the ordinary balance-import workflow the study currency is immutable.
            if (!international && targetStudy is not null) return;
            if (SetProperty(ref sourceCurrency, value)) { InvalidatePreparation(true); Refresh(); }
        }
    }
    public string ApprovalReference
    {
        get => approvalReference;
        set { if (SetProperty(ref approvalReference, value)) { if (confirmedImport is not null) InvalidatePreparation(false); Refresh(); } }
    }
    private string EffectiveApprovalReference => !string.IsNullOrWhiteSpace(approvalReference) ? approvalReference :
        "Reviewed in VALDRIO: " + amountMode + "; " + validation?.Source.FileHash + "; " + targetStudy?.PeriodStart + "/" + targetStudy?.PeriodEnd;
    public bool InterpretationApproved
    {
        get => interpretationApproved;
        set { if (SetProperty(ref interpretationApproved, value)) Refresh(); }
    }
    public string Target => targetStudy is null ? this["NoStudy"] : string.Format(Ui, this["Target"],
        targetStudy.Name, targetStudy.PeriodStart, targetStudy.PeriodEnd, targetStudy.Currency.FunctionalCurrencyCode, targetStudy.Revision);
    public string ConfirmationSummary
    {
        get
        {
            if (confirmedImport is { } batch) return string.Format(Ui, this["ConfirmedSummary"], batch.Id,
                batch.Resources.Count, batch.ConfirmedStudy.Revision);
            if (preparedImport is not { } prepared) return "";
            var r = prepared.Reconciliation; var culture = CultureInfo.GetCultureInfo(preferences.Current.FormatCultureTag);
            return string.Format(Ui, this["Totals"], r.ReadRows, r.SelectedRows, r.IgnoredRows, r.InvalidRows,
                r.PositiveAmount.ToString(culture), r.NegativeAmount.ToString(culture), r.NetAmount.ToString(culture), r.Currency);
        }
    }
    public string PreparationIssues => preparedImport is null ? "" : string.Join("\n", preparedImport.Diagnostics.Where(d => d.Diagnostic.Code != "IMP_APPROVAL_REQUIRED").Take(100).Select(d =>
        string.Format(Ui, this["FriendlyIssueLine"],
            d.Location.RecordNumber?.ToString(CultureInfo.InvariantCulture) ?? "—",
            FormatDiagnosticMessage(d.Diagnostic))));
    public void BindTarget(IImportWorkspace? port, StudyDefinition? study, bool canWrite, Func<ImportedBatch, Task>? onConfirmed, bool primaryClosed = false)
    {
        if (busy) throw new InvalidOperationException("Import is busy.");
        // Every newly attached import defaults to the analysis currency. This
        // prevents a prior legacy international batch from leaking its hidden
        // conversion mode into an ordinary balance import.
        var changed = !ReferenceEquals(importPort, port) || targetStudy != study || international || currencyFromColumn;
        importPort = port; targetStudy = study; afterConfirmed = onConfirmed; writable = canWrite; this.primaryClosed = primaryClosed;
        international = false;
        currencyFromColumn = false;
        if (changed)
        {
            sourceCurrency = study?.Currency.FunctionalCurrencyCode.ToString() ?? "";
            InvalidatePreparation(true);
        }
        Refresh();
    }
    private void InvalidatePreparation(bool resetApproval)
    {
        if (preparedImport is not null || confirmedImport is not null) statusKey = "ReviewChanged";
        preparedImport = null; confirmedImport = null; requestId = new(Guid.NewGuid());
        if (resetApproval)
        {
            interpretationApproved = false;

        }
    }
    private async Task Prepare()
    {
        preparedImport = null; confirmedImport = null;
        await Run(async token =>
        {
            var selected = validation!; var study = targetStudy!; CurrencyCode? currency = international && currencyFromColumn ? null : international ? new CurrencyCode(sourceCurrency) : study.Currency.FunctionalCurrencyCode;
            var approved = interpretationApproved;
            if (amountMode == "cumulative" && comparison is null) throw ImportRules.Error("IMP_COMPARISON_INVALID", "Import.ComparisonInvalid");
            if (replaceBatch && replacement is null) throw ImportRules.Error("IMP_REPLACEMENT_INVALID", "Import.ReplacementInvalid");
            var profile = new SourceParseProfileVersion(international ? SourceParseProfileVersion.InternationalSchema : amountMode == "cumulative" ? SourceParseProfileVersion.ComparisonSchema : SourceParseProfileVersion.GroupingSchema, new(Guid.NewGuid()), 1,
                selected.Source.FileName, selected.Settings.SourceKind, selected.Settings.WorksheetName,
                selected.Settings.HeaderRow, selected.Settings.FirstDataRow, selected.Columns, selected.Settings.Csv,
                selected.NumberFormat, international ? datePattern : "yyyy-MM-dd", amountMode switch
                {
                    "period" => ImportAmountMode.PeriodAmount,
                    "closing-full" => ImportAmountMode.ClosingBalanceAsStudyAmount,
                    "cumulative" => ImportAmountMode.CumulativeExpenseDifference,
                    _ => throw ImportRules.Error("IMP_PROFILE_INVALID", "Import.InvalidProfile")
                },
                selected.Columns.Debit.HasValue ? ImportSignConvention.DebitMinusCredit : ImportSignConvention.SignedAmount,
                study.PeriodStart, study.PeriodEnd, currency, international && currencyFromColumn ? currencyColumn : null, ImportDuplicatePolicy.AggregateWithProvenance, ImportZeroPolicy.Keep,
                ImportAccountSelection.All, new(approved ? ImportProfileApprovalState.ApprovedBusiness : ImportProfileApprovalState.PendingBusinessApproval,
                    approved ? EffectiveApprovalReference : "pending"), approved,
                xlsxNumericAccountPolicy: selected.NumericAccountPolicy, grouping: SelectedRecords(), comparison: amountMode == "cumulative" ? comparison : null, conversion: international ? BuildConversion() : null);
            var port = importPort!;
            var prepared = await Task.Run(() => port.PrepareImport(study.CompanyId, study.Id, study.Revision,
                new(Guid.NewGuid()), selected.Source, profile, token), token);
            token.ThrowIfCancellationRequested(); preparedImport = prepared; requestId = new(Guid.NewGuid());
            statusKey = HasConfirmableReview ? "PreparedReady" : "HasErrors";
        }, keepSelection: true);
    }
    private async Task Confirm()
    {
        if (!CanSaveReviewedImport) return;
        // Apply review approval to the immutable profile at the explicit save action.
        if (preparedImport?.CanConfirm != true || preparedImport.Profile.Approval.Reference != EffectiveApprovalReference)
            await Prepare();
        if (preparedImport?.CanConfirm != true) return;
        await CommitPreparedImport();
    }
    private async Task CommitPreparedImport() => await Run(async token =>
    {
        var prepared = preparedImport!; var port = importPort!;
        // Recheck the chosen path; the preview is an immutable capture, but a changed file requires a new preview.
        using var currentFile = await factory.OpenAsync(sourcePath!, token);
        var currentSource = await currentFile.ReadAsync(readSettings!, token);
        if (currentSource.FileHash != prepared.Source.FileHash)
        { preparedImport = null; throw ImportRules.Error("IMP_SOURCE_CHANGED", "Import.SourceChanged"); }
        if (prepared.Profile.Comparison is { } previous)
        {
            using var previousFile = await factory.OpenAsync(comparisonPath!, token);
            var settings = new ImportSourceReadSettings(previous.Profile.SourceKind, previous.Profile.WorksheetName,
                previous.Profile.HeaderRow, previous.Profile.FirstDataRow, previous.Profile.Csv);
            var original = await previousFile.ReadAsync(settings, token);
            if (original.FileHash != previous.Source.FileHash)
            { preparedImport = null; throw ImportRules.Error("IMP_SOURCE_CHANGED", "Import.SourceChanged"); }
        }
        var command = new ConfirmImport(prepared.StudyContext.CompanyId, prepared.StudyContext.Id, prepared.StudyContext.Revision,
            requestId, prepared.Fingerprint, prepared, replaceBatch ? ImportConfirmMode.ReplaceIdentifiedBatch : ImportConfirmMode.AppendBatch, "local-user",
            replaceBatch ? replacement?.Id : null);
        var result = await Task.Run(() => port.ConfirmImport(command, token), token);
        var outcome = await ConfirmedCommitRefresh.ApplyThenRefreshAsync(result,
            value => { confirmedImport = value; targetStudy = value.ConfirmedStudy; statusKey = "Confirmed"; Refresh(); },
            () => afterConfirmed?.Invoke(result) ?? Task.CompletedTask);
        if (!outcome.RefreshSucceeded) statusKey = "CommittedRefreshPending";
    }, keepSelection: true);
}
