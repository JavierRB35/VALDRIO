using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Valdrio.Application;
using Valdrio.Domain;
using Valdrio.Localization;

namespace Valdrio.Desktop;

public interface IImportSourceDialogs { string? SelectSource(); }
public sealed class ImportMappingOption(string value, string label) : ObservableObject
{
    private string label = label;
    public string Value => value;
    public string Label { get => label; set => SetProperty(ref label, value); }
}
public sealed class ImportColumnOption(int index, string header, string label) : ObservableObject
{
    private string label = label;
    public int Index => index;
    public string Header => header;
    public string Label { get => label; set => SetProperty(ref label, value); }
}
public sealed record ImportPreviewRow(int Row, string Account, string OriginalAmount, string ParsedAmount, string Issues,
    string OriginalDebit = "", string OriginalCredit = "", string Description = "");

public sealed partial class ImportColumnMappingViewModel : ObservableObject, IDisposable
{
    private readonly IImportSourceSessionFactory factory;
    private readonly IImportSourceDialogs dialogs;
    private readonly UiTextCatalog texts;
    private readonly PresentationPreferencesService preferences;
    private IImportSourceSession? session;
    private ImportSourceDocument? document;
    private ImportSourceReadSettings? readSettings;
    private ImportColumnSelectionResult? validation;
    private DomainDiagnostic? diagnostic;
    private CancellationTokenSource? cancellation;
    private bool busy, pseudo;
    private string statusKey = "ChooseFile", headerRow = "1", firstDataRow = "2", delimiter = ";", encoding = "utf-8";
    private string decimalSeparator = ",", groupSeparator = "", negativeSyntax = "minus";
    private string amountLayout = "amount";
    private string? worksheet;
    private int? accountColumn, descriptionColumn, amountColumn, debitColumn, creditColumn;
    private bool allowNumericAccounts;
    private IReadOnlyList<ImportColumnOption> columnOptions = [];
    private readonly ImportColumnOption noDescriptionOption = new(0, "", "");
    private IReadOnlyList<ImportColumnOption> descriptionOptions = [];
    private readonly ImportMappingOption[] delimiters = [new(";", ";"), new(",", ","), new("tab", "")];
    private readonly ImportMappingOption[] groups = [new("", ""), new(".", "."), new(",", ","), new(" ", ""), new("\u00a0", "NBSP"), new("\u202f", "NNBSP")];
    private readonly ImportMappingOption[] amountLayouts = [new("amount", ""), new("debit-credit", "")];

    public ImportColumnMappingViewModel(IImportSourceSessionFactory factory, IImportSourceDialogs dialogs,
        UiTextCatalog texts, PresentationPreferencesService preferences, ICurrencyCatalog? currencyCatalog = null)
    {
        this.currencyCatalog = currencyCatalog;
        this.factory = factory; this.dialogs = dialogs; this.texts = texts; this.preferences = preferences;
        OpenCommand = new AsyncRelayCommand(Open, () => !busy);
        ReadCommand = new AsyncRelayCommand(Read, () => !busy && session is not null && (!IsXlsx || !string.IsNullOrEmpty(worksheet)));
        ValidateCommand = new AsyncRelayCommand(Validate, () => !busy && document is not null && AccountColumn.HasValue &&
            (IsSignedAmount && AmountColumn.HasValue || IsDebitCredit && DebitColumn.HasValue && CreditColumn.HasValue));
        PrepareCommand = new AsyncRelayCommand(Prepare, () => !busy && IsReady && IsAccountSelectionReady && targetStudy?.State == StudyState.Open && importPort is not null);
        ConfirmCommand = new AsyncRelayCommand(Confirm, () => !busy && CanSaveReviewedImport);
        CancelCommand = new RelayCommand(() => cancellation?.Cancel(), () => busy);
        SelectAllRecordsCommand = new RelayCommand(() => ImportAllAccounts = true, () => !busy && HasAccountChoices);
        DeselectAllRecordsCommand = new RelayCommand(() => ImportAllAccounts = false, () => !busy && HasAccountChoices);
        FreezeComparisonCommand = new RelayCommand(FreezeComparison, () => !busy && IsReady && IsAccountSelectionReady &&
            fiscalStart.HasValue && fiscalEnd.HasValue && previousEnd.HasValue && !string.IsNullOrWhiteSpace(comparisonScope) &&
            (international && currencyFromColumn || !string.IsNullOrWhiteSpace(sourceCurrency)));
        ClearComparisonCommand = new RelayCommand(ClearComparison, () => !busy && comparison is not null);
        InitializeConversion();
        preferences.Changed += PreferencesChanged;
        Reformat();
    }

