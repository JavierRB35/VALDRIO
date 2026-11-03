using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Valdrio.Application;
using Valdrio.Domain;
using Valdrio.Localization;

namespace Valdrio.Desktop;
public sealed record CenterTreeRow(string Label, IReadOnlyList<NodeOption> Children);
public sealed record CenterOption(PhysicalCenter Value, string Label, string StatusLabel);
public sealed record UnitOption(DriverUnitState Value, string Label);
public sealed record ModelOption(ModelVersionSnapshot Value, string Label);
public sealed record RoleOption(NodeRole Value, string Label);
public sealed record LayerOption(CostLayer Value, string Label);
public sealed record NodeOption(
    CalculationNodeDefinition Node,
    CostPoolDefinition? Pool,
    string Label,
    string CenterLabel,
    string RoleLabel,
    string UnitLabel,
    string LayerLabel,
    string FunctionsLabel,
    string PoolLabel);
public sealed partial class ModelWorkspaceViewModel : ObservableObject, IDisposable
{
    private readonly CompanyFileViewModel company;
    private readonly UiTextCatalog texts;
    private readonly PresentationPreferencesService preferences;
    private readonly IModelEditorDialogs dialogs;
    private readonly PresentationFormatter formatter;
    private bool busy, dirty, pseudo, nodeFormEdited, nodeCodeManuallyEdited;
    private string centerCode = "", centerName = "", description = "", nodeCode = "", functionTags = "", poolDescription = "";
    private CenterOption? master, physical;
    private UnitOption? unit, unitMaster;
    private ModelOption? choice;
    private NodeOption? node;
    private RoleOption? role;
    private LayerOption? layer;
    private DomainDiagnostic? diagnostic;
    private IReadOnlyList<DomainDiagnostic> validation = [];
    private string statusKey = "Model.NoFile";
    private CancellationTokenSource? cancellation;
    private readonly List<CalculationNodeDefinition> draftNodes = [];
    private readonly List<CostPoolDefinition> draftPools = [];
    private IReadOnlyList<PhysicalCenter> centerValues = [];
    private IReadOnlyList<DriverUnitState> unitValues = [];
    private IReadOnlyList<ModelVersionSnapshot> modelValues = [];
    private bool loading;
    private bool committedRefreshPending;
    private string? boundPath;
    public ModelWorkspaceViewModel(CompanyFileViewModel company, UiTextCatalog texts, PresentationPreferencesService preferences, IModelEditorDialogs dialogs, PresentationFormatter formatter)
    {
        this.company = company;
        this.texts = texts;
        this.preferences = preferences;
        this.dialogs = dialogs;
        this.formatter = formatter;
        CreateCenterCommand = new AsyncRelayCommand(CreateCenter, () => Ready && company.CanWrite && !DuplicateCenterCode);
        SaveCenterCommand = new AsyncRelayCommand(SaveCenter, () => Ready && company.CanWrite && Master is not null && !DuplicateCenterCode);
        NewCenterCommand = new AsyncRelayCommand(NewCenter, () => Ready && company.CanWrite && (IsEditingCenter || CenterFormEdited));
        SaveCenterFormCommand = new AsyncRelayCommand(SaveCenterForm, () => Ready && company.CanWrite &&
            !string.IsNullOrWhiteSpace(CenterCode) && !string.IsNullOrWhiteSpace(CenterName) &&
            (Master is null || MasterEdited) && !DuplicateCenterCode);
        DeleteCenterCommand = new AsyncRelayCommand(DeleteCenter, () => Ready && company.CanWrite && Master is not null && !MasterEdited);
        ToggleCenterCommand = new AsyncRelayCommand(ToggleCenter, () => Ready && company.CanWrite && Master is not null && !MasterEdited);
        ToggleUnitCommand = new AsyncRelayCommand(ToggleUnit, () => Ready && company.CanWrite && UnitMaster is not null);
        NewDraftCommand = new AsyncRelayCommand(NewDraft, () => Ready && company.CanWrite);
        LoadCommand = new AsyncRelayCommand(LoadChosen, () => Ready && ModelChoice is not null);
        SaveCommand = new AsyncRelayCommand(Save, () => CanEdit && (dirty || nodeFormEdited));
        ValidateCommand = new AsyncRelayCommand(Validate, () => Ready && Current is not null && !dirty && !nodeFormEdited);
        PublishCommand = new AsyncRelayCommand(Publish, () => CanEdit && !IsPublished && !dirty && !nodeFormEdited);
        CloneCommand = new AsyncRelayCommand(Clone, () => Ready && company.CanWrite && Current?.Status == ModelStatus.Published);
        ApplyNodeCommand = new RelayCommand(ApplyNode, () => CanEdit);
        SaveNodeCommand = new AsyncRelayCommand(SaveNode, () => CanEdit && nodeFormEdited);
        RemoveNodeCommand = new RelayCommand(RemoveNode, () => CanEdit && !IsPublished && SelectedNode is not null);
        DeleteNodeCommand = new AsyncRelayCommand(DeleteNode, () => CanEdit && !IsPublished && SelectedNode is not null && !nodeFormEdited);
        NewNodeCommand = new AsyncRelayCommand(NewNode, () => CanEdit && !IsPublished);
        DiscardCommand = new RelayCommand(() =>
        {
            if (Current is not null)
                SetCurrent(Current);
        }, () => !busy && (dirty || nodeFormEdited));
        CancelCommand = new RelayCommand(() => cancellation?.Cancel(), () => busy);
        RefreshCommand = new AsyncRelayCommand(RefreshWorkspace, () => Ready);
        preferences.Changed += PreferencesChanged;
        company.PropertyChanged += CompanyStateChanged;
        company.WorkspaceBusy = () => busy;
        company.BeforeChangeAsync = CanLeaveAsync;
        company.AfterChangeAsync = CompanyChanged;
        Reformat();
    }

