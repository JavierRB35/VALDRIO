using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Valdrio.Application;
using Valdrio.Domain;
using Valdrio.Localization;

namespace Valdrio.Desktop;

public sealed record StudyOption(StudyDefinition Value, string Label);
public sealed record ScenarioOption(StudyScenario Value, string Label);
public sealed record SnapshotOption(StudySnapshotReceipt Value, string Label);

public sealed partial class StudyWorkspaceViewModel : ObservableObject, IDisposable
{
    private readonly CompanyFileViewModel company;
    private readonly UiTextCatalog texts;
    private readonly PresentationPreferencesService preferences;
    private readonly PresentationFormatter formatter;
    private readonly IModelEditorDialogs dialogs;
    private readonly Func<bool>? earlierBusy;
    private readonly Func<Task<bool>>? earlierBefore;
    private readonly Func<Task>? earlierAfter;
    private bool busy, loading, dirty, pseudo, committedRefreshPending;
    private string name = "", currency = "EUR", statusKey = "Study.NoFile";
    private DateTime? start, end;
    private ModelOption? modelChoice;
    private StudyOption? studyChoice;
    private ScenarioOption? scenario;
    private SnapshotOption? snapshotChoice;
    private EntityId createId = new(Guid.NewGuid());
    private DomainDiagnostic? diagnostic;
    private CancellationTokenSource? cancellation;
    private string? boundPath;
    private IReadOnlyList<ModelVersionSnapshot> modelValues = [];
    private IReadOnlyList<StudyDefinition> studyValues = [];
    private IReadOnlyList<StudySnapshotReceipt> snapshotValues = [];
    private IReadOnlyList<ImportedBatch> importValues = [];
    private ImportColumnMappingViewModel? importColumns;

