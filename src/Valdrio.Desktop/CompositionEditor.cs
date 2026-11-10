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
    [ObservableProperty] private string code = "";
    [ObservableProperty] private string name = "";
    [ObservableProperty] private CompositionItemKind kind;
    [ObservableProperty] private UnitId unit;
    [ObservableProperty] private bool active = true;
    [ObservableProperty] private decimal? referenceUnitCost;
    [ObservableProperty] private string referenceCostSource = "";
    partial void OnReferenceUnitCostChanged(decimal? value)
    {
        ReferenceCostSource = value.HasValue ? "Manual · " + DateTimeOffset.Now.ToString("yyyy-MM-dd") : "";
    }
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
}
public sealed record CompositionTarget(EntityId Id, int Kind, UnitId Unit, string Label);
public sealed partial class CompositionEditor : ObservableObject
{
    private readonly StudyWorkspaceViewModel host;
    private readonly UiTextCatalog texts = new();
    private CompositionCatalog catalog = null!;
    private CompositionVersion? current;
    private bool loading;
    private CompositionPool[] pools = [];
    public ObservableCollection<CompositionItemRow> Items { get; } = [];
    public ObservableCollection<CompositionLineRow> Lines { get; } = [];
    public ObservableCollection<CompositionResultRow> Results { get; } = [];
    public ObservableCollection<CompositionAnswer> Answers { get; } = [];
    public IReadOnlyList<CompositionChoice<UnitId>> Units => host.PhysicalCompany!.Units.Select(u => new CompositionChoice<UnitId>(u.Id, u.Code + " · " + u.Symbol)).ToArray();
    public IReadOnlyList<CompositionChoice<CompositionItemKind>> Kinds => Enum.GetValues<CompositionItemKind>().Select(v => new CompositionChoice<CompositionItemKind>(v, this[v.ToString()])).ToArray();
    public IReadOnlyList<CompositionChoice<CompositionItemKind>> ImportKinds => new[] { CompositionItemKind.Product, CompositionItemKind.Material, CompositionItemKind.Service }.Select(v => new CompositionChoice<CompositionItemKind>(v, this[v.ToString()])).ToArray();
    public IReadOnlyList<CompositionChoice<ConsumptionBasis>> Bases => Enum.GetValues<ConsumptionBasis>().Select(v => new CompositionChoice<ConsumptionBasis>(v, this[v.ToString()])).ToArray();
    public IReadOnlyList<CompositionChoice<CompositionLossKind>> Losses => Enum.GetValues<CompositionLossKind>().Select(v => new CompositionChoice<CompositionLossKind>(v, this[v.ToString()])).ToArray();
    public IReadOnlyList<CompositionChoice<EntityId>> Products => catalog.Items.Where(i => i.Kind != CompositionItemKind.Material).Select(i => new CompositionChoice<EntityId>(i.Id, i.Code + " · " + i.Name)).ToArray();
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
    public bool Editable => Writable && current?.State == CompositionState.Draft && current.ProductId == Product && current.Id == Version;
    public bool HasPendingCompositionOpen => Version != default && (current is null || current.Id != Version || current.ProductId != Product);
    public bool LinesReadOnly => !Editable;
    public string NumericCulture => host.PhysicalFormatCulture.Name;
    public string Context => host.PhysicalCompany!.Name + " · " + host.Current!.Name + " · " + host.ContextSummary;
    public string ActionLabel(string key) => texts.Get("UX." + key, host.PhysicalUiCulture);
    public string State => current is null ? this["ChooseRecipe"] : catalog.Items.Single(i => i.Id == current.ProductId).Name + " · " + this[current.State.ToString()] + " · v" + current.Number + (Dirty ? " · " + this["Unsaved"] : "");
    public string this[string key] => texts.Get("Composition." + key, host.PhysicalUiCulture);
    public CompositionEditor(StudyWorkspaceViewModel host) { this.host = host; Reload(); }
    partial void OnProductChanged(EntityId value) { OnPropertyChanged(nameof(Versions)); Version = Versions.LastOrDefault()?.Value ?? default; SelectionChanged(); }
    partial void OnVersionChanged(EntityId value) => SelectionChanged();
    private void SelectionChanged() { Invalidate(); OnPropertyChanged(nameof(Editable)); OnPropertyChanged(nameof(LinesReadOnly)); OnPropertyChanged(nameof(HasPendingCompositionOpen)); }
    partial void OnBatchSizeChanged(string value) => Changed();
    partial void OnNoteChanged(string value) => Changed();
    partial void OnOutputQuantityChanged(string value) => Invalidate();
    // Decimal round-trip: reopening a draft must not truncate conversion factors.
    private string Q(decimal value) => value.ToString("0.############################", host.PhysicalFormatCulture);
    private decimal Parse(string value)
    {
        if (!decimal.TryParse(value.Trim(), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, host.PhysicalFormatCulture, out var result))
            throw CompositionCalculator.Error("Number");
        return result;
    }
    private void Changed() { if (loading) return; Dirty = true; OnPropertyChanged(nameof(Dirty)); OnPropertyChanged(nameof(State)); Invalidate(); }
    private void Invalidate()
    {
        Results.Clear(); Answers.Clear();
        Answers.Add(new(this["QNeeds"], this["PreviewPending"]));
        Answers.Add(new(this["QCost"], this["T18"]));
        Answers.Add(new(this["QActual"], this["T19"]));
    }
    public void Reload()
    {
        loading = true;
        try
        {
            catalog = host.ActiveCompositions!.ReadCompositions();
            // Validation also needs pinned models belonging to other saved versions.
            pools = host.PhysicalModels!.ReadModels().Where(m => m.Status == ModelStatus.Published).SelectMany(m => m.Structure.Pools.Select(p => new CompositionPool(p.Id, m.Structure.Id, m.Structure.Nodes.Single(n => n.Id == p.NodeId).Code + " · " + p.Description, p.DriverUnitId, m.Structure.Nodes.Single(n => n.Id == p.NodeId).IsActive))).ToArray();
            Items.Clear();
            foreach (var i in catalog.Items)
            {
                var row = new CompositionItemRow { Id = i.Id, Persisted = true, Code = i.Code, Name = i.Name, Kind = i.Kind, Unit = i.UnitId, Active = i.Active,
                    ReferenceUnitCost = i.ReferenceUnitCost, ReferenceCostSource = i.ReferenceCostSource ?? "" };
                row.PropertyChanged += (_, _) => Changed(); Items.Add(row);
            }
            Targets = catalog.Items.Where(i => i.Kind == CompositionItemKind.Material).Select(i => new CompositionTarget(i.Id, 0, i.UnitId, this["Material"] + " · " + i.Code + " · " + i.Name))
                .Concat(catalog.Versions.Where(v => v.State == CompositionState.Published && v.ModelVersionId == host.Current!.SelectedModelVersionId && catalog.Items.Single(i => i.Id == v.ProductId).Kind == CompositionItemKind.SemiFinished).Select(v => { var i = catalog.Items.Single(i => i.Id == v.ProductId); return new CompositionTarget(v.Id, 1, i.UnitId, this["SemiFinished"] + " · " + i.Code + " · v" + v.Number); }))
                .Concat(pools.Where(p => p.ModelId == host.Current!.SelectedModelVersionId).Select(p => new CompositionTarget(p.Id, 2, p.UnitId, this["Activity"] + " · " + p.Name))).ToArray();
            current = null; Lines.Clear(); Dirty = false; Feedback = "";
            OnPropertyChanged(string.Empty); Invalidate();
        }
        finally { loading = false; }
    }
    public void AddItem()
    {
        if (!Writable) return;
        var row = new CompositionItemRow { Unit = Units.FirstOrDefault()?.Value ?? default };
        row.PropertyChanged += (_, _) => Changed(); Items.Add(row); Changed();
    }
    public void RemoveItem(CompositionItemRow? row)
    {
        if (!Writable || row is null) return;
        if (row.Persisted) { Feedback = this["DeactivateHelp"]; return; }
        Items.Remove(row); Changed();
    }
    public void ImportProducts(string path)
    {
        if (!Writable) { Feedback = this["ReadOnlyImport"]; return; }
        try
        {
            var read = ProductCostImport.ReadResult(path, host.PhysicalFormatCulture);
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
                    var row = new CompositionItemRow { Id = item.Id, Code = item.Code, Name = item.Name, Kind = item.Kind,
                        Unit = item.UnitId, Active = item.Active, ReferenceUnitCost = item.ReferenceUnitCost,
                        ReferenceCostSource = item.ReferenceCostSource ?? "" };
                    row.PropertyChanged += (_, _) => Changed(); Items.Add(row); rowsById.Add(row.Id, row);
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
                "ReferenceAmbiguous" => this["ImportReferenceAmbiguous"], _ => this["ImportFailed"]
            };
    }
    public bool OpenProduct(EntityId id)
    {
        if (!catalog.Items.Any(i => i.Id == id && i.Kind != CompositionItemKind.Material)) return false;
        Product = id;
        var versions = catalog.Versions.Where(v => v.ProductId == id && v.ModelVersionId == host.Current!.SelectedModelVersionId);
        var selected = versions.FirstOrDefault(v => v.State == CompositionState.Draft) ?? versions.OrderBy(v => v.Number).LastOrDefault();
        if (selected is null) NewVersion();
        else { Version = selected.Id; Open(); }
        return true;
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
                Attach(new() { Id = l.Id, Target = Targets.Single(t => t.Id == (l.MaterialId ?? l.RecipeVersionId ?? l.PoolId)), Quantity = Q(l.Quantity), Unit = l.InputUnitId, Factor = Q(l.ConversionFactor), Basis = l.Basis, Loss = l.LossKind, Percent = Q(l.LossPercent) });
            Dirty = false; OnPropertyChanged(string.Empty); Invalidate();
        }
        finally { loading = false; }
    }
    public void NewVersion()
    {
        if (!Writable || !catalog.Items.Any(i => i.Id == Product && i.Kind != CompositionItemKind.Material)) return;
        if (catalog.Versions.Any(v => v.ProductId == Product && v.State == CompositionState.Draft)) { Feedback = this["ExistingDraft"]; return; }
        var previous = catalog.Versions.Where(v => v.ProductId == Product).OrderBy(v => v.Number).LastOrDefault();
        current = new(new(Guid.NewGuid()), Product, (previous?.Number ?? 0) + 1, host.Current!.SelectedModelVersionId, previous?.BatchSize ?? 1, CompositionState.Draft, "", previous?.ModelVersionId == host.Current.SelectedModelVersionId ? previous.Lines.Select(l => l with { Id = new(Guid.NewGuid()) }).ToArray() : []);
        catalog = catalog with { Versions = catalog.Versions.Append(current).ToArray() };
        Version = current.Id; Open(); Changed(); OnPropertyChanged(nameof(Versions));
    }
    private void Attach(CompositionLineRow row)
    {
        row.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(row.Target) && row.Target is { } t) { row.Unit = t.Unit; row.Factor = "1"; } Changed(); };
        Lines.Add(row);
    }
    public void AddLine() { if (Editable) { Attach(new() { Unit = Units.FirstOrDefault()?.Value ?? default }); Changed(); } }
    public void RemoveLine(CompositionLineRow? row) { if (Editable && row is not null) { Lines.Remove(row); Changed(); } }
    private bool ValidateInput(bool publish, bool preview = false)
    {
        bool Number(string text, out decimal value) => decimal.TryParse(text.Trim(), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, host.PhysicalFormatCulture, out value);
        if (Items.Select(i => i.Code.Trim()).Distinct(StringComparer.Ordinal).Count() != Items.Count)
        { Feedback = this["InvalidCatalog"]; return false; }
        if (Items.Any(i => string.IsNullOrWhiteSpace(i.Code) || string.IsNullOrWhiteSpace(i.Name) || !Units.Any(u => u.Value == i.Unit) ||
            i.ReferenceUnitCost < 0 || i.ReferenceUnitCost.HasValue && string.IsNullOrWhiteSpace(i.ReferenceCostSource)))
        { Feedback = this["InvalidItem"]; return false; }
        if (current?.State == CompositionState.Draft)
        {
            if (!Number(BatchSize, out var batch) || batch <= 0) { Feedback = this["InvalidVersion"]; return false; }
            if (publish && Lines.Count == 0) { Feedback = this["Empty"]; return false; }
            foreach (var line in Lines)
            {
                if (!Number(line.Quantity, out var quantity) || !Number(line.Factor, out var factor) || !Number(line.Percent, out var percent))
                { Feedback = this["Number"]; return false; }
                if (line.Target is null || quantity < 0 || factor <= 0 || percent < 0 || line.Loss == CompositionLossKind.None && percent != 0 || line.Loss == CompositionLossKind.Yield && (percent == 0 || percent > 100) || !Units.Any(u => u.Value == line.Unit))
                { Feedback = this["InvalidLine"]; return false; }
            }
        }
        if (preview && (!Number(OutputQuantity, out var output) || output <= 0)) { Feedback = this["Number"]; return false; }
        return true;
    }
    private CompositionCatalog Proposal(bool publish)
    {
        var versions = catalog.Versions;
        if (current is { State: CompositionState.Draft } c)
        {
            var lines = Lines.Select(l => new CompositionLine(l.Id, l.Target?.Kind == 0 ? l.Target.Id : null, l.Target?.Kind == 1 ? l.Target.Id : null, l.Target?.Kind == 2 ? l.Target.Id : null, Parse(l.Quantity), l.Unit, Parse(l.Factor), l.Basis, l.Loss, Parse(l.Percent))).ToArray();
            var edited = c with { BatchSize = Parse(BatchSize), Note = Note, Lines = lines, State = publish ? CompositionState.Published : CompositionState.Draft };
            versions = versions.Select(v => v.Id == c.Id ? edited : v).ToArray();
        }
        return catalog with { Items = Items.Select(i => i.Value).ToArray(), Versions = versions };
    }
    public bool Save(bool publish)
    {
        if (!Writable || publish && !Editable || !ValidateInput(publish)) return false;
        try
        {
            var id = current?.Id;
            host.ActiveCompositions!.SaveCompositions(Proposal(publish), catalog.Revision, "local-user");
            Reload(); if (id.HasValue) { Version = id.Value; Open(); }
            Feedback = this["Saved"]; return true;
        }
        catch (DomainRuleException e) { Feedback = texts.Get(e.Diagnostic.MessageKey, host.PhysicalUiCulture); return false; }
        catch (CompanyFileException e) { Feedback = texts.Get(e.Diagnostic.MessageKey, host.PhysicalUiCulture); return false; }
    }
    public async Task<bool> SaveAsync(bool publish)
    {
        if (!Writable || Busy || publish && !Editable || !ValidateInput(publish)) return false;
        var committed = false;
        try
        {
            var proposal = Proposal(publish); var id = current?.Id;
            var store = host.ActiveCompositions!;
            Busy = true; host.SetCompositionBusy(true); Feedback = this["Saving"];
            await Task.Run(() => store.SaveCompositions(proposal, catalog.Revision, "local-user"));
            committed = true;
            Dirty = false;
            // Validate the committed file off the UI thread before repainting.
            await Task.Run(() => store.ReadCompositions());
            Reload(); if (id.HasValue) { Version = id.Value; Open(); }
            Feedback = this["Saved"]; return true;
        }
        catch (DomainRuleException e) { Feedback = (committed ? this["Saved"] + " " : "") + texts.Get(e.Diagnostic.MessageKey, host.PhysicalUiCulture); return false; }
        catch (CompanyFileException e) { Feedback = (committed ? this["Saved"] + " " : "") + texts.Get(e.Diagnostic.MessageKey, host.PhysicalUiCulture); return false; }
        finally { Busy = false; host.SetCompositionBusy(false); OnPropertyChanged(nameof(Dirty)); }
    }
    public void Preview()
    {
        Invalidate();
        if (current is null || current.Id != Version || current.ProductId != Product) { Feedback = this["ChooseRecipe"]; return; }
        try
        {
            if (!ValidateInput(false, true)) return;
            var p = Proposal(false);
            var result = CompositionCalculator.Preview(p, current.Id, Parse(OutputQuantity), host.PhysicalCompany!.Units, pools);
            foreach (var r in result.Lines)
                Results.Add(new(r.Path, r.Name, this[r.Expanded ? "SemiFinished" : r.Activity ? "Activity" : "Material"], Q(r.BaseQuantity), Q(r.AdjustedQuantity), Q(r.RequiredQuantity), host.PhysicalCompany.Units.Single(u => u.Id == r.UnitId).Symbol,
                    host.PhysicalCompany.Units.Single(u => u.Id == r.InputUnitId).Symbol, Q(r.BasisCount), Q(r.ConvertedQuantity)));
            Answers.Clear();
            Answers.Add(new(this["QNeeds"], string.Join("; ", result.Totals.Select(g =>
                g.Name + ": " + Q(g.Quantity) + " " + host.PhysicalCompany.Units.Single(u => u.Id == g.UnitId).Symbol))));
            var selected = p.Versions.Single(v => v.Id == current.Id);
            Answers.Add(new(this["QBatches"], selected.Lines.Any(l => l.Basis == ConsumptionBasis.PerStartedBatch)
                ? Q(result.StartedBatches) + " · " + this["StartedHelp"] : this["NoBatches"]));
            Answers.Add(new(this["QVersion"], State + (Dirty ? " · " + this["Unsaved"] : "")));
            Answers.Add(new(this["QLoss"], string.Join("; ", selected.Lines.Select(l =>
                Targets.Single(t => t.Id == (l.MaterialId ?? l.RecipeVersionId ?? l.PoolId)).Label + ": " +
                this[l.LossKind.ToString()] + (l.LossKind == CompositionLossKind.None ? "" : " " + UiCompactNumbers.Percent(l.LossPercent, host.PhysicalFormatCulture))))));
            var previous = p.Versions.SingleOrDefault(v => v.ProductId == current.ProductId && v.Number == current.Number - 1);
            // Ignore line identity when comparing a cloned version with its predecessor.
            var same = previous is not null && previous.BatchSize == selected.BatchSize &&
                previous.ModelVersionId == selected.ModelVersionId &&
                previous.Lines.Select(l => l with { Id = default }).SequenceEqual(selected.Lines.Select(l => l with { Id = default }));
            Answers.Add(new(this["QChanges"], previous is null ? this["FirstVersion"] : same ? this["SameComposition"] : this["ChangedComposition"] + " v" + previous.Number));
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
