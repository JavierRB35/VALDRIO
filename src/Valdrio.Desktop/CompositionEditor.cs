using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using Valdrio.Application;
using Valdrio.Calculation;
using Valdrio.Domain;
using Valdrio.Localization;

namespace Valdrio.Desktop;
public sealed record CompositionChoice<T>(T Value, string Label);
public sealed record CompositionAnswer(string Question, string Answer);
public sealed record CompositionResultRow(string Path, string Name, string Kind, string Base, string Adjusted, string Required, string Unit, string InputUnit, string Count, string Converted);
public sealed partial class CompositionItemRow : ObservableObject
{
    public EntityId Id { get; init; } = new(Guid.NewGuid());
    public bool Persisted { get; init; }
    // Inactive references keep a read-only consultation path when they still
    // have a saved composition; they are never offered as new inputs.
    public bool HasSavedComposition { get; init; }
    [ObservableProperty] private string code = "";
    [ObservableProperty] private string name = "";
    [ObservableProperty] private CompositionItemKind kind;
    [ObservableProperty] private UnitId unit;
    [ObservableProperty] private bool active = true;
    [ObservableProperty] private decimal? referenceUnitCost;
    [ObservableProperty] private string referenceUnitCostInput = "";
    [ObservableProperty] private string referenceCostSource = "";
    private bool syncingCostInput;
    public CultureInfo CostInputCulture { get; init; } = CultureInfo.InvariantCulture;
    public bool IsCostInputValid => TryCostInput(ReferenceUnitCostInput, out _);
    public static bool TryCostInput(string? input, out decimal? cost)
    {
        cost = null;
        if (string.IsNullOrWhiteSpace(input)) return true;
        var normalized = input.Trim().Replace(',', '.');
        if (!decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out var parsed) || parsed < 0)
            return false;
        cost = parsed;
        return true;
    }
    partial void OnReferenceUnitCostInputChanged(string value)
    {
        OnPropertyChanged(nameof(IsCostInputValid));
        OnPropertyChanged(nameof(CanOpenComposition));
        if (syncingCostInput || !TryCostInput(value, out var parsed)) return;
        syncingCostInput = true;
        try
        {
            ReferenceUnitCost = parsed;
            ReferenceCostSource = parsed.HasValue ? "Manual · " + DateTimeOffset.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "";
        }
        finally { syncingCostInput = false; }
    }
    public void NormalizeCostInput()
    {
        if (!TryCostInput(ReferenceUnitCostInput, out var parsed)) return;
        syncingCostInput = true;
        try { ReferenceUnitCostInput = parsed?.ToString("0.############################", CostInputCulture) ?? ""; }
        finally { syncingCostInput = false; }
    }
    public string ManualOriginLabel { get; init; } = "Manual";
    public string CompositionOriginLabel { get; init; } = "Composition";
    public string CostOriginDisplay => !ReferenceUnitCost.HasValue ? "" :
        ReferenceCostSource.StartsWith("Composition · ", StringComparison.Ordinal)
            ? CompositionOriginLabel : ManualOriginLabel;
    // An existing identity is required before editing its composition, so the
    // catalogue can always be recovered without creating orphan recipes.
    public bool CanOpenComposition => Persisted && Kind != CompositionItemKind.Material &&
        (Active || HasSavedComposition) &&
        !string.IsNullOrWhiteSpace(Code) && !string.IsNullOrWhiteSpace(Name) && Unit.Value.IsValid && IsCostInputValid;
    partial void OnReferenceUnitCostChanged(decimal? value)
    {
        if (!syncingCostInput)
        {
            syncingCostInput = true;
            try { ReferenceUnitCostInput = value?.ToString("0.############################", CostInputCulture) ?? ""; }
            finally { syncingCostInput = false; }
        }
        ReferenceCostSource = value.HasValue ? "Manual · " + DateTimeOffset.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "";
        OnPropertyChanged(nameof(CostOriginDisplay));
    }
    partial void OnReferenceCostSourceChanged(string value) => OnPropertyChanged(nameof(CostOriginDisplay));
    partial void OnCodeChanged(string value) => OnPropertyChanged(nameof(CanOpenComposition));
    partial void OnNameChanged(string value) => OnPropertyChanged(nameof(CanOpenComposition));
    partial void OnUnitChanged(UnitId value) => OnPropertyChanged(nameof(CanOpenComposition));
    partial void OnKindChanged(CompositionItemKind value) => OnPropertyChanged(nameof(CanOpenComposition));
    partial void OnActiveChanged(bool value) => OnPropertyChanged(nameof(CanOpenComposition));
    public string Label => Code + " · " + Name;
    public CompositionItem Value => new(Id, Code.Trim(), Name.Trim(), Unit, Kind, Active)
    {
        ReferenceUnitCost = ReferenceUnitCost,
        ReferenceCostSource = ReferenceUnitCost.HasValue ? ReferenceCostSource : null
    };
}
public sealed partial class CompositionLineRow : ObservableObject
{
    public EntityId Id { get; init; } = new(Guid.NewGuid());
    [ObservableProperty] private CompositionTarget? target;
    [ObservableProperty] private string quantity = "";
    [ObservableProperty] private UnitId unit;
    [ObservableProperty] private string factor = "1";
    [ObservableProperty] private ConsumptionBasis basis;
    [ObservableProperty] private CompositionLossKind loss;
    [ObservableProperty] private string percent = "0";
    public CultureInfo DisplayCulture { get; init; } = CultureInfo.InvariantCulture;
    public string QuantityWord { get; init; } = "Quantity";
    public string LossFactorDisplay
    {
        get
        {
            if (!CompositionItemRow.TryCostInput(Percent, out var fraction) || fraction is null)
                return "—";
            var percent = fraction.Value;
            try
            {
                decimal factor = Loss switch
                {
                    CompositionLossKind.None when percent == 0 => 1m,
                    CompositionLossKind.ExtraInput when percent >= 0 => 1m + percent / 100m,
                    CompositionLossKind.Yield when percent is > 0 and <= 100 => 100m / percent,
                    _ => -1m
                };
                return factor < 0 ? "—" : "× " + factor.ToString("0.############", DisplayCulture);
            }
            catch (ArithmeticException) { return "—"; }
        }
    }
    public string LossFormulaText => Loss switch
    {
        CompositionLossKind.None => QuantityWord + " × 1",
        CompositionLossKind.ExtraInput => QuantityWord + " × (1 + " + Percent + "/100)",
        CompositionLossKind.Yield => QuantityWord + " / (" + Percent + "/100)",
        _ => "—"
    };
    public string AdjustedQuantityDisplay
    {
        get
        {
            if (!CompositionItemRow.TryCostInput(Quantity, out var quantity) || quantity is null ||
                !CompositionItemRow.TryCostInput(Percent, out var percent) || percent is null || LossFactorDisplay == "—") return "—";
            try
            {
                var adjusted = Loss switch
                {
                    CompositionLossKind.ExtraInput => checked(quantity.Value * (1m + percent.Value / 100m)),
                    CompositionLossKind.Yield => quantity.Value / (percent.Value / 100m),
                    _ => quantity.Value
                };
                return quantity.Value.ToString("0.############", DisplayCulture) + " → " + adjusted.ToString("0.############", DisplayCulture);
            }
            catch (ArithmeticException) { return "—"; }
        }
    }
    partial void OnQuantityChanged(string value) => UpdateLossExplanation();
    partial void OnLossChanged(CompositionLossKind value) => UpdateLossExplanation();
    partial void OnPercentChanged(string value) => UpdateLossExplanation();
    private void UpdateLossExplanation()
    {
        OnPropertyChanged(nameof(LossFactorDisplay));
        OnPropertyChanged(nameof(LossFormulaText));
        OnPropertyChanged(nameof(AdjustedQuantityDisplay));
    }
}
public sealed record CompositionTarget(EntityId Id, int Kind, UnitId Unit, string Label);
public sealed partial class CompositionEditor : ObservableObject
{
    private readonly StudyWorkspaceViewModel host;
    private readonly UiTextCatalog texts = new();
    private CompositionCatalog catalog = new(default, 0, [], []);
    private CompositionVersion? current;
    private bool loading;
    private EntityId? transientDraftId;
    private bool compositionModified;
    private CompositionPool[] pools = [];
    public ObservableCollection<CompositionItemRow> Items { get; } = [];
    public ObservableCollection<CompositionLineRow> Lines { get; } = [];
    public ObservableCollection<CompositionResultRow> Results { get; } = [];
    public ObservableCollection<CompositionAnswer> Answers { get; } = [];
    public IReadOnlyList<CompositionChoice<UnitId>> Units => host.PhysicalCompany!.Units.Select(u =>
        new CompositionChoice<UnitId>(u.Id,
            (DefaultActivityUnitCatalog.IsPreset(u) ? texts.Get("ActivityUnit." + u.Code, host.PhysicalUiCulture) : u.Code)
            + " · " + u.Symbol)).ToArray();
    public bool HasUnitPreset => host.PhysicalCompany!.Units.Any(u => u.Code == "unit" && u.Dimension == "count" && u.IsDiscrete);
    public bool CanInstallUnitPreset => Writable && !HasUnitPreset;
    public async Task<bool> InstallDefaultUnitsAsync()
    {
        if (!await host.InstallProductUnitsAsync())
        { Feedback = this["InstallUnitsFailed"]; return false; }
        await ReloadForDisplayAsync();
        return HasUnitPreset;
    }
    public IReadOnlyList<CompositionChoice<CompositionItemKind>> Kinds => Enum.GetValues<CompositionItemKind>().Select(v => new CompositionChoice<CompositionItemKind>(v, this[v.ToString()])).ToArray();
    public IReadOnlyList<CompositionChoice<CompositionItemKind>> ImportKinds => new[] { CompositionItemKind.Product, CompositionItemKind.Material, CompositionItemKind.Service }.Select(v => new CompositionChoice<CompositionItemKind>(v, this[v.ToString()])).ToArray();
    public IReadOnlyList<CompositionChoice<ConsumptionBasis>> Bases => Enum.GetValues<ConsumptionBasis>().Select(v => new CompositionChoice<ConsumptionBasis>(v, this[v.ToString()])).ToArray();
    public IReadOnlyList<CompositionChoice<CompositionLossKind>> Losses => Enum.GetValues<CompositionLossKind>().Select(v => new CompositionChoice<CompositionLossKind>(v, this[v.ToString()])).ToArray();
    public IReadOnlyList<CompositionChoice<EntityId>> Products => catalog.Items
        .Where(i => i.Kind != CompositionItemKind.Material &&
            (i.Active || catalog.Versions.Any(v => v.ProductId == i.Id && v.ModelVersionId == host.Current!.SelectedModelVersionId)))
        .Select(i => new CompositionChoice<EntityId>(i.Id, i.Code + " · " + i.Name +
            (i.Active ? "" : " · " + this["InactiveReference"]))).ToArray();
    public IReadOnlyList<CompositionChoice<EntityId>> Versions => catalog.Versions.Where(v => v.ProductId == Product && v.ModelVersionId == host.Current!.SelectedModelVersionId).Select(v => new CompositionChoice<EntityId>(v.Id, "v" + v.Number + " · " + this[v.State.ToString()])).ToArray();
    public IReadOnlyList<CompositionTarget> Targets { get; private set; } = [];
    [ObservableProperty] private EntityId product;
    [ObservableProperty] private EntityId version;
    [ObservableProperty] private string batchSize = "1";
    [ObservableProperty] private string note = "";
    [ObservableProperty] private string outputQuantity = "1";
    [ObservableProperty] private CompositionItemKind importKind = CompositionItemKind.Product;
    [ObservableProperty] private string feedback = "";
    public bool Dirty { get; private set; }
    public bool Busy { get; private set; }
    public bool Writable => host.PhysicalWritable;
    private bool ProductIsActive => catalog.Items.Any(i => i.Id == Product && i.Active);
    // An inactive reference can only be consulted; editing and publishing its
    // saved composition stay disabled.
    public bool Editable => Writable && ProductIsActive && current?.State == CompositionState.Draft && current.ProductId == Product && current.Id == Version;
    public bool HasPendingCompositionOpen => Product.IsValid && (current is null || current.Id != Version || current.ProductId != Product);
    public bool LinesReadOnly => !Editable;
    public string NumericCulture => host.PhysicalFormatCulture.Name;
    public string Context => host.PhysicalCompany!.Name + " · " + host.Current!.Name + " · " + host.ContextSummary;
    public string ActionLabel(string key) => texts.Get("UX." + key, host.PhysicalUiCulture);
    public string State
    {
        get
        {
            if (current is null) return this["ChooseRecipe"];
            var output = catalog.Items.Single(i => i.Id == current.ProductId);
            return output.Name + (output.Active ? "" : " · " + this["HistoricalState"]) +
                (Dirty ? " · " + this["Unsaved"] : "");
        }
    }
    public bool CanSaveDefinition => Editable && Dirty && !Busy;
    public bool CanSaveChanges => Writable && Dirty && !Busy;
    public bool HasCompositionChanges => Editable && compositionModified;
    public bool SaveChanges() => Save(HasCompositionChanges);
    public Task<bool> SaveChangesAsync() => SaveAsync(HasCompositionChanges);
    public string this[string key] => texts.Get("Composition." + key, host.PhysicalUiCulture);
    public CompositionEditor(StudyWorkspaceViewModel host, bool initialize = true) { this.host = host; if (initialize) Reload(); }
    partial void OnProductChanged(EntityId value) { OnPropertyChanged(nameof(Versions)); Version = Versions.LastOrDefault()?.Value ?? default; SelectionChanged(); }
    partial void OnVersionChanged(EntityId value) => SelectionChanged();
    private void SelectionChanged() { Invalidate(); OnPropertyChanged(nameof(Editable)); OnPropertyChanged(nameof(LinesReadOnly)); OnPropertyChanged(nameof(HasPendingCompositionOpen)); }
    partial void OnBatchSizeChanged(string value) => CompositionChanged();
    partial void OnNoteChanged(string value) => CompositionChanged();
    partial void OnOutputQuantityChanged(string value) => Invalidate();
    // Decimal round-trip: reopening a draft must not truncate conversion factors.
    private string Q(decimal value) => value.ToString("0.############################", host.PhysicalFormatCulture);
    private decimal Parse(string value)
    {
        if (!CompositionItemRow.TryCostInput(value, out var result) || result is null)
            throw CompositionCalculator.Error("Number");
        return result.Value;
    }
    private void Changed() { if (loading) return; Dirty = true; OnPropertyChanged(nameof(Dirty)); OnPropertyChanged(nameof(State)); OnPropertyChanged(nameof(CanSaveDefinition)); OnPropertyChanged(nameof(CanSaveChanges)); OnPropertyChanged(nameof(Editable)); OnPropertyChanged(nameof(LinesReadOnly)); OnPropertyChanged(nameof(HasPendingCompositionOpen)); Invalidate(); }
    private void CompositionChanged() { if (loading) return; compositionModified = true; Changed(); }
    private CompositionItemRow Row(CompositionItem i, bool persisted = true)
    {
        var row = new CompositionItemRow { Id = i.Id, Persisted = persisted, Code = i.Code, Name = i.Name,
            Kind = i.Kind, Unit = i.UnitId, Active = i.Active, CostInputCulture = host.PhysicalFormatCulture,
            ReferenceUnitCost = i.ReferenceUnitCost, ReferenceCostSource = i.ReferenceCostSource ?? "",
            HasSavedComposition = catalog.Versions.Any(v => v.ProductId == i.Id && v.ModelVersionId == host.Current!.SelectedModelVersionId),
            ManualOriginLabel = this["OriginManual"],
            CompositionOriginLabel = this["OriginComposition"] };
        row.PropertyChanged += (_, _) => Changed();
        return row;
    }
    private void Invalidate()
    {
        Results.Clear(); Answers.Clear();
        Answers.Add(new(this["QNeeds"], this["PreviewPending"]));
        Answers.Add(new(this["QCost"], this["T18"]));
        Answers.Add(new(this["QActual"], this["T19"]));
    }
    public void Reload() => ReloadCore(false).GetAwaiter().GetResult();
    public Task ReloadAsync() => ReloadCore(true);
    public async Task ReloadForDisplayAsync()
    {
        try { await ReloadAsync(); }
        catch (DomainRuleException e) { Feedback = texts.Get(e.Diagnostic.MessageKey, host.PhysicalUiCulture); }
        catch (CompanyFileException e) { Feedback = texts.Get(e.Diagnostic.MessageKey, host.PhysicalUiCulture); }
    }
    private async Task ReloadCore(bool background)
    {
        using var activity = host.BeginActivity();
        loading = true;
        try
        {
            var read = () =>
            {
                var loadedCatalog = host.ActiveCompositions!.ReadCompositions();
                // Validation also needs pinned models belonging to other saved versions.
                var loadedPools = host.PhysicalModels!.ReadModels().Where(m => m.Status == ModelStatus.Published).SelectMany(m =>
                    m.Structure.Pools.Select(p =>
                    {
                        var node = m.Structure.Nodes.Single(n => n.Id == p.NodeId);
                        // Auxiliary nodes are not direct product inputs; their costs
                        // reach principal nodes through the secondary allocation.
                        return new CompositionPool(p.Id, m.Structure.Id, node.Code + " · " + p.Description,
                            p.DriverUnitId, node.IsActive, node.Role);
                    })).ToArray();
                return (loadedCatalog, loadedPools);
            };
            var loaded = background ? await Task.Run(read) : read();
            catalog = loaded.loadedCatalog;
            pools = loaded.loadedPools;
            Items.Clear();
            foreach (var i in catalog.Items)
            {
                Items.Add(Row(i));
            }
            Targets = catalog.Items.Where(i => i.Kind == CompositionItemKind.Material && i.Active)
                .Select(i => new CompositionTarget(i.Id, 0, i.UnitId, this["Material"] + " · " + i.Code + " · " + i.Name))
                .Concat(catalog.Versions.Where(v => v.State == CompositionState.Published && v.ModelVersionId == host.Current!.SelectedModelVersionId &&
                    catalog.Items.Any(i => i.Id == v.ProductId && i.Active && i.Kind == CompositionItemKind.SemiFinished))
                    .Select(v => new CompositionTarget(v.Id, 1, catalog.Items.Single(i => i.Id == v.ProductId).UnitId,
                        this["SemiFinished"] + " · " + catalog.Items.Single(i => i.Id == v.ProductId).Name + " · " + v.Number)))
                .Concat(pools.Where(p => p.ModelId == host.Current!.SelectedModelVersionId &&
                    p.Active && p.Role == NodeRole.Principal)
                    .Select(p => new CompositionTarget(p.Id, 2, p.UnitId, this["Activity"] + " · " + p.Name))).ToArray();
            current = null; transientDraftId = null; compositionModified = false; Lines.Clear(); Dirty = false; Feedback = "";
            OnPropertyChanged(string.Empty); Invalidate();
        }
        finally { loading = false; }
    }
    public void Discard() => DiscardCore(false).GetAwaiter().GetResult();
    public Task DiscardAsync() => DiscardCore(true);
    // Discard restores the saved state of the document that was open. A draft
    // that never reached SQLite cannot be recovered, so the page is left empty
    // with an explicit message instead of quietly recreating it.
    private async Task DiscardCore(bool background)
    {
        var product = Product;
        var hadComposition = current is not null;
        var versionId = current?.Id;
        await ReloadCore(background);
        var persisted = versionId.HasValue
            ? catalog.Versions.FirstOrDefault(v => v.Id == versionId.Value && v.ProductId == product &&
                v.ModelVersionId == host.Current!.SelectedModelVersionId)
            : null;
        if (persisted is not null)
        {
            Version = persisted.Id; Open(); Feedback = this["Discarded"];
            return;
        }
        // There is nothing saved to reopen: keep the product context and leave
        // the composition explicitly empty so the analyst can press Open.
        var latest = catalog.Versions.Where(v => v.ProductId == product &&
            v.ModelVersionId == host.Current!.SelectedModelVersionId).OrderBy(v => v.Number).LastOrDefault();
        Version = latest?.Id ?? default;
        current = null; transientDraftId = null; compositionModified = false; Lines.Clear(); Dirty = false;
        OnPropertyChanged(string.Empty); Invalidate();
        Feedback = hadComposition ? this["DiscardedEmpty"] : this["Discarded"];
    }
    public void AddItem()
    {
        if (!Writable) return;
        var row = new CompositionItemRow { Unit = Units.FirstOrDefault()?.Value ?? default,
            CostInputCulture = host.PhysicalFormatCulture, ManualOriginLabel = this["OriginManual"], CompositionOriginLabel = this["OriginComposition"] };
        row.PropertyChanged += (_, _) => Changed(); Items.Add(row); Changed();
    }
    public void RemoveItem(CompositionItemRow? row)
    {
        if (!Writable || row is null) return;
        if (row.Persisted)
        {
            // Historical recipes and estimates retain this stable identity.
            // Removal from the active catalogue is a reversible deactivation.
            row.Active = false; Feedback = this["Deactivated"]; Changed(); return;
        }
        Items.Remove(row); Changed();
    }
    public void ImportProducts(string path) => ImportProductsCore(path, false).GetAwaiter().GetResult();
    public Task ImportProductsAsync(string path) => ImportProductsCore(path, true);
    private async Task ImportProductsCore(string path, bool background)
    {
        using var activity = host.BeginActivity();
        if (!Writable) { Feedback = this["ReadOnlyImport"]; return; }
        try
        {
            var read = background ? await Task.Run(() => ProductCostImport.ReadResult(path, host.PhysicalFormatCulture)) : ProductCostImport.ReadResult(path, host.PhysicalFormatCulture);
            if (read.Value is not { } imported) { ShowImportError(read.Error!); return; }
            var source = Path.GetFileName(path) + " · " + DateTimeOffset.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var units = host.PhysicalCompany!.Units;
            var merge = ProductCostImport.MergeResult(Items.Select(i => i.Value).ToArray(), imported, units, ImportKind, source);
            if (merge.Value is not { } merged) { ShowImportError(merge.Error!); return; }
            var rowsById = Items.ToDictionary(item => item.Id);
            foreach (var item in merged)
            {
                if (rowsById.TryGetValue(item.Id, out var existing))
                {
                    existing.Name = item.Name;
                    existing.ReferenceUnitCost = item.ReferenceUnitCost;
                    existing.ReferenceCostSource = item.ReferenceCostSource ?? "";
                }
                else
                {
                    var row = Row(item, persisted: false);
                    Items.Add(row); rowsById.Add(row.Id, row);
                }
            }
            Changed(); Feedback = string.Format(host.PhysicalUiCulture, this["ImportComplete"], imported.Count, source);
        }
        catch (InvalidDataException ex)
        {
            ShowImportError(ex.Message);
        }
        catch (IOException) { Feedback = this["ImportFailed"]; }
        catch (UnauthorizedAccessException) { Feedback = this["ImportFailed"]; }
    }
    private void ShowImportError(string error)
    {
            var details = error.Split(':');
            Feedback = details[0] switch
            {
                "Unit" => string.Format(host.PhysicalUiCulture, this["ImportUnknownUnit"], details.Last()),
                "UnitMismatch" => string.Format(host.PhysicalUiCulture, this["ImportUnitMismatch"], details.Last()),
                "Cost" or "Data" or "Columns" => string.Format(host.PhysicalUiCulture, this["ImportInvalidRow"], details.Last()),
                "Empty" => this["ImportEmpty"], "Headers" => this["ImportHeaders"], "Quotes" => this["ImportQuotes"],
                "ReferenceAmbiguous" => this["ImportReferenceAmbiguous"],
                "Xlsx" => this["ImportXlsxInvalid"], _ => this["ImportFailed"]
            };
    }
    public bool OpenProduct(EntityId id)
    {
        if (Dirty) return false;
        if (transientDraftId.HasValue)
            catalog = catalog with { Versions = catalog.Versions.Where(v => v.Id != transientDraftId.Value).ToArray() };
        var item = catalog.Items.FirstOrDefault(i => i.Id == id && i.Kind != CompositionItemKind.Material);
        var hasSaved = catalog.Versions.Any(v => v.ProductId == id && v.ModelVersionId == host.Current!.SelectedModelVersionId);
        // Historical consultation requires a saved composition; materials are never outputs.
        if (item is null || (!item.Active && !hasSaved)) return false;
        current = null; transientDraftId = null; Lines.Clear();
        Product = id;
        var versions = catalog.Versions.Where(v => v.ProductId == id && v.ModelVersionId == host.Current!.SelectedModelVersionId);
        var selected = versions.FirstOrDefault(v => v.State == CompositionState.Draft) ?? versions.OrderBy(v => v.Number).LastOrDefault();
        if (!item.Active)
        {
            // Inactive references are consulted read-only: no clone, draft or publication.
            Version = selected!.Id; Open(); Feedback = this["HistoricalReadOnly"];
            return current is not null;
        }
        if (!Writable)
        {
            Version = selected?.Id ?? default; Open();
            return current is not null;
        }
        if (catalog.Versions.Any(v => v.ProductId == id && v.State == CompositionState.Draft && v.ModelVersionId != host.Current!.SelectedModelVersionId))
        { Feedback = this["DraftOtherModel"]; OnPropertyChanged(string.Empty); return false; }
        if (selected is null || selected.State == CompositionState.Published)
        {
            // One editable definition in the UX. Internally the previous
            // publication remains immutable for accepted orders and audit.
            NewVersion();
            if (current is not null)
            {
                transientDraftId = current.Id;
                compositionModified = false;
                Dirty = false;
                OnPropertyChanged(nameof(Dirty));
                OnPropertyChanged(nameof(State));
                OnPropertyChanged(nameof(CanSaveDefinition));
            }
        }
        else
        {
            // A previously saved draft is unfinished work: expose it as dirty
            // so Save composition and catalogue can make it effective without
            // forcing the analyst to invent a dummy edit.
            Version = selected.Id; Open(); Changed();
        }
        return current is not null;
    }
    public void Open()
    {
        loading = true;
        try
        {
            current = catalog.Versions.SingleOrDefault(v => v.Id == Version);
            Lines.Clear();
            BatchSize = Q(current?.BatchSize ?? 1); Note = current?.Note ?? "";
            foreach (var l in current?.Lines ?? [])
                Attach(new() { Id = l.Id, Target = ResolveTarget(l), Quantity = Q(l.Quantity),
                    Unit = l.InputUnitId, Factor = Q(l.ConversionFactor), Basis = l.Basis,
                    Loss = l.LossKind, Percent = Q(l.LossPercent),
                    DisplayCulture = host.PhysicalFormatCulture, QuantityWord = this["Quantity"] });
            compositionModified = current?.State == CompositionState.Draft && transientDraftId != current.Id;
            Dirty = false; OnPropertyChanged(string.Empty); Invalidate();
        }
        finally { loading = false; }
    }
    private CompositionTarget? ResolveTarget(CompositionLine line)
    {
        var id = line.MaterialId ?? line.RecipeVersionId ?? line.PoolId;
        var allowed = Targets.FirstOrDefault(t => t.Id == id);
        if (allowed is not null) return allowed;
        // Historical definitions can still be opened/read; an auxiliary or
        // deprecated component never becomes a selectable new input.
        var pool = pools.FirstOrDefault(p => p.Id == id);
        if (pool is not null) return new(pool.Id, 2, pool.UnitId, this["UnavailableComponent"] + " · " + pool.Name);
        var version = catalog.Versions.FirstOrDefault(v => v.Id == id);
        var item = version is not null ? catalog.Items.FirstOrDefault(i => i.Id == version.ProductId)
            : catalog.Items.FirstOrDefault(i => i.Id == id);
        return item is null ? null : new(id!.Value, version is null ? 0 : 1, item.UnitId,
            this["UnavailableComponent"] + " · " + item.Code + " · " + item.Name);
    }
    public void NewVersion()
    {
        if (!Writable || !catalog.Items.Any(i => i.Id == Product && i.Active && i.Kind != CompositionItemKind.Material)) return;
        if (catalog.Versions.Any(v => v.ProductId == Product && v.State == CompositionState.Draft)) { Feedback = this["ExistingDraft"]; return; }
        var previous = catalog.Versions.Where(v => v.ProductId == Product).OrderBy(v => v.Number).LastOrDefault();
        current = new(new(Guid.NewGuid()), Product, (previous?.Number ?? 0) + 1, host.Current!.SelectedModelVersionId, previous?.BatchSize ?? 1, CompositionState.Draft, previous?.Note ?? "", previous?.ModelVersionId == host.Current.SelectedModelVersionId ? previous.Lines.Select(l => l with { Id = new(Guid.NewGuid()) }).ToArray() : []);
        catalog = catalog with { Versions = catalog.Versions.Append(current).ToArray() };
        Version = current.Id; Open(); Changed(); OnPropertyChanged(nameof(Versions));
    }
    private void Attach(CompositionLineRow row)
    {
        row.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(row.Target) && row.Target is { } t) { row.Unit = t.Unit; row.Factor = "1"; } CompositionChanged(); };
        Lines.Add(row);
    }
    public void AddLine() { if (Editable) { Attach(new() { Unit = Units.FirstOrDefault()?.Value ?? default,
        DisplayCulture = host.PhysicalFormatCulture, QuantityWord = this["Quantity"] }); CompositionChanged(); } }
    public void RemoveLine(CompositionLineRow? row) { if (Editable && row is not null) { Lines.Remove(row); CompositionChanged(); } }
    private bool ValidateInput(bool publish, bool preview = false)
    {
        bool Number(string text, out decimal value)
        {
            var valid = CompositionItemRow.TryCostInput(text, out var parsed);
            value = parsed ?? 0;
            return valid && parsed.HasValue;
        }
        if (Items.Select(i => i.Code.Trim()).Distinct(StringComparer.Ordinal).Count() != Items.Count)
        { Feedback = this["InvalidCatalog"]; return false; }
        if (Items.Any(i => string.IsNullOrWhiteSpace(i.Code) || string.IsNullOrWhiteSpace(i.Name) ||
            !Units.Any(u => u.Value == i.Unit) || !i.IsCostInputValid ||
            i.ReferenceUnitCost < 0 || i.ReferenceUnitCost.HasValue && string.IsNullOrWhiteSpace(i.ReferenceCostSource)))
        { Feedback = this["InvalidItem"]; return false; }
        if (current?.State == CompositionState.Draft && (publish || preview || compositionModified))
        {
            if (!Number(BatchSize, out var batch) || batch <= 0) { Feedback = this["InvalidVersion"]; return false; }
            if (publish && Lines.Count == 0) { Feedback = this["Empty"]; return false; }
            foreach (var line in Lines)
            {
                if (!Number(line.Quantity, out var quantity) || !Number(line.Factor, out var factor) || !Number(line.Percent, out var percent))
                { Feedback = this["Number"]; return false; }
                if (line.Target is null || publish && !Targets.Any(t => t.Id == line.Target.Id) ||
                    quantity < 0 || factor <= 0 || percent < 0 || line.Loss == CompositionLossKind.None && percent != 0 ||
                    line.Loss == CompositionLossKind.Yield && (percent == 0 || percent > 100) ||
                    !Units.Any(u => u.Value == line.Unit))
                { Feedback = this["InvalidLine"]; return false; }
            }
        }
        if (preview && (!Number(OutputQuantity, out var output) || output <= 0)) { Feedback = this["Number"]; return false; }
        return true;
    }
    private CompositionCatalog Proposal(bool publish, bool preview = false)
    {
        var versions = catalog.Versions;
        if (!publish && !preview && !compositionModified && transientDraftId.HasValue)
            versions = versions.Where(v => v.Id != transientDraftId.Value).ToArray();
        if (current is { State: CompositionState.Draft } c &&
            (publish || preview || compositionModified || transientDraftId != c.Id))
        {
            var lines = Lines.Select(l => new CompositionLine(l.Id, l.Target?.Kind == 0 ? l.Target.Id : null, l.Target?.Kind == 1 ? l.Target.Id : null, l.Target?.Kind == 2 ? l.Target.Id : null, Parse(l.Quantity), l.Unit, Parse(l.Factor), l.Basis, l.Loss, Parse(l.Percent))).ToArray();
            var edited = c with { BatchSize = Parse(BatchSize), Note = Note, Lines = lines, State = publish ? CompositionState.Published : CompositionState.Draft };
            versions = versions.Select(v => v.Id == c.Id ? edited : v).ToArray();
        }
        return catalog with { Items = Items.Select(i => i.Value).ToArray(), Versions = versions };
    }
    private CompositionCatalog ApplyCompleteCompositionCost(CompositionCatalog proposal, bool publish)
    {
        if (!publish || current is not { State: CompositionState.Draft }) return proposal;
        var recipe = proposal.Versions.Single(v => v.Id == current.Id);
        var quantity = recipe.BatchSize;
        var preview = CompositionCalculator.Preview(proposal, recipe.Id, quantity, host.PhysicalCompany!.Units, pools);
        // Per-order inputs cannot be made into a stable unit price without an
        // explicitly defined order quantity, including when they come from a
        // semi-finished expansion. Activity also needs T15 tariffs.
        if (preview.HasOrderDependentBasis) return WithoutStaleCalculatedCost();
        var cost = CompositionCostEstimator.Estimate(proposal, preview, recipe.ProductId,
            host.PhysicalCompany.DefaultCurrency);
        if (cost.HasActivity || cost.UnpricedComponents.Count > 0 || cost.PricedComponents is not { } subtotal)
            return WithoutStaleCalculatedCost();
        var unitCost = checked(subtotal / quantity);
        return proposal with
        {
            Items = proposal.Items.Select(item => item.Id == recipe.ProductId
                ? item with { ReferenceUnitCost = unitCost,
                    ReferenceCostSource = "Composition · " + recipe.Id }
                : item).ToArray()
        };
        CompositionCatalog WithoutStaleCalculatedCost() => proposal with
        {
            Items = proposal.Items.Select(item => item.Id == recipe.ProductId &&
                item.ReferenceCostSource?.StartsWith("Composition · ", StringComparison.Ordinal) == true
                ? item with { ReferenceUnitCost = null, ReferenceCostSource = null } : item).ToArray()
        };
    }
    private string SavedFeedback(bool publish, EntityId? id) => publish && id.HasValue &&
        catalog.Versions.FirstOrDefault(v => v.Id == id) is { } saved &&
        catalog.Items.Single(i => i.Id == saved.ProductId).ReferenceCostSource != "Composition · " + id
            ? this["PartialCostSaved"] : this["Saved"];

    public bool Save(bool publish)
    {
        if (!Writable || publish && !Editable || !ValidateInput(publish)) return false;
        try
        {
            var id = current?.Id;
            host.ActiveCompositions!.SaveCompositions(ApplyCompleteCompositionCost(Proposal(publish), publish),
                catalog.Revision, "local-user");
            Reload(); if (id.HasValue) { Version = id.Value; Open(); }
            Feedback = SavedFeedback(publish, id); return true;
        }
        catch (DomainRuleException e) { Feedback = texts.Get(e.Diagnostic.MessageKey, host.PhysicalUiCulture); return false; }
        catch (CompanyFileException e) { Feedback = texts.Get(e.Diagnostic.MessageKey, host.PhysicalUiCulture); return false; }
        catch (OverflowException) { Feedback = this["Overflow"]; return false; }
    }
    public async Task<bool> SaveAsync(bool publish)
    {
        using var activity = host.BeginActivity();
        if (!Writable || Busy || publish && !Editable || !ValidateInput(publish)) return false;
        var committed = false;
        try
        {
            var proposal = Proposal(publish); var id = current?.Id;
            var store = host.ActiveCompositions!;
            Busy = true; OnPropertyChanged(string.Empty); Feedback = this["Saving"];
            await Task.Run(() => store.SaveCompositions(ApplyCompleteCompositionCost(proposal, publish), catalog.Revision, "local-user"));
            committed = true;
            Dirty = false;
            // Validate the committed file off the UI thread before repainting.
            await ReloadAsync(); if (id.HasValue) { Version = id.Value; Open(); }
            Feedback = SavedFeedback(publish, id); return true;
        }
        catch (DomainRuleException e) { Feedback = (committed ? this["Saved"] + " " : "") + texts.Get(e.Diagnostic.MessageKey, host.PhysicalUiCulture); return false; }
        catch (CompanyFileException e) { Feedback = (committed ? this["Saved"] + " " : "") + texts.Get(e.Diagnostic.MessageKey, host.PhysicalUiCulture); return false; }
        catch (OverflowException) { Feedback = this["Overflow"]; return false; }
        finally { Busy = false; OnPropertyChanged(string.Empty); }
    }
    public void Preview() => PreviewCore(false).GetAwaiter().GetResult();
    public Task PreviewAsync() => PreviewCore(true);
    private async Task PreviewCore(bool background)
    {
        using var activity = host.BeginActivity();
        Invalidate();
        if (current is null || current.Id != Version || current.ProductId != Product) { Feedback = this["ChooseRecipe"]; return; }
        try
        {
            if (!ValidateInput(false, true)) return;
            var p = Proposal(false, preview: true);
            var id = current.Id; var quantity = Parse(OutputQuantity); var units = host.PhysicalCompany!.Units;
            var calculate = () => CompositionCalculator.Preview(p, id, quantity, units, pools);
            var result = background ? await Task.Run(calculate) : calculate();
            foreach (var r in result.Lines)
                Results.Add(new(r.Path, r.Name, this[r.Expanded ? "SemiFinished" : r.Activity ? "Activity" : "Material"], Q(r.BaseQuantity), Q(r.AdjustedQuantity), Q(r.RequiredQuantity), host.PhysicalCompany.Units.Single(u => u.Id == r.UnitId).Symbol,
                    host.PhysicalCompany.Units.Single(u => u.Id == r.InputUnitId).Symbol, Q(r.BasisCount), Q(r.ConvertedQuantity)));
            Answers.Clear();
            Answers.Add(new(this["QNeeds"], string.Join("; ", result.Totals.Select(g =>
                g.Name + ": " + Q(g.Quantity) + " " + host.PhysicalCompany.Units.Single(u => u.Id == g.UnitId).Symbol))));
            var selected = p.Versions.Single(v => v.Id == current.Id);
            Answers.Add(new(this["QBatches"], selected.Lines.Any(l => l.Basis == ConsumptionBasis.PerStartedBatch)
                ? Q(result.StartedBatches) + " · " + this["StartedHelp"] : this["NoBatches"]));
            Answers.Add(new(this["QVersion"], State));
            Answers.Add(new(this["QLoss"], string.Join("; ", selected.Lines.Select(l =>
                (ResolveTarget(l)?.Label ?? this["UnavailableComponent"]) + ": " +
                this[l.LossKind.ToString()] + (l.LossKind == CompositionLossKind.None ? "" : " " + UiCompactNumbers.Percent(l.LossPercent, host.PhysicalFormatCulture))))));
            var estimate = CompositionCostEstimator.Estimate(p, result, selected.ProductId, host.PhysicalCompany!.DefaultCurrency);
            var costParts = new List<string>
            {
                string.Format(host.PhysicalUiCulture, this["EstimatedComponents"], UiCompactNumbers.Number(estimate.PricedComponents!.Value, host.PhysicalFormatCulture), estimate.Currency)
            };
            if (estimate.UnpricedComponents.Count > 0) costParts.Add(string.Format(host.PhysicalUiCulture, this["MissingPrices"], string.Join(", ", estimate.UnpricedComponents)));
            if (estimate.HasActivity) costParts.Add(this["ActivityNotIncluded"]);
            if (estimate.ImportedOutputReference is { } reference)
            {
                var outputItem = p.Items.Single(i => i.Id == selected.ProductId);
                var outputUnit = host.PhysicalCompany.Units.Single(u => u.Id == outputItem.UnitId).Symbol;
                costParts.Add(string.Format(host.PhysicalUiCulture, this["ImportedOutputReference"], UiCompactNumbers.Number(outputItem.ReferenceUnitCost!.Value, host.PhysicalFormatCulture), estimate.Currency,
                    outputUnit, UiCompactNumbers.Number(reference, host.PhysicalFormatCulture), result.OutputQuantity.ToString("0.############", host.PhysicalFormatCulture)));
            }
            else costParts.Add(this["NoOutputReference"]);
            Answers.Add(new(this["QCost"], string.Join(" · ", costParts))); Answers.Add(new(this["QActual"], this["T19"]));
            Feedback = this["PreviewReady"];
        }
        catch (DomainRuleException e) { Feedback = texts.Get(e.Diagnostic.MessageKey, host.PhysicalUiCulture); }
        catch (OverflowException) { Feedback = this["Overflow"]; }
    }
}
