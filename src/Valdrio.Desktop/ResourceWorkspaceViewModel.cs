using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Valdrio.Application;
using Valdrio.Domain;
using Valdrio.Localization;
namespace Valdrio.Desktop;

public sealed partial class ResourceWorkspaceViewModel : ObservableObject, IDisposable
{
    private readonly CompanyFileViewModel company;
    private readonly StudyWorkspaceViewModel studies;
    private readonly UiTextCatalog texts;
    private readonly PresentationPreferencesService preferences;
    private readonly ICurrencyCatalog catalog;
    private readonly IModelEditorDialogs dialogs;
    private readonly Func<bool>? earlierBusy;
    private readonly Func<Task<bool>>? earlierBefore;
    private readonly Func<Task>? earlierAfter;
    private bool busy, loading, dirty, pseudo, partDirty, adjustmentDirty, objectDirty, cultureAcknowledged;
    private ResourceWorkspaceSnapshot? workspace, preview;
    private ResourceDraft? editing;
    private readonly List<ResourcePartDraft> parts = [];
    private readonly List<ResourceAdjustmentRequest> deltas = [];
    private EntityId? partId;
    private DomainDiagnostic? diagnostic;
    private string statusKey = "Ready", reason = "";
    private int stage;
    private CancellationTokenSource? cancellation;
    private IReadOnlyList<ResourceMutationReceipt> history = [];
    private string? path;
    public ResourceWorkspaceViewModel(CompanyFileViewModel company, StudyWorkspaceViewModel studies, ICurrencyCatalog catalog,
        UiTextCatalog texts, PresentationPreferencesService preferences, IModelEditorDialogs dialogs)
    {
        this.company = company; this.studies = studies; this.catalog = catalog; this.texts = texts; this.preferences = preferences; this.dialogs = dialogs;
        RefreshCommand = new AsyncRelayCommand(async () => { if (await CanLeaveAsync()) await EnterAsync(true); }, () => !busy);
        OpenSelectedResourceCommand = new AsyncRelayCommand(OpenSelectedAccountAsync, () => CanNavigate && workspace is not null && ResourceChoice is not null);
        ApplyPartCommand = new RelayCommand(() => Try(ApplyPart), () => CanEdit && editing is not null);
        RemovePartCommand = new RelayCommand(() => Try(() => { if (PartChoice is { } p) { parts.RemoveAll(x => x.Id == p.Value.Id); dirty = true; ClearPart(); Recompute(); } }), () => CanEdit && PartChoice is not null);
        LoadPartCommand = new RelayCommand(() => Try(LoadPart), () => !busy && PartChoice is not null && !partDirty);
        ClearPartCommand = new RelayCommand(ClearPart, () => CanEdit);
        AddPendingCommand = new RelayCommand(() => Try(AddPending), () => CanEdit && editing is not null && !partDirty);
        AddAdjustmentCommand = new RelayCommand(() => Try(AddAdjustment), () => CanEdit && editing is not null && !IsRevaluing);
        ReverseAdjustmentCommand = new RelayCommand(() => Try(ReverseAdjustment), () => CanEdit && AdjustmentChoice is not null && !IsRevaluing);
        ClearAdjustmentCommand = new RelayCommand(ClearAdjustment, () => CanEdit);
        PutObjectCommand = new AsyncRelayCommand(SaveObject, () => CanEdit && !IsRevaluing);
        ClearObjectCommand = new RelayCommand(ClearObject, () => CanEdit);
        DiscardCommand = new RelayCommand(Discard, () => !busy && IsDirty);
        CancelCommand = new RelayCommand(() => cancellation?.Cancel(), () => busy);
        ResetCommand = new AsyncRelayCommand(Reset, () => CanEdit && !IsDirty);
        AcknowledgeCultureCommand = new RelayCommand(() => { cultureAcknowledged = true; Refresh(); }, () => CultureChanged && !busy);
        InitializeRevalueCommands();
        PartForm.Edited += PartEdited; AdjustmentForm.Edited += AdjustmentEdited; ObjectForm.Edited += ObjectEdited;
        earlierBusy = company.WorkspaceBusy; earlierBefore = studies.BeforeChangeAsync; earlierAfter = company.AfterChangeAsync;
        company.WorkspaceBusy = () => busy || correctionCommitPending || earlierBusy?.Invoke() == true;
        studies.BeforeChangeAsync = async () => await CanLeaveAsync() && (earlierBefore is null || await earlierBefore());
        company.AfterChangeAsync = async () => { if (earlierAfter is not null) await earlierAfter(); ClearWorkspace(); };
        preferences.Changed += PreferencesChanged;
        ClearPart(); ClearAdjustment(); ClearObject();
    }
    public ResourceEditorForm PartForm { get; } = new();
    public ResourceEditorForm AdjustmentForm { get; } = new();
    public ResourceEditorForm ObjectForm { get; } = new();
    [ObservableProperty] private ResourceGridRow? resourceChoice;
    [ObservableProperty] private ResourcePartRow? partChoice;
    [ObservableProperty] private ResourceAdjustmentRow? adjustmentChoice;
    partial void OnResourceChoiceChanged(ResourceGridRow? value) => RefreshCommands();
    partial void OnPartChoiceChanged(ResourcePartRow? value) => RefreshCommands();
    partial void OnAdjustmentChoiceChanged(ResourceAdjustmentRow? value) => RefreshCommands();
    public ObservableCollection<ResourceGridRow> Rows { get; } = [];
    public ObservableCollection<ResourcePartRow> Parts { get; } = [];
    public ObservableCollection<ResourceAdjustmentRow> Adjustments { get; } = [];
    public IReadOnlyList<string> CurrencyOptions => catalog.GetAll().Where(c => c.IsMonetary).Select(c => c.AlphaCode.ToString()).ToArray();
    public IReadOnlyList<ResourceChoice<ResourceRoutingKind>> Routes => Choices<ResourceRoutingKind>();
    public IReadOnlyList<ResourceChoice<ResourceBehavior>> Behaviors => Choices<ResourceBehavior>();
    public IReadOnlyList<ResourceChoice<ResourceCostLayer>> Layers => Choices<ResourceCostLayer>();
    public IReadOnlyList<ResourceChoice<ResourceCapacityTreatment>> Capacities => Choices<ResourceCapacityTreatment>();
    private IReadOnlyList<ResourceChoice<T>> Choices<T>() where T : struct, Enum => Enum.GetValues<T>().Select(v => new ResourceChoice<T>(v, this[v.ToString()])).ToArray();
    public IReadOnlyList<ResourceChoice<EntityId>> Pools => workspace?.Model.Structure.Pools.Where(p => workspace.Model.Structure.Nodes.Any(n => n.Id == p.NodeId && n.IsActive))
        .Select(p => new ResourceChoice<EntityId>(p.Id, workspace.Model.Structure.Nodes.Single(n => n.Id == p.NodeId).Code + " · " + p.Description)).ToArray() ?? [];
    public IReadOnlyList<ResourceChoice<UnitId>> Units => workspace?.Model.DriverUnits.Select(u => new ResourceChoice<UnitId>(u.Id, u.Code + " · " + u.Symbol)).ToArray() ?? [];
    public IReadOnlyList<ResourceChoice<EntityId>> Objects => workspace?.Ledger.Objects.Select(o => new ResourceChoice<EntityId>(o.Id, o.Code + " · " + o.Name)).ToArray() ?? [];
    public string this[string key] => Text("Resources." + key);
    public string Guide(string key) => Text("Guide." + key);
    private CultureInfo Format => CultureInfo.GetCultureInfo(preferences.Current.FormatCultureTag);
    private string Text(string key) { var s = texts.Get(key, CultureInfo.GetCultureInfo(preferences.Current.UiLanguageTag)); return pseudo ? PseudoLocalization.Expand(s) : s; }
    private string MoneyText(Money m) => m.Amount.ToString("0.########", Format) + " " + m.Currency;
    public bool IsBusy => busy;
    public bool CanNavigate => !busy && !correctionCommitPending;
    public bool CanEditAdjustmentsAndObjects => CanEdit && !IsRevaluing;
    public bool CanEdit => !busy && company.IsIdle && earlierBusy?.Invoke() != true && company.CanWrite && workspace?.Study.State == StudyState.Open;
    public bool IsDirty => dirty || partDirty || adjustmentDirty || objectDirty || IsRevaluing;
    public bool HasResourceChanges => dirty || partDirty || adjustmentDirty;
    public int Stage => stage;
    public bool ShowList => true;
    public bool ShowAdjustments => false;
    public bool ShowParts => stage == 2;
    public bool ShowReview => stage == 2;
    public bool ShowDirect => PartForm.Route == ResourceRoutingKind.DirectObject;
    public bool ShowPool => PartForm.Route == ResourceRoutingKind.AllocationPool;
    public bool ShowExclusion => PartForm.Route == ResourceRoutingKind.Excluded;
    public bool ShowContext => PartForm.Behavior is ResourceBehavior.Fixed or ResourceBehavior.Variable or ResourceBehavior.MixedPending || PartForm.UnitId.HasValue;
    public bool ShowCapacity => PartForm.Capacity == ResourceCapacityTreatment.AssessAtPool;
    public bool CanAdvance => CanNavigate && workspace is not null && (stage == 0 || CanEdit);
    public string NextAction => this[stage == 0 ? "EditAccount" : IsRevaluing ? "KeepRevalueParts" : "SaveAccount"];
    public string Requirement => this[workspace is null ? "NeedStudy" : workspace.Resources.Count == 0 ? "NeedImport" : !company.CanWrite ? "ReadOnly" :
        workspace.Study.State == StudyState.Closed ? "Closed" : stage == 0 ? "ChooseAccount" : "PartsHelp"];
    public string Status => diagnostic is null ? this[statusKey] : new DomainDiagnosticFormatter(texts)
        .Format(diagnostic, CultureInfo.GetCultureInfo(preferences.Current.UiLanguageTag), Format).Message;
    public string ProgressSummary
    {
        get
        {
            var current = preview ?? workspace;
            if (current is null || current.Preparation.Coverage.Count == 0) return "";
            var coverage = current.Preparation.Coverage;
            var reconciled = coverage.Count(c => c.Difference.Amount == 0m);
            var pending = coverage.Count(c => c.Pending.Amount != 0m);
            var unexplained = coverage.Count(c => c.Difference.Amount != 0m);
            return string.Format(Format, this["ProgressSummary"], reconciled, coverage.Count, pending, unexplained);
        }
    }
    public string CurrentAccount => workspace is null || editing is null ? "" : workspace.Origins.Single(o => o.Id == editing.Id).Code;
    public string SaveDestination => workspace is null ? "" : workspace.Study.Name + " · " + company.ActivePath;
    public string Reason { get => reason; set { if (SetProperty(ref reason, value) && !loading) { dirty = true; Refresh(); } } }
    public bool CultureChanged => !cultureAcknowledged && new[] { PartForm, AdjustmentForm, ObjectForm }.Concat(RevalueRows.Select(r => r.Form))
        .Any(f => f.Numbers.CapturedCulture.Name != Format.Name) && IsDirty;
    public string CultureMessage => string.Format(Format, this["CultureChanged"], PartForm.Numbers.CapturedCulture.Name);
    public string CoverageText => preview is null || editing is null ? "" : Describe(preview.Preparation.Coverage.Single(c => c.ResourceId == editing.Id));
    private string Describe(ResourceCoverage c) => string.Format(Format, this["Coverage"], MoneyText(c.ConvertedOriginal), MoneyText(c.AdjustmentTotal), MoneyText(c.Effective),
        MoneyText(c.Direct), MoneyText(c.Pools), MoneyText(c.Excluded), MoneyText(c.Pending), MoneyText(c.Difference));
    public string Warnings => preview is null ? "" : string.Join(Environment.NewLine, preview.Preparation.Warnings.Select(d =>
        new DomainDiagnosticFormatter(texts).Format(d, CultureInfo.GetCultureInfo(preferences.Current.UiLanguageTag), Format).Message));
    public string Sources => workspace is null || editing is null ? "" : string.Join(Environment.NewLine, workspace.Origins.Single(o => o.Id == editing.Id).Rows.Select(r =>
        r.FileName + " · " + this["Record"] + " " + r.RecordNumber.ToString(Format) + (r.Earlier ? " · " + this["Earlier"] : "") + " · " + MoneyText(r.Conversion.OriginalMoney)));
    public string History => string.Join(Environment.NewLine, history.Select(h => string.Format(Format, this["HistoryRow"], h.ConfirmedStudy.Revision,
        h.AtUtc.ToString(), h.Request.Actor, this[h.Request.Kind.ToString()], h.Request.Reason)));
    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand OpenSelectedResourceCommand { get; }
    public IRelayCommand ApplyPartCommand { get; }
    public IRelayCommand RemovePartCommand { get; }
    public IRelayCommand LoadPartCommand { get; }
    public IRelayCommand ClearPartCommand { get; }
    public IRelayCommand AddPendingCommand { get; }
    public IRelayCommand AddAdjustmentCommand { get; }
    public IRelayCommand ReverseAdjustmentCommand { get; }
    public IRelayCommand ClearAdjustmentCommand { get; }
    public IAsyncRelayCommand PutObjectCommand { get; }
    public IRelayCommand ClearObjectCommand { get; }
    public IRelayCommand DiscardCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public IAsyncRelayCommand ResetCommand { get; }
    public IRelayCommand AcknowledgeCultureCommand { get; }

