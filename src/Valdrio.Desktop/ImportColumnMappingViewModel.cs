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
public sealed record ImportPreviewRow(int Row, string Account, string OriginalAmount, string ParsedAmount, string Issues);

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
    private string? worksheet;
    private int? accountColumn, amountColumn;
    private bool allowNumericAccounts;
    private IReadOnlyList<ImportColumnOption> columnOptions = [];
    private readonly ImportMappingOption[] delimiters = [new(";", ";"), new(",", ","), new("tab", "")];
    private readonly ImportMappingOption[] groups = [new("", ""), new(".", "."), new(",", ","), new(" ", "")];

    public ImportColumnMappingViewModel(IImportSourceSessionFactory factory, IImportSourceDialogs dialogs,
        UiTextCatalog texts, PresentationPreferencesService preferences)
    {
        this.factory = factory; this.dialogs = dialogs; this.texts = texts; this.preferences = preferences;
        OpenCommand = new AsyncRelayCommand(Open, () => !busy);
        ReadCommand = new AsyncRelayCommand(Read, () => !busy && session is not null && (!IsXlsx || !string.IsNullOrEmpty(worksheet)));
        ValidateCommand = new AsyncRelayCommand(Validate, () => !busy && document is not null && AccountColumn.HasValue && AmountColumn.HasValue);
        PrepareCommand = new AsyncRelayCommand(Prepare, () => !busy && IsReady && targetStudy?.State == StudyState.Open && importPort is not null);
        ConfirmCommand = new AsyncRelayCommand(Confirm, () => !busy && writable && preparedImport?.CanConfirm == true && confirmedImport is null);
        CancelCommand = new RelayCommand(() => cancellation?.Cancel(), () => busy);
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
    public bool HasColumns => document is not null;
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
    public int? AmountColumn { get => amountColumn; set { if (SetProperty(ref amountColumn, value)) InvalidateMapping(); } }
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
            .Select(d => FormatDiagnostic(d.Diagnostic)) ?? [];
        return new ImportPreviewRow(row.RecordNumber,
            Display(accountColumn is { } a ? row.GetCell(a).TextValue : null),
            Display(amountColumn is { } b ? row.GetCell(b).TextValue : null),
            parsed?.Amount?.ToString(CultureInfo.GetCultureInfo(preferences.Current.FormatCultureTag)) ?? "",
            string.Join("\n", issues));
    }).ToArray();
    public string Status => diagnostic is null ? this[statusKey] : FormatDiagnostic(diagnostic);
    public string Summary => validation is null ? "" : string.Format(Ui, this["Summary"],
        validation.Rows.Count, validation.Diagnostics.Count, validation.IsValid ? this["Valid"] : this["Blocked"]);
    public string GlobalIssues => validation is null ? "" : string.Join("\n", validation.Diagnostics.Take(100).Select(d =>
        $"{d.Location.RecordNumber?.ToString(CultureInfo.InvariantCulture) ?? "—"} · {d.Location.Column?.ToString(CultureInfo.InvariantCulture) ?? "—"} · {FormatDiagnostic(d.Diagnostic)}"));

    private CultureInfo Ui => CultureInfo.GetCultureInfo(preferences.Current.UiLanguageTag);
    private string Text(string key) { var value = texts.Get(key, Ui); return pseudo ? PseudoLocalization.Expand(value) : value; }
    private string FormatDiagnostic(DomainDiagnostic value) => value.Code + " · " +
        new DomainDiagnosticFormatter(texts).Format(value, Ui, CultureInfo.GetCultureInfo(preferences.Current.FormatCultureTag)).Message;
    private static string Display(string? value) => value is null ? "" : value.Length <= 200 ? value : value[..200] + "…";
    private static string ColumnName(int index)
    { var text = ""; while (index > 0) { index--; text = (char)('A' + index % 26) + text; index /= 26; } return text; }
    private void PreferencesChanged(object? sender, PresentationPreferencesChangedEventArgs e) => Reformat();
    public void RefreshPresentation(bool value) { pseudo = value; Reformat(); }
    private string ColumnLabel(int index, string header) =>
        $"{ColumnName(index)} · {(header.Length == 0 ? this["NoHeader"] : Display(header))}";
    private void Reformat()
    {
        delimiters[2].Label = this["Tab"]; groups[0].Label = this["None"]; groups[3].Label = this["Space"];
        foreach (var c in columnOptions) c.Label = ColumnLabel(c.Index, c.Header); Refresh();
    }
    private void InvalidateMapping()
    { cancellation?.Cancel(); validation = null; InvalidatePreparation(true); diagnostic = null; statusKey = "SelectColumns"; Refresh(); }
    private void InvalidateSource()
    {
        cancellation?.Cancel(); document = null; readSettings = null; accountColumn = null; amountColumn = null;
        columnOptions = []; validation = null; InvalidatePreparation(true); diagnostic = null; statusKey = "ReadSource"; Refresh();
    }
    private void Refresh()
    {
        OnPropertyChanged(""); OpenCommand.NotifyCanExecuteChanged(); ReadCommand.NotifyCanExecuteChanged();
        ValidateCommand.NotifyCanExecuteChanged(); CancelCommand.NotifyCanExecuteChanged();
        PrepareCommand.NotifyCanExecuteChanged(); ConfirmCommand.NotifyCanExecuteChanged();
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
        document = null; validation = null; readSettings = null; accountColumn = null; amountColumn = null; columnOptions = []; InvalidatePreparation(true);
        await Run(async token =>
        {
            var settings = Settings(); var source = await session!.ReadAsync(settings, token);
            token.ThrowIfCancellationRequested(); document = source; readSettings = settings;
            columnOptions = ImportColumnSelection.GetColumns(source).Select(c =>
                new ImportColumnOption(c.Index, c.Header, ColumnLabel(c.Index, c.Header))).ToArray();
            statusKey = "SelectColumns";
        });
    }
    private async Task Validate()
    {
        validation = null; InvalidatePreparation(true);
        await Run(async token =>
        {
            if (accountColumn == amountColumn) throw ImportRules.Error("IMP_COLUMN_SELECTION", "Import.ColumnsDistinct");
            var negative = negativeSyntax switch
            { "minus" => ImportNegativeSyntax.LeadingMinus, "parentheses" => ImportNegativeSyntax.Parentheses,
                "trailing" => ImportNegativeSyntax.TrailingMinus, _ => throw ImportRules.Error("IMP_PROFILE_INVALID", "Import.InvalidProfile") };
            var number = new SourceNumberFormat(decimalSeparator, groupSeparator.Length == 0 ? null : groupSeparator,
                groupSeparator.Length == 0 ? [] : [3], negative);
            var columns = new ImportColumnMap(accountColumn!.Value, amount: amountColumn!.Value);
            var policy = allowNumericAccounts ? XlsxNumericAccountPolicy.PreserveStoredLexical : XlsxNumericAccountPolicy.Reject;
            var source = document!; var settings = readSettings!;
            var result = await Task.Run(() => ImportColumnSelection.Validate(source, settings, columns, number, policy, token), token);
            token.ThrowIfCancellationRequested(); validation = result; statusKey = result.IsValid ? "Ready" : "HasErrors";
        });
    }
    public void Dispose() { cancellation?.Cancel(); session?.Dispose(); preferences.Changed -= PreferencesChanged; }
}