    public bool IsBusy => busy;
    private bool MasterEdited => master is not null && (centerCode != master.Value.Code || centerName != master.Value.Name);
    // Match the case-sensitive, company-wide uniqueness enforced by SQLite.
    // During editing the selected centre's own ID is not a conflict.
    private bool DuplicateCenterCode => !string.IsNullOrWhiteSpace(centerCode) &&
        Centers.Any(c => c.Value.Code == centerCode &&
            (master is null || c.Value.Id != master.Value.Id));
    public string CenterCodeValidation => DuplicateCenterCode ? Text("Model.CenterCodeInUse") : "";
    private bool CenterFormEdited => master is null
        ? !string.IsNullOrWhiteSpace(centerCode) || !string.IsNullOrWhiteSpace(centerName)
        : MasterEdited;
    public bool IsDirty => dirty || nodeFormEdited || CenterFormEdited;
    public bool IsPublished => Current?.Status == ModelStatus.Published;
    public bool HasNoModel => Current is null;
    public string NodeEditorPublishedHelp => Text(EditingStudyNodes ? "Model.StudyNodesClosedHelp" : "Model.NodeEditorPublishedHelp");
    public string NodeEditorMissingModelHelp => Text("Model.NodeEditorMissingModelHelp");
    public string NodeClassificationHelp => Text("Model.NodeClassificationHelp");
    private bool Ready => !busy && company.WorkspaceIdle && company.ActiveModels is not null;
    public bool IsEditable => Current?.Status == ModelStatus.Draft || CanEditStudyNodes;
    public bool CanEdit => Ready && IsEditable && company.CanWrite;
    public bool CanEditMasters => Ready && company.CanWrite;
    public bool DescriptionReadOnly => IsPublished || !company.CanWrite;
    public bool InputsEnabled => Ready;
    public bool HasError => diagnostic is not null;
    public string TableEditHelp => Text("Model.TableEditHelp");
    public bool CentersReadOnly => !CanEditMasters;
    public bool NodesReadOnly => !CanEdit;
    public string NewCenterFormVisibility => Master is null ? "Visible" : "Collapsed";
    public string NewNodeFormVisibility => SelectedNode is null ? "Visible" : "Collapsed";
    public bool IsEditingCenter => Master is not null;
    public bool CanSelectNode => !busy;
    public ModelVersionSnapshot? Current { get; private set; }
    public ObservableCollection<CenterOption> Centers { get; } = [];
    public ObservableCollection<UnitOption> Units { get; } = [];
    public ObservableCollection<ModelOption> Models { get; } = [];
    public ObservableCollection<NodeOption> Nodes { get; } = [];
    public ObservableCollection<RoleOption> Roles { get; } = [];
    public ObservableCollection<LayerOption> Layers { get; } = [];

    public CenterOption? Master
    {
        get => master;
        set
        {
            if (!loading && CenterFormEdited && master != value)
            {
                var decision = dialogs.ResolvePending(Text("Model.PendingTitle"), Text("Model.PendingBody"));
                if (decision == PendingEditChoice.Cancel)
                {
                    OnPropertyChanged(nameof(Master));
                    return;
                }

                if (decision == PendingEditChoice.Save)
                {
                    _ = SaveMasterThenSelect(value);
                    return;
                }

                ClearCenterFormCore();
            }

            if (SetProperty(ref master, value) && !loading)
            {
                centerCode = value?.Value.Code ?? "";
                centerName = value?.Value.Name ?? "";
                Refresh();
            }
        }
    }

    private async Task SaveMasterThenSelect(CenterOption? selection)
    {
        await SaveCenterForm();
        if (!CenterFormEdited)
            Master = Centers.SingleOrDefault(x => x.Value.Id == selection?.Value.Id);
        else
            OnPropertyChanged(nameof(Master));
    }

    public CenterOption? Physical
    {
        get => physical;
        set
        {
            if (SetProperty(ref physical, value) && !loading && IsEditable)
            {
                RefreshNodeFormState();
            }
        }
    }

    public UnitOption? UnitMaster
    {
        get => unitMaster;
        set
        {
            SetProperty(ref unitMaster, value);
            OnPropertyChanged(nameof(ToggleUnitLabel));
            RefreshCommands();
        }
    }

    public UnitOption? DriverUnit
    {
        get => unit;
        set
        {
            if (SetProperty(ref unit, value) && !loading && IsEditable)
            {
                RefreshNodeFormState();
            }

            RefreshCommands();
        }
    }

    public ModelOption? ModelChoice
    {
        get => choice;
        set
        {
            SetProperty(ref choice, value);
            RefreshCommands();
        }
    }

    public NodeOption? SelectedNode
    {
        get => node;
        set
        {
            if (!loading && node?.Node.Id != value?.Node.Id && nodeFormEdited)
            {
                var decision = dialogs.ResolvePending(Text("Model.PendingTitle"), Text("Model.PendingBody"));
                if (decision == PendingEditChoice.Cancel)
                {
                    OnPropertyChanged(nameof(SelectedNode));
                    return;
                }
                if (decision == PendingEditChoice.Save)
                {
                    _ = SaveNodeThenSelect(value?.Node.Id);
                    return;
                }
            }

            if (!SetProperty(ref node, value) || loading) return;
            loading = true;
            nodeCode = value?.Node.Code ?? "";
            functionTags = value is null ? "" : string.Join(", ", value.Node.FunctionTags);
            poolDescription = value?.Pool?.Description ?? "";
            physical = Centers.SingleOrDefault(c => c.Value.Id == value?.Node.PhysicalCenterId);
            unit = Units.SingleOrDefault(u => u.Value.Definition.Id == value?.Pool?.DriverUnitId);
            role = Roles.FirstOrDefault(x => x.Value == (value?.Node.Role ?? NodeRole.Principal));
            layer = Layers.FirstOrDefault(x => x.Value == (value?.Pool?.CostLayer ?? CostLayer.Unclassified));
            nodeCodeManuallyEdited = value is not null;
            loading = false;
            nodeFormEdited = false;
            Refresh();
        }
    }

    private async Task SaveNodeThenSelect(EntityId? selectedId)
    {
        await SaveNode();
        if (nodeFormEdited || diagnostic is not null)
        {
            OnPropertyChanged(nameof(SelectedNode));
            return;
        }
        SelectedNode = Nodes.SingleOrDefault(x => x.Node.Id == selectedId);
    }

    private bool NodeFormHasChanges()
    {
        if (node is null)
            return !string.IsNullOrWhiteSpace(nodeCode) || !string.IsNullOrWhiteSpace(poolDescription) ||
                !string.IsNullOrWhiteSpace(functionTags) || physical is not null || unit is not null ||
                role?.Value is not (null or NodeRole.Principal);
        return nodeCode != node.Node.Code ||
            poolDescription != (node.Pool?.Description ?? "") ||
            functionTags != string.Join(", ", node.Node.FunctionTags) ||
            physical?.Value.Id != node.Node.PhysicalCenterId ||
            unit?.Value.Definition.Id != node.Pool?.DriverUnitId ||
            role?.Value != node.Node.Role ||
            layer?.Value != (node.Pool?.CostLayer ?? CostLayer.Unclassified);
    }

