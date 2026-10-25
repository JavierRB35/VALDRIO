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
    private string? upgradeSourcePath;
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
        CreateCommand = new AsyncRelayCommand(Create, () => Available && !string.IsNullOrWhiteSpace(CompanyName));
        OpenCommand = new AsyncRelayCommand(Open, () => Available);
        OpenExampleCommand = new AsyncRelayCommand(OpenExample, () => Available);
        CloseCommand = new AsyncRelayCommand(Close, () => Available && active is not null);
        NewProjectCommand = new AsyncRelayCommand(NewProject, () => Available && active is not null);
        AddUnitCommand = new AsyncRelayCommand(AddUnit, () => Available && active is not null &&
            !string.IsNullOrWhiteSpace(UnitCode) && !string.IsNullOrWhiteSpace(UnitDimension));
        AddDefaultUnitsCommand = new AsyncRelayCommand(AddDefaultUnits,
            () => Available && CanWrite && Snapshot is not null && DefaultActivityUnitCatalog.HasMissing(Snapshot.Units));
        BackupCommand = new AsyncRelayCommand(Backup, () => Available && active is not null);
        RestoreCommand = new AsyncRelayCommand(Restore, () => Available);
        UpgradeCommand = new AsyncRelayCommand(Upgrade, () => Available);
        CancelCommand = new RelayCommand(() => cancellation?.Cancel(), () => busy);
        preferences.Changed += PreferencesChanged;
    }
    public IAsyncRelayCommand CreateCommand { get; }
    public IAsyncRelayCommand OpenCommand { get; }
    public IAsyncRelayCommand OpenExampleCommand { get; }
    public string ExampleLabel => Text("Guide.OpenExample");
    public string ExampleHelp => Text("Guide.ExampleHelp");
    public string ImportStartLabel => Text("Guide.ImportStart");
    public string ImportStartHelp => Text("Guide.ImportStartHelp");
    public IAsyncRelayCommand CloseCommand { get; }
    public IAsyncRelayCommand NewProjectCommand { get; }
    public IAsyncRelayCommand AddUnitCommand { get; }
    public IAsyncRelayCommand AddDefaultUnitsCommand { get; }
    public IAsyncRelayCommand BackupCommand { get; }
    public IAsyncRelayCommand RestoreCommand { get; }
    public IAsyncRelayCommand UpgradeCommand { get; }
    public string UpgradeLabel => Text("CompanyUpgrade");
    public bool UpgradeSuggested => diagnostic?.Code == "FILE_UPGRADE_REQUIRED";
    public IRelayCommand CancelCommand { get; }
    public IReadOnlyList<string> CurrencyOptions { get; }
    public IReadOnlyList<int> ScaleOptions { get; } = Enumerable.Range(0,13).ToArray();
    public string CompanyName
    {
        get => companyName;
        set
        {
            if (SetProperty(ref companyName, value))
                CreateCommand.NotifyCanExecuteChanged();
        }
    }
    public string Currency { get => currency; set => SetProperty(ref currency, value); }
    public string UnitCode
    {
        get => unitCode;
        set
        {
            if (SetProperty(ref unitCode, value))
                AddUnitCommand.NotifyCanExecuteChanged();
        }
    }
    public string UnitDimension
    {
        get => unitDimension;
        set
        {
            if (SetProperty(ref unitDimension, value))
                AddUnitCommand.NotifyCanExecuteChanged();
        }
    }
    public string UnitSymbol { get => unitSymbol; set => SetProperty(ref unitSymbol, value); }
    public int UnitScale { get => unitScale; set => SetProperty(ref unitScale, value); }
    public bool UnitDiscrete { get => unitDiscrete; set => SetProperty(ref unitDiscrete, value); }
    public bool IsIdle => !busy;
    public Func<bool>? WorkspaceBusy { get; set; }
    public Func<Task<bool>>? BeforeChangeAsync { get; set; }
    public Func<Task>? AfterChangeAsync { get; set; }
    // A recoverable file-open error is surfaced visibly without failing the UI dispatcher.
    public Action<string>? OpenFailure { get; set; }
    public Func<string, string, string, bool>? ConfirmFileOperation { get; set; }
    public IModelWorkspace? ActiveModels => active?.Models;
    public IStudyWorkspace? ActiveStudies => active?.Studies;
    public IImportWorkspace? ActiveImports => active?.Imports;
    public IResourceWorkspace? ActiveResources => active?.Resources;
    public IAllocationWorkspace? ActiveAllocations => active?.Allocations;
    public IRunStore? ActiveRuns => active?.Runs;
    public IPhysicalActivityWorkspace? ActivePhysicalActivity => active?.PhysicalActivity;
    public IEstimateWorkspace? ActiveEstimates => active?.Estimates;
    public ICompositionWorkspace? ActiveCompositions => active?.Compositions;
    public bool CanWrite => active?.Mode == CompanyFileOpenMode.Edit;
    public bool WorkspaceIdle => !busy && WorkspaceBusy?.Invoke() != true;
    // Central activity state shared by every workspace. Existing asynchronous
    // workflows call NotifyWorkspaceStateChanged at start/end, so the shell
    // indicator follows file, model, study, import, resources and allocation.
    public bool IsProcessing => !WorkspaceIdle;
    public string WorkingLabel => Text("GlobalProcessing");
    private bool Available => !busy && WorkspaceBusy?.Invoke() != true;
    public void NotifyWorkspaceStateChanged() => Refresh();
    public CompanyFileSnapshot? Snapshot { get; private set; }
    public bool HasProject => Snapshot is not null;
    public bool NoProject => Snapshot is null;
    public string? ActivePath => active?.Path;
    public string Title => Text("CompanyTitle");
    public string ProjectIntro => Text("CompanyProjectIntro");
    public string OpenProjectTitle => Text("CompanyOpenProjectTitle");
    public string OpenProjectHelp => Text("CompanyOpenProjectHelp");
    public string NewProjectTitle => Text("CompanyNewProjectTitle");
    public string NewProjectHelp => Text("CompanyNewProjectHelp");
    public string ProjectReadyHelp => Text("CompanyProjectReadyHelp");
    public string FileOperationsLabel => Text("CompanyFileOperations");
    public string FileOperationsHelp => Text("CompanyFileOperationsHelp");
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
    public string UnitCreationHelp => Text("CompanyUnitCreationHelp");
    public string DefaultUnitsHelp => Text("CompanyDefaultsHelp");
    public string AddDefaultUnitsLabel => Text("CompanyInstallDefaults");
    public bool MissingDefaultUnits => CanWrite && Snapshot is not null && DefaultActivityUnitCatalog.HasMissing(Snapshot.Units);
    public string UnitRequiredHelp => Text("CompanyUnitRequiredHelp");
    public string UnitAdvancedLabel => Text("CompanyUnitAdvanced");
    public string DimensionLabel => Text("CompanyDimensionLabel");
    public string SymbolLabel => Text("CompanySymbolLabel");
    public string ScaleLabel => Text("CompanyScaleLabel");
    public string DiscreteLabel => Text("CompanyDiscreteLabel");
    public string AddUnitLabel => Text("CompanyAddUnit");
    public string Notes => Text("CompanyNotes");
    private CultureInfo UiCulture => CultureInfo.GetCultureInfo(preferences.Current.UiLanguageTag);
    // UI-02: distinguish a domain validation error from neutral operation feedback.
    public bool HasStatusError => diagnostic is not null;
    public string Status => diagnostic is null ? Text(statusKey)
        : new DomainDiagnosticFormatter(texts).Format(diagnostic, UiCulture,
            CultureInfo.GetCultureInfo(preferences.Current.FormatCultureTag)).Message;
    public string? DiagnosticCode => diagnostic?.Code;
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
        OpenExampleCommand.NotifyCanExecuteChanged();
        CreateCommand.NotifyCanExecuteChanged(); OpenCommand.NotifyCanExecuteChanged(); CloseCommand.NotifyCanExecuteChanged(); NewProjectCommand.NotifyCanExecuteChanged();
        AddUnitCommand.NotifyCanExecuteChanged(); AddDefaultUnitsCommand.NotifyCanExecuteChanged(); BackupCommand.NotifyCanExecuteChanged(); RestoreCommand.NotifyCanExecuteChanged(); UpgradeCommand.NotifyCanExecuteChanged(); CancelCommand.NotifyCanExecuteChanged();
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
        var old = active;
        var previousSnapshot = Snapshot;
        active = candidate;
        Snapshot = snapshot;
        try
        {
            if (AfterChangeAsync is not null) await AfterChangeAsync();
        }
        catch
        {
            // Keep the previous session usable if a dependent workspace rejects the switch.
            active = old;
            Snapshot = previousSnapshot;
            candidate.Dispose();
            if (AfterChangeAsync is not null)
            {
                try { await AfterChangeAsync(); }
                catch (Exception rollbackError) { Trace.TraceError("Workspace restoration after failed open: {0}", rollbackError); }
            }
            throw;
        }
        old?.Dispose();
    }
    private async Task Create()
    {
        if (BeforeChangeAsync is not null && !await BeforeChangeAsync()) return;
        var name = CompanyName; var selectedCurrency = Currency;
        var path = dialogs.SelectNewFile(name); if (path is null) return;
        await Run(async token =>
        {
            var candidate = await Task.Run(() =>
            {
                var session = files.Create(
                    new(path, name, new CurrencyCode(selectedCurrency), PrecisionPolicy.Pdec1Proposed), token);
                try
                {
                    var created = session.Read(token);
                    var defaults = DefaultActivityUnitCatalog.Missing(created.Units);
                    if (defaults.Count != 0)
                        session.AddUnits(defaults, created.Revision, "system-defaults", token);
                    return session;
                }
                catch
                {
                    session.Dispose();
                    throw;
                }
            }, token);
            await ReplaceAsync(candidate);
        });
    }
    private async Task OpenExample()
    {
        if (BeforeChangeAsync is not null && !await BeforeChangeAsync()) return;
        var destination = dialogs.SelectNewFile("VALDRIO - Example");
        if (destination is null) return;
        if (System.IO.File.Exists(destination) || System.IO.Directory.Exists(destination))
        {
            diagnostic = new("FILE_ALREADY_EXISTS", "File.AlreadyExists", []);
            Refresh();
            return;
        }
        await Run(async token =>
        {
            var candidate = await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                var source = System.IO.Path.Combine(AppContext.BaseDirectory, "examples", "services.valdrio");
                // The shipped fixture is immutable. Older bundled examples are
                // migrated directly into a NEW destination with atomic promotion;
                // never open an incompatible raw copy as a current-format file.
                if (files.RequiresUpgrade(source, token))
                    return files.Upgrade(source, destination, "local-user", token);
                try { System.IO.File.Copy(source, destination, overwrite: false); }
                catch (System.IO.IOException ex) when (System.IO.File.Exists(destination) || System.IO.Directory.Exists(destination))
                {
                    // Another process may create the destination after the preflight check.
                    throw new CompanyFileException("FILE_ALREADY_EXISTS", "File.AlreadyExists", ex);
                }
                return files.Open(destination, cancellationToken: token);
            }, token);
            await ReplaceAsync(candidate);
        });
    }

    private async Task Open()
    {
        try
        {
            var path = dialogs.SelectFile();
            if (path is not null) await OpenPath(path);
        }
        catch (Exception ex)
        {
            SetUnexpectedOpenFailure(ex);
            ReportOpenFailure();
        }
    }
    public async Task OpenPath(string path)
    {
        try
        {
            if (BeforeChangeAsync is not null && !await BeforeChangeAsync()) return;
            upgradeSourcePath = null;
            var requiresUpgrade = false;
            await Run(async token =>
            {
                requiresUpgrade = await Task.Run(() => files.RequiresUpgrade(path, token), token);
                if (!requiresUpgrade)
                    await ReplaceAsync(await Task.Run(() => files.Open(path, cancellationToken: token), token));
            });
            if (requiresUpgrade && diagnostic is null)
            {
                // Retain the selected source. "Upgrade" will ask only for a new destination.
                // A previous project, if open, is left untouched.
                upgradeSourcePath = path;
                diagnostic = new("FILE_UPGRADE_REQUIRED", "File.UpgradeRequired", []);
                Refresh();
            }
        }
        catch (OperationCanceledException)
        {
            diagnostic = null;
            statusKey = "CompanyCancelled";
            Refresh();
        }
        catch (Exception ex)
        {
            // Includes failures from pending-edit coordination outside Run().
            SetUnexpectedOpenFailure(ex);
        }
        ReportOpenFailure();
    }
    private void SetUnexpectedOpenFailure(Exception ex)
    {
        Trace.TraceError("Opening company failed: {0}", ex);
        diagnostic = ex switch
        {
            CompanyFileException file => file.Diagnostic,
            DomainRuleException domain => domain.Diagnostic,
            _ => new DomainDiagnostic("FILE_IO_FAILED", "File.IoFailed", [])
        };
        Refresh();
    }
    private void ReportOpenFailure()
    {
        if (diagnostic is null) return;
        try { OpenFailure?.Invoke(Status); }
        catch (Exception ex) { Trace.TraceError("Company open alert failed: {0}", ex); }
    }
    private async Task NewProject()
    {
        await Close();
        // A refused project change leaves the current project and its data untouched.
        if (active is not null) return;
        CompanyName = "";
        Currency = CurrencyOptions.Contains("EUR", StringComparer.Ordinal) ? "EUR" : CurrencyOptions.FirstOrDefault() ?? "";
        UnitCode = "";
        UnitDimension = "time";
        UnitSymbol = "h";
        UnitScale = 12;
        UnitDiscrete = false;
        Refresh();
    }

    private async Task Close()
    {
        if (BeforeChangeAsync is not null && !await BeforeChangeAsync()) return;
        active?.Dispose(); active = null; Snapshot = null; diagnostic = null; statusKey = "CompanyNoFile";
        if (AfterChangeAsync is not null) await AfterChangeAsync(); Refresh();
    }
    private async Task AddDefaultUnits()
    {
        await Run(async token =>
        {
            var before = Snapshot!;
            var missing = DefaultActivityUnitCatalog.Missing(before.Units);
            if (missing.Count == 0) return;
            Snapshot = await Task.Run(() =>
                active!.AddUnits(missing, before.Revision, "local-user", token), token);
            if (AfterChangeAsync is not null) await AfterChangeAsync();
        });
        if (diagnostic is null && statusKey == "CompanyCompleted")
        {
            statusKey = "CompanyDefaultsInstalled";
            Refresh();
        }
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
        if (ConfirmFileOperation?.Invoke(Text("CompanyBackup"), ActivePath ?? "", path) == false) return;
        await Run(token => Task.Run(() => active!.Backup(path, token)));
    }
    private async Task Restore()
    {
        if (BeforeChangeAsync is not null && !await BeforeChangeAsync()) return;
        var source = dialogs.SelectBackupSource(); if (source is null) return;
        var target = dialogs.SelectRestoreDestination(); if (target is null) return;
        if (ConfirmFileOperation?.Invoke(Text("CompanyRestore"), source, target) == false) return;
        await Run(async token => await ReplaceAsync(await Task.Run(() => files.Restore(source, target, token))));
    }
    private async Task Upgrade()
    {
        if (BeforeChangeAsync is not null && !await BeforeChangeAsync()) return;
        var source = upgradeSourcePath ?? dialogs.SelectFile(); if (source is null) return;
        var target = dialogs.SelectNewFile(); if (target is null) return;
        if (ConfirmFileOperation?.Invoke(Text("CompanyUpgrade"), source, target) == false) return;
        await Run(async token => await ReplaceAsync(await Task.Run(() => files.Upgrade(source, target, "local-user", token))));
        if (diagnostic is null) upgradeSourcePath = null;
    }
    public void Dispose() { preferences.Changed -= PreferencesChanged; cancellation?.Cancel(); active?.Dispose(); active = null; }
}
