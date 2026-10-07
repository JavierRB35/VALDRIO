using System.Diagnostics;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Valdrio.Application;
using Valdrio.Domain;
using Valdrio.Localization;

namespace Valdrio.Desktop;

public sealed partial class CompanyFileViewModel : ObservableObject, IDisposable
{
    private readonly ICompanyFileService files;
    private readonly ICompanyFileDialogs dialogs;
    private readonly UiTextCatalog texts;
    private readonly PresentationPreferencesService preferences;
    private readonly PresentationFormatter formatter;
    private ICompanyFileSession? active;
    private CancellationTokenSource? cancellation;
    private DomainDiagnostic? diagnostic;
    private string statusKey = "CompanyNoFile";
    private bool busy;
    private bool pseudo;
    private string companyName = "";
    private string currency = "EUR";
    private string unitCode = "";
    private string unitDimension = "time";
    private string unitSymbol = "h";
    private int unitScale = 12;
    private bool unitDiscrete;
    public CompanyFileViewModel(ICompanyFileService files, ICompanyFileDialogs dialogs, ICurrencyCatalog catalog,
        UiTextCatalog texts, PresentationPreferencesService preferences, PresentationFormatter formatter)
    {
        this.files = files; this.dialogs = dialogs; this.texts = texts; this.preferences = preferences; this.formatter = formatter;
        CurrencyOptions = catalog.GetAll().Where(x => x.IsMonetary).Select(x => x.AlphaCode.ToString()).ToArray();
        CreateCommand = new AsyncRelayCommand(Create, () => Available);
        OpenCommand = new AsyncRelayCommand(Open, () => Available);
        CloseCommand = new AsyncRelayCommand(Close, () => Available && active is not null);
        AddUnitCommand = new AsyncRelayCommand(AddUnit, () => Available && active is not null);
        BackupCommand = new AsyncRelayCommand(Backup, () => Available && active is not null);
        RestoreCommand = new AsyncRelayCommand(Restore, () => Available);
        UpgradeCommand = new AsyncRelayCommand(Upgrade, () => Available);
        CancelCommand = new RelayCommand(() => cancellation?.Cancel(), () => busy);
        preferences.Changed += PreferencesChanged;
    }
    public IAsyncRelayCommand CreateCommand { get; }
    public IAsyncRelayCommand OpenCommand { get; }
    public IAsyncRelayCommand CloseCommand { get; }
    public IAsyncRelayCommand AddUnitCommand { get; }
    public IAsyncRelayCommand BackupCommand { get; }
    public IAsyncRelayCommand RestoreCommand { get; }
    public IAsyncRelayCommand UpgradeCommand { get; }
    public string UpgradeLabel => Text("CompanyUpgrade");
    public IRelayCommand CancelCommand { get; }
    public IReadOnlyList<string> CurrencyOptions { get; }
    public IReadOnlyList<int> ScaleOptions { get; } = Enumerable.Range(0,13).ToArray();
    public string CompanyName { get => companyName; set => SetProperty(ref companyName, value); }
    public string Currency { get => currency; set => SetProperty(ref currency, value); }
    public string UnitCode { get => unitCode; set => SetProperty(ref unitCode, value); }
    public string UnitDimension { get => unitDimension; set => SetProperty(ref unitDimension, value); }
    public string UnitSymbol { get => unitSymbol; set => SetProperty(ref unitSymbol, value); }
    public int UnitScale { get => unitScale; set => SetProperty(ref unitScale, value); }
    public bool UnitDiscrete { get => unitDiscrete; set => SetProperty(ref unitDiscrete, value); }
    public bool IsIdle => !busy;
    public Func<bool>? WorkspaceBusy { get; set; }
    public Func<Task<bool>>? BeforeChangeAsync { get; set; }
    public Func<Task>? AfterChangeAsync { get; set; }
    public IModelWorkspace? ActiveModels => active?.Models;
    public IStudyWorkspace? ActiveStudies => active?.Studies;
    public IImportWorkspace? ActiveImports => active?.Imports;
    public IResourceWorkspace? ActiveResources => active?.Resources;
    public bool CanWrite => active?.Mode == CompanyFileOpenMode.Edit;
    public bool WorkspaceIdle => !busy && WorkspaceBusy?.Invoke() != true;
    private bool Available => !busy && WorkspaceBusy?.Invoke() != true;
    public void NotifyWorkspaceStateChanged() => Refresh();
    public CompanyFileSnapshot? Snapshot { get; private set; }
    public string? ActivePath => active?.Path;
    public string Title => Text("CompanyTitle");
    public string ActiveCompanyLabel => Text("CompanyActiveLabel");
    public string ActiveCompanyName => Snapshot?.Name ?? Text("CompanyNoFile");
    public string TechnicalDetailsLabel => Text("CompanyTechnicalDetails");
    public string NameLabel => Text("CompanyNameLabel");
    public string CurrencyLabel => Text("CompanyCurrencyLabel");
    public string CreateLabel => Text("CompanyCreate");
    public string OpenLabel => Text("CompanyOpen");
    public string CloseLabel => Text("CompanyClose");
    public string BackupLabel => Text("CompanyBackup");
    public string RestoreLabel => Text("CompanyRestore");
    public string CancelLabel => Text("CompanyCancel");
    public string UnitLabel => Text("CompanyUnitLabel");
    public string DimensionLabel => Text("CompanyDimensionLabel");
    public string SymbolLabel => Text("CompanySymbolLabel");
    public string ScaleLabel => Text("CompanyScaleLabel");
    public string DiscreteLabel => Text("CompanyDiscreteLabel");
    public string AddUnitLabel => Text("CompanyAddUnit");
    public string Notes => Text("CompanyNotes");
    private CultureInfo UiCulture => CultureInfo.GetCultureInfo(preferences.Current.UiLanguageTag);
    public string Status => diagnostic is null ? Text(statusKey)
        : diagnostic.Code + ": " + new DomainDiagnosticFormatter(texts).Format(diagnostic, UiCulture,
            CultureInfo.GetCultureInfo(preferences.Current.FormatCultureTag)).Message;
    public string Details => Snapshot is null ? "—" : string.Format(UiCulture, Text("CompanyDetails"),
        Snapshot.Name, Snapshot.Id, Snapshot.DefaultCurrency,
        Snapshot.Revision.ToString("N0", CultureInfo.GetCultureInfo(preferences.Current.FormatCultureTag)),
        Snapshot.SchemaVersion, Snapshot.PrecisionPolicy.Version,
        formatter.FormatInstant(Snapshot.CreatedAt.Value, preferences.Current.DisplayTimeZoneId,
            CultureInfo.GetCultureInfo(preferences.Current.FormatCultureTag)),
        string.Join(", ", Snapshot.Units.Select(x => x.Code)), ActivePath);
    private string Text(string key) { var value = texts.Get(key, UiCulture); return pseudo ? PseudoLocalization.Expand(value) : value; }
    public void RefreshPresentation(bool usePseudo) { pseudo = usePseudo; OnPropertyChanged(string.Empty); }
    private void PreferencesChanged(object? sender, PresentationPreferencesChangedEventArgs args) => OnPropertyChanged(string.Empty);
    private void Refresh()
    {
        OnPropertyChanged(string.Empty);
        CreateCommand.NotifyCanExecuteChanged(); OpenCommand.NotifyCanExecuteChanged(); CloseCommand.NotifyCanExecuteChanged();
        AddUnitCommand.NotifyCanExecuteChanged(); BackupCommand.NotifyCanExecuteChanged(); RestoreCommand.NotifyCanExecuteChanged(); UpgradeCommand.NotifyCanExecuteChanged(); CancelCommand.NotifyCanExecuteChanged();
    }
    private async Task Run(Func<CancellationToken, Task> operation)
    {
        busy = true; diagnostic = null; statusKey = "CompanyWorking";
        using var source = new CancellationTokenSource(); cancellation = source; Refresh();
        try { await operation(source.Token); statusKey = "CompanyCompleted"; }
        catch (CompanyFileException ex) { diagnostic = ex.Diagnostic; }
        catch (DomainRuleException ex) { diagnostic = ex.Diagnostic; }
        catch (OperationCanceledException) { statusKey = "CompanyCancelled"; }
        catch (ArgumentException) { diagnostic = new("FILE_INPUT_INVALID", "File.InputInvalid", []); }
        catch (Exception ex) { Trace.TraceError("Company command failed: {0}", ex); diagnostic = new("FILE_IO_FAILED", "File.IoFailed", []); }
        finally { busy = false; cancellation = null; Refresh(); }
    }
    private async Task ReplaceAsync(ICompanyFileSession candidate)
    {
        CompanyFileSnapshot snapshot;
        try { snapshot = await Task.Run(() => candidate.Read()); } catch { candidate.Dispose(); throw; }
        var old = active; active = candidate; Snapshot = snapshot; old?.Dispose();
        if (AfterChangeAsync is not null) await AfterChangeAsync();
    }
    private async Task Create()
    {
        if (BeforeChangeAsync is not null && !await BeforeChangeAsync()) return;
        var path = dialogs.SelectNewFile(); if (path is null) return;
        var name = CompanyName; var selectedCurrency = Currency;
        await Run(async token => await ReplaceAsync(await Task.Run(() => files.Create(
            new(path, name, new CurrencyCode(selectedCurrency), PrecisionPolicy.Pdec1Proposed), token))));
    }
    private async Task Open() { var path = dialogs.SelectFile(); if (path is not null) await OpenPath(path); }
    public async Task OpenPath(string path)
    {
        if (BeforeChangeAsync is not null && !await BeforeChangeAsync()) return;
        await Run(async token => await ReplaceAsync(await Task.Run(() => files.Open(path, cancellationToken: token))));
    }
    private async Task Close()
    {
        if (BeforeChangeAsync is not null && !await BeforeChangeAsync()) return;
        active?.Dispose(); active = null; Snapshot = null; diagnostic = null; statusKey = "CompanyNoFile";
        if (AfterChangeAsync is not null) await AfterChangeAsync(); Refresh();
    }
    private async Task AddUnit()
    {
        await Run(async token =>
        {
            var unit = new UnitDefinition(new(new(Guid.NewGuid())), UnitCode, UnitDimension, UnitSymbol, UnitScale, UnitDiscrete);
            Snapshot = await Task.Run(() => active!.AddUnits([unit], Snapshot!.Revision, "local-user", token));
            if (AfterChangeAsync is not null) await AfterChangeAsync();
        });

        if (diagnostic is null && statusKey == "CompanyCompleted")
        {
            statusKey = "CompanyUnitSaved";
            UnitCode = "";
            UnitDimension = "time";
            UnitSymbol = "h";
            UnitScale = 12;
            UnitDiscrete = false;
            Refresh();
        }
    }
    private async Task Backup()
    {
        var path = dialogs.SelectBackupDestination(); if (path is null) return;
        await Run(token => Task.Run(() => active!.Backup(path, token)));
    }
    private async Task Restore()
    {
        if (BeforeChangeAsync is not null && !await BeforeChangeAsync()) return;
        var source = dialogs.SelectBackupSource(); if (source is null) return;
        var target = dialogs.SelectRestoreDestination(); if (target is null) return;
        await Run(async token => await ReplaceAsync(await Task.Run(() => files.Restore(source, target, token))));
    }
    private async Task Upgrade()
    {
        if (BeforeChangeAsync is not null && !await BeforeChangeAsync()) return;
        var source = dialogs.SelectFile(); if (source is null) return;
        var target = dialogs.SelectNewFile(); if (target is null) return;
        await Run(async token => await ReplaceAsync(await Task.Run(() => files.Upgrade(source, target, "local-user", token))));
    }
    public void Dispose() { preferences.Changed -= PreferencesChanged; cancellation?.Cancel(); active?.Dispose(); active = null; }
}