    public StudyWorkspaceViewModel(CompanyFileViewModel company, ICurrencyCatalog catalog, UiTextCatalog texts,
        PresentationPreferencesService preferences, PresentationFormatter formatter, IModelEditorDialogs dialogs)
    {
        this.company = company; this.texts = texts; this.preferences = preferences; this.formatter = formatter; this.dialogs = dialogs;
        CurrencyOptions = catalog.GetAll().Where(c => c.IsMonetary && c.MinorUnitDigits.HasValue).Select(c => c.AlphaCode.ToString()).ToArray();
        NewCommand = new AsyncRelayCommand(New, () => Ready);
        CreateCommand = new AsyncRelayCommand(Create, () => Ready && Current is null);
        SaveCommand = new AsyncRelayCommand(Save, () => Ready && Current?.State == StudyState.Open && IsDirty);
        LoadCommand = new AsyncRelayCommand(Load, () => Ready && StudyChoice is not null);
        RefreshCommand = new AsyncRelayCommand(() => Run(Reload), () => Ready);
        CaptureCommand = new AsyncRelayCommand(Capture, () => Ready && Current is not null && !IsDirty);
        ToggleStateCommand = new AsyncRelayCommand(ToggleState, () => Ready && Current is not null && !IsDirty);
        DiscardCommand = new RelayCommand(() => SetCurrent(Current), () => Ready && IsDirty);
        CancelCommand = new RelayCommand(() => cancellation?.Cancel(), () => busy);
        earlierBusy = company.WorkspaceBusy; earlierBefore = company.BeforeChangeAsync; earlierAfter = company.AfterChangeAsync;
        company.WorkspaceBusy = () => busy || ImportColumns?.IsBusy == true || earlierBusy?.Invoke() == true;
        company.BeforeChangeAsync = async () => await CanLeaveAsync() && (earlierBefore is null || await earlierBefore());
        company.AfterChangeAsync = async () => { if (earlierAfter is not null) await earlierAfter(); await CompanyChanged(); };
        preferences.Changed += PreferencesChanged; company.PropertyChanged += CompanyStateChanged; Reformat();
    }
    public bool IsBusy => busy;
    public ImportColumnMappingViewModel? ImportColumns
    {
        get => importColumns;
        init { importColumns = value; if (value is not null) value.PropertyChanged += ImportStateChanged; }
    }
    private void ImportStateChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => Refresh();
    public async Task<bool> AttachImportTarget(bool previewOnly = false)
    {
        if (busy || ImportColumns is null || ImportColumns.IsBusy) return false;
        if (previewOnly)
        {
            // Inspection never writes or resolves pending study edits, even for a saved study.
            ImportColumns.BindTarget(null, null, false, null);
            ImportColumns.SetExistingBatches([]);
            return true;
        }
        // Reading a source does not abandon the unsaved study form. Only a saved,
        // unchanged study can be used as a destination for confirmation.
        if (Current is not null && !await CanLeaveAsync()) return false;
        ImportColumns.BindTarget(company.ActiveImports, Current, company.CanWrite, async batch =>
        {
            await ApplyConfirmedThenReload(batch.ConfirmedStudy, ApplyConfirmedStudy);
            if (committedRefreshPending) throw new InvalidOperationException("Committed import refresh pending.");
        });
        ImportColumns.SetExistingBatches(importValues);
        return true;
    }
    public string ImportColumnsLabel => Text("ImportMapping.OpenMapping");
    public bool HasStudies => Studies.Count > 0;
    public bool HasPublishedModels => PublishedModels.Count > 0;
    public string StudyListHelp => Text(HasStudies ? "Study.ChooseExisting" : "Study.NoStudiesYet");
    public string ModelHelp => Text(HasPublishedModels ? "Study.ChooseModelHelp" : "Study.NoModelsHelp");
    public string GoToModelsLabel => Text("Study.GoToModels");
    public string WorkflowHelp => Text(Current is null ? "Study.CreationHelp" : "Study.SavedHelp");
    public string ImportHelp => Text(Current is null ? "Study.PreviewHelp" : "Study.ImportHelp");
    public bool IsDirty => dirty;
    public bool Ready => !busy && company.WorkspaceIdle && company.ActiveStudies is not null;
    public bool CanEdit => Ready && Current?.State != StudyState.Closed;
    public StudyDefinition? Current { get; private set; }
    public ObservableCollection<ModelOption> PublishedModels { get; } = [];
    public ObservableCollection<StudyOption> Studies { get; } = [];
    public ObservableCollection<ScenarioOption> Scenarios { get; } = [];
    public ObservableCollection<SnapshotOption> Snapshots { get; } = [];
    public IReadOnlyList<string> CurrencyOptions { get; }
    public IAsyncRelayCommand NewCommand { get; }
    public IAsyncRelayCommand CreateCommand { get; }
    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand LoadCommand { get; }
    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand CaptureCommand { get; }
    public IAsyncRelayCommand ToggleStateCommand { get; }
    public IRelayCommand DiscardCommand { get; }
    public IRelayCommand CancelCommand { get; }
    private void Edited() { if (!loading) { dirty = true; Refresh(); } }
    public string Name { get => name; set { if (SetProperty(ref name, value)) Edited(); } }
    public string Currency { get => currency; set { if (SetProperty(ref currency, value)) Edited(); } }
    public DateTime? PeriodStart { get => start; set { if (SetProperty(ref start, value)) Edited(); } }
    public DateTime? PeriodEnd { get => end; set { if (SetProperty(ref end, value)) Edited(); } }
    public ModelOption? ModelChoice { get => modelChoice; set { if (SetProperty(ref modelChoice, value)) Edited(); } }
    public ScenarioOption? Scenario { get => scenario; set { if (SetProperty(ref scenario, value)) Edited(); } }
    public StudyOption? StudyChoice { get => studyChoice; set { SetProperty(ref studyChoice, value); RefreshCommands(); } }
    public SnapshotOption? SnapshotChoice { get => snapshotChoice; set { SetProperty(ref snapshotChoice, value); OnPropertyChanged(nameof(SnapshotDetails)); OnPropertyChanged(nameof(Preparation)); OnPropertyChanged(nameof(HistoricalDefinitions)); } }
    private CultureInfo Ui => CultureInfo.GetCultureInfo(preferences.Current.UiLanguageTag);
    private CultureInfo Format => CultureInfo.GetCultureInfo(preferences.Current.FormatCultureTag);
    private string Text(string key) { var value = texts.Get(key, Ui); return pseudo ? PseudoLocalization.Expand(value) : value; }
    private string DiagnosticText(DomainDiagnostic value) => new DomainDiagnosticFormatter(texts).Format(value, Ui, Format).Message;
    private string StateText(StudyState state) => Text(state == StudyState.Open ? "Study.Open" : "Study.Closed");
    private string ScenarioText(StudyScenario value) => Text(value switch { StudyScenario.Actual => "Study.Actual", StudyScenario.Budget => "Study.Budget", _ => "Study.WhatIf" });
    public string Title => Text("Study.Title");
    public string Notes => Text("Study.Notes");
    public string OpenSectionTitle => Text("Study.OpenSectionTitle");
    public string OpenSectionHelp => Text("Study.OpenSectionHelp");
    public string FormTitle => Text(Current is null ? "Study.NewFormTitle" : "Study.EditFormTitle");
    public string FormHelp => Text(Current is null ? "Study.NewFormHelp" : "Study.EditFormHelp");
    public string ImportHistoryTitle => Text("Study.ImportHistoryTitle");
    public string ImportHistoryHelp => Text("Study.ImportHistoryHelp");
    public string AdvancedHelp => Text("Study.AdvancedHelp");
    public string ModelRemedyVisibility => HasPublishedModels ? "Collapsed" : "Visible";
    public string NameLabel => Text("Study.Name");
    public string StartLabel => Text("Study.PeriodStart");
    public string EndLabel => Text("Study.PeriodEnd");
    public string CurrencyLabel => Text("Study.Currency");
    public string ModelLabel => Text("Study.Model");
    public string ScenarioLabel => Text("Study.Scenario");
    public string NewLabel => Text(Current is null ? "Study.NewBlank" : "Study.NewPeriod");
    public string CreateLabel => Text("Study.Create");
    public string SaveLabel => Text("Study.Save");
    public string LoadLabel => Text("Study.Load");
    public string RefreshLabel => Text("Study.Refresh");
    public string CaptureLabel => Text("Study.Capture");
    public string ToggleLabel => Text(Current?.State == StudyState.Closed ? "Study.Reopen" : "Study.Close");
    public string DiscardLabel => Text("Model.Discard");
    public string CancelLabel => Text("CompanyCancel");
    public string SnapshotLabel => Text("Study.Snapshots");
    public string RunHistory => Text("Study.RunHistoryEmpty");
    public string ImportedAmounts
    {
        get
        {
            if (importValues.Count == 0) return Text("ImportMapping.NoImports");
            var active = ImportConfirmationRules.ActiveBatches(importValues).Select(b => b.Id).ToHashSet();
            return string.Join(Environment.NewLine, importValues.Select(b =>
                Text(active.Contains(b.Id) ? "ImportMapping.BatchActive" : "ImportMapping.BatchReplaced") + " · " +
                string.Format(Format, Text("ImportMapping.ImportHistory"), b.Id, b.Prepared.Source.FileName,
                      b.Resources.Count, b.Prepared.Reconciliation.NetAmount, b.Prepared.Reconciliation.Currency, b.ConfirmedStudy.Revision) +
                  (b.Prepared.ConversionBridge is not { } bridge ? "" : Environment.NewLine +
                      string.Join(Environment.NewLine, bridge.OriginalTotals.Select(t => string.Format(Format, Text("ImportMapping.CurrencyTotals"),
                          t.Currency, t.Positive, t.Negative, t.Net))) + Environment.NewLine +
                      string.Format(Format, Text("ImportMapping.FunctionalTotal"), bridge.FunctionalTotal,
                          b.Prepared.Reconciliation.Currency, bridge.RoundingAdjustment))));
        }
    }
    public string Status => diagnostic is null ? Text(statusKey) : DiagnosticText(diagnostic);
    public string Details => Current is null ? Text("Study.NoneSelected") : string.Format(Format, Text("Study.Details"), Current.Name,
        formatter.FormatDate(Current.PeriodStart.Value, Format, DateDisplayPreset.Short), formatter.FormatDate(Current.PeriodEnd.Value, Format, DateDisplayPreset.Short),
        Current.Currency.FunctionalCurrencyCode, ScenarioText(Current.ScenarioKind), Current.Revision, StateText(Current.State), Current.SelectedModelVersionId,
        IsDirty ? Text("Model.Unsaved") : Text("Model.Saved"));
    public string SnapshotDetails => SnapshotChoice is not { } selected ? Text("Study.NoSnapshot") : string.Format(Format, Text("Study.SnapshotDetails"),
        selected.Value.Id, selected.Value.InputRevision, Current is not null && selected.Value.IsCurrentFor(Current) ? Text("Study.CurrentRevision") : Text("Study.HistoricalRevision"),
        formatter.FormatInstant(selected.Value.CapturedAtUtc.Value, preferences.Current.DisplayTimeZoneId, Format), selected.Value.EconomicInputHash, selected.Value.FullSnapshotHash);
    public string Preparation => SnapshotChoice is not { } selected ? Text("Study.PrepareFirst") :
        Text(selected.Value.Input is ImportedStudySnapshotInput or ResourceStudySnapshotInput ? "Study.ImportedCaptured" : "Study.ContextCaptured") + Environment.NewLine +
        string.Join(Environment.NewLine, StudyPreparationService.Evaluate(selected.Value.Input).Diagnostics.Select(DiagnosticText));
    public string HistoricalDefinitions => SnapshotChoice is not { } selected ? "" : string.Join(Environment.NewLine,
        selected.Value.Input.Model.PhysicalCenters.Select(c => c.Code + " · " + c.Name).Concat(selected.Value.Input.Model.DriverUnits.Select(u => u.Code + " · " + u.Dimension + " · " + u.Symbol)));