    public async Task EnterAsync(bool force = false)
    {
        if (busy) return;
        var current = studies.Current;
        if (current is null || company.ActiveResources is null) { ClearWorkspace(); return; }
        if (!force && workspace?.Study.Id == current.Id && workspace.Study.Revision == current.Revision && path == company.ActivePath) { Refresh(); return; }
        if (IsDirty) { diagnostic = ResourceWorkspaceRules.Error("RES_RELOAD_PENDING", "Resource.ReloadPending").Diagnostic; Refresh(); return; }
        await Run(async token =>
        {
            var port = company.ActiveResources!;
            var data = await Task.Run(() => (port.ReadResources(current.CompanyId, current.Id, token), port.ReadResourceHistory(current.CompanyId, current.Id, token)), token);
            workspace = data.Item1; history = data.Item2; path = company.ActivePath; preview = workspace; editing = null; stage = 0;
            statusKey = "Ready"; RebuildRows(); Refresh();
        });
    }
    public async Task AdvanceAsync()
    {
        if (!CanAdvance) return;
        if (stage == 0) await OpenSelectedAccountAsync();
        else if (stage == 2)
        {
            if (partDirty || objectDirty) { statusKey = "ApplyPartFirst"; Refresh(); return; }
            if (IsRevaluing) KeepRevalueParts();
            else await SaveAsync();
        }
        else if (IsRevaluing) KeepRevalueParts();
        else await SaveAsync();
    }
    private async Task OpenSelectedAccountAsync()
    {
        if (!CanNavigate || workspace is null) return;

            if (ResourceChoice is null) { statusKey = "SelectAccountFirst"; Refresh(); return; }
            if (editing?.Id == ResourceChoice.Id) { stage = 2; Refresh(); return; }
            if (IsRevaluing && editing is not null && HasResourceChanges)
            {
                ResourceChoice = Rows.Single(r => r.Id == editing.Id);
                stage = 2; statusKey = "RevalueFinishAccount"; Refresh(); return;
            }
            var selectedId = ResourceChoice.Id;
            if (!IsRevaluing && !await CanLeaveAsync()) return;
            LoadAccount(selectedId); stage = 2; Refresh();
    }
    public bool PreviousStep() { if (!CanNavigate || stage == 0) return false; stage = 0; Refresh(); return true; }
    private void LoadAccount(EntityId id)
    {
        editing = (IsRevaluing ? preview : workspace)!.Resources.Single(r => r.Id == id);
        parts.Clear(); parts.AddRange(editing.Parts); deltas.Clear(); dirty = false; reason = "";
        ClearPart(); ClearAdjustment(); ClearObject(); Recompute();
    }
    private void ClearWorkspace() { workspace = preview = null; history = []; editing = null; Discard(); RebuildRows(); Refresh(); }
    private void Discard()
    {
        dirty = false; editing = null; parts.Clear(); deltas.Clear(); reason = ""; stage = 0; preview = workspace; CancelRevalue();
        ClearPart(); ClearAdjustment(); ClearObject(); statusKey = "DraftDiscarded"; diagnostic = null; RebuildRows(); Refresh();
    }
    public async Task<bool> CanLeaveAsync()
    {
        if (busy || correctionCommitPending) return false;
        if (!IsDirty) return true;
        return dialogs.ResolvePending(this["PendingTitle"], this["PendingBody"]) switch
        {
            PendingEditChoice.Cancel => false,
            PendingEditChoice.Discard => DiscardAndLeave(),
            _ => await SaveAsync()
        };
    }
    private bool DiscardAndLeave() { Discard(); return true; }
    public async Task<bool> SaveAsync()
    {
        if (!CanEdit || workspace is null || editing is null || IsRevaluing) return false;
        if (partDirty || adjustmentDirty || objectDirty) { statusKey = "ApplyFormsFirst"; Refresh(); return false; }
        if (!CheckCulture()) return false;
        return await Run(async token =>
        {
            var command = EditCommand(Reason); _ = ResourceWorkspaceRules.Apply(workspace, command);
            await Commit(command, token);
        });
    }
    private ResourceMutation EditCommand(string explanation) => new(ResourceMutationKind.EditResource, workspace!.Study.CompanyId, workspace.Study.Id,
        workspace.Study.Revision, new(Guid.NewGuid()), "local-user", explanation, editing!.Id, parts.ToArray(), deltas.ToArray(), null, null, [], []);
    private async Task Commit(ResourceMutation command, CancellationToken token)
    {
        var port = company.ActiveResources!;
        var receipt = await Task.Run(() => port.ChangeResources(command, token), token);
        workspace = new(receipt.ConfirmedStudy, workspace!.Precision, workspace.Model, workspace.Origins, receipt.Ledger);
        history = history.Append(receipt).ToArray(); Discard(); statusKey = "Saved";
        // Apply a successful receipt before refreshing dependent panels; a refresh failure must not invite duplicate saving.
        try { await studies.ApplyResourceReceiptAsync(receipt.ConfirmedStudy); }
        catch (Exception e) when (e is IOException or CompanyFileException or InvalidOperationException) { statusKey = "SavedRefreshPending"; }
        RebuildRows(); Refresh();
    }
    private async Task<bool> Run(Func<CancellationToken, Task> work)
    {
        if (busy) return false;
        busy = true; diagnostic = null; cancellation = new(); company.NotifyWorkspaceStateChanged(); Refresh();
        try { await work(cancellation.Token); return true; }
        catch (OperationCanceledException) { statusKey = "Cancelled"; return false; }
        catch (DomainRuleException e) { diagnostic = e.Diagnostic; return false; }
        catch (CompanyFileException e) { diagnostic = e.Diagnostic; return false; }
        catch (Exception e) when (e is ArgumentException or OverflowException or InvalidOperationException or IOException)
        { diagnostic = ResourceWorkspaceRules.Error("RES_INPUT_INVALID", "Resource.InputInvalid").Diagnostic; return false; }
        finally { cancellation.Dispose(); cancellation = null; busy = false; company.NotifyWorkspaceStateChanged(); Refresh(); }
    }
    private void Try(Action action)
    {
        diagnostic = null;
        try { if (CheckCulture()) action(); }
        catch (DomainRuleException e) { diagnostic = e.Diagnostic; }
        catch (Exception e) when (e is ArgumentException or OverflowException or InvalidOperationException)
        { diagnostic = ResourceWorkspaceRules.Error("RES_INPUT_INVALID", "Resource.InputInvalid").Diagnostic; }
        Refresh();
    }
    private bool CheckCulture() { if (!CultureChanged) return true; statusKey = "ConfirmCultureFirst"; Refresh(); return false; }
    private void Recompute()
    {
        if (editing is null || workspace is null) return;
        if (IsRevaluing) RecomputeRevalue();
        else preview = ResourceWorkspaceRules.Preview(workspace, EditCommand("Preview"));
        RebuildRows(); Refresh();
    }
    private void RebuildRows()
    {
        var id = ResourceChoice?.Id; Rows.Clear();
        foreach (var resource in (preview ?? workspace)?.Resources ?? [])
        {
            var w = preview ?? workspace!; var c = w.Preparation.Coverage.Single(c => c.ResourceId == resource.Id);
            var originals = string.Join(" + ", ResourceWorkspaceRules.OriginalTotals(resource, w.Precision).Select(MoneyText));
            Rows.Add(new(resource.Id, w.Origins.Single(o => o.Id == resource.Id).Code, originals, MoneyText(c.ConvertedOriginal),
                MoneyText(c.AdjustmentTotal), MoneyText(c.Effective), MoneyText(c.Pending), MoneyText(c.Difference))
                { Revision = w.Study.Revision, CorrectionNumber = c.AdjustmentTotal.Amount.ToString("0.########", Format), FormatCulture = Format.Name });
        }
        ResourceChoice = Rows.FirstOrDefault(r => r.Id == id) ?? Rows.FirstOrDefault();
        var oldPart = PartChoice?.Value.Id; Parts.Clear();
        foreach (var p in parts) Parts.Add(new(p, MoneyText(p.Amount), this[p.Routing.ToString()],
            p.PoolId is { } pool ? Pools.FirstOrDefault(o => o.Value == pool)?.Label ?? pool.ToString() :
            p.Routing == ResourceRoutingKind.DirectObject ? string.Join("; ", p.DirectAssignments.Select(a => (Objects.FirstOrDefault(o => o.Value == a.ObjectId)?.Label ?? a.ObjectId.ToString()) + " = " + MoneyText(a.Amount))) : p.ExclusionReason ?? "",
            this[p.Classification.Behavior.ToString()], this[p.Classification.Layer.ToString()]));
        PartChoice = Parts.FirstOrDefault(p => p.Value.Id == oldPart);
        var oldAdjustment = AdjustmentChoice?.Value.Id; Adjustments.Clear();
        foreach (var a in preview?.Resources.FirstOrDefault(r => r.Id == editing?.Id)?.Adjustments ?? [])
            Adjustments.Add(new(a, MoneyText(a.Conversion.OriginalMoney), MoneyText(a.Conversion.ConvertedOriginalMoney), a.Type, a.Reason,
                this[a.ReversalOfId.HasValue ? "Reversal" : "Adjustment"]));
        AdjustmentChoice = Adjustments.FirstOrDefault(a => a.Value.Id == oldAdjustment);
    }
    private void PartEdited() { if (!loading) { partDirty = true; cultureAcknowledged = false; } Refresh(); }
    private void AdjustmentEdited() { if (!loading) { adjustmentDirty = true; cultureAcknowledged = false; } Refresh(); }
    private void ObjectEdited() { if (!loading) { objectDirty = true; cultureAcknowledged = false; } Refresh(); }
    public void RefreshPresentation(bool value) { pseudo = value; RebuildRows(); Refresh(); }
    private void PreferencesChanged(object? sender, PresentationPreferencesChangedEventArgs e) { cultureAcknowledged = false; RebuildRows(); Refresh(); }
    private void Refresh() { OnPropertyChanged(string.Empty); RefreshCommands(); }
    private void RefreshCommands()
    {
        foreach (var command in new IRelayCommand?[] { RefreshCommand, OpenSelectedResourceCommand, ApplyPartCommand, RemovePartCommand, LoadPartCommand, ClearPartCommand, AddPendingCommand,
            AddAdjustmentCommand, ReverseAdjustmentCommand, ClearAdjustmentCommand, PutObjectCommand, ClearObjectCommand, DiscardCommand, CancelCommand, ResetCommand,
            AcknowledgeCultureCommand, PrepareRevalueCommand, CommitRevalueCommand, CancelRevalueCommand, StartRevalueCommand }) command?.NotifyCanExecuteChanged();
    }
    public void Dispose()
    {
        cancellation?.Cancel(); preferences.Changed -= PreferencesChanged;
        company.WorkspaceBusy = earlierBusy; studies.BeforeChangeAsync = earlierBefore; company.AfterChangeAsync = earlierAfter;
    }
}
