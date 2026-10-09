using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Valdrio.Application;
using Valdrio.Calculation;
using Valdrio.Domain;
using Valdrio.Localization;
namespace Valdrio.Desktop;

public sealed partial class AllocationLineEditor(EntityId id, string label, string unit, bool included, string value) : ObservableObject
{
    public EntityId Id { get; } = id;
    public string Label { get; } = label;
    public string Unit { get; } = unit;
    [ObservableProperty] private bool included = included;
    [ObservableProperty] private string value = value;
}
public sealed record AllocationKeyRow(AllocationKeyVersion Value, string Name, string Criterion, string Stage, string State);
public sealed record AllocationRuleRow(EntityId Id, string Account, string Key, string Priority, string State);
public sealed record AllocationSecondaryRow(EntityId Id, string Source, string Group, string Key);
public sealed partial class AllocationCoverageRow(EntityId id, string source, string amount, string key, string status, EntityId? selectedKeyId) : ObservableObject
{
    public EntityId Id { get; } = id;
    [ObservableProperty] private string source = source;
    [ObservableProperty] private string amount = amount;
    [ObservableProperty] private string key = key;
    [ObservableProperty] private string status = status;
    [ObservableProperty] private EntityId? selectedKeyId = selectedKeyId;
}
public sealed record AllocationPartChoice(EntityId Value, string Label, Money Amount);