    private void RefreshCommands()
    {
        foreach (var cmd in new IRelayCommand[] { NewCommand, CreateCommand, SaveCommand, LoadCommand, RefreshCommand, CaptureCommand, ToggleStateCommand, DiscardCommand, CancelCommand }) cmd.NotifyCanExecuteChanged();
    }
    private void Refresh() { OnPropertyChanged(string.Empty); RefreshCommands(); company.NotifyWorkspaceStateChanged(); }
    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> values) { target.Clear(); foreach (var v in values) target.Add(v); }
    private void Reformat()
    {
        loading = true;
        var modelId = modelChoice?.Value.Structure.Id; var studyId = studyChoice?.Value.Id; var snapshotId = snapshotChoice?.Value.Id;
        var scenarioValue = scenario?.Value ?? StudyScenario.Actual;
        Replace(PublishedModels, modelValues.Where(m => m.Status == ModelStatus.Published).Select(m => new ModelOption(m, m.Structure.Description + " · " + m.Structure.VersionNumber.ToString(Format))));
        Replace(Studies, studyValues.Select(s => new StudyOption(s, s.Name + " · " + s.Currency.FunctionalCurrencyCode + " · " + StateText(s.State))));
        Replace(Scenarios, Enum.GetValues<StudyScenario>().Select(s => new ScenarioOption(s, ScenarioText(s))));
        Replace(Snapshots, snapshotValues.Select(s => new SnapshotOption(s, s.InputRevision.ToString(Format) + " · " + formatter.FormatInstant(s.CapturedAtUtc.Value, preferences.Current.DisplayTimeZoneId, Format) + " · " + s.Id)));
        modelChoice = PublishedModels.SingleOrDefault(m => m.Value.Structure.Id == modelId);
        studyChoice = Studies.SingleOrDefault(s => s.Value.Id == studyId);
        snapshotChoice = Snapshots.SingleOrDefault(s => s.Value.Id == snapshotId);
        scenario = Scenarios.Single(s => s.Value == scenarioValue);
        loading = false; Refresh();
    }
    private void PreferencesChanged(object? sender, PresentationPreferencesChangedEventArgs e) => Reformat();
    private void CompanyStateChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    { RefreshCommands(); OnPropertyChanged(nameof(Ready)); OnPropertyChanged(nameof(CanEdit)); }
    public void RefreshPresentation(bool value) { pseudo = value; Reformat(); }
    private async Task Run(Func<CancellationToken, Task> action)
    {
        busy = true; diagnostic = null; committedRefreshPending = false; statusKey = "Study.Working"; Refresh();
        using var source = new CancellationTokenSource(); cancellation = source;
        try
        {
            await action(source.Token);
            if (!committedRefreshPending) statusKey = "Study.Completed";
        }
        catch (DomainRuleException e) { diagnostic = e.Diagnostic; }
        catch (CompanyFileException e) { diagnostic = e.Diagnostic; }
        catch (OperationCanceledException) { statusKey = "CompanyCancelled"; }
        catch (ArgumentException) { diagnostic = StudyRules.Error("STUDY_CONTEXT_INVALID", "Study.ContextInvalid").Diagnostic; }
        catch (Exception e) { Trace.TraceError("Study command: {0}", e); diagnostic = new("FILE_IO_FAILED", "File.IoFailed", []); }
        finally { busy = false; cancellation = null; Refresh(); }
    }
    private async Task Reload(CancellationToken token)
    {
        var port = company.ActiveStudies!; var models = company.ActiveModels!; var current = Current; var preservePendingEdit = dirty;
        var values = await Task.Run(() => (models.ReadModels(token), port.ReadStudies(token),
            current is null ? (IReadOnlyList<StudySnapshotReceipt>)[] : port.ReadSnapshots(current.CompanyId, current.Id, token),
            current is null ? (IReadOnlyList<ImportedBatch>)[] : company.ActiveImports!.ReadImports(current.CompanyId, current.Id, token)), token);
        modelValues = values.Item1; studyValues = values.Item2; snapshotValues = values.Item3; importValues = values.Item4; Reformat();
        if (!preservePendingEdit && current is not null)
        {
            var persisted = studyValues.SingleOrDefault(study => study.Id == current.Id);
            if (persisted is not null) SetCurrent(persisted);
        }
    }

    private static IReadOnlyList<StudyDefinition> UpsertStudy(
        IReadOnlyList<StudyDefinition> values,
        StudyDefinition confirmed) =>
        values.Where(value => value.Id != confirmed.Id).Append(confirmed).ToArray();

    private void ApplyConfirmedStudy(StudyDefinition confirmed)
    {
        studyValues = UpsertStudy(studyValues, confirmed);
        Reformat();
        SetCurrent(confirmed);
    }

    private void ApplyConfirmedSnapshot(StudySnapshotReceipt confirmed)
    {
        snapshotValues = snapshotValues.Where(value => value.Id != confirmed.Id).Append(confirmed).ToArray();
        Reformat();
        SnapshotChoice = Snapshots.Single(value => value.Value.Id == confirmed.Id);
    }

    private async Task ApplyConfirmedThenReload<T>(T confirmed, Action<T> applyConfirmed)
    {
        var outcome = await ConfirmedCommitRefresh.ApplyThenRefreshAsync(
            confirmed,
            applyConfirmed,
            () => Reload(CancellationToken.None));
        if (outcome.RefreshSucceeded) return;

        committedRefreshPending = true;
        statusKey = "Study.CommittedRefreshPending";
        Trace.TraceWarning("Study write committed but refresh failed: {0}", outcome.RefreshFailure);
    }
    public async Task CompanyChanged()
    {
        if (boundPath != company.ActivePath)
        {
            Current = null; dirty = false; name = ""; start = null; end = null; modelChoice = null; studyChoice = null; snapshotChoice = null;
            snapshotValues = []; importValues = []; createId = new(Guid.NewGuid()); currency = company.Snapshot?.DefaultCurrency.ToString() ?? "EUR";
            ImportColumns?.BindTarget(null, null, false, null);
        }
        boundPath = company.ActivePath;
        if (company.ActiveStudies is null) { modelValues = []; studyValues = []; snapshotValues = []; importValues = []; statusKey = "Study.NoFile"; Reformat(); return; }
        await Run(Reload);
    }
    private void SetCurrent(StudyDefinition? value)
    {
        loading = true; Current = value; dirty = false; diagnostic = null;
        name = value?.Name ?? ""; start = value?.PeriodStart.Value.ToDateTime(TimeOnly.MinValue); end = value?.PeriodEnd.Value.ToDateTime(TimeOnly.MinValue);
        currency = value?.Currency.FunctionalCurrencyCode.ToString() ?? company.Snapshot?.DefaultCurrency.ToString() ?? "EUR";
        modelChoice = PublishedModels.SingleOrDefault(m => m.Value.Structure.Id == value?.SelectedModelVersionId);
        scenario = Scenarios.Single(s => s.Value == (value?.ScenarioKind ?? StudyScenario.Actual));
        studyChoice = Studies.SingleOrDefault(s => s.Value.Id == value?.Id); loading = false; Refresh();
    }
    public Func<Task<bool>>? BeforeChangeAsync { get; set; }
    public bool HasConfirmedImports => importValues.Count > 0;
    public int ConfirmedImportCount => importValues.Count;
    public Task ApplyResourceReceiptAsync(StudyDefinition confirmed) => ApplyConfirmedThenReload(confirmed, ApplyConfirmedStudy);
    public async Task<bool> CanLeaveAsync()
    {
        if (busy) return false;
        if (BeforeChangeAsync is not null && !await BeforeChangeAsync()) return false;
        if (!IsDirty) return true;
        switch (dialogs.ResolvePending(Text("Study.PendingTitle"), Text("Study.PendingBody")))
        {
            case PendingEditChoice.Cancel: return false;
            case PendingEditChoice.Discard: SetCurrent(Current); return true;
            default: if (Current is null) await Create(); else await Save(); return !IsDirty;
        }
    }
    private async Task New()
    {
        if (!await CanLeaveAsync()) return;

        // Reusing the current cost structure, currency and scenario is the common analyst workflow
        // for a new accounting period. Dates and name remain intentionally blank so the new
        // analysis cannot accidentally inherit the previous period.
        var previousModelId = Current?.SelectedModelVersionId ?? ModelChoice?.Value.Structure.Id;
        var previousCurrency = Current?.Currency.FunctionalCurrencyCode.ToString() ?? Currency;
        var previousScenario = Current?.ScenarioKind ?? Scenario?.Value ?? StudyScenario.Actual;

        snapshotValues = [];
        importValues = [];
        snapshotChoice = null;
        createId = new(Guid.NewGuid());
        SetCurrent(null);

        loading = true;
        modelChoice = previousModelId is null
            ? modelChoice
            : PublishedModels.SingleOrDefault(m => m.Value.Structure.Id == previousModelId);
        currency = previousCurrency;
        scenario = Scenarios.Single(s => s.Value == previousScenario);
        loading = false;
        Reformat();
        Refresh();
    }
    private async Task Load()
    {
        var id = StudyChoice?.Value.Id;
        if (id is null || !await CanLeaveAsync()) return;
        await Run(async token => { await Reload(token); SetCurrent(studyValues.Single(s => s.Id == id)); await Reload(token); SnapshotChoice = Snapshots.LastOrDefault(); });
    }
    private BusinessDate Date(DateTime? value)
    {
        if (value is null) throw StudyRules.Error("STUDY_DATES_REQUIRED", "Study.DatesRequired");
        return new(DateOnly.FromDateTime(value.Value));
    }
    private EntityId ModelId() => ModelChoice?.Value.Structure.Id ?? throw StudyRules.Error("STUDY_MODEL_NOT_PUBLISHED", "Study.ModelNotPublished");
    private async Task Create() => await Run(async token =>
    {
        var port = company.ActiveStudies!;
        var command = new CreateStudy(company.Snapshot!.Id, createId, Name, Date(PeriodStart), Date(PeriodEnd), StudyPurpose.Management,
            Scenario!.Value, ModelId(), new(Currency), "local-user");
        var result = await Task.Run(() => port.Create(command, token), token);
        await ApplyConfirmedThenReload(result, ApplyConfirmedStudy);
    });
    private async Task Save() => await Run(async token =>
    {
        var port = company.ActiveStudies!; var current = Current!;
        var command = new UpdateStudyContext(current.CompanyId, current.Id, current.Revision, Name, Date(PeriodStart), Date(PeriodEnd),
            current.Purpose, Scenario!.Value, ModelId(), new(Currency), "local-user");
        var result = await Task.Run(() => port.UpdateContext(command, token), token);
        await ApplyConfirmedThenReload(result, ApplyConfirmedStudy);
    });
    private async Task Capture() => await Run(async token =>
    {
        var port = company.ActiveStudies!; var current = Current!;
        var request = new CaptureStudySnapshot(current.CompanyId, current.Id, current.Revision, new(Guid.NewGuid()), "local-user");
        var result = await Task.Run(() => port.Capture(request, token), token);
        await ApplyConfirmedThenReload(result, ApplyConfirmedSnapshot);
    });
    private async Task ToggleState() => await Run(async token =>
    {
        var port = company.ActiveStudies!; var current = Current!;
        var request = new ChangeStudyState(current.CompanyId, current.Id, current.Revision, current.State == StudyState.Open ? StudyState.Closed : StudyState.Open, "local-user");
        var result = await Task.Run(() => port.ChangeState(request, token), token);
        await ApplyConfirmedThenReload(result, ApplyConfirmedStudy);
    });
    public void Dispose()
    {
        preferences.Changed -= PreferencesChanged; company.PropertyChanged -= CompanyStateChanged;
        if (ImportColumns is not null) ImportColumns.PropertyChanged -= ImportStateChanged;
        cancellation?.Cancel(); company.WorkspaceBusy = earlierBusy; company.BeforeChangeAsync = earlierBefore; company.AfterChangeAsync = earlierAfter;
    }
}
