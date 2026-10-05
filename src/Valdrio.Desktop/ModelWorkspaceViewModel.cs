using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Valdrio.Application;
using Valdrio.Domain;
using Valdrio.Localization;

namespace Valdrio.Desktop;
public sealed record CenterOption(PhysicalCenter Value, string Label);
public sealed record UnitOption(DriverUnitState Value, string Label);
public sealed record ModelOption(ModelVersionSnapshot Value, string Label);
public sealed record RoleOption(NodeRole Value, string Label);
public sealed record LayerOption(CostLayer Value, string Label);
public sealed record NodeOption(CalculationNodeDefinition Node, CostPoolDefinition? Pool, string Label);
public sealed class ModelWorkspaceViewModel : ObservableObject, IDisposable
{
    private readonly CompanyFileViewModel company;
    private readonly UiTextCatalog texts;
    private readonly PresentationPreferencesService preferences;
    private readonly IModelEditorDialogs dialogs;
    private readonly PresentationFormatter formatter;
    private bool busy, dirty, pseudo, nodeFormEdited;
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
    private string? boundPath;
    public ModelWorkspaceViewModel(CompanyFileViewModel company, UiTextCatalog texts, PresentationPreferencesService preferences, IModelEditorDialogs dialogs, PresentationFormatter formatter)
    {
        this.company = company;
        this.texts = texts;
        this.preferences = preferences;
        this.dialogs = dialogs;
        this.formatter = formatter;
        CreateCenterCommand = new AsyncRelayCommand(CreateCenter, () => Ready);
        SaveCenterCommand = new AsyncRelayCommand(SaveCenter, () => Ready && Master is not null);
        ToggleCenterCommand = new AsyncRelayCommand(ToggleCenter, () => Ready && Master is not null);
        ToggleUnitCommand = new AsyncRelayCommand(ToggleUnit, () => Ready && UnitMaster is not null);
        NewDraftCommand = new AsyncRelayCommand(NewDraft, () => Ready);
        LoadCommand = new AsyncRelayCommand(LoadChosen, () => Ready && ModelChoice is not null);
        SaveCommand = new AsyncRelayCommand(Save, () => Ready && IsEditable && (dirty || nodeFormEdited));
        ValidateCommand = new AsyncRelayCommand(Validate, () => Ready && Current is not null && !dirty && !nodeFormEdited);
        PublishCommand = new AsyncRelayCommand(Publish, () => Ready && IsEditable && !dirty && !nodeFormEdited);
        CloneCommand = new AsyncRelayCommand(Clone, () => Ready && Current?.Status == ModelStatus.Published);
        ApplyNodeCommand = new RelayCommand(ApplyNode, () => Ready && IsEditable);
        RemoveNodeCommand = new RelayCommand(RemoveNode, () => Ready && IsEditable && SelectedNode is not null);
        NewNodeCommand = new RelayCommand(ClearNodeForm, () => Ready && IsEditable);
        DiscardCommand = new RelayCommand(() =>
        {
            if (Current is not null)
                SetCurrent(Current);
        }, () => !busy && (dirty || nodeFormEdited));
        CancelCommand = new RelayCommand(() => cancellation?.Cancel(), () => busy);
        preferences.Changed += PreferencesChanged;
        company.PropertyChanged += CompanyStateChanged;
        company.WorkspaceBusy = () => busy;
        company.BeforeChangeAsync = CanLeaveAsync;
        company.AfterChangeAsync = CompanyChanged;
        Reformat();
    }

