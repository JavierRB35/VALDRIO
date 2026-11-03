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
    private readonly WorkspaceChoices choices = new();
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
    private IReadOnlyList<AccountDescriptionChoice> accountDescriptions = [];
    private string? path;
    public ResourceWorkspaceViewModel(CompanyFileViewModel company, StudyWorkspaceViewModel studies, ICurrencyCatalog catalog,
        UiTextCatalog texts, PresentationPreferencesService preferences, IModelEditorDialogs dialogs)
    {
        this.company = company; this.studies = studies; this.catalog = catalog; this.texts = texts; this.preferences = preferences; this.dialogs = dialogs;
        RefreshCommand = new AsyncRelayCommand(async () => { if (await CanLeaveAsync()) await EnterAsync(true); }, () => !busy);
        CloseDestinationsCommand = new AsyncRelayCommand(CloseDestinationsAsync, () => CanNavigate && ShowParts);
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
        DiscardCommand = new RelayCommand(() => { RuleCards.Clear(); RebuildRuleCards(); Discard(); }, () => !busy && IsDirty);
        CancelCommand = new RelayCommand(() => cancellation?.Cancel(), () => busy);
        ResetCommand = new AsyncRelayCommand(Reset, () => CanEdit && !IsDirty);
        AcknowledgeCultureCommand = new RelayCommand(() => { cultureAcknowledged = true; Refresh(); }, () => CultureChanged && !busy);
        InitializeRevalueCommands(); InitializeRuleCommands();
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
    // Raised synchronously on the UI thread before replacing selected DataGrid
    // items. The WPF automation peer cannot describe a removed cell whose item
    // has become null while a virtualized grid generates its containers.
    public event EventHandler? RowsRebuilding;
    public ObservableCollection<ResourceGridRow> Rows { get; } = [];
    public ObservableCollection<ResourcePartRow> Parts { get; } = [];
    public ObservableCollection<ResourceAdjustmentRow> Adjustments { get; } = [];
    public IReadOnlyList<ResourceChoice<ResourceRoutingKind>> Routes => choices.Get<ResourceChoice<ResourceRoutingKind>>((workspace, preferences.Current.UiLanguageTag, preferences.Current.FormatCultureTag, pseudo), () => Choices<ResourceRoutingKind>());
    public IReadOnlyList<ResourceChoice<ResourceBehavior>> Behaviors => choices.Get<ResourceChoice<ResourceBehavior>>((workspace, preferences.Current.UiLanguageTag, preferences.Current.FormatCultureTag, pseudo), () => Choices<ResourceBehavior>());
    public IReadOnlyList<ResourceChoice<ResourceCostLayer>> Layers => choices.Get<ResourceChoice<ResourceCostLayer>>((workspace, preferences.Current.UiLanguageTag, preferences.Current.FormatCultureTag, pseudo), () => Choices<ResourceCostLayer>());
    public IReadOnlyList<ResourceChoice<ResourceCapacityTreatment>> Capacities => choices.Get<ResourceChoice<ResourceCapacityTreatment>>((workspace, preferences.Current.UiLanguageTag, preferences.Current.FormatCultureTag, pseudo), () => Choices<ResourceCapacityTreatment>());
    private IReadOnlyList<ResourceChoice<T>> Choices<T>() where T : struct, Enum => Enum.GetValues<T>().Select(v => new ResourceChoice<T>(v, this[v.ToString()])).ToArray();
    public IReadOnlyList<ResourceChoice<EntityId>> Pools => choices.Get<ResourceChoice<EntityId>>((workspace, preferences.Current.UiLanguageTag, preferences.Current.FormatCultureTag, pseudo), () => workspace?.Model.Structure.Pools
        .Where(p => workspace.Model.Structure.Nodes.Any(n => n.Id == p.NodeId && n.IsActive))
        .Select(p => new ResourceChoice<EntityId>(p.Id, PoolLabel(p))).OrderBy(p => p.Label, StringComparer.Ordinal).ToArray() ?? []);
    private string PoolLabel(CostPoolDefinition pool)
    {
        var model = workspace!.Model;
        var node = model.Structure.Nodes.Single(n => n.Id == pool.NodeId);
        var center = model.PhysicalCenters.Single(c => c.Id == node.PhysicalCenterId);
        var unit = model.DriverUnits.Single(u => u.Id == pool.DriverUnitId);
        var description = string.IsNullOrWhiteSpace(pool.Description) ? this["PoolWithoutDescription"] : pool.Description;
        return string.Format(Format, this["PoolChoice"], center.Code + " · " + center.Name, node.Code,
            Text(node.Role == NodeRole.Principal ? "Model.Principal" : "Model.Auxiliary"), description,
            unit.Code + " · " + unit.Symbol, string.Join(", ", node.FunctionTags));
    }
    public IReadOnlyList<ResourceChoice<UnitId>> Units => choices.Get<ResourceChoice<UnitId>>((workspace, preferences.Current.UiLanguageTag, preferences.Current.FormatCultureTag, pseudo), () => workspace?.Model.DriverUnits.Select(u => new ResourceChoice<UnitId>(u.Id, u.Code + " · " + u.Symbol)).ToArray() ?? []);
    public IReadOnlyList<ResourceChoice<EntityId>> Objects => choices.Get<ResourceChoice<EntityId>>((workspace, preferences.Current.UiLanguageTag, preferences.Current.FormatCultureTag, pseudo), () => workspace?.Ledger.Objects.Select(o => new ResourceChoice<EntityId>(o.Id, o.Code + " · " + o.Name)).ToArray() ?? []);
    public string this[string key] => Text("Resources." + key);
    public string Guide(string key) => Text("Guide." + key);
    private CultureInfo Format => CultureInfo.GetCultureInfo(preferences.Current.FormatCultureTag);
    private string Text(string key) { var s = texts.Get(key, CultureInfo.GetCultureInfo(preferences.Current.UiLanguageTag)); return pseudo ? PseudoLocalization.Expand(s) : s; }
    private string MoneyText(Money m) => m.Amount.ToString("0.########", Format) + " " + m.Currency;
    // Resource totals are derived from the current economic coverage, never from
    // formatted table strings. Do not add balances in different original currencies.
    private string SumResourceCoverage(Func<ResourceCoverage, Money> field)
    {
        var current = preview ?? workspace;
        var coverage = current?.Preparation.Coverage;
        if (coverage is null || coverage.Count == 0) return "";
        var sum = coverage.Sum(row => field(row).Amount);
        return sum.ToString("0.########", Format) + " " + field(coverage[0]).Currency;
    }
    public string ResourcesTotalOriginal
    {
        get
        {
            var current = preview ?? workspace;
            if (current is null) return "";
            return string.Join(" / ", current.Resources
                .SelectMany(resource => ResourceWorkspaceRules.OriginalTotals(resource, current.Precision))
                .GroupBy(m => m.Currency)
                .OrderBy(group => group.Key.ToString(), StringComparer.Ordinal)
                .Select(group => group.Sum(m => m.Amount).ToString("0.########", Format) + " " + group.Key));
        }
    }
    public string ResourcesTotalConverted => SumResourceCoverage(c => c.ConvertedOriginal);
    public string ResourcesTotalAdjustments => SumResourceCoverage(c => c.AdjustmentTotal - AdjustmentRuleEngine.RuleTotal((preview ?? workspace)!, c.ResourceId));
    public string ResourcesTotalEffective => SumResourceCoverage(c => c.Effective);
    public string ResourcesTotalPending => SumResourceCoverage(c => c.Pending);
    public string ResourcesTotalDifference => SumResourceCoverage(c => c.Difference);
    // Parts and review show the same selected account, not an extra economic layer.
    public string ResourcePartsTotal => parts.Count == 0 ? "" :
        parts.Sum(part => part.Amount.Amount).ToString("0.########", Format) + " " + parts[0].Amount.Currency;

    public bool IsBusy => busy;
    public bool CanNavigate => !busy && !correctionCommitPending;
    public bool CanEditAdjustmentsAndObjects => CanEdit && !IsRevaluing;
    private bool primaryClosed;
    public bool CanEdit => !primaryClosed && !busy && company.IsIdle && earlierBusy?.Invoke() != true && company.CanWrite && workspace?.Study.State == StudyState.Open;
    public bool IsDirty => dirty || partDirty || adjustmentDirty || objectDirty || IsRevaluing || RuleCards.Any(c => c.IsDirty);
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
    public string Requirement => this[primaryClosed ? "PrimaryClosed" : workspace is null ? "NeedStudy" : workspace.Resources.Count == 0 ? "NeedImport" : !company.CanWrite ? "ReadOnly" :
        workspace.Study.State == StudyState.Closed ? "Closed" : stage == 0 ? "ChooseAccount" : "PartsHelp"];
    // UI-02: distinguish a domain validation error from neutral operation feedback.
    public bool HasStatusError => diagnostic is not null;
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
            var pendingRoutes = coverage.Count(c => c.HasPendingParts);
            return string.Format(Format, this["ProgressSummary"], reconciled, coverage.Count, pending, unexplained, pendingRoutes);
        }
    }
    public string CurrentAccount => workspace is null || editing is null ? "" : AccountLabel(workspace.Origins.Single(o => o.Id == editing.Id));
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
    public IAsyncRelayCommand CloseDestinationsCommand { get; }
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
            var data = await Task.Run(() => (port.ReadResources(current.CompanyId, current.Id, token), port.ReadResourceHistory(current.CompanyId, current.Id, token), company.ActiveAllocations!.ReadAllocations(current.CompanyId, current.Id, token).Configuration), token);
            savedRules = await Task.Run(() => port.ReadAdjustmentRules(current.CompanyId, token), token);
            workspace = data.Item1; history = data.Item2; accountDescriptions = data.Item3.Descriptions; primaryClosed = data.Item3.PrimaryClosed; path = company.ActivePath; preview = workspace; editing = null; stage = 0;
            statusKey = "Ready"; RebuildRuleCards(); RebuildRows(); Refresh();
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
    public async Task<bool> CloseDestinationsAsync()
    {
        if (!CanNavigate) return false;
        if (!await CanLeaveAsync()) return false;
        if (IsRevaluing) { stage = 0; editing = null; parts.Clear(); ClearPart(); RebuildRows(); }
        else Discard();
        statusKey = "BackToCorrections"; correctionFailed = false; Refresh();
        return true;
    }
    public bool PreviousStep() { if (!CanNavigate || stage == 0) return false; stage = 0; Refresh(); return true; }
    private void LoadAccount(EntityId id)
    {
        editing = (IsRevaluing ? preview : workspace)!.Resources.Single(r => r.Id == id);
        parts.Clear(); parts.AddRange(editing.Parts); deltas.Clear(); dirty = false; reason = "";
        ClearPart(); ClearAdjustment(); ClearObject(); Recompute();
    }
    private void ClearWorkspace() { RuleCards.Clear(); savedRules = []; primaryClosed = false; workspace = preview = null; history = []; editing = null; Discard(); RebuildRows(); Refresh(); }
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
            _ => await SavePendingAndLeaveAsync()
        };
    }
    private bool DiscardAndLeave() { RuleCards.Clear(); RebuildRuleCards(); Discard(); return true; }
    public async Task<bool> SavePendingAndLeaveAsync()
    {
        if (!CanEdit || !CheckCulture()) return false;
        if (IsRevaluing) { statusKey = "ApplyFormsFirst"; Refresh(); return false; }
        foreach (var card in RuleCards.Where(c => c.IsDirty).ToArray()) { await SaveRule(card); if (card.IsDirty) return false; }
        if (objectDirty) { await SaveObject(); if (objectDirty) return false; }
        if (adjustmentDirty) { Try(AddAdjustment); if (adjustmentDirty) return false; }
        if (partDirty) { Try(ApplyPart); if (partDirty) return false; }
        return !IsDirty || await SaveAsync();
    }
    public async Task<bool> SaveAsync()
    {
        if (!CanEdit || workspace is null || editing is null || IsRevaluing) return false;
        if (partDirty || adjustmentDirty || objectDirty) { statusKey = "ApplyFormsFirst"; Refresh(); return false; }
        if (!CheckCulture()) return false;
        if (string.IsNullOrWhiteSpace(Reason)) return RejectInput("RES_COMMAND_INVALID", "Resource.CommandInvalid");
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
    private string AccountLabel(ResourceOrigin origin)
    {
        var chosen = accountDescriptions.SingleOrDefault(d => d.Account == origin.Code)?.Description;
        return origin.Code + " — " + (chosen is not null && origin.Descriptions.Contains(chosen, StringComparer.Ordinal) ? chosen : origin.Descriptions.Count == 0
            ? this["DescriptionMissing"] : origin.HasDescriptionConflict
            ? this["DescriptionConflict"] + ": " + string.Join(" / ", origin.Descriptions) : origin.Descriptions[0]);
    }
    private void RebuildRows()
    {
        // Preserve stable domain IDs before the view clears obsolete visual cells.
        var id = ResourceChoice?.Id;
        var oldPart = PartChoice?.Value.Id;
        var oldAdjustment = AdjustmentChoice?.Value.Id;
        RowsRebuilding?.Invoke(this, EventArgs.Empty);
        Rows.Clear();
        foreach (var resource in (preview ?? workspace)?.Resources ?? [])
        {
            var w = preview ?? workspace!; var c = w.Preparation.Coverage.Single(c => c.ResourceId == resource.Id);
            var ruleTotal = AdjustmentRuleEngine.RuleTotal(w, resource.Id);
            var manual = c.AdjustmentTotal - ruleTotal;
            var originals = string.Join(" + ", ResourceWorkspaceRules.OriginalTotals(resource, w.Precision).Select(MoneyText));
            Rows.Add(new(resource.Id, w.Origins.Single(o => o.Id == resource.Id).Code, originals, MoneyText(c.ConvertedOriginal),
                MoneyText(manual), MoneyText(c.Effective), MoneyText(c.Pending), MoneyText(c.Difference))
                { DisplayName = AccountLabel(w.Origins.Single(o => o.Id == resource.Id)), Revision = w.Study.Revision, RuleAdjustments = MoneyText(ruleTotal), CorrectionNumber = manual.Amount.ToString("0.########", Format), FormatCulture = Format.Name, AssignmentStatus = this[c.HasPendingParts ? "AssignmentPending" : "AssignmentComplete"] });
        }
        ResourceChoice = Rows.FirstOrDefault(r => r.Id == id) ?? Rows.FirstOrDefault();
        Parts.Clear();
        foreach (var p in parts) Parts.Add(new(p, MoneyText(p.Amount), this[p.Routing.ToString()],
            p.PoolId is { } pool ? Pools.FirstOrDefault(o => o.Value == pool)?.Label ?? pool.ToString() :
            p.Routing == ResourceRoutingKind.DirectObject ? string.Join("; ", p.DirectAssignments.Select(a => (Objects.FirstOrDefault(o => o.Value == a.ObjectId)?.Label ?? a.ObjectId.ToString()) + " = " + MoneyText(a.Amount))) : p.ExclusionReason ?? "",
            this[p.Classification.Behavior.ToString()], this[p.Classification.Layer.ToString()]));
        PartChoice = Parts.FirstOrDefault(p => p.Value.Id == oldPart);
        Adjustments.Clear();
        foreach (var a in preview?.Resources.FirstOrDefault(r => r.Id == editing?.Id)?.Adjustments ?? [])
            Adjustments.Add(new(a, MoneyText(a.Conversion.OriginalMoney), MoneyText(a.Conversion.ConvertedOriginalMoney), a.Type, a.Reason,
                this[a.ReversalOfId.HasValue ? "Reversal" : "Adjustment"]));
        AdjustmentChoice = Adjustments.FirstOrDefault(a => a.Value.Id == oldAdjustment);
    }
    private void PartEdited() { if (loading) return; partDirty = true; cultureAcknowledged = false; RefreshEditor(); }
    private void AdjustmentEdited() { if (loading) return; adjustmentDirty = true; cultureAcknowledged = false; RefreshEditor(); }
    private void ObjectEdited() { if (loading) return; objectDirty = true; cultureAcknowledged = false; RefreshEditor(); }
    private void RefreshEditor()
    {
        foreach (var property in new[] { nameof(IsDirty), nameof(HasResourceChanges), nameof(CultureChanged), nameof(CultureMessage),
            nameof(ShowDirect), nameof(ShowPool), nameof(ShowExclusion), nameof(ShowContext), nameof(ShowCapacity),
            nameof(ShowAdjustmentFx), nameof(ObjectKind), nameof(CanEditCorrections), nameof(CorrectionsReadOnly), nameof(CorrectionLockMessage) })
            OnPropertyChanged(property);
        RefreshCommands();
    }
    public void RefreshPresentation(bool value) { pseudo = value; RebuildRows(); Refresh(); }
    private void PreferencesChanged(object? sender, PresentationPreferencesChangedEventArgs e) { cultureAcknowledged = false; RebuildRows(); Refresh(); }
    private void Refresh() { OnPropertyChanged(string.Empty); RefreshCommands(); }
    private void RefreshCommands()
    {
        AddRuleCommand?.NotifyCanExecuteChanged();
        foreach (var card in RuleCards) card.RefreshState();
        foreach (var command in new IRelayCommand?[] { RefreshCommand, OpenSelectedResourceCommand, CloseDestinationsCommand, ApplyPartCommand, RemovePartCommand, LoadPartCommand, ClearPartCommand, AddPendingCommand,
            AddAdjustmentCommand, ReverseAdjustmentCommand, ClearAdjustmentCommand, PutObjectCommand, ClearObjectCommand, DiscardCommand, CancelCommand, ResetCommand,
            AcknowledgeCultureCommand, PrepareRevalueCommand, CommitRevalueCommand, CancelRevalueCommand, StartRevalueCommand }) command?.NotifyCanExecuteChanged();
    }
    public void Dispose()
    {
        cancellation?.Cancel(); preferences.Changed -= PreferencesChanged;
        company.WorkspaceBusy = earlierBusy; studies.BeforeChangeAsync = earlierBefore; company.AfterChangeAsync = earlierAfter;
    }
}