    public IAsyncRelayCommand OpenCommand { get; }
    public IAsyncRelayCommand ReadCommand { get; }
    public IAsyncRelayCommand ValidateCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public bool IsBusy => busy;
    public bool IsIdle => !busy;
    public bool HasFile => session is not null;
    public bool IsXlsx => session?.SourceKind == ImportSourceKind.Xlsx;
    public bool IsCsv => session?.SourceKind == ImportSourceKind.Csv;
    public bool IsSignedAmount => amountLayout == "amount";
    public bool IsDebitCredit => amountLayout == "debit-credit";
    public IReadOnlyList<ImportMappingOption> AmountLayouts => amountLayouts;
    public string AmountLayout
    {
        get => amountLayout;
        set
        {
            if (!SetProperty(ref amountLayout, value)) return;
            amountColumn = debitColumn = creditColumn = null; InvalidateMapping();
        }
    }
    public string InterpretationPrompt => this[amountMode == "cumulative" ? "ApproveComparison" : amountMode == "closing-full" ? (IsDebitCredit ? "ApproveClosingDebitCredit" : "ApproveClosing") : (IsDebitCredit ? "ApproveDebitCredit" : "ApproveInterpretation")];
    public bool HasColumns => document is not null;
    // Highlight required columns as soon as the source has been read.
    public bool MissingAccountColumn => HasColumns && !AccountColumn.HasValue;
    public bool MissingAmountColumn => HasColumns && IsSignedAmount && !AmountColumn.HasValue;
    public bool MissingDebitColumn => HasColumns && IsDebitCredit && !DebitColumn.HasValue;
    public bool MissingCreditColumn => HasColumns && IsDebitCredit && !CreditColumn.HasValue;
    public bool IsReady => validation?.IsValid == true;
    public ImportColumnSelectionResult? ValidatedSelection => IsReady ? validation : null;
    public string FileName => session?.FileName ?? "";
    public IReadOnlyList<string> Worksheets => session?.WorksheetNames ?? [];
    public string this[string key] => Text("ImportMapping." + key);
    public string Title => this["Title"];
    public string Worksheet { get => worksheet ?? ""; set { if (SetProperty(ref worksheet, value)) InvalidateSource(); } }
    public string HeaderRow { get => headerRow; set { if (SetProperty(ref headerRow, value)) InvalidateSource(); } }
    public string FirstDataRow { get => firstDataRow; set { if (SetProperty(ref firstDataRow, value)) InvalidateSource(); } }
    public string Delimiter { get => delimiter; set { if (SetProperty(ref delimiter, value)) InvalidateSource(); } }
    public string Encoding { get => encoding; set { if (SetProperty(ref encoding, value)) InvalidateSource(); } }
    public int? AccountColumn { get => accountColumn; set { if (SetProperty(ref accountColumn, value)) InvalidateMapping(); } }
    public int? DescriptionColumn { get => descriptionColumn ?? 0; set { var column = value == 0 ? null : value; if (SetProperty(ref descriptionColumn, column)) InvalidateMapping(); } }
    public IReadOnlyList<ImportColumnOption> DescriptionColumns => descriptionOptions;
    public int? AmountColumn { get => amountColumn; set { if (SetProperty(ref amountColumn, value)) InvalidateMapping(); } }
    public int? DebitColumn { get => debitColumn; set { if (SetProperty(ref debitColumn, value)) InvalidateMapping(); } }
    public int? CreditColumn { get => creditColumn; set { if (SetProperty(ref creditColumn, value)) InvalidateMapping(); } }
    public string DecimalSeparator { get => decimalSeparator; set { if (SetProperty(ref decimalSeparator, value)) InvalidateMapping(); } }
    public string GroupSeparator { get => groupSeparator; set { if (SetProperty(ref groupSeparator, value)) InvalidateMapping(); } }
    public string NegativeSyntax { get => negativeSyntax; set { if (SetProperty(ref negativeSyntax, value)) InvalidateMapping(); } }
    public bool AllowNumericAccounts { get => allowNumericAccounts; set { if (SetProperty(ref allowNumericAccounts, value)) InvalidateMapping(); } }
    public IReadOnlyList<string> Encodings { get; } = ["utf-8", "utf-16le", "utf-16be", "iso-8859-1"];
    public IReadOnlyList<string> DecimalSeparators { get; } = [",", "."];
    public IReadOnlyList<ImportMappingOption> Delimiters => delimiters;
    public IReadOnlyList<ImportMappingOption> GroupSeparators => groups;
    public IReadOnlyList<ImportMappingOption> NegativeSyntaxes { get; } =
        [new("minus", "-123"), new("parentheses", "(123)"), new("trailing", "123-")];
    public IReadOnlyList<ImportColumnOption> Columns => columnOptions;
    public IReadOnlyList<ImportPreviewRow> Preview => document is null ? [] : document.Rows.Take(50).Select(row =>
    {
        var parsed = validation?.Rows.FirstOrDefault(r => r.RecordNumber == row.RecordNumber);
        var issues = validation?.Diagnostics.Where(d => d.Location.RecordNumber == row.RecordNumber)
            .Select(d => FormatDiagnosticMessage(d.Diagnostic)) ?? [];
        return new ImportPreviewRow(row.RecordNumber,
            Display(accountColumn is { } a ? row.GetCell(a).TextValue : null),
            Display(amountColumn is { } b ? row.GetCell(b).TextValue : null),
            parsed?.Amount?.ToString(CultureInfo.GetCultureInfo(preferences.Current.FormatCultureTag)) ?? "",
            string.Join("\n", issues),
            Display(debitColumn is { } d ? row.GetCell(d).TextValue : null),
            Display(creditColumn is { } c ? row.GetCell(c).TextValue : null),
            descriptionColumn is { } description && !string.IsNullOrWhiteSpace(row.GetCell(description).TextValue) ? Display(row.GetCell(description).TextValue) : this["DescriptionMissing"]);
    }).ToArray();
    public string Status => diagnostic is null ? this[statusKey] : FormatDiagnostic(diagnostic);
    public string UserStatus => diagnostic is null ? this[statusKey] : FormatDiagnosticMessage(diagnostic);
    public bool HasUserError => diagnostic is not null || validation is { IsValid: false };
    public string Summary => validation is null ? "" : string.Format(Ui, this["Summary"],
        validation.Rows.Count, validation.Diagnostics.Count, validation.IsValid ? this["Valid"] : this["Blocked"]);
    public string FriendlySummary => validation is null ? "" : validation.IsValid
        ? string.Format(Ui, this["FriendlyValidSummary"], validation.Rows.Count)
        : string.Format(Ui, this["FriendlyInvalidSummary"], validation.Rows.Count, validation.Diagnostics.Count);
    public string GlobalIssues => validation is null ? "" : string.Join("\n", validation.Diagnostics.Take(100).Select(d =>
        $"{d.Location.RecordNumber?.ToString(CultureInfo.InvariantCulture) ?? "—"} · {d.Location.Column?.ToString(CultureInfo.InvariantCulture) ?? "—"} · {FormatDiagnostic(d.Diagnostic)}"));
    public string FriendlyGlobalIssues => validation is null ? "" : string.Join("\n", validation.Diagnostics.Take(100).Select(d =>
        string.Format(Ui, this["FriendlyIssueLine"],
            d.Location.RecordNumber?.ToString(CultureInfo.InvariantCulture) ?? "—",
            FormatDiagnosticMessage(d.Diagnostic))));
    public string FileStepRequirement => !HasFile ? this["NeedFile"] :
        IsXlsx && string.IsNullOrWhiteSpace(Worksheet) ? this["NeedWorksheet"] :
        HasColumns ? this["FileReadReady"] : this["FileContinueHint"];
    public string ColumnStepRequirement => !HasColumns ? this["NeedReadFirst"] :
        !AccountColumn.HasValue ? this["NeedAccountColumn"] :
        IsSignedAmount && !AmountColumn.HasValue ? this["NeedAmountColumn"] :
        IsDebitCredit && (!DebitColumn.HasValue || !CreditColumn.HasValue) ? this["NeedDebitCreditColumns"] :
        validation is { IsValid: false } ? this["NeedFixRows"] : this["ColumnsContinueHint"];