public sealed partial class AllocationWorkspaceViewModel : ObservableObject, IDisposable
{
    private readonly CompanyFileViewModel company;
    private readonly WorkspaceChoices choices = new();
    private readonly StudyWorkspaceViewModel studies;
    private readonly UiTextCatalog texts;
    private readonly PresentationPreferencesService preferences;
    private readonly IModelEditorDialogs dialogs;
    private readonly Func<bool>? earlierBusy;
    private readonly Func<Task<bool>>? earlierBefore;
    private readonly Func<Task>? earlierAfter;
    private AllocationWorkspaceSnapshot? workspace;
    private AllocationKeyVersion? editingKey;
    private EntityId? newParent, editingRule, editingSecondary;
    private bool busy, loading, keyDirty, ruleDirty, absoluteDirty, pseudo;
    private string? path;
    private string statusKey = "Ready";
    private DomainDiagnostic? diagnostic;
    private DecimalEditSession numbers;
    private AllocationStage stage;
    private AllocationKeyMode mode;
    private string name = "", ruleAccount = "", prefix = "", priority = "0", groupName = "", chosenDescription = "";
    private UnitId? unit;
    private bool selfAllowed;
    private AccountSelectorKind selectorKind;
    private EntityId? ruleKey, auxiliary, secondaryKey, part, partKey;
    private string? account;
    public AllocationWorkspaceViewModel(CompanyFileViewModel company, StudyWorkspaceViewModel studies,
        UiTextCatalog texts, PresentationPreferencesService preferences, IModelEditorDialogs dialogs)
    {
        this.company = company; this.studies = studies; this.texts = texts; this.preferences = preferences; this.dialogs = dialogs;
        numbers = new(Format);
        NewKeyCommand = new AsyncRelayCommand(async () => { if (await CanLeaveAsync()) { LoadKey(null); if (IsPrimaryClosed) Stage = AllocationStage.Secondary; } }, () => CanEdit || CanManageSecondary);
        LoadKeyCommand = new AsyncRelayCommand(async () => { if (SelectedKey is { } row && await CanLeaveAsync()) LoadKey(row.Value); }, () => !busy && SelectedKey is not null);
        NewVersionCommand = new AsyncRelayCommand(async () =>
        {
            if (SelectedKey?.Value is not { Status: ModelStatus.Published } source || !await CanLeaveAsync()) return;
            LoadKey(source); editingKey = null; newParent = source.Id; keyDirty = true; Refresh();
        }, () => (CanEdit || CanManageSecondary && SelectedKey?.Value.Stage == AllocationStage.Secondary) && SelectedKey?.Value.Status == ModelStatus.Published);
        DeleteKeyCommand = new AsyncRelayCommand(DeleteKeyAsync, () => (CanEdit || CanManageSecondary && SelectedKey?.Value.Stage == AllocationStage.Secondary) && !IsDirty && SelectedKey is { } row && !workspace!.UsedKeyIds.Contains(row.Value.Id));
        TogglePrimaryCommand = new AsyncRelayCommand(TogglePrimaryAsync, () => CanManagePrimary && !IsSecondaryClosed && !IsDirty && (IsPrimaryClosed || primaryComplete));
        SaveDraftCommand = new AsyncRelayCommand(async () => await SaveKeyAsync(false), () => CanEditKey && MissingKeyInput(false) is null);
        PublishCommand = new AsyncRelayCommand(async () => await SaveKeyAsync(true), () => CanEditKey && MissingKeyInput(true) is null);
        RefreshCommand = new AsyncRelayCommand(async () => { if (await CanLeaveAsync()) await EnterAsync(true); }, () => !busy);
        CheckReciprocalCommand = new RelayCommand(CheckReciprocal, () => workspace is not null && !busy);
        CalculateSecondaryCommand = new RelayCommand(CalculateSecondary, () => CanCalculateSecondary);
        ExplainSecondaryCommand = new RelayCommand(ShowSecondaryExplanation, () => SelectedSecondaryResultRow is not null);
        ToggleSecondaryCommand = new AsyncRelayCommand(ToggleSecondaryAsync,
            () => !busy && CanManagePrimary && !IsDirty && (IsSecondaryClosed || secondaryCalculated));
        SaveSecondaryGroupCommand = new AsyncRelayCommand(SaveSecondaryPartGroupAsync, () => CanSaveSecondaryGroup);
        AssignSecondaryKeyToMissingCommand = new AsyncRelayCommand(AssignSecondaryKeyToMissingAsync, () => CanAssignSecondaryKeyToMissing);
        ClearObsoleteSecondaryGroupsCommand = new AsyncRelayCommand(ClearObsoleteSecondaryGroupsAsync, () => CanEditSecondaryConfiguration && HasObsoleteSecondaryGroups);
        SaveRuleCommand = new AsyncRelayCommand(() => SaveRuleAsync(false), () => CanSaveRule);
        ApplyRuleCommand = new AsyncRelayCommand(() => SaveRuleAsync(true), () => CanSaveRule);
        SaveKeyCommand = new AsyncRelayCommand(async () => await SaveKeyAsync(true), () => CanEditKey && MissingKeyInput(true) is null);
        RemoveRuleCommand = new AsyncRelayCommand(() => CommitConfig(c => new(c.PrimaryRules.Where(r => r.Id != SelectedRule!.Id).ToArray(), c.SecondaryRules, c.ExplicitAssignments, c.AbsoluteAssignments, c.Descriptions)), () => CanEditConfiguration && !HasUnappliedRule && SelectedRule is not null);
        LoadRuleCommand = new RelayCommand(LoadRule, () => CanEdit && SelectedRule is not null && !ruleDirty);
        NewRuleCommand = new RelayCommand(ClearRule, () => CanEditConfiguration && !HasUnappliedRule);
        SaveSecondaryCommand = new AsyncRelayCommand(SaveSecondaryAsync, () => CanEditSecondaryConfiguration && Auxiliary.HasValue && SecondaryKey.HasValue);
        LoadSecondaryCommand = new RelayCommand(LoadSecondary, () => CanEditSecondaryConfiguration && SelectedSecondary is not null && !ruleDirty);
        RemoveSecondaryCommand = new AsyncRelayCommand(() => CommitConfig(c => c.WithSecondaryRules(c.SecondaryRules.Where(r => r.Id != SelectedSecondary!.Id).ToArray()), true), () => CanEditSecondaryConfiguration && !HasUnappliedRule && SelectedSecondary is not null);
        NewSecondaryCommand = new RelayCommand(ClearRule, () => CanEditSecondaryConfiguration && !HasUnappliedRule);
        AssignCommand = new AsyncRelayCommand(AssignAsync, () => CanEditConfiguration && !absoluteDirty && Part.HasValue && PartKey.HasValue);
        AutomaticCommand = new AsyncRelayCommand(() => CommitConfig(c => new(c.PrimaryRules, c.SecondaryRules, c.ExplicitAssignments.Where(a => a.PartId != Part).ToArray(), c.AbsoluteAssignments.Where(a => a.PartId != Part).ToArray(), c.Descriptions)), () => CanEditConfiguration && !absoluteDirty && Part.HasValue);
        ClearObsoleteCommand = new AsyncRelayCommand(() => CommitConfig(c => new(c.PrimaryRules, c.SecondaryRules,
            c.ExplicitAssignments.Where(a => Parts.Any(p => p.Value == a.PartId)).ToArray(),
            c.AbsoluteAssignments.Where(a => Parts.Any(p => p.Value == a.PartId)).ToArray(),
            c.Descriptions.Where(d => workspace!.Resources.Origins.Any(o => o.Code == d.Account && o.Descriptions.Contains(d.Description, StringComparer.Ordinal))).ToArray())), () => CanEditConfiguration && !HasUnappliedRule && HasObsolete);
        SaveAbsoluteCommand = new AsyncRelayCommand(SaveAbsoluteAsync, () => CanEditConfiguration && Part.HasValue);
        ChooseDescriptionCommand = new AsyncRelayCommand(() => CommitConfig(c => new(c.PrimaryRules, c.SecondaryRules, c.ExplicitAssignments, c.AbsoluteAssignments,
            c.Descriptions.Where(d => d.Account != Account).Append(new(Account!, ChosenDescription)).ToArray())), () => CanEdit && Account is not null && DescriptionOptions.Contains(ChosenDescription, StringComparer.Ordinal));
        DiscardCommand = new RelayCommand(() => { LoadKey(editingKey); ClearRule(); LoadAbsolute(); statusKey = "Discarded"; Refresh(); }, () => !busy && IsDirty);
        earlierBusy = company.WorkspaceBusy; earlierBefore = studies.BeforeChangeAsync; earlierAfter = company.AfterChangeAsync;
        company.WorkspaceBusy = () => busy || earlierBusy?.Invoke() == true;
        studies.BeforeChangeAsync = async () => await CanLeaveAsync() && (earlierBefore is null || await earlierBefore());
        company.AfterChangeAsync = async () => { if (earlierAfter is not null) await earlierAfter(); workspace = null; path = null; LoadKey(null); ClearRule(); LoadAbsolute(); Rebuild(); };
        preferences.Changed += PreferencesChanged;
    }
    public string this[string key] => Text("AllocationUI." + key);
    private string Text(string key) { var value = texts.Get(key, CultureInfo.GetCultureInfo(preferences.Current.UiLanguageTag)); return pseudo ? PseudoLocalization.Expand(value) : value; }
    private CultureInfo Format => CultureInfo.GetCultureInfo(preferences.Current.FormatCultureTag);
    private string MoneyText(Money value) => value.Amount.ToString("0.########", Format) + " " + value.Currency;
    public bool IsBusy => busy;
    public bool CanManagePrimary => !busy && earlierBusy?.Invoke() != true && company.CanWrite && workspace?.Resources.Study.State == StudyState.Open;
    public bool IsPrimaryClosed => workspace?.Configuration.PrimaryClosed == true;
    public bool CanEdit => CanManagePrimary && !IsPrimaryClosed;
    public string PrimaryState => this[IsPrimaryClosed ? "PrimaryClosedState" : "PrimaryOpenState"];
    public string PrimaryToggleLabel => this[IsPrimaryClosed ? "ReopenPrimary" : "ClosePrimary"];
    private bool CanSaveRule => CanEditConfiguration && RuleKey.HasValue && !string.IsNullOrWhiteSpace(IsExact ? RuleAccount : Prefix);
    public bool CanAssignCoverage => CanEditConfiguration && !HasUnappliedRule;
    public bool CanEditConfiguration => CanEdit && !keyDirty;
    public bool CanEditKey => (Stage == AllocationStage.Secondary ? CanManageSecondary : CanEdit) && !HasUnappliedRule;
    private bool IsKeyUsed => editingKey is not null && workspace?.UsedKeyIds.Contains(editingKey.Id) == true;
    public bool CanEditComposition => CanEditKey && !IsKeyUsed;
    public bool HasObsolete => workspace is not null && (workspace.Configuration.ExplicitAssignments.Any(a => !Parts.Any(p => p.Value == a.PartId))
        || workspace.Configuration.AbsoluteAssignments.Any(a => !Parts.Any(p => p.Value == a.PartId))
        || workspace.Configuration.Descriptions.Any(d => !workspace.Resources.Origins.Any(o => o.Code == d.Account && o.Descriptions.Contains(d.Description, StringComparer.Ordinal))));
    public bool IsDirty => keyDirty || ruleDirty || absoluteDirty;
    public bool HasUnappliedRule => ruleDirty || absoluteDirty;
    public void ExplainPendingRule() { statusKey = "ApplyRuleFirst"; Refresh(); }
    public bool IsPercentage => Mode == AllocationKeyMode.Percentage;
    public bool IsDriver => Mode == AllocationKeyMode.DriverUnits;
    public bool CanEditWeights => CanEditComposition && Mode != AllocationKeyMode.EqualShares;
    public bool IsSecondary => Stage == AllocationStage.Secondary;
    public bool IsExact => SelectorKind == AccountSelectorKind.Exact;
    public bool IsPrefix => !IsExact;
    public string Requirement => this[workspace is null ? "NeedStudy" : !company.CanWrite ? "ReadOnly" : workspace.Resources.Study.State == StudyState.Closed ? "Closed" : "Help"];
    public string Status => diagnostic is null ? this[statusKey] : new DomainDiagnosticFormatter(texts).Format(diagnostic, CultureInfo.GetCultureInfo(preferences.Current.UiLanguageTag), Format).Message;
    public string KeyState => this[IsKeyUsed ? "UsedKeyHelp" : keyDirty ? "Unsaved" : "DraftHelp"];
    public string Currency => workspace?.Resources.Study.Currency.FunctionalCurrencyCode.ToString() ?? "";
    public string NumberFormatHelp => string.Format(Format, this["NumberFormat"], numbers.CapturedCulture.Name, numbers.CapturedCulture.NumberFormat.NumberDecimalSeparator);
    private int coveredCount;
    public string CoverageSummary => workspace is null ? "" : string.Format(Format, this["CoverageSummary"], coveredCount, Coverage.Count);
    public string Total
    {
        get
        {
            try
            {
                var value = Lines.Where(l => l.Included).Sum(l => Mode == AllocationKeyMode.EqualShares ? 1 : Parse(l.Value));
                return value.ToString("0.##################", numbers.CapturedCulture) + (IsPercentage ? " %" : IsDriver ? " " + Units.FirstOrDefault(u => u.Value == Unit)?.Label : " " + this["Destinations"]);
            }
            catch (Exception ex) when (ex is DomainRuleException or OverflowException) { return this["CheckNumbers"]; }
        }
    }
    public string Validation
    {
        get
        {
            if (workspace is null) return this["NeedStudy"];
            if (MissingKeyInput(true) is { } missing) return this[missing];
            try { _ = PreviewKey(true); return this["Complete"]; }
            catch (DomainRuleException ex) { return new DomainDiagnosticFormatter(texts).Format(ex.Diagnostic, CultureInfo.GetCultureInfo(preferences.Current.UiLanguageTag), Format).Message; }
            catch (Exception ex) when (ex is ArgumentException or OverflowException or InvalidOperationException) { return this["CheckNumbers"]; }
        }
    }
    public ObservableCollection<AllocationKeyRow> Keys { get; } = [];
    public ObservableCollection<AllocationLineEditor> Lines { get; } = [];
    public ObservableCollection<AllocationLineEditor> AbsoluteLines { get; } = [];
    public ObservableCollection<AllocationRuleRow> PrimaryRules { get; } = [];
    public ObservableCollection<AllocationSecondaryRow> SecondaryRules { get; } = [];
    public ObservableCollection<AllocationCoverageRow> Coverage { get; } = [];
    [ObservableProperty] private AllocationKeyRow? selectedKey;
    [ObservableProperty] private AllocationRuleRow? selectedRule;
    [ObservableProperty] private AllocationSecondaryRow? selectedSecondary;
    partial void OnSelectedKeyChanged(AllocationKeyRow? value) => Commands();
    partial void OnSelectedRuleChanged(AllocationRuleRow? value) => Commands();
    partial void OnSelectedSecondaryChanged(AllocationSecondaryRow? value) => Commands();
    public string Name { get => name; set { if (SetProperty(ref name, value)) KeyEdited(); } }
    public AllocationStage Stage { get => stage; set { if (SetProperty(ref stage, value)) KeyEdited(); } }
    public AllocationKeyMode Mode { get => mode; set { if (SetProperty(ref mode, value)) { if (value == AllocationKeyMode.EqualShares) foreach (var line in Lines) line.Value = "1"; KeyEdited(); } } }
    public UnitId? Unit { get => unit; set { if (SetProperty(ref unit, value)) KeyEdited(); } }
    public bool SelfAllowed { get => selfAllowed; set { if (SetProperty(ref selfAllowed, value)) KeyEdited(); } }
    public AccountSelectorKind SelectorKind { get => selectorKind; set { if (SetProperty(ref selectorKind, value)) RuleEdited(); } }
    public string RuleAccount { get => ruleAccount; set { if (SetProperty(ref ruleAccount, value ?? "")) RuleEdited(); } }
    public string Prefix { get => prefix; set { if (SetProperty(ref prefix, value)) RuleEdited(); } }
    public string Priority { get => priority; set { if (SetProperty(ref priority, value)) RuleEdited(); } }
    public EntityId? RuleKey { get => ruleKey; set { if (SetProperty(ref ruleKey, value)) RuleEdited(); } }
    public EntityId? Auxiliary { get => auxiliary; set { if (SetProperty(ref auxiliary, value)) RuleEdited(); } }
    public EntityId? SecondaryKey { get => secondaryKey; set { if (SetProperty(ref secondaryKey, value)) RuleEdited(); } }
    public string GroupName { get => groupName; set { if (SetProperty(ref groupName, value)) RuleEdited(); } }
    public EntityId? Part { get => part; set
    {
        if (absoluteDirty && part != value) { ExplainPendingRule(); OnPropertyChanged(nameof(Part)); return; }
        if (SetProperty(ref part, value)) { LoadAbsolute(); Refresh(); }
    } }
    public EntityId? PartKey { get => partKey; set { if (SetProperty(ref partKey, value)) Commands(); } }
    public string? Account { get => account; set { if (SetProperty(ref account, value)) { chosenDescription = workspace?.Configuration.Descriptions.SingleOrDefault(d => d.Account == value)?.Description ?? ""; Refresh(); } } }
    public string ChosenDescription { get => chosenDescription; set { if (SetProperty(ref chosenDescription, value)) Commands(); } }
    public IReadOnlyList<string> DescriptionOptions => workspace?.Resources.Origins.SingleOrDefault(o => o.Code == Account)?.Descriptions ?? [];
    public string DescriptionProvenance => string.Join(Environment.NewLine, workspace?.Resources.Origins.SingleOrDefault(o => o.Code == Account)?.Rows
        .Select(r => (string.IsNullOrWhiteSpace(r.Description) ? this["NoDescription"] : r.Description) + " · " + r.FileName + " · " + this["Row"] + " " + r.RecordNumber.ToString(Format)) ?? []);
    public IReadOnlyList<ResourceChoice<AllocationStage>> Stages => choices.Get<ResourceChoice<AllocationStage>>((workspace, preferences.Current.UiLanguageTag, preferences.Current.FormatCultureTag, pseudo), () => Enum.GetValues<AllocationStage>().Select(v => new ResourceChoice<AllocationStage>(v, this[v.ToString()])).ToArray());
    public IReadOnlyList<ResourceChoice<AllocationKeyMode>> Modes => choices.Get<ResourceChoice<AllocationKeyMode>>((workspace, preferences.Current.UiLanguageTag, preferences.Current.FormatCultureTag, pseudo), () => Enum.GetValues<AllocationKeyMode>().Select(v => new ResourceChoice<AllocationKeyMode>(v, this[v.ToString()])).ToArray());
    public IReadOnlyList<ResourceChoice<AccountSelectorKind>> Selectors => choices.Get<ResourceChoice<AccountSelectorKind>>((workspace, preferences.Current.UiLanguageTag, preferences.Current.FormatCultureTag, pseudo), () => Enum.GetValues<AccountSelectorKind>().Select(v => new ResourceChoice<AccountSelectorKind>(v, this[v.ToString()])).ToArray());
    public IReadOnlyList<ResourceChoice<UnitId>> Units => choices.Get<ResourceChoice<UnitId>>((workspace, preferences.Current.UiLanguageTag, preferences.Current.FormatCultureTag, pseudo), () => workspace?.Resources.Model.DriverUnits.Select(u => new ResourceChoice<UnitId>(u.Id, u.Code + " · " + u.Symbol)).ToArray() ?? []);
    public IReadOnlyList<ResourceChoice<EntityId>> PublishedPrimary => choices.Get<ResourceChoice<EntityId>>((workspace, preferences.Current.UiLanguageTag, preferences.Current.FormatCultureTag, pseudo), () => Published(AllocationStage.Primary));
    public IReadOnlyList<ResourceChoice<EntityId>> PublishedSecondary => choices.Get<ResourceChoice<EntityId>>((workspace, preferences.Current.UiLanguageTag, preferences.Current.FormatCultureTag, pseudo), () => Published(AllocationStage.Secondary));
    private IReadOnlyList<ResourceChoice<EntityId>> Published(AllocationStage type) => workspace?.Keys.Where(k => k.Stage == type && k.Status == ModelStatus.Published).Select(k => new ResourceChoice<EntityId>(k.Id, KeyLabel(k))).ToArray() ?? [];
    public IReadOnlyList<ResourceChoice<string>> Accounts => choices.Get<ResourceChoice<string>>((workspace, preferences.Current.UiLanguageTag, preferences.Current.FormatCultureTag, pseudo), () => workspace?.Resources.Origins.Select(o => new ResourceChoice<string>(o.Code, AccountLabel(o))).ToArray() ?? []);
    public IReadOnlyList<ResourceChoice<EntityId>> Auxiliaries => choices.Get<ResourceChoice<EntityId>>((workspace, preferences.Current.UiLanguageTag, preferences.Current.FormatCultureTag, pseudo), () => workspace?.Resources.Model.Structure.Nodes.Where(n => n.IsActive && n.Role == NodeRole.Auxiliary).Select(n => new ResourceChoice<EntityId>(n.Id, NodeLabel(n))).ToArray() ?? []);
    public IReadOnlyList<AllocationPartChoice> Parts => choices.Get<AllocationPartChoice>((workspace, preferences.Current.UiLanguageTag, preferences.Current.FormatCultureTag, pseudo), () => workspace?.Resources.Resources.SelectMany(r => r.Parts.Where(p => p.Routing is ResourceRoutingKind.AllocationPool or ResourceRoutingKind.Pending)
        .Select((p, i) => new AllocationPartChoice(p.Id, AccountLabel(workspace.Resources.Origins.Single(o => o.Id == r.Id)) + " · " + this["Part"] + " " + (i + 1).ToString(Format) + " · " + MoneyText(p.Amount), p.Amount))).ToArray() ?? []);
    private string KeyLabel(AllocationKeyVersion key) => key.Name;
    private string AccountLabel(ResourceOrigin origin)
    {
        var chosen = workspace?.Configuration.Descriptions.SingleOrDefault(d => d.Account == origin.Code)?.Description;
        var description = chosen is not null && origin.Descriptions.Contains(chosen, StringComparer.Ordinal) ? chosen : origin.HasDescriptionConflict
            ? this["NameConflict"] + ": " + string.Join(" / ", origin.Descriptions) : origin.Descriptions.FirstOrDefault() ?? this["NoDescription"];
        return origin.Code + " — " + description;
    }
    private string NodeLabel(CalculationNodeDefinition node)
    {
        var model = workspace!.Resources.Model; var center = model.PhysicalCenters.Single(c => c.Id == node.PhysicalCenterId);
        return center.Code + " · " + center.Name + " / " + node.Code + " · " + Text(node.Role == NodeRole.Principal ? "Model.Principal" : "Model.Auxiliary");
    }
    private string NodeUnit(CalculationNodeDefinition node)
    {
        var model = workspace!.Resources.Model; var pool = model.Structure.Pools.Single(p => p.NodeId == node.Id);
        var u = model.DriverUnits.Single(u => u.Id == pool.DriverUnitId); return u.Code == u.Symbol ? u.Code : u.Code + " · " + u.Symbol;
    }
    private decimal Parse(string value) => numbers.TryCommit(value, out var parsed) ? parsed : throw AllocationDiagnostics.Error("KEY_NUMBER_INVALID", "Allocation.NumberInvalid");
    private void KeyEdited() { if (loading) return; keyDirty = true; RefreshEditor(); }
    private void RuleEdited() { if (loading) return; ruleDirty = true; RefreshEditor(); }
    private void RefreshEditor()
    {
        foreach (var property in new[] { nameof(IsDirty), nameof(HasUnappliedRule), nameof(KeyState), nameof(CanEditConfiguration),
            nameof(CanAssignCoverage), nameof(CanEditKey), nameof(CanEditComposition), nameof(CanEditWeights), nameof(IsPercentage), nameof(IsDriver), nameof(IsSecondary),
            nameof(IsExact), nameof(IsPrefix), nameof(Total), nameof(Validation) }) OnPropertyChanged(property);
        Commands();
    }
    private void LineChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (Mode == AllocationKeyMode.EqualShares && sender is AllocationLineEditor line && line.Value != "1") line.Value = "1";
        KeyEdited();
    }
    private void AbsoluteChanged(object? sender, PropertyChangedEventArgs e) { if (loading) return; absoluteDirty = true; RefreshEditor(); }
    private void LoadKey(AllocationKeyVersion? key)
    {
        loading = true; editingKey = key; newParent = key?.ParentVersionId; numbers = new(Format);
        name = key?.Name ?? ""; mode = key?.Mode ?? AllocationKeyMode.Percentage; stage = key?.Stage ?? AllocationStage.Primary;
        unit = key?.UnitId; selfAllowed = key?.SelfAllocation == SelfAllocationPolicy.Allowed;
        foreach (var line in Lines) line.PropertyChanged -= LineChanged;
        Lines.Clear();
        foreach (var node in workspace?.Resources.Model.Structure.Nodes.Where(n => n.IsActive).OrderBy(n => NodeLabel(n), StringComparer.Ordinal).ToArray() ?? [])
        {
            var saved = key?.Lines.SingleOrDefault(l => l.DestinationNodeId == node.Id);
            var line = new AllocationLineEditor(node.Id, NodeLabel(node), NodeUnit(node), saved is not null,
                (saved?.Value ?? 0).ToString("0.##################", numbers.CapturedCulture));
            line.PropertyChanged += LineChanged; Lines.Add(line);
        }
        keyDirty = false; loading = false; diagnostic = null; Refresh();
    }
    private void LoadAbsolute()
    {
        foreach (var line in AbsoluteLines) line.PropertyChanged -= AbsoluteChanged;
        AbsoluteLines.Clear(); absoluteDirty = false;
        var saved = workspace?.Configuration.AbsoluteAssignments.SingleOrDefault(a => a.PartId == Part);
        foreach (var node in workspace?.Resources.Model.Structure.Nodes.Where(n => n.IsActive) ?? [])
        {
            var old = saved?.Lines.SingleOrDefault(l => l.DestinationNodeId == node.Id);
            var line = new AllocationLineEditor(node.Id, NodeLabel(node), Currency, old is not null, (old?.Amount.Amount ?? 0).ToString("0.########", numbers.CapturedCulture));
            line.PropertyChanged += AbsoluteChanged; AbsoluteLines.Add(line);
        }
    }
    public async Task EnterAsync(bool force = false)
    {
        if (busy) return;
        if (company.ActiveAllocations is not { } port || studies.Current is not { } current) { workspace = null; LoadKey(null); Rebuild(); return; }
        if (!force && workspace?.Resources.Study.Id == current.Id && workspace.Resources.Study.Revision == current.Revision && path == company.ActivePath) return;
        if (IsDirty) { statusKey = "PendingBody"; Refresh(); return; }
        await Run(async () => { workspace = await Task.Run(() => port.ReadAllocations(current.CompanyId, current.Id)); path = company.ActivePath; LoadKey(null); ClearRule(); Rebuild(); });
    }
    private SaveAllocationKey BuildKey(bool publish) => new(workspace!.Resources.Study.CompanyId, workspace.Resources.Model.Structure.Id,
        editingKey?.Id ?? previewId, newParent, editingKey?.Revision ?? -1, Name, Stage, Mode, IsDriver ? Unit : null,
        SelfAllowed ? SelfAllocationPolicy.Allowed : SelfAllocationPolicy.Forbidden,
        Lines.Where(l => l.Included).Select(l => new AllocationKeyLine(l.Id, Mode == AllocationKeyMode.EqualShares ? 1m : Parse(l.Value))).ToArray(), publish, new(Guid.NewGuid()), "local-user");
    private EntityId previewId = new(Guid.NewGuid());
    private AllocationKeyVersion PreviewKey(bool publish) => AllocationWorkspaceRules.SaveKey(BuildKey(publish), workspace!.Resources.Model, workspace.Resources.Precision,
        workspace.Keys, UtcInstant.ParseIso("2000-01-01T00:00:00Z"), !IsKeyUsed);
    private string? MissingKeyInput(bool publish) => string.IsNullOrWhiteSpace(Name) ? "NameRequired"
        : IsDriver && !Unit.HasValue ? "UnitRequired"
        : publish && !Lines.Any(l => l.Included) ? "DestinationsRequired" : null;
    public async Task<bool> SaveKeyAsync(bool publish)
    {
        if (!CanEditKey || workspace is null) return false;
        if (MissingKeyInput(publish) is { } missing)
        {
            diagnostic = null; statusKey = missing; Refresh(); return false;
        }
        return await Run(async () =>
        {
            var command = BuildKey(publish || editingKey?.Status == ModelStatus.Published); var port = company.ActiveAllocations!;
            var receipt = await Task.Run(() => port.SaveAllocationKey(command));
            workspace = new(workspace.Resources, workspace.Keys.Where(k => k.Id != receipt.After.Id).Append(receipt.After).ToArray(), workspace.Configuration, workspace.ConfigurationRevision, includeUnroutedAccounts: workspace.IncludeUnroutedAccounts,
                usedKeyIds: workspace.UsedKeyIds, secondaryPublication: workspace.SecondaryPublication);
            LoadKey(receipt.After); previewId = new(Guid.NewGuid()); Rebuild(); statusKey = publish ? "PublishedSaved" : "DraftSaved";
        });
    }
    private void ClearRule()
    {
        loading = true; editingRule = editingSecondary = null; ruleAccount = prefix = groupName = ""; priority = "0";
        ruleKey = auxiliary = secondaryKey = null; selectorKind = AccountSelectorKind.Exact; ruleDirty = false; loading = false; Refresh();
    }
    private void LoadRule()
    {
        var r = workspace!.Configuration.PrimaryRules.Single(r => r.Id == SelectedRule!.Id);
        loading = true; editingRule = r.Id; selectorKind = r.SelectorKind; ruleAccount = prefix = r.SelectorValue;
        ruleKey = r.KeyVersionId; priority = r.Priority.ToString(numbers.CapturedCulture); ruleDirty = false; loading = false; Refresh();
    }
    private void LoadSecondary()
    {
        var r = workspace!.Configuration.SecondaryRules.Single(r => r.Id == SelectedSecondary!.Id);
        loading = true; editingSecondary = r.Id; auxiliary = r.AuxiliaryNodeId; groupName = r.ComponentRoutingGroup == "all" ? "" : r.ComponentRoutingGroup;
        secondaryKey = r.KeyVersionId; ruleDirty = false; loading = false; Refresh();
    }
    private async Task SaveRuleAsync(bool apply)
    {
        EntityId? savedId = null;
        var saved = await CommitConfig(c =>
    {
        if (!RuleKey.HasValue || !int.TryParse(Priority, NumberStyles.Integer, numbers.CapturedCulture, out var preference)) throw AllocationDiagnostics.Error("KEY_RULE_INVALID", "Allocation.RuleInvalid");
        var previous = c.PrimaryRules.SingleOrDefault(r => r.Id == editingRule);
        var r = new PrimaryRule(previous is { IsActive: true } && !apply ? new(Guid.NewGuid()) : editingRule ?? new(Guid.NewGuid()), workspace!.Resources.Study.CompanyId, workspace.Resources.Model.Structure.Id,
            SelectorKind, IsExact ? RuleAccount : Prefix, preference, RuleKey.Value, apply)
        { DraftOfId = apply ? null : previous?.DraftOfId ?? (previous?.IsActive == true ? previous.Id : null) };
        savedId = r.Id;
        return new(c.PrimaryRules.Where(old => old.Id != r.Id && !(apply && old.Id == previous?.DraftOfId)
            && !(old.SelectorKind == r.SelectorKind && old.SelectorValue == r.SelectorValue && (apply || !old.IsActive)))
            .Append(r).ToArray(), c.SecondaryRules, c.ExplicitAssignments, c.AbsoluteAssignments, c.Descriptions);
    });
        if (saved && savedId is { } id && workspace!.Configuration.PrimaryRules.Any(r => r.Id == id))
        { SelectedRule = PrimaryRules.Single(r => r.Id == id); LoadRule(); statusKey = apply ? "RuleApplied" : "RuleSavedOnly"; Refresh(); }
    }
    private Task SaveSecondaryAsync() => CommitConfig(c =>
    {
        if (!Auxiliary.HasValue || !SecondaryKey.HasValue) throw AllocationDiagnostics.Error("KEY_RULE_INVALID", "Allocation.RuleInvalid");
        var r = new SecondaryRule(editingSecondary ?? new(Guid.NewGuid()), workspace!.Resources.Study.CompanyId, workspace.Resources.Model.Structure.Id,
            Auxiliary.Value, string.IsNullOrWhiteSpace(GroupName) ? "all" : GroupName.Trim(), SecondaryKey.Value, true);
        return c.WithSecondaryRules(c.SecondaryRules.Where(old => old.Id != r.Id).Append(r).ToArray());
    }, true);
    private Task AssignAsync() => CommitConfig(c => new(c.PrimaryRules, c.SecondaryRules,
        c.ExplicitAssignments.Where(a => a.PartId != Part).Append(new(Part!.Value, PartKey!.Value)).ToArray(), c.AbsoluteAssignments.Where(a => a.PartId != Part).ToArray(), c.Descriptions));
    private Task SaveAbsoluteAsync() => CommitConfig(c =>
    {
        var selected = workspace!.Resources.Resources.SelectMany(r => r.Parts).Single(p => p.Id == Part);
        var absolute = new AbsolutePartAllocation(selected.Id, workspace!.Resources.EconomicRevision, selected.Amount,
            AbsoluteLines.Where(l => l.Included).Select(l => new AbsoluteAllocationLine(l.Id, new Money(Parse(l.Value), selected.Amount.Currency, selected.Amount.PrecisionPolicy))).ToArray());
        return new(c.PrimaryRules, c.SecondaryRules, c.ExplicitAssignments.Where(a => a.PartId != Part).ToArray(), c.AbsoluteAssignments.Where(a => a.PartId != Part).Append(absolute).ToArray(), c.Descriptions);
    });
    private async Task<bool> CommitConfig(Func<AllocationConfiguration, AllocationConfiguration> change, bool allowClosed = false)
    {
        if ((!CanEdit && !(allowClosed && CanManagePrimary)) || workspace is null) return false;
        return await Run(async () =>
        {
            var study = workspace.Resources.Study;
            var previous = workspace.Configuration;
            var config = change(previous);
            // Legacy primary editor commands rebuild only the original five
            // fields. Never silently drop an explicit T11 group assignment.
            if (previous.SecondaryPartRoutingGroups is not null &&
                config.SecondaryPartRoutingGroups is null)
                config = new AllocationConfiguration(config.PrimaryRules, config.SecondaryRules,
                    config.ExplicitAssignments, config.AbsoluteAssignments, config.Descriptions,
                    config.PrimaryClosed, config.SecondaryClosed, config.SecondaryCalculationHash,
                    previous.SecondaryPartRoutingGroups);
            var port = company.ActiveAllocations!;
            var receipt = await Task.Run(() => port.ChangeAllocations(new(study.CompanyId, study.Id, study.Revision, new(Guid.NewGuid()), "local-user", config)));
            // Retain the committed revision even if a dependent refresh fails.
            var r = workspace.Resources;
            workspace = new(new(receipt.ConfirmedStudy, r.Precision, r.Model, r.Origins, r.Ledger, r.EconomicRevision), workspace.Keys, config, receipt.ConfirmedStudy.Revision, includeUnroutedAccounts: workspace.IncludeUnroutedAccounts,
                usedKeyIds: workspace.UsedKeyIds.Concat(receipt.ReferencedKeys.Where(k => config.ExplicitAssignments.Any(a => a.KeyVersionId == k.Id) || config.PrimaryRules.Any(r => r.IsActive && r.KeyVersionId == k.Id) || config.SecondaryRules.Any(r => r.IsActive && r.KeyVersionId == k.Id)).Select(k => k.Id)).Distinct().ToArray(), secondaryPublication: receipt.SecondaryPublication);
            ClearRule(); LoadAbsolute(); Rebuild(); statusKey = "Saved";
            try { await studies.ApplyResourceReceiptAsync(receipt.ConfirmedStudy); }
            catch (Exception ex) when (ex is IOException or CompanyFileException or InvalidOperationException) { statusKey = "SavedRefreshPending"; }
        });
    }
    public Func<Task>? AfterPrimaryStateChanged { get; set; }
    private async Task TogglePrimaryAsync()
    {
        if (!CanManagePrimary || IsDirty || earlierBefore is not null && !await earlierBefore()) return;
        await EnterAsync(true);
        if (await CommitConfig(c => c.WithPrimaryClosed(!c.PrimaryClosed), true))
        { statusKey = IsPrimaryClosed ? "PrimaryClosedState" : "PrimaryOpenState"; Refresh(); if (AfterPrimaryStateChanged is not null) await AfterPrimaryStateChanged(); }
    }
    private async Task DeleteKeyAsync()
    {
        if ((!CanEdit && !(CanManageSecondary && SelectedKey?.Value.Stage == AllocationStage.Secondary)) || IsDirty || SelectedKey is not { } row) return;
        await Run(async () =>
        {
            var k = row.Value;
            var command = new SaveAllocationKey(k.CompanyId, k.ModelVersionId, k.Id, k.ParentVersionId, k.Revision,
                k.Name, k.Stage, k.Mode, k.UnitId, k.SelfAllocation, k.Lines, k.Status == ModelStatus.Published,
                new(Guid.NewGuid()), "local-user") { Delete = true };
            await Task.Run(() => company.ActiveAllocations!.SaveAllocationKey(command));
            workspace = new(workspace!.Resources, workspace.Keys.Where(key => key.Id != k.Id).ToArray(),
                workspace.Configuration, workspace.ConfigurationRevision, includeUnroutedAccounts: workspace.IncludeUnroutedAccounts,
                usedKeyIds: workspace.UsedKeyIds, secondaryPublication: workspace.SecondaryPublication);
            if (editingKey?.Id == k.Id) LoadKey(null);
            Rebuild(); statusKey = "KeyDeleted";
        });
    }
    public async Task<bool> CanLeaveAsync()
    {
        if (busy) return false;
        if (!IsDirty) return true;
        var choice = dialogs.ResolvePending(this["PendingTitle"], this["PendingBody"]);
        if (choice == PendingEditChoice.Cancel) return false;
        if (choice == PendingEditChoice.Discard) { LoadKey(editingKey); ClearRule(); LoadAbsolute(); return true; }
        if (HasUnappliedRule) { statusKey = "ApplyRuleFirst"; Refresh(); return false; }
        return await SaveKeyAsync(false);
    }
    private void Rebuild()
    {
        var keyId = SelectedKey?.Value.Id; Keys.Clear(); PrimaryRules.Clear(); SecondaryRules.Clear();
        var nextCoverage = new List<AllocationCoverageRow>();
        if (workspace is not null)
        {
            foreach (var k in workspace.Keys.OrderBy(k => k.Stage).ThenBy(k => k.Name, StringComparer.Ordinal).ThenBy(k => k.VersionNumber)) Keys.Add(new(k, KeyLabel(k), this[k.Mode.ToString()], this[k.Stage.ToString()], this[k.Status == ModelStatus.Published ? "AvailableKey" : "IncompleteKey"]));
            foreach (var r in workspace.Configuration.PrimaryRules) PrimaryRules.Add(new(r.Id, r.SelectorKind == AccountSelectorKind.Exact ? Accounts.FirstOrDefault(a => a.Value == r.SelectorValue)?.Label ?? r.SelectorValue : this["Prefix"] + ": " + r.SelectorValue, KeyLabel(workspace.Keys.Single(k => k.Id == r.KeyVersionId)), r.Priority.ToString(Format), this[r.IsActive ? "RuleActive" : "RulePending"]));
            foreach (var r in workspace.Configuration.SecondaryRules) SecondaryRules.Add(new(r.Id, NodeLabel(workspace.Resources.Model.Structure.Nodes.Single(n => n.Id == r.AuxiliaryNodeId)), r.ComponentRoutingGroup == "all" ? this["AllComponents"] : r.ComponentRoutingGroup, KeyLabel(workspace.Keys.Single(k => k.Id == r.KeyVersionId))));
            var partIds = Parts.Select(p => p.Value).ToHashSet();
            var coverage = workspace.Coverage.Where(c => partIds.Contains(c.SourceId)).ToArray();
            coveredCount = coverage.Count(c => c.Diagnostic is null);
            foreach (var c in coverage.OrderBy(c => c.Account, StringComparer.Ordinal).ThenBy(c => c.SourceId))
            {
                var item = Parts.FirstOrDefault(p => p.Value == c.SourceId);
                var origin = workspace.Resources.Origins.FirstOrDefault(o => o.Code == c.Account);
                var resource = workspace.Resources.Resources.FirstOrDefault(r => r.Parts.Any(p => p.Id == c.SourceId));
                var source = origin is null ? c.Account : AccountLabel(origin);
                if (resource?.Parts.Count > 1) source += " · " + this["Part"] + " " + (resource.Parts.ToList().FindIndex(p => p.Id == c.SourceId) + 1).ToString(Format);
                if (string.IsNullOrWhiteSpace(source)) source = this["ChangedPart"];
                var status = c.Diagnostic is null ? this["Covered"] : new DomainDiagnosticFormatter(texts).Format(c.Diagnostic, CultureInfo.GetCultureInfo(preferences.Current.UiLanguageTag), Format).Message;
                nextCoverage.Add(new(c.SourceId, source, item is null ? "" : MoneyText(item.Amount), c.KeyId is { } id ? KeyLabel(workspace.Keys.Single(k => k.Id == id)) : c.Diagnostic is null ? this["Absolute"] : "—", status, c.KeyId));
            }
        }
        // Keep row identity and collection order so WPF preserves selection, sorting and scroll.
        var ids = nextCoverage.Select(r => r.Id).ToHashSet();
        foreach (var old in Coverage.Where(r => !ids.Contains(r.Id)).ToArray()) Coverage.Remove(old);
        foreach (var next in nextCoverage)
        {
            var old = Coverage.FirstOrDefault(r => r.Id == next.Id);
            if (old is null) Coverage.Add(next);
            else { old.Source = next.Source; old.Amount = next.Amount; old.Key = next.Key; old.Status = next.Status; old.SelectedKeyId = next.SelectedKeyId; }
        }
        RebuildPrimary();
        RebuildSecondary();
        ResetReciprocal();
        SelectedKey = Keys.FirstOrDefault(k => k.Value.Id == keyId); Refresh();
    }
    private async Task<bool> Run(Func<Task> work)
    {
        if (busy) return false; busy = true; diagnostic = null; company.NotifyWorkspaceStateChanged(); Refresh();
        try { await work(); return true; }
        catch (DomainRuleException ex) { diagnostic = ex.Diagnostic; return false; }
        catch (CompanyFileException ex) { diagnostic = ex.Diagnostic; return false; }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OverflowException or IOException)
        { diagnostic = AllocationDiagnostics.Error("KEY_INPUT_INVALID", "Allocation.InputInvalid").Diagnostic; return false; }
        finally { busy = false; company.NotifyWorkspaceStateChanged(); Refresh(); }
    }
    public IAsyncRelayCommand DeleteKeyCommand { get; }
    public IAsyncRelayCommand TogglePrimaryCommand { get; }
    public IAsyncRelayCommand NewKeyCommand { get; }
    public IAsyncRelayCommand LoadKeyCommand { get; }
    public IAsyncRelayCommand NewVersionCommand { get; }
    public IAsyncRelayCommand SaveDraftCommand { get; }
    public IAsyncRelayCommand PublishCommand { get; }
    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand SaveRuleCommand { get; }
    public IAsyncRelayCommand ApplyRuleCommand { get; }
    public IAsyncRelayCommand SaveKeyCommand { get; }
    public async Task AssignCoverageAsync(EntityId partId, EntityId keyId)
    {
        if (!CanEditConfiguration || HasUnappliedRule) return;
        await CommitConfig(c => new(c.PrimaryRules, c.SecondaryRules,
            c.ExplicitAssignments.Where(a => a.PartId != partId).Append(new(partId, keyId)).ToArray(),
            c.AbsoluteAssignments.Where(a => a.PartId != partId).ToArray(), c.Descriptions));
    }
    public IAsyncRelayCommand RemoveRuleCommand { get; }
    public IRelayCommand LoadRuleCommand { get; }
    public IRelayCommand NewRuleCommand { get; }
    public IAsyncRelayCommand SaveSecondaryCommand { get; }
    public IRelayCommand LoadSecondaryCommand { get; }
    public IRelayCommand NewSecondaryCommand { get; }
    public IAsyncRelayCommand RemoveSecondaryCommand { get; }
    public IAsyncRelayCommand AssignCommand { get; }
    public IAsyncRelayCommand AutomaticCommand { get; }
    public IAsyncRelayCommand SaveAbsoluteCommand { get; }
    public IAsyncRelayCommand ClearObsoleteCommand { get; }
    public IAsyncRelayCommand ChooseDescriptionCommand { get; }
    public IRelayCommand DiscardCommand { get; }
    private void Commands()
    {
        foreach (var command in new IRelayCommand?[] { DeleteKeyCommand, TogglePrimaryCommand, NewKeyCommand, LoadKeyCommand, NewVersionCommand, SaveDraftCommand, PublishCommand, SaveKeyCommand, ApplyRuleCommand, RefreshCommand,
            SaveRuleCommand, RemoveRuleCommand, LoadRuleCommand, NewRuleCommand, SaveSecondaryCommand, LoadSecondaryCommand, NewSecondaryCommand,
            RemoveSecondaryCommand, ClearObsoleteCommand, AssignCommand, AutomaticCommand, SaveAbsoluteCommand, ChooseDescriptionCommand, DiscardCommand, CheckReciprocalCommand, CalculateSecondaryCommand, ExplainSecondaryCommand, ToggleSecondaryCommand,
            SaveSecondaryGroupCommand, ClearObsoleteSecondaryGroupsCommand, AssignSecondaryKeyToMissingCommand }) command?.NotifyCanExecuteChanged();
    }
    private void Refresh() { OnPropertyChanged(string.Empty); Commands(); }
    public void RefreshPresentation(bool value) { pseudo = value; Rebuild(); }
    private void PreferencesChanged(object? sender, PresentationPreferencesChangedEventArgs e) { if (!IsDirty) { LoadKey(editingKey); LoadAbsolute(); } Rebuild(); }
    public void Dispose() { preferences.Changed -= PreferencesChanged; company.WorkspaceBusy = earlierBusy; studies.BeforeChangeAsync = earlierBefore; company.AfterChangeAsync = earlierAfter; }
}