    public bool IsBusy => busy;
    private bool MasterEdited => master is not null && (centerCode != master.Value.Code || centerName != master.Value.Name);
    public bool IsDirty => dirty || nodeFormEdited || MasterEdited;
    public bool IsPublished => Current?.Status == ModelStatus.Published;
    private bool Ready => !busy && company.IsIdle && company.ActiveModels is not null;
    public bool IsEditable => Current?.Status == ModelStatus.Draft;
    public bool CanEdit => Ready && IsEditable;
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
            if (!loading && MasterEdited && master != value)
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
        await SaveCenter();
        if (!MasterEdited)
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
                nodeFormEdited = true;
                Refresh();
            }
        }
    }

    public UnitOption? UnitMaster
    {
        get => unitMaster;
        set
        {
            SetProperty(ref unitMaster, value);
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
                nodeFormEdited = true;
                Refresh();
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
            if (SetProperty(ref node, value) && value is not null && !loading)
            {
                nodeCode = value.Node.Code;
                functionTags = string.Join(", ", value.Node.FunctionTags);
                poolDescription = value.Pool?.Description ?? "";
                physical = Centers.SingleOrDefault(c => c.Value.Id == value.Node.PhysicalCenterId);
                unit = Units.SingleOrDefault(u => u.Value.Definition.Id == value.Pool?.DriverUnitId);
                role = Roles.Single(x => x.Value == value.Node.Role);
                layer = Layers.Single(x => x.Value == (value.Pool?.CostLayer ?? CostLayer.Unclassified));
                nodeFormEdited = false;
                Refresh();
            }
        }
    }

    public RoleOption? Role
    {
        get => role;
        set
        {
            if (SetProperty(ref role, value) && !loading && IsEditable)
            {
                nodeFormEdited = true;
                Refresh();
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
                nodeFormEdited = true;
                Refresh();
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
            if (SetProperty(ref description, value) && !loading && IsEditable)
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
                nodeFormEdited = true;
                Refresh();
            }
        }
    }

    public string FunctionTags
    {
        get => functionTags;
        set
        {
            if (SetProperty(ref functionTags, value) && !loading && IsEditable)
            {
                nodeFormEdited = true;
                Refresh();
            }
        }
    }

    public string PoolDescription
    {
        get => poolDescription;
        set
        {
            if (SetProperty(ref poolDescription, value) && !loading && IsEditable)
            {
                nodeFormEdited = true;
                Refresh();
            }
        }
    }

    public IAsyncRelayCommand CreateCenterCommand { get; }
    public IAsyncRelayCommand SaveCenterCommand { get; }
    public IAsyncRelayCommand ToggleCenterCommand { get; }
    public IAsyncRelayCommand ToggleUnitCommand { get; }
    public IAsyncRelayCommand NewDraftCommand { get; }
    public IAsyncRelayCommand LoadCommand { get; }
    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand ValidateCommand { get; }
    public IAsyncRelayCommand PublishCommand { get; }
    public IAsyncRelayCommand CloneCommand { get; }
    public IRelayCommand ApplyNodeCommand { get; }
    public IRelayCommand RemoveNodeCommand { get; }
    public IRelayCommand NewNodeCommand { get; }
    public IRelayCommand DiscardCommand { get; }
    public IRelayCommand CancelCommand { get; }
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

    public string Status => diagnostic is null ? Text(statusKey) : DiagnosticText(diagnostic);
    public string ValidationText => string.Join(Environment.NewLine, validation.Select(DiagnosticText));
    public string Details => Current is null ? Text("Model.NoneSelected") : string.Format(Format, Text("Model.Details"), Current.Structure.Description, Text(Current.Status == ModelStatus.Draft ? "Model.Draft" : "Model.Published"), Current.Structure.VersionNumber, Current.Revision, Current.Structure.ParentVersionId?.ToString() ?? "—", Current.PublishedAtUtc is null ? "—" : formatter.FormatInstant(Current.PublishedAtUtc.Value.Value, preferences.Current.DisplayTimeZoneId, Format), Current.StructuralHash ?? "—", IsDirty ? Text("Model.Unsaved") : Text("Model.Saved"));
    public string HistoricalDefinitions => Current?.Publication is not { } p ? "" : string.Join(Environment.NewLine, p.PhysicalCenters.Select(c => c.Code + " · " + c.Name).Concat(p.DriverUnits.Select(u => u.Code + " · " + u.Dimension + " · " + u.Symbol)));
    public string Title => Text("Model.Title");
    public string Notes => Text("Model.Notes");
    public string CenterLabel => Text("Model.Centers");
    public string CenterCodeLabel => Text("Model.CenterCode");
    public string CenterNameLabel => Text("Model.CenterName");
    public string CreateCenterLabel => Text("Model.CreateCenter");
    public string SaveCenterLabel => Text("Model.SaveCenter");
    public string ToggleCenterLabel => Text("Model.ToggleCenter");
    public string ModelLabel => Text("Model.Models");
    public string DescriptionLabel => Text("Model.Description");
    public string NewDraftLabel => Text("Model.NewDraft");
    public string LoadLabel => Text("Model.Load");
    public string SaveLabel => Text("Model.Save");
    public string ValidateLabel => Text("Model.Validate");
    public string PublishLabel => Text("Model.Publish");
    public string CloneLabel => Text("Model.Clone");
    public string DiscardLabel => Text("Model.Discard");
    public string NodeLabel => Text("Model.Nodes");
    public string NodeCodeLabel => Text("Model.NodeCode");
    public string PhysicalLabel => Text("Model.Physical");
    public string RoleLabel => Text("Model.Role");
    public string FunctionLabel => Text("Model.FunctionTags");
    public string UnitLabel => Text("Model.DriverUnit");
    public string LayerLabel => Text("Model.CostLayer");
    public string PoolLabel => Text("Model.PoolDescription");
    public string ApplyLabel => Text("Model.ApplyNode");
    public string NewNodeLabel => Text("Model.NewNode");
    public string RemoveLabel => Text("Model.RemoveNode");
    public string ToggleUnitLabel => Text("Model.ToggleUnit");
    public string CancelLabel => Text("CompanyCancel");

    private void RefreshCommands()
    {
        foreach (var c in new IRelayCommand[]
        {
            CreateCenterCommand,
            SaveCenterCommand,
            ToggleCenterCommand,
            ToggleUnitCommand,
            NewDraftCommand,
            LoadCommand,
            SaveCommand,
            ValidateCommand,
            PublishCommand,
            CloneCommand,
            ApplyNodeCommand,
            RemoveNodeCommand,
            NewNodeCommand,
            DiscardCommand,
            CancelCommand
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
        Replace(Centers, centerValues.Select(c => new CenterOption(c, c.Code + " · " + c.Name + " · " + Text(c.IsActive ? "Model.Active" : "Model.Inactive"))));
        Replace(Units, unitValues.Select(u => new UnitOption(u, u.Definition.Code + " · " + u.Definition.Dimension + " · " + Text(u.IsActive ? "Model.Active" : "Model.Inactive"))));
        Replace(Models, modelValues.Select(m => new ModelOption(m, m.Structure.Description + " · " + Text(m.Status == ModelStatus.Draft ? "Model.Draft" : "Model.Published") + " · " + m.Structure.VersionNumber.ToString(Format))));
        Replace(Roles, Enum.GetValues<NodeRole>().Select(r => new RoleOption(r, Text(r == NodeRole.Principal ? "Model.Principal" : "Model.Auxiliary"))));
        Replace(Layers, Enum.GetValues<CostLayer>().Select(l => new LayerOption(l, Text(l switch
        {
            CostLayer.Fixed => "Model.LayerFixed",
            CostLayer.Variable => "Model.LayerVariable",
            CostLayer.Mixed => "Model.LayerMixed",
            _ => "Model.LayerUnclassified"
        }))));
        Replace(Nodes, draftNodes.Select(n =>
        {
            var p = draftPools.SingleOrDefault(p => p.NodeId == n.Id);
            var unitName = unitValues.SingleOrDefault(u => u.Definition.Id == p?.DriverUnitId)?.Definition.Code ?? "—";
            return new NodeOption(n, p, n.Code + " · " + Text(n.Role == NodeRole.Principal ? "Model.Principal" : "Model.Auxiliary") + " · " + string.Join(",", n.FunctionTags) + " · " + unitName);
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

    private async Task Reload(CancellationToken token)
    {
        var port = company.ActiveModels!;
        var data = await Task.Run(() => (port.ReadCenters(token), port.ReadDriverUnits(token), port.ReadModels(token)), token);
        centerValues = data.Item1;
        unitValues = data.Item2;
        modelValues = data.Item3;
        Reformat();
    }

    public async Task CompanyChanged()
    {
        var id = Current?.Structure.CompanyId;
        if (company.ActiveModels is null)
        {
            statusKey = "Model.NoFile";
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
    }

    private void ResetNode()
    {
        nodeFormEdited = false;
        node = null;
        nodeCode = "";
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
                if (master is not null)
                {
                    centerCode = master.Value.Code;
                    centerName = master.Value.Name;
                }

                if (Current is not null)
                    SetCurrent(Current);
                Refresh();
                return true;
            case PendingEditChoice.Save:
                if (MasterEdited)
                {
                    await SaveCenter();
                    if (MasterEdited)
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

    private async Task CreateCenter() => await Run(async t =>
    {
        var c = await Task.Run(() => company.ActiveModels!.CreateCenter(new(company.Snapshot!.Id, CenterCode, CenterName, "local-user"), t));
        await Reload(t);
        SetMaster(c.Id);
    });
    private async Task SaveCenter() => await ChangeCenter(Master!.Value.IsActive);
    private async Task ToggleCenter() => await ChangeCenter(!Master!.Value.IsActive);
    private async Task ChangeCenter(bool active) => await Run(async t =>
    {
        var c = Master!.Value;
        var result = await Task.Run(() => company.ActiveModels!.ChangeCenter(new(company.Snapshot!.Id, c.Id, c.Revision, CenterCode, CenterName, active, "local-user"), t));
        await Reload(t);
        SetMaster(result.Id);
    });
    private async Task ToggleUnit() => await Run(async t =>
    {
        var u = UnitMaster!.Value;
        await Task.Run(() => company.ActiveModels!.ChangeUnitState(new(company.Snapshot!.Id, u.Definition.Id, u.Revision, !u.IsActive, "local-user"), t));
        await Reload(t);
    });
    private async Task NewDraft()
    {
        if (!await CanLeaveAsync())
            return;
        await Run(async t =>
        {
            var value = await Task.Run(() => company.ActiveModels!.CreateDraft(new(company.Snapshot!.Id, Description, "local-user"), t));
            await Reload(t);
            SetCurrent(value);
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

        await Run(async t =>
        {
            var value = Structure();
            var port = company.ActiveModels!;
            var current = Current!;
            var saved = await Task.Run(() => port.SaveDraft(new(value, current.Revision, "local-user"), t));
            SetCurrent(saved);
            await Reload(CancellationToken.None);
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
        SetCurrent(result);
        await Reload(CancellationToken.None);
    });
    private async Task Clone() => await Run(async t =>
    {
        var current = Current!;
        var result = await Task.Run(() => company.ActiveModels!.Clone(new(company.Snapshot!.Id, current.Structure.Id, current.Revision, new(Guid.NewGuid()), current.Structure.Description, "local-user"), t));
        SetCurrent(result);
        await Reload(CancellationToken.None);
    });
    private void ApplyNode()
    {
        try
        {
            if (Physical is null || DriverUnit is null || Role is null || Layer is null)
                throw ModelRules.Error("MODEL_INPUT_INVALID", "Model.InputInvalid");
            var id = SelectedNode?.Node.Id ?? new(Guid.NewGuid());
            var modelId = Current!.Structure.Id;
            var n = new CalculationNodeDefinition(id, modelId, Physical.Value.Id, NodeCode, Role.Value, FunctionTags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), true);
            var p = new CostPoolDefinition(SelectedNode?.Pool?.Id ?? new(Guid.NewGuid()), modelId, n.Id, DriverUnit.Value.Definition.Id, PoolDescription, Layer.Value, ModelPolicyReference.PendingCapacity);
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