    private CultureInfo Ui => CultureInfo.GetCultureInfo(preferences.Current.UiLanguageTag);
    private string Text(string key) { var value = texts.Get(key, Ui); return pseudo ? PseudoLocalization.Expand(value) : value; }
    private string FormatDiagnosticMessage(DomainDiagnostic value) =>
        new DomainDiagnosticFormatter(texts).Format(value, Ui, CultureInfo.GetCultureInfo(preferences.Current.FormatCultureTag)).Message;
    private string FormatDiagnostic(DomainDiagnostic value) => value.Code + " · " + FormatDiagnosticMessage(value);
    private static string Display(string? value) => value is null ? "" : value.Length <= 200 ? value : value[..200] + "…";
    private static string ColumnName(int index)
    { var text = ""; while (index > 0) { index--; text = (char)('A' + index % 26) + text; index /= 26; } return text; }
    private void PreferencesChanged(object? sender, PresentationPreferencesChangedEventArgs e) => Reformat();
    public void RefreshPresentation(bool value) { pseudo = value; Reformat(); }
    private string ColumnLabel(int index, string header) =>
        $"{ColumnName(index)} · {(header.Length == 0 ? this["NoHeader"] : Display(header))}";
    private void Reformat()
    {
        foreach (var choice in accountChoices) choice.Reformat(CultureInfo.GetCultureInfo(preferences.Current.FormatCultureTag));
        amountModes[0].Label = this["PeriodMode"]; amountModes[1].Label = this["ClosingMode"];
        amountModes[2].Label = this["CumulativeMode"];
        delimiters[2].Label = this["Tab"]; groups[0].Label = this["None"]; groups[3].Label = this["Space"];
        amountLayouts[0].Label = this["SignedLayout"]; amountLayouts[1].Label = this["DebitCreditLayout"];
        noDescriptionOption.Label = this["NoDescriptionColumn"];
        foreach (var c in columnOptions) c.Label = ColumnLabel(c.Index, c.Header); Refresh();
    }
    private void InvalidateMapping()
    { cancellation?.Cancel(); validation = null; ResetAccountChoices(); InvalidatePreparation(true); diagnostic = null; statusKey = "SelectColumns"; Refresh(); }
    private void InvalidateSource()
    {
        cancellation?.Cancel(); document = null; readSettings = null; accountColumn = descriptionColumn = amountColumn = debitColumn = creditColumn = null;
        currencyColumn = dateColumn = null;
        columnOptions = []; descriptionOptions = []; validation = null; ResetAccountChoices(); InvalidatePreparation(true); diagnostic = null; statusKey = "ReadSource"; Refresh();
    }
    private void Refresh()
    {
        OnPropertyChanged(""); OpenCommand.NotifyCanExecuteChanged(); ReadCommand.NotifyCanExecuteChanged();
        ValidateCommand.NotifyCanExecuteChanged(); CancelCommand.NotifyCanExecuteChanged();
        PrepareCommand.NotifyCanExecuteChanged(); ConfirmCommand.NotifyCanExecuteChanged();
        SelectAllRecordsCommand.NotifyCanExecuteChanged(); DeselectAllRecordsCommand.NotifyCanExecuteChanged();
        FreezeComparisonCommand.NotifyCanExecuteChanged(); ClearComparisonCommand.NotifyCanExecuteChanged();
        AddRateCommand?.NotifyCanExecuteChanged(); ApplyRateCommand?.NotifyCanExecuteChanged();
    }
    private async Task Run(Func<CancellationToken, Task> action, bool keepSelection = false)
    {
        busy = true; diagnostic = null; statusKey = "Working";
        using var token = new CancellationTokenSource(); cancellation = token; Refresh();
        try { await action(token.Token); }
        catch (OperationCanceledException) { if (!keepSelection) validation = null; statusKey = "Cancelled"; }
        catch (DomainRuleException ex) { if (!keepSelection) validation = null; diagnostic = ex.Diagnostic; }
        catch (CompanyFileException ex) { if (!keepSelection) validation = null; diagnostic = ex.Diagnostic; }
        catch (ArgumentException) { if (!keepSelection) validation = null; diagnostic = ImportRules.Error("IMP_PROFILE_INVALID", "Import.InvalidProfile").Diagnostic; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { if (!keepSelection) validation = null; statusKey = "FileError"; }
        finally { cancellation = null; busy = false; Refresh(); }
    }
    private async Task Open()
    {
        var path = dialogs.SelectSource(); if (path is null) return;
        // A failed replacement must not leave the previous file marked ready.
        session?.Dispose(); session = null; worksheet = null; InvalidateSource();
        await Run(async token =>
        {
            session = await factory.OpenAsync(path, token); token.ThrowIfCancellationRequested(); sourcePath = path;
            statusKey = IsXlsx ? "SelectWorksheet" : "ReadSource";
        });
    }
    private ImportSourceReadSettings Settings()
    {
        if (!int.TryParse(headerRow, NumberStyles.None, CultureInfo.InvariantCulture, out var header) ||
            !int.TryParse(firstDataRow, NumberStyles.None, CultureInfo.InvariantCulture, out var first))
            throw ImportRules.Error("IMP_PROFILE_INVALID", "Import.InvalidProfile");
        var csv = IsCsv ? new CsvSourceDialect(encoding, delimiter switch
        { ";" => ';', "," => ',', "tab" => '\t', _ => throw ImportRules.Error("IMP_PROFILE_INVALID", "Import.InvalidProfile") }, '"', CsvBomPolicy.Optional) : null;
        return new ImportSourceReadSettings(session!.SourceKind, IsXlsx ? worksheet : null, header, first, csv);
    }
    private async Task Read()
    {
        document = null; validation = null; readSettings = null; accountColumn = descriptionColumn = amountColumn = debitColumn = creditColumn = null;
        columnOptions = []; descriptionOptions = []; ResetAccountChoices(); InvalidatePreparation(true);
        await Run(async token =>
        {
            var settings = Settings(); var source = await session!.ReadAsync(settings, token);
            token.ThrowIfCancellationRequested(); document = source; readSettings = settings;
            columnOptions = ImportColumnSelection.GetColumns(source).Select(c =>
                new ImportColumnOption(c.Index, c.Header, ColumnLabel(c.Index, c.Header))).ToArray();
            descriptionOptions = new[] { noDescriptionOption }.Concat(columnOptions).ToArray();
            statusKey = "SelectColumns";
        });
    }
    private async Task Validate()
    {
        // Rechecking unchanged columns must preserve record inclusion and manual rate assignments.
        if (IsReady) return;
        validation = null; ResetAccountChoices(); InvalidatePreparation(true);
        await Run(async token =>
        {
            var assigned = IsSignedAmount ? new[] { accountColumn, amountColumn } :
                IsDebitCredit ? new[] { accountColumn, debitColumn, creditColumn } :
                throw ImportRules.Error("IMP_PROFILE_INVALID", "Import.InvalidProfile");
            if (assigned.Any(c => c is null)) throw ImportRules.Error("IMP_COLUMN_SELECTION", "Import.ColumnsRequired");
            if (assigned.Distinct().Count() != assigned.Length) throw ImportRules.Error("IMP_COLUMN_SELECTION", "Import.ColumnsDistinct");
            var negative = negativeSyntax switch
            { "minus" => ImportNegativeSyntax.LeadingMinus, "parentheses" => ImportNegativeSyntax.Parentheses,
                "trailing" => ImportNegativeSyntax.TrailingMinus, _ => throw ImportRules.Error("IMP_PROFILE_INVALID", "Import.InvalidProfile") };
            var number = new SourceNumberFormat(decimalSeparator, groupSeparator.Length == 0 ? null : groupSeparator,
                groupSeparator.Length == 0 ? [] : [3], negative);
            var columns = IsSignedAmount ? new ImportColumnMap(accountColumn!.Value, description: descriptionColumn, amount: amountColumn!.Value) :
                new ImportColumnMap(accountColumn!.Value, description: descriptionColumn, debit: debitColumn!.Value, credit: creditColumn!.Value);
            var policy = allowNumericAccounts ? XlsxNumericAccountPolicy.PreserveStoredLexical : XlsxNumericAccountPolicy.Reject;
            var source = document!; var settings = readSettings!;
            var result = await Task.Run(() => ImportColumnSelection.Validate(source, settings, columns, number, policy, token,
                ImportDuplicatePolicy.AggregateWithProvenance), token);
            token.ThrowIfCancellationRequested(); validation = result; SetAccountChoices(result); statusKey = result.IsValid ? "Ready" : "HasErrors";
        });
    }
    public void Dispose() { cancellation?.Cancel(); session?.Dispose(); ResetAccountChoices(); preferences.Changed -= PreferencesChanged; }
}