    private void RefreshNodeFormState()
    {
        nodeFormEdited = NodeFormHasChanges();
        Refresh();
    }

    private string SuggestNodeCode(string name)
    {
        var normalized = name.Trim().ToUpperInvariant().Normalize(NormalizationForm.FormD);
        var plain = new string(normalized.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
        var candidate = string.Join("-", plain.Split('-', StringSplitOptions.RemoveEmptyEntries));
        if (candidate.Length > 32) candidate = candidate[..32].TrimEnd('-');
        if (string.IsNullOrEmpty(candidate)) return "";
        var original = candidate;
        for (var suffix = 2; draftNodes.Any(n => n.Code == candidate); suffix++)
            candidate = original + "-" + suffix.ToString(CultureInfo.InvariantCulture);
        return candidate;
    }

    public RoleOption? Role
    {
        get => role;
        set
        {
            if (SetProperty(ref role, value) && !loading && IsEditable)
            {
                RefreshNodeFormState();
            }
        }
    }

    public LayerOption? Layer
    {
        get => layer;
        set
        {
            if (SetProperty(ref layer, value) && !loading && IsEditable)
            {
                RefreshNodeFormState();
            }
        }
    }

    public string CenterCode
    {
        get => centerCode;
        set
        {
            if (SetProperty(ref centerCode, value))
                Refresh();
        }
    }

    public string CenterName
    {
        get => centerName;
        set
        {
            if (SetProperty(ref centerName, value))
                Refresh();
        }
    }

    public string Description
    {
        get => description;
        set
        {
            if (SetProperty(ref description, value) && !loading && IsEditable && company.CanWrite)
            {
                dirty = true;
                Refresh();
            }
        }
    }

    public string NodeCode
    {
        get => nodeCode;
        set
        {
            if (SetProperty(ref nodeCode, value) && !loading && IsEditable)
            {
                nodeCodeManuallyEdited = true;
                RefreshNodeFormState();
            }
        }
    }

    public string FunctionTags
    {
        get => functionTags;
        set
        {
            if (SetProperty(ref functionTags, value) && !loading && IsEditable)
                RefreshNodeFormState();
        }
    }

    public string PoolDescription
    {
        get => poolDescription;
        set
        {
            if (!SetProperty(ref poolDescription, value) || loading || !IsEditable) return;
            if (node is null && !nodeCodeManuallyEdited)
            {
                nodeCode = SuggestNodeCode(value);
                OnPropertyChanged(nameof(NodeCode));
            }
            RefreshNodeFormState();
        }
    }

    public IAsyncRelayCommand CreateCenterCommand { get; }
    public IAsyncRelayCommand SaveCenterCommand { get; }
    public IAsyncRelayCommand NewCenterCommand { get; }
    public IAsyncRelayCommand SaveCenterFormCommand { get; }
    public IAsyncRelayCommand DeleteCenterCommand { get; }
    public string DeleteCenterLabel => Text("Model.DeleteCenter");
    public IAsyncRelayCommand ToggleCenterCommand { get; }
    public IAsyncRelayCommand ToggleUnitCommand { get; }
    public IAsyncRelayCommand NewDraftCommand { get; }
    public IAsyncRelayCommand LoadCommand { get; }
    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand ValidateCommand { get; }
    public IAsyncRelayCommand PublishCommand { get; }
    public IAsyncRelayCommand CloneCommand { get; }
    public IRelayCommand ApplyNodeCommand { get; }
    public IAsyncRelayCommand SaveNodeCommand { get; }
    public IRelayCommand RemoveNodeCommand { get; }
    public IAsyncRelayCommand DeleteNodeCommand { get; }
    public IAsyncRelayCommand NewNodeCommand { get; }
    public IRelayCommand DiscardCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public IAsyncRelayCommand RefreshCommand { get; }
    public bool HasPendingRefresh => committedRefreshPending;
    private CultureInfo Ui => CultureInfo.GetCultureInfo(preferences.Current.UiLanguageTag);
    private CultureInfo Format => CultureInfo.GetCultureInfo(preferences.Current.FormatCultureTag);

    public string Text(string key)
    {
        var value = texts.Get(key, Ui);
        return pseudo ? PseudoLocalization.Expand(value) : value;
    }

    private string DiagnosticText(DomainDiagnostic d)
    {
        var localized = new DomainDiagnosticFormatter(texts).Format(d, Ui, Format);
        var references = d.Arguments.Select(a =>
        {
            if (a.Name != "entityId")
                return a.CanonicalValue;
            var node = Current?.Structure.Nodes.FirstOrDefault(n => n.Id.ToString() == a.CanonicalValue);
            var pool = Current?.Structure.Pools.FirstOrDefault(p => p.Id.ToString() == a.CanonicalValue);
            return node?.Code ?? (pool is null ? null : Current!.Structure.Nodes.FirstOrDefault(n => n.Id == pool.NodeId)?.Code) ?? centerValues.FirstOrDefault(c => c.Id.ToString() == a.CanonicalValue)?.Code ?? a.CanonicalValue;
        });
        var detail = string.Join(", ", references);
        return d.Code + ": " + localized.Message + (detail.Length == 0 ? "" : " [" + detail + "]");
    }

    public string Status => diagnostic is null ? Text(committedRefreshPending && !busy ? "Model.CommittedRefreshPending" : statusKey) : DiagnosticText(diagnostic);
    public string CenterFeedback => diagnostic is not null && (CenterFormEdited || Master is not null) ||
        statusKey.StartsWith("Model.Center", StringComparison.Ordinal) ? Status : "";
    public string NodeFeedback => diagnostic is not null && (nodeFormEdited || SelectedNode is not null || dirty) ||
        statusKey.StartsWith("Model.Node", StringComparison.Ordinal) ? Status : "";
    public string ValidationText => string.Join(Environment.NewLine, validation.Select(DiagnosticText));
    public string TechnicalDetails => Current is null ? Text("Model.NoneSelected") : string.Format(Format, Text("Model.Details"), Current.Structure.Description, Text(Current.Status == ModelStatus.Draft ? "Model.Draft" : "Model.Published"), Current.Structure.VersionNumber, Current.Revision, Current.Structure.ParentVersionId?.ToString() ?? "—", Current.PublishedAtUtc is null ? "—" : formatter.FormatInstant(Current.PublishedAtUtc.Value.Value, preferences.Current.DisplayTimeZoneId, Format), Current.StructuralHash ?? "—", IsDirty ? Text("Model.Unsaved") : Text("Model.Saved"));
    public string Details => Current is null ? Text("Model.NoneSelected") : string.Format(Format, Text("Model.ReviewSummary"), Current.Structure.Description, Text(IsPublished ? "Model.Published" : "Model.Draft"), IsDirty ? Text("Model.Unsaved") : Text("Model.Saved"));
    public string TechnicalDetailsLabel => Text("Model.TechnicalDetails");
    public string ReviewTreeLabel => Text("Model.ReviewTree");
    public string RefreshHelp => Text("Model.RefreshHelp");
    public string NewCenterVisibility => IsEditingCenter || CenterFormEdited ? "Visible" : "Collapsed";
    public IReadOnlyList<CenterTreeRow> ReviewTree => Nodes.GroupBy(n => n.Node.PhysicalCenterId)
        .Select(g => new CenterTreeRow(g.First().CenterLabel, g.ToArray())).ToArray();
    public string HistoricalDefinitions => Current?.Publication is not { } p ? "" : string.Join(Environment.NewLine, p.PhysicalCenters.Select(c => c.Code + " · " + c.Name).Concat(p.DriverUnits.Select(u => u.Code + " · " + u.Dimension + " · " + u.Symbol)));
    public string Title => Text("Model.Title");
    public string Notes => Text("Model.Notes");
    public string CenterLabel => Text("Model.Centers");
    public string CenterCodeLabel => Text("Model.CenterCode");
    public string CenterNameLabel => Text("Model.CenterName");
    public string CreateCenterLabel => Text("Model.CreateCenter");
    public string SaveCenterLabel => Text("Model.SaveCenter");
    public string ToggleCenterLabel => Text("Model.ToggleCenter");
    public string CenterSectionHelp => Text("Model.CenterSectionHelp");
    public string CenterRequiredHelp => Text("Model.CenterRequiredHelp");
    public string CenterListTitle => Text("Model.CenterListTitle");
    public string CenterListHelp => Text("Model.CenterListHelp");
    public string CenterFormTitle => Text(IsEditingCenter ? "Model.CenterFormEdit" : "Model.CenterFormNew");
    public string NewCenterLabel => Text("Model.NewCenter");
    public string CenterPrimaryActionLabel => Text(IsEditingCenter ? "Model.CenterSaveEdit" : "Model.CenterSaveCreate");
    public string CenterStateActionLabel => Text(Master?.Value.IsActive == false ? "Model.CenterReactivate" : "Model.CenterDeactivate");
    public string CenterStateHelp => Text("Model.CenterStateHelp");
    public string ModelLabel => Text("Model.Models");
    public string ModelStepHelp => Text("Model.ModelStepHelp");
    public string ExistingModelHelp => Text("Model.ExistingModelHelp");
    public string NewModelHelp => Text("Model.NewModelHelp");
    public string UnitSectionHelp => Text("Model.UnitSectionHelp");
    public string UnitCatalogTitle => Text("Model.UnitCatalogTitle");
    public string CreateUnitHereLabel => Text("Model.CreateUnitHere");
    public string CreateUnitHereHelp => Text("Model.CreateUnitHereHelp");
    public string ReviewModelHelp => Text("Model.ReviewModelHelp");
    public string DescriptionLabel => Text("Model.Description");
    public string NewDraftLabel => Text("Model.NewDraft");
    public string LoadLabel => Text("Model.Load");
    public string SaveLabel => Text("Model.Save");
    public string ValidateLabel => Text("Model.Validate");
    public string PublishLabel => Text("Model.Publish");
    public string CloneLabel => Text("Model.Clone");
    public string DiscardLabel => Text("Model.Discard");
    public string NodeLabel => Text("Model.Nodes");
    public string NodeSectionHelp => Text("Model.NodeSectionHelp");
    public string NodeRequiredHelp => Text("Model.NodeRequiredHelp");
    public string NodeListTitle => Text("Model.NodeListTitle");
    public string NodeListHelp => Text("Model.NodeListHelp");
    public string NodeFormTitle => Text(SelectedNode is null ? "Model.NodeFormNew" : "Model.NodeFormEdit");
    public string NodePrimaryActionLabel => Text(SelectedNode is null ? "Model.NodeSaveCreate" : "Model.NodeSaveEdit");
    public string NodePersistenceHelp => Text("Model.NodePersistenceHelp");
    public string OptionalNodeDataLabel => Text("Model.OptionalNodeData");
    public string FunctionHelp => Text("Model.FunctionHelp");
    public string RoleHelp => Text("Model.RoleHelp");
    public string UnitHelp => Text("Model.UnitHelp");
    public string LayerHelp => Text("Model.LayerHelp");
    public string PoolHelp => Text("Model.PoolHelp");
    public string NodeCodeLabel => Text("Model.NodeCode");
    public string PhysicalLabel => Text("Model.Physical");
    public string RoleLabel => Text("Model.Role");
    public string FunctionLabel => Text("Model.FunctionTags");
    public string UnitLabel => Text("Model.DriverUnit");
    public string LayerLabel => Text("Model.CostLayer");
    public string PoolLabel => Text("Model.NodeDescription");
    public string ApplyLabel => Text("Model.ApplyNode");
    public string NewNodeLabel => Text("Model.NewNode");
    public string RemoveLabel => Text("Model.RemoveNode");
    public string ToggleUnitLabel => Text(UnitMaster?.Value.IsActive == false ? "Model.ActivateUnit" : "Model.DeactivateUnit");
    public string CancelLabel => Text("CompanyCancel");
    public string RefreshLabel => Text("Model.Refresh");

    private void RefreshCommands()
    {
        foreach (var c in new IRelayCommand[]
        {
            CreateCenterCommand,
            SaveCenterCommand,
            NewCenterCommand,
            SaveCenterFormCommand,
            DeleteCenterCommand, ToggleCenterCommand,
            ToggleUnitCommand,
            NewDraftCommand,
            LoadCommand,
            SaveCommand,
            ValidateCommand,
            PublishCommand,
            CloneCommand,
            ApplyNodeCommand,
            SaveNodeCommand,
            RemoveNodeCommand,
            DeleteNodeCommand,
            NewNodeCommand,
            DiscardCommand,
            CancelCommand,
            RefreshCommand
        }

        )
            c.NotifyCanExecuteChanged();
    }

    private void Refresh()
    {
        OnPropertyChanged(string.Empty);
        RefreshCommands();
        company.NotifyWorkspaceStateChanged();
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> values)
    {
        target.Clear();
        foreach (var value in values)
            target.Add(value);
    }

    private string ActivityUnitName(UnitDefinition definition)
    {
        var name = DefaultActivityUnitCatalog.IsPreset(definition)
            ? Text("ActivityUnit." + definition.Code)
            : definition.Code;
        return string.IsNullOrWhiteSpace(definition.Symbol)
            ? name
            : name + " (" + definition.Symbol + ")";
    }

    private void Reformat()
    {
        loading = true;
        var preservedCenterCode = centerCode;
        var preservedCenterName = centerName;
        var masterId = master?.Value.Id;
        var physicalId = physical?.Value.Id;
        var unitId = unit?.Value.Definition.Id;
        var unitMasterId = unitMaster?.Value.Definition.Id;
        var choiceId = choice?.Value.Structure.Id;
        var nodeId = node?.Node.Id;
        var roleValue = role?.Value ?? NodeRole.Principal;
        var layerValue = layer?.Value ?? CostLayer.Unclassified;
        Replace(Centers, centerValues.Select(c => new CenterOption(
            c,
            c.Code + " · " + c.Name + " · " + Text(c.IsActive ? "Model.Active" : "Model.Inactive"),
            Text(c.IsActive ? "Model.Active" : "Model.Inactive"))));
        Replace(Units, unitValues.Select(u => new UnitOption(u, ActivityUnitName(u.Definition) + " · " + Text(u.IsActive ? "Model.Active" : "Model.Inactive"))));
        Replace(Models, modelValues.Select(m => new ModelOption(m, m.Structure.Description + " · " + Text(m.Status == ModelStatus.Draft ? "Model.Draft" : "Model.Published") + " · " + m.Structure.VersionNumber.ToString(Format))));
        Replace(Roles, Enum.GetValues<NodeRole>().Select(r => new RoleOption(r, Text(r == NodeRole.Principal ? "Model.Principal" : "Model.Auxiliary"))));
        Replace(Layers, Enum.GetValues<CostLayer>().Select(l => new LayerOption(l, Text(l switch
        {
            CostLayer.Fixed => "Model.LayerFixed",
            CostLayer.Variable => "Model.LayerVariable",
            CostLayer.Mixed => "Model.LayerMixed",
            _ => "Model.LayerUnclassified"
        }))));
        Replace(Nodes, draftNodes.OrderBy(n => Current?.Publication?.PhysicalCenters.SingleOrDefault(c => c.Id == n.PhysicalCenterId)?.Code ?? centerValues.SingleOrDefault(c => c.Id == n.PhysicalCenterId)?.Code, StringComparer.Ordinal).ThenBy(n => n.Code, StringComparer.Ordinal).ThenBy(n => n.Role == NodeRole.Principal ? 0 : 1).Select(n =>
        {
            var p = draftPools.SingleOrDefault(p => p.NodeId == n.Id);
            var unitDefinition = Current?.Publication?.DriverUnits.SingleOrDefault(u => u.Id == p?.DriverUnitId)
                ?? unitValues.SingleOrDefault(u => u.Definition.Id == p?.DriverUnitId)?.Definition;
            var unitName = unitDefinition is null ? "—" : ActivityUnitName(unitDefinition);
            var frozenCenter = Current?.Publication?.PhysicalCenters.SingleOrDefault(c => c.Id == n.PhysicalCenterId);
            var liveCenter = centerValues.SingleOrDefault(c => c.Id == n.PhysicalCenterId);
            var centerName = frozenCenter is not null ? frozenCenter.Code + " · " + frozenCenter.Name : liveCenter is not null ? liveCenter.Code + " · " + liveCenter.Name : "—";
            var roleName = Text(n.Role == NodeRole.Principal ? "Model.Principal" : "Model.Auxiliary");
            var layerName = Text((p?.CostLayer ?? CostLayer.Unclassified) switch
            {
                CostLayer.Fixed => "Model.LayerFixed",
                CostLayer.Variable => "Model.LayerVariable",
                CostLayer.Mixed => "Model.LayerMixed",
                _ => "Model.LayerUnclassified"
            });
            var functions = string.Join(", ", n.FunctionTags);
            return new NodeOption(
                n,
                p,
                (p?.Description ?? n.Code) + " · " + n.Code + " · " + centerName + " · " + roleName + " · " + functions + " · " + unitName,
                centerName,
                roleName,
                unitName,
                layerName,
                functions,
                p?.Description ?? "");
        }));
        master = Centers.SingleOrDefault(c => c.Value.Id == masterId);
        physical = Centers.SingleOrDefault(c => c.Value.Id == physicalId);
        unit = Units.SingleOrDefault(u => u.Value.Definition.Id == unitId);
        unitMaster = Units.SingleOrDefault(u => u.Value.Definition.Id == unitMasterId);
        choice = Models.SingleOrDefault(m => m.Value.Structure.Id == choiceId);
        node = Nodes.SingleOrDefault(n => n.Node.Id == nodeId);
        role = Roles.Single(r => r.Value == roleValue);
        layer = Layers.Single(l => l.Value == layerValue);
        centerCode = preservedCenterCode;
        centerName = preservedCenterName;
        loading = false;
        Refresh();
    }

    private void CompanyStateChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        RefreshCommands();
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(CanEditMasters));
        OnPropertyChanged(nameof(CentersReadOnly)); OnPropertyChanged(nameof(NodesReadOnly));
        OnPropertyChanged(nameof(DescriptionReadOnly));
        OnPropertyChanged(nameof(InputsEnabled));
    }

    private void PreferencesChanged(object? sender, PresentationPreferencesChangedEventArgs e) => Reformat();
    public void RefreshPresentation(bool usePseudo)
    {
        pseudo = usePseudo;
        Reformat();
    }

    private async Task Run(Func<CancellationToken, Task> operation)
    {
        busy = true;
        diagnostic = null;
        statusKey = "Model.Working";
        using var source = new CancellationTokenSource();
        cancellation = source;
        Refresh();
        try
        {
            await operation(source.Token);
            if (statusKey == "Model.Working")
                statusKey = "Model.Completed";
        }
        catch (DomainRuleException ex)
        {
            diagnostic = ex.Diagnostic;
        }
        catch (CompanyFileException ex)
        {
            diagnostic = ex.Diagnostic;
        }
        catch (OperationCanceledException)
        {
            statusKey = "CompanyCancelled";
        }
        catch (ArgumentException)
        {
            diagnostic = new("MODEL_INPUT_INVALID", "Model.InputInvalid", []);
        }
        catch (Exception ex)
        {
            Trace.TraceError("Model command: {0}", ex);
            diagnostic = new("FILE_IO_FAILED", "File.IoFailed", []);
        }
        finally
        {
            busy = false;
            cancellation = null;
            Refresh();
        }
    }

    private async Task RefreshWorkspace() => await Run(async token =>
    {
        await Reload(token);
        statusKey = "Model.Refreshed";
    });

    private async Task Reload(CancellationToken token)
    {
        var port = company.ActiveModels!;
        var editedMaster = MasterEdited ? master?.Value : null;
        var modelWasClean = !dirty && !nodeFormEdited && !CenterFormEdited;
        var data = await Task.Run(() => port.ReadWorkspace(token), token);
        centerValues = editedMaster is null ? data.Centers :
            data.Centers.Select(c => c.Id == editedMaster.Id ? editedMaster : c).ToArray();
        unitValues = data.DriverUnits;
        modelValues = data.Models;
        committedRefreshPending = false;
        Reformat();
        if (editedMaster is null && master is not null)
            SetMaster(master.Value.Id);
        if (modelWasClean && Current is not null &&
            modelValues.SingleOrDefault(m => m.Structure.Id == Current.Structure.Id) is { } refreshed)
            SetCurrent(refreshed);
    }

    private async Task ApplyConfirmedThenReload<T>(T confirmed, Action<T> apply)
    {
        var outcome = await ConfirmedCommitRefresh.ApplyThenRefreshAsync(
            confirmed, apply, () => Reload(CancellationToken.None));
        if (!outcome.RefreshSucceeded)
        {
            committedRefreshPending = true;
            statusKey = "Model.CommittedRefreshPending";
            Trace.TraceWarning("Model commit succeeded; refresh pending: {0}", outcome.RefreshFailure);
        }
    }

    private void ApplyConfirmedModel(ModelVersionSnapshot value)
    {
        modelValues = modelValues.Where(m => m.Structure.Id != value.Structure.Id).Append(value).ToArray();
        SetCurrent(value);
    }

    private void ApplyConfirmedCenter(PhysicalCenter value)
    {
        centerValues = centerValues.Where(c => c.Id != value.Id).Append(value).ToArray();
        Reformat();
        ClearCenterFormCore();
    }

    private void ApplyConfirmedUnit(DriverUnitState value)
    {
        unitValues = unitValues.Where(u => u.Definition.Id != value.Definition.Id).Append(value).ToArray();
        Reformat();
        unitMaster = Units.Single(u => u.Value.Definition.Id == value.Definition.Id);
        Refresh();
    }

    public async Task CompanyChanged()
    {
        var id = Current?.Structure.CompanyId;
        if (company.ActiveModels is null)
        {
            statusKey = "Model.NoFile";
            committedRefreshPending = false;
            Current = null;
            dirty = false;
            master = null;
            centerCode = "";
            centerName = "";
            nodeFormEdited = false;
            centerValues = [];
            unitValues = [];
            modelValues = [];
            draftNodes.Clear();
            draftPools.Clear();
            Reformat();
            return;
        }

        await Run(async token =>
        {
            await Reload(token);
            if (boundPath != company.ActivePath)
            {
                Current = null;
                dirty = false;
                draftNodes.Clear();
                draftPools.Clear();
                description = "";
                ResetNode();
            }

            boundPath = company.ActivePath;
            Reformat();
        });
    }

    private void SetCurrent(ModelVersionSnapshot current)
    {
        loading = true;
        Current = current;
        description = current.Structure.Description;
        dirty = false;
        validation = [];
        draftNodes.Clear();
        draftNodes.AddRange(current.Structure.Nodes);
        draftPools.Clear();
        draftPools.AddRange(current.Structure.Pools);
        loading = false;
        ResetNode();
        Reformat();
    }

    private async Task NewNode()
    {
        if (nodeFormEdited)
        {
            var decision = dialogs.ResolvePending(Text("Model.PendingTitle"), Text("Model.PendingBody"));
            if (decision == PendingEditChoice.Cancel) return;
            if (decision == PendingEditChoice.Save)
            {
                await SaveNode();
                if (nodeFormEdited || diagnostic is not null) return;
            }
        }
        ResetNode();
        diagnostic = null;
        statusKey = "Model.Ready";
        Refresh();
    }

    private void ClearNodeForm()
    {
        if (nodeFormEdited)
        {
            var choice = dialogs.ResolvePending(Text("Model.PendingTitle"), Text("Model.PendingBody"));
            if (choice == PendingEditChoice.Cancel)
                return;
            if (choice == PendingEditChoice.Save)
            {
                ApplyNode();
                if (nodeFormEdited)
                    return;
            }
        }

        ResetNode();
        diagnostic = null;
        statusKey = "Model.Ready";
        Refresh();
    }

    private void ResetNode()
    {
        nodeFormEdited = false;
        node = null;
        nodeCode = "";
        nodeCodeManuallyEdited = false;
        functionTags = "";
        poolDescription = "";
        physical = null;
        unit = null;
        role = Roles.FirstOrDefault();
        layer = Layers.FirstOrDefault();
        Refresh();
    }

    private ModelStructure Structure() => new(Current!.Structure.Id, Current.Structure.CompanyId, Current.Structure.FamilyId, Current.Structure.VersionNumber, Current.Structure.ParentVersionId, Description, Current.Structure.PrecisionPolicyVersion, Current.Structure.MethodPolicy, draftNodes.ToArray(), draftPools.ToArray());
    public async Task<bool> CanLeaveAsync()
    {
        if (busy)
            return false;
        if (!IsDirty)
            return true;
        switch (dialogs.ResolvePending(Text("Model.PendingTitle"), Text("Model.PendingBody")))
        {
            case PendingEditChoice.Cancel:
                return false;
            case PendingEditChoice.Discard:
                ClearCenterFormCore();
                if (Current is not null)
                    SetCurrent(Current);
                Refresh();
                return true;
            case PendingEditChoice.Save:
                if (CenterFormEdited)
                {
                    await SaveCenterForm();
                    if (CenterFormEdited)
                        return false;
                }

                if (dirty || nodeFormEdited)
                    await Save();
                return !IsDirty;
            default:
                return false;
        }
    }

    private void SetMaster(EntityId id)
    {
        master = Centers.Single(x => x.Value.Id == id);
        centerCode = master.Value.Code;
        centerName = master.Value.Name;
        Refresh();
    }

    private async Task CreateCenter()
    {
        if (DuplicateCenterCode) { Refresh(); return; }
        if (string.IsNullOrWhiteSpace(CenterCode) || string.IsNullOrWhiteSpace(CenterName))
        { diagnostic = new("MODEL_CENTER_INVALID", "Model.CenterInvalid", []); Refresh(); return; }
        await Run(async t =>
    {
        var created = await Task.Run(() => company.ActiveModels!.CreateCenter(new(company.Snapshot!.Id, CenterCode, CenterName, "local-user"), t));
        await ApplyConfirmedThenReload(created, ApplyConfirmedCenter);
        if (!committedRefreshPending) statusKey = "Model.CenterCreated";
        });
    }

    private async Task SaveCenter() => await ChangeCenter(Master!.Value.IsActive, "Model.CenterSaved");
    private async Task DeleteCenter()
    {
        if (Master is not { } selected) return;
        if (!dialogs.ConfirmEconomicChange(DeleteCenterLabel, selected.Label + "\n" + Text("Model.DeleteCenterConfirm"), DeleteCenterLabel, Text("Model.Cancel"))) return;
        await Run(async token =>
        {
            await Task.Run(() => company.ActiveModels!.DeleteCenter(new(selected.Value.CompanyId, selected.Value.Id, selected.Value.Revision, "local-user"), token));
            ClearCenterFormCore();
            await Reload(token);
        });
    }
    private async Task ToggleCenter() => await ChangeCenter(!Master!.Value.IsActive, "Model.CenterStateChanged");

    private async Task SaveCenterForm()
    {
        if (Master is null) await CreateCenter();
        else await SaveCenter();
    }

    private async Task NewCenter()
    {
        if (CenterFormEdited)
        {
            var choice = dialogs.ResolvePending(Text("Model.PendingTitle"), Text("Model.PendingBody"));
            if (choice == PendingEditChoice.Cancel) return;
            if (choice == PendingEditChoice.Save)
            {
                await SaveCenterForm();
                if (CenterFormEdited) return;
            }
        }
        ClearCenterFormCore();
    }

    private void ClearCenterFormCore()
    {
        loading = true;
        master = null;
        centerCode = "";
        centerName = "";
        loading = false;
        diagnostic = null;
        statusKey = "Model.Ready";
        Refresh();
    }

    private async Task ChangeCenter(bool active, string successKey)
    {
        if (DuplicateCenterCode) { Refresh(); return; }
        if (string.IsNullOrWhiteSpace(CenterCode) || string.IsNullOrWhiteSpace(CenterName))
        { diagnostic = new("MODEL_CENTER_INVALID", "Model.CenterInvalid", []); Refresh(); return; }
        await Run(async t =>
    {
        var current = Master!.Value;
        var result = await Task.Run(() => company.ActiveModels!.ChangeCenter(new(company.Snapshot!.Id, current.Id, current.Revision, CenterCode, CenterName, active, "local-user"), t));
        await ApplyConfirmedThenReload(result, ApplyConfirmedCenter);
        if (!committedRefreshPending) statusKey = successKey;
        });
    }
    private async Task ToggleUnit() => await Run(async t =>
    {
        var u = UnitMaster!.Value;
        var result = await Task.Run(() => company.ActiveModels!.ChangeUnitState(new(company.Snapshot!.Id, u.Definition.Id, u.Revision, !u.IsActive, "local-user"), t));
        await ApplyConfirmedThenReload(result, ApplyConfirmedUnit);
    });
    private async Task NewDraft()
    {
        if (!await CanLeaveAsync())
            return;
        await Run(async t =>
        {
            if (string.IsNullOrWhiteSpace(Description))
            { diagnostic = new("MODEL_STRUCTURE_INVALID", "Model.StructureInvalid", []); return; }
            var value = await Task.Run(() => company.ActiveModels!.CreateDraft(new(company.Snapshot!.Id, Description, "local-user"), t));
            await ApplyConfirmedThenReload(value, ApplyConfirmedModel);
            if (!committedRefreshPending) statusKey = "Model.StructureCreated";
        });
    }

    private async Task LoadChosen()
    {
        var selected = ModelChoice!.Value;
        if (!await CanLeaveAsync())
            return;
        SetCurrent(selected);
    }

    private async Task Save()
    {
        if (nodeFormEdited)
        {
            ApplyNode();
            if (nodeFormEdited)
                return;
        }

        if (EditingStudyNodes) { await SaveStudyNodesAsync(); return; }

        await Run(async t =>
        {
            if (string.IsNullOrWhiteSpace(Description))
            { diagnostic = new("MODEL_STRUCTURE_INVALID", "Model.StructureInvalid", []); return; }
            var value = Structure();
            var port = company.ActiveModels!;
            var current = Current!;
            var saved = await Task.Run(() => port.SaveDraft(new(value, current.Revision, "local-user"), t));
            await ApplyConfirmedThenReload(saved, ApplyConfirmedModel);
            if (!committedRefreshPending) statusKey = "Model.StructureSaved";
        });
    }

    private async Task Validate() => await Run(async t =>
    {
        validation = await Task.Run(() => company.ActiveModels!.ValidateModel(company.Snapshot!.Id, Current!.Structure.Id, t));
        if (validation.Count == 0)
            statusKey = "Model.ValidStructure";
    });
    private async Task Publish() => await Run(async t =>
    {
        var current = Current!;
        var result = await Task.Run(() => company.ActiveModels!.Publish(new(company.Snapshot!.Id, current.Structure.Id, current.Revision, "local-user"), t));
        await ApplyConfirmedThenReload(result, ApplyConfirmedModel);
        if (!committedRefreshPending) statusKey = "Model.StructurePublished";
    });
    private async Task Clone() => await Run(async t =>
    {
        var current = Current!;
        var result = await Task.Run(() => company.ActiveModels!.Clone(new(company.Snapshot!.Id, current.Structure.Id, current.Revision, new(Guid.NewGuid()), current.Structure.Description, "local-user"), t));
        await ApplyConfirmedThenReload(result, ApplyConfirmedModel);
    });
    private void ApplyNode()
    {
        try
        {
            if (Physical is null || DriverUnit is null || Role is null)
            { diagnostic = new("MODEL_INPUT_INVALID", "Model.InputInvalid", []); Refresh(); return; }
            var tags = FunctionTags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (string.IsNullOrWhiteSpace(NodeCode) || tags.Distinct(StringComparer.Ordinal).Count() != tags.Length)
            { diagnostic = new("MODEL_NODE_INVALID", "Model.NodeInvalid", []); Refresh(); return; }
            if (string.IsNullOrWhiteSpace(Description))
            { diagnostic = new("MODEL_STRUCTURE_INVALID", "Model.StructureInvalid", []); Refresh(); return; }
            if (draftNodes.Any(n => n.Id != SelectedNode?.Node.Id && n.Code == NodeCode))
            { diagnostic = new("MODEL_DUPLICATE", "Model.Duplicate", []); Refresh(); return; }
            var id = SelectedNode?.Node.Id ?? new(Guid.NewGuid());
            var modelId = Current!.Structure.Id;
            var n = new CalculationNodeDefinition(id, modelId, Physical.Value.Id, NodeCode, Role.Value, FunctionTags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), true);
            var p = new CostPoolDefinition(SelectedNode?.Pool?.Id ?? new(Guid.NewGuid()), modelId, n.Id, DriverUnit.Value.Definition.Id, PoolDescription, SelectedNode?.Pool?.CostLayer ?? CostLayer.Unclassified, SelectedNode?.Pool?.CapacityPolicy ?? ModelPolicyReference.PendingCapacity);
            var ns = draftNodes.Where(x => x.Id != id).Append(n).ToArray();
            var ps = draftPools.Where(x => x.NodeId != id).Append(p).ToArray();
            _ = new ModelStructure(Current.Structure.Id, Current.Structure.CompanyId, Current.Structure.FamilyId, Current.Structure.VersionNumber, Current.Structure.ParentVersionId, Description, Current.Structure.PrecisionPolicyVersion, Current.Structure.MethodPolicy, ns, ps);
            draftNodes.Clear();
            draftNodes.AddRange(ns);
            draftPools.Clear();
            draftPools.AddRange(ps);
            dirty = true;
            diagnostic = null;
            validation = [];
            ResetNode();
            Reformat();
        }
        catch (DomainRuleException ex)
        {
            diagnostic = ex.Diagnostic;
            Refresh();
        }
        catch (ArgumentException)
        {
            diagnostic = new("MODEL_INPUT_INVALID", "Model.InputInvalid", []);
            Refresh();
        }
    }

    private async Task SaveNode()
    {
        if (!nodeFormEdited) return;

        var selectedId = SelectedNode?.Node.Id;
        var code = NodeCode;
        var functions = FunctionTags;
        var pool = PoolDescription;
        var manuallyEdited = nodeCodeManuallyEdited;
        var physicalId = Physical?.Value.Id;
        var unitId = DriverUnit?.Value.Definition.Id;
        var roleValue = Role?.Value;
        var layerValue = Layer?.Value;
        var wasDirty = dirty;
        var originalNodes = draftNodes.ToArray();
        var originalPools = draftPools.ToArray();

        ApplyNode();
        if (nodeFormEdited || diagnostic is not null) return;

        await Save();
        if (!IsDirty && diagnostic is null)
        {
            statusKey = "Model.NodeSaved";
            Refresh();
            return;
        }

        // Persistence failed: restore the saved draft and the user's form so retrying is safe.
        draftNodes.Clear();
        draftNodes.AddRange(originalNodes);
        draftPools.Clear();
        draftPools.AddRange(originalPools);
        dirty = wasDirty;
        Reformat();

        loading = true;
        node = selectedId is null ? null : Nodes.SingleOrDefault(n => n.Node.Id == selectedId);
        nodeCode = code;
        nodeCodeManuallyEdited = manuallyEdited;
        functionTags = functions;
        poolDescription = pool;
        physical = Centers.SingleOrDefault(c => c.Value.Id == physicalId);
        unit = Units.SingleOrDefault(u => u.Value.Definition.Id == unitId);
        role = roleValue is null ? Roles.FirstOrDefault() : Roles.SingleOrDefault(r => r.Value == roleValue);
        layer = layerValue is null ? Layers.FirstOrDefault() : Layers.SingleOrDefault(l => l.Value == layerValue);
        nodeFormEdited = true;
        loading = false;
        Refresh();
    }

    private async Task DeleteNode()
    {
        if (SelectedNode is null || nodeFormEdited) return;

        var selectedId = SelectedNode.Node.Id;
        var wasDirty = dirty;
        var originalNodes = draftNodes.ToArray();
        var originalPools = draftPools.ToArray();

        RemoveNode();
        await Save();
        if (!IsDirty && diagnostic is null)
        {
            statusKey = "Model.NodeRemoved";
            Refresh();
            return;
        }

        // Failed deletion must not make the UI look as if persistence succeeded.
        draftNodes.Clear();
        draftNodes.AddRange(originalNodes);
        draftPools.Clear();
        draftPools.AddRange(originalPools);
        dirty = wasDirty;
        Reformat();
        SelectedNode = Nodes.SingleOrDefault(n => n.Node.Id == selectedId);
        Refresh();
    }

    private void RemoveNode()
    {
        var id = SelectedNode!.Node.Id;
        draftNodes.RemoveAll(n => n.Id == id);
        draftPools.RemoveAll(p => p.NodeId == id);
        dirty = true;
        validation = [];
        ResetNode();
        Reformat();
    }

    public void Dispose()
    {
        company.PropertyChanged -= CompanyStateChanged;
        preferences.Changed -= PreferencesChanged;
        cancellation?.Cancel();
    }
}
