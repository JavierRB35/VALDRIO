using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using Valdrio.Application;
using Valdrio.Domain;

namespace Valdrio.Desktop;

public sealed record ImportGroupPreview(string Code, string Records, string Amount);

public sealed partial class ImportColumnMappingViewModel
{
    private string amountMode = "period";
    private readonly ImportMappingOption[] amountModes = [new("period", ""), new("closing-full", "")];
    public IReadOnlyList<ImportMappingOption> AmountModes => amountModes;
    public string AmountMode
    {
        get => amountMode;
        set { if (SetProperty(ref amountMode, value)) { InvalidatePreparation(true); Refresh(); } }
    }
    public string AmountBasis => amountMode == "closing-full"
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
    private bool interpretationApproved, writable;
    public IAsyncRelayCommand PrepareCommand { get; }
    public IAsyncRelayCommand ConfirmCommand { get; }
    public PreparedImport? Preparation => preparedImport;
    public ImportedBatch? ConfirmedImport => confirmedImport;
    public bool HasPreparedGroups => preparedImport?.Groups is { Count: > 0 };
    public IReadOnlyList<ImportGroupPreview> PreparedGroups => preparedImport?.Groups?.Select(g => new ImportGroupPreview(
        g.Code, string.Join(", ", g.Records), g.Amount.Amount.ToString(CultureInfo.GetCultureInfo(preferences.Current.FormatCultureTag))))
        .ToArray() ?? [];
    public string SourceCurrency
    {
        get => sourceCurrency;
        set { if (SetProperty(ref sourceCurrency, value)) { InvalidatePreparation(true); Refresh(); } }
    }
    public string ApprovalReference
    {
        get => approvalReference;
        set { if (SetProperty(ref approvalReference, value)) { InvalidatePreparation(false); Refresh(); } }
    }
    public bool InterpretationApproved
    {
        get => interpretationApproved;
        set { if (SetProperty(ref interpretationApproved, value)) { InvalidatePreparation(false); Refresh(); } }
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
    public string PreparationIssues => preparedImport is null ? "" : string.Join("\n", preparedImport.Diagnostics.Take(100).Select(d =>
        $"{d.Location.RecordNumber?.ToString(CultureInfo.InvariantCulture) ?? "—"} · {d.Location.Column?.ToString(CultureInfo.InvariantCulture) ?? "—"} · {FormatDiagnostic(d.Diagnostic)}"));
    public void BindTarget(IImportWorkspace? port, StudyDefinition? study, bool canWrite, Func<ImportedBatch, Task>? onConfirmed)
    {
        if (busy) throw new InvalidOperationException("Import is busy.");
        var changed = !ReferenceEquals(importPort, port) || targetStudy != study;
        importPort = port; targetStudy = study; afterConfirmed = onConfirmed; writable = canWrite;
        if (changed) { sourceCurrency = ""; InvalidatePreparation(true); }
        Refresh();
    }
    private void InvalidatePreparation(bool resetApproval)
    {
        preparedImport = null; confirmedImport = null; requestId = new(Guid.NewGuid());
        if (resetApproval) interpretationApproved = false;
    }
    private async Task Prepare()
    {
        preparedImport = null; confirmedImport = null;
        await Run(async token =>
        {
            var selected = validation!; var study = targetStudy!; var currency = new CurrencyCode(sourceCurrency);
            var approved = interpretationApproved && !string.IsNullOrWhiteSpace(approvalReference);
            var profile = new SourceParseProfileVersion(SourceParseProfileVersion.GroupingSchema, new(Guid.NewGuid()), 1,
                selected.Source.FileName, selected.Settings.SourceKind, selected.Settings.WorksheetName,
                selected.Settings.HeaderRow, selected.Settings.FirstDataRow, selected.Columns, selected.Settings.Csv,
                selected.NumberFormat, "yyyy-MM-dd", amountMode switch
                {
                    "period" => ImportAmountMode.PeriodAmount,
                    "closing-full" => ImportAmountMode.ClosingBalanceAsStudyAmount,
                    _ => throw ImportRules.Error("IMP_PROFILE_INVALID", "Import.InvalidProfile")
                },
                selected.Columns.Debit.HasValue ? ImportSignConvention.DebitMinusCredit : ImportSignConvention.SignedAmount,
                study.PeriodStart, study.PeriodEnd, currency, null, ImportDuplicatePolicy.AggregateWithProvenance, ImportZeroPolicy.Keep,
                ImportAccountSelection.All, new(approved ? ImportProfileApprovalState.ApprovedBusiness : ImportProfileApprovalState.PendingBusinessApproval,
                    string.IsNullOrWhiteSpace(approvalReference) ? "pending" : approvalReference), approved,
                xlsxNumericAccountPolicy: selected.NumericAccountPolicy, grouping: SelectedRecords());
            var port = importPort!;
            var prepared = await Task.Run(() => port.PrepareImport(study.CompanyId, study.Id, study.Revision,
                new(Guid.NewGuid()), selected.Source, profile, token), token);
            token.ThrowIfCancellationRequested(); preparedImport = prepared; requestId = new(Guid.NewGuid());
            statusKey = prepared.CanConfirm ? "PreparedReady" : "HasErrors";
        }, keepSelection: true);
    }
    private async Task Confirm() => await Run(async token =>
    {
        var prepared = preparedImport!; var port = importPort!;
        // Recheck the chosen path; the preview is an immutable capture, but a changed file requires a new preview.
        using var currentFile = await factory.OpenAsync(sourcePath!, token);
        var currentSource = await currentFile.ReadAsync(readSettings!, token);
        if (currentSource.FileHash != prepared.Source.FileHash)
        { preparedImport = null; throw ImportRules.Error("IMP_SOURCE_CHANGED", "Import.SourceChanged"); }
        var command = new ConfirmImport(prepared.StudyContext.CompanyId, prepared.StudyContext.Id, prepared.StudyContext.Revision,
            requestId, prepared.Fingerprint, prepared, ImportConfirmMode.AppendBatch, "local-user");
        var result = await Task.Run(() => port.ConfirmImport(command, token), token);
        var outcome = await ConfirmedCommitRefresh.ApplyThenRefreshAsync(result,
            value => { confirmedImport = value; targetStudy = value.ConfirmedStudy; statusKey = "Confirmed"; Refresh(); },
            () => afterConfirmed?.Invoke(result) ?? Task.CompletedTask);
        if (!outcome.RefreshSucceeded) statusKey = "CommittedRefreshPending";
    }, keepSelection: true);
}
