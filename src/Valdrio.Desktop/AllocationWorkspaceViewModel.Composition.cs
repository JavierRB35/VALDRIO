using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Valdrio.Application;
using Valdrio.Domain;

namespace Valdrio.Desktop;

public sealed class CompositionAccountRow(EntityId id, string account, string amount,
    string behavior, string review, bool canBulk, bool isDefault) : ObservableObject
{
    private bool selected;
    public EntityId Id { get; } = id;
    public string Account { get; } = account;
    public string Amount { get; } = amount;
    public string Behavior { get; } = behavior;
    public string Review { get; } = review;
    public bool CanBulk { get; } = canBulk;
    public bool IsDefault { get; } = isDefault;
    public bool Selected { get => selected; set => SetProperty(ref selected, value); }
}

public sealed partial class AllocationWorkspaceViewModel
{
    public ObservableCollection<CompositionAccountRow> CompositionAccounts { get; } = [];
    private ICollectionView? compositionView;
    public ICollectionView CompositionView
    {
        get
        {
            if (compositionView is not null) return compositionView;
            compositionView = CollectionViewSource.GetDefaultView(CompositionAccounts);
            compositionView.Filter = MatchComposition;
            return compositionView;
        }
    }
    private bool compositionSelectionBatch;
    private string compositionSearch = "";
    private bool compositionOnlyExceptions;
    public string CompositionSearch
    {
        get => compositionSearch;
        set
        {
            if (!SetProperty(ref compositionSearch, value)) return;
            ClearCompositionSelection();
            compositionView?.Refresh();
        }
    }
    public bool CompositionOnlyExceptions
    {
        get => compositionOnlyExceptions;
        set
        {
            if (!SetProperty(ref compositionOnlyExceptions, value)) return;
            ClearCompositionSelection();
            compositionView?.Refresh();
        }
    }
    private bool MatchComposition(object obj)
    {
        return obj is CompositionAccountRow row && IsCompositionVisible(row);
    }
    private bool IsCompositionVisible(CompositionAccountRow row)
    {
        if (CompositionOnlyExceptions && row.IsDefault) return false;
        var term = CompositionSearch.Trim();
        return term.Length == 0 || row.Account.Contains(term, StringComparison.CurrentCultureIgnoreCase) ||
            row.Behavior.Contains(term, StringComparison.CurrentCultureIgnoreCase) ||
            row.Review.Contains(term, StringComparison.CurrentCultureIgnoreCase);
    }
    private void SetCompositionSelection(IEnumerable<CompositionAccountRow> rows, bool selected)
    {
        compositionSelectionBatch = true;
        try { foreach (var row in rows) row.Selected = selected; }
        finally { compositionSelectionBatch = false; }
        OnPropertyChanged(nameof(CompositionSelected));
        Commands();
    }
    private void ClearCompositionSelection() => SetCompositionSelection(CompositionAccounts, false);
    private UnitId? compositionUnit;
    private string compositionMinimum = "", compositionMaximum = "";
    public UnitId? CompositionUnit { get => compositionUnit; set => SetProperty(ref compositionUnit, value); }
    public string CompositionMinimum { get => compositionMinimum; set => SetProperty(ref compositionMinimum, value); }
    public string CompositionMaximum { get => compositionMaximum; set => SetProperty(ref compositionMaximum, value); }
    private string compositionFixedPercentage = "100", compositionVariablePercentage = "0";
    public string CompositionFixedPercentage { get => compositionFixedPercentage; set => SetProperty(ref compositionFixedPercentage, value); }
    public string CompositionVariablePercentage { get => compositionVariablePercentage; set => SetProperty(ref compositionVariablePercentage, value); }
    public bool CanEditClassification => CanEdit && !IsDirty && workspace is not null;
    private CompositionAccountRow[] CompositionSelection =>
        CompositionAccounts.Where(IsCompositionVisible).Where(r => r.CanBulk && r.Selected).ToArray();
    public string CompositionSelected => string.Format(Format, this["CompositionSelected"], CompositionSelection.Length);
    public string CompositionSummary
    {
        get
        {
            if (workspace is null) return this["NeedStudy"];
            var parts = workspace.Resources.Resources.SelectMany(r => r.Parts).ToArray();
            var defaults = parts.Count(p => p.Classification.Behavior is ResourceBehavior.Unclassified or ResourceBehavior.AssumedFixed);
            var mixed = parts.Count(p => p.Classification.Behavior == ResourceBehavior.MixedPending);
            return string.Format(Format, this["CompositionSummary"], parts.Length, defaults, parts.Length - defaults, mixed);
        }
    }
    public IAsyncRelayCommand CompositionFixedCommand { get; private set; } = null!;
    public IAsyncRelayCommand CompositionVariableCommand { get; private set; } = null!;
    public IAsyncRelayCommand CompositionReviewedFixedCommand { get; private set; } = null!;
    public IAsyncRelayCommand CompositionMixedCommand { get; private set; } = null!;
    public IAsyncRelayCommand CompositionApplyWeightsCommand { get; private set; } = null!;
    public IRelayCommand CompositionSelectVisibleCommand { get; private set; } = null!;
    public IRelayCommand CompositionClearSelectionCommand { get; private set; } = null!;
    private void InitializeCompositionCommands()
    {
        CompositionSelectVisibleCommand = new RelayCommand(() =>
            SetCompositionSelection(CompositionAccounts.Where(IsCompositionVisible).Where(r => r.CanBulk), true), () => CanEditClassification && CompositionAccounts.Any(r => IsCompositionVisible(r) && r.CanBulk));
        CompositionClearSelectionCommand = new RelayCommand(ClearCompositionSelection,
            () => CanEditClassification && CompositionSelection.Length > 0);
        CompositionFixedCommand = new AsyncRelayCommand(() => UpdateCompositionAsync(ResourceBehavior.AssumedFixed), () => CanEditClassification && CompositionSelection.Length > 0);
        CompositionVariableCommand = new AsyncRelayCommand(() => UpdateCompositionAsync(ResourceBehavior.Variable), () => CanEditClassification && CompositionSelection.Length > 0);
        CompositionReviewedFixedCommand = new AsyncRelayCommand(() => UpdateCompositionAsync(ResourceBehavior.Fixed), () => CanEditClassification && CompositionSelection.Length > 0);
        CompositionMixedCommand = new AsyncRelayCommand(() => UpdateCompositionAsync(ResourceBehavior.MixedPending), () => CanEditClassification && CompositionSelection.Length > 0);
        CompositionApplyWeightsCommand = new AsyncRelayCommand(ApplyCompositionWeightsAsync,
            () => CanEditClassification && CompositionSelection.Length > 0);
    }
    private void CompositionSelectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!compositionSelectionBatch && e.PropertyName == nameof(CompositionAccountRow.Selected))
        { OnPropertyChanged(nameof(CompositionSelected)); Commands(); }
    }
    private void RebuildComposition()
    {
        var previous = CompositionAccounts.Where(r => r.Selected).Select(r => r.Id).ToHashSet();
        foreach (var row in CompositionAccounts) row.PropertyChanged -= CompositionSelectionChanged;
        CompositionAccounts.Clear();
        if (workspace is not null)
        {
            // Build lookups once: typical companies can contain thousands of accounts.
            var resourceLookup = workspace.Resources.Resources.ToDictionary(r => r.Id);
            var coverageLookup = workspace.Resources.Preparation.Coverage.ToDictionary(c => c.ResourceId);
            foreach (var origin in workspace.Resources.Origins.OrderBy(o => o.Code, StringComparer.Ordinal))
            {
                var resource = resourceLookup[origin.Id];
                var amount = coverageLookup[resource.Id].Effective;
                var single = resource.Parts.Count == 1;
                var behavior = single ? resource.Parts[0].Classification.Behavior : ResourceBehavior.MixedPending;
                var type = !single ? this["CompositionSplit"] :
                    behavior is ResourceBehavior.Unclassified or ResourceBehavior.AssumedFixed ? this["CompositionFixedDefault"] :
                    behavior == ResourceBehavior.Fixed ? this["CompositionFixedReviewed"] :
                    behavior == ResourceBehavior.Variable ? this["CompositionVariable"] : this["CompositionMixed"];
                if (single && resource.Parts[0].Classification.VariablePercentage is { } variablePercentage)
                    type = string.Format(Format, this["CompositionWeightsSummary"], 100m - variablePercentage, variablePercentage);
                var isDefault = single && behavior is ResourceBehavior.Unclassified or ResourceBehavior.AssumedFixed;
                var review = !single ? this["CompositionDetail"] :
                    isDefault ? this["CompositionUnreviewed"] : this["CompositionReviewed"];
                var row = new CompositionAccountRow(resource.Id, AccountLabel(origin), MoneyText(amount), type, review, single, isDefault);
                row.Selected = single && previous.Contains(resource.Id);
                row.PropertyChanged += CompositionSelectionChanged;
                CompositionAccounts.Add(row);
            }
        }
        OnPropertyChanged(nameof(CompositionSelected));
        OnPropertyChanged(nameof(CompositionSummary));
        Commands();
    }

    private async Task UpdateCompositionAsync(ResourceBehavior behavior)
    {
        if (!CanEditClassification || workspace is null) return;
        var ids = CompositionSelection.Select(r => r.Id).ToHashSet();
        if (ids.Count == 0) return;
        ResourceBehaviorContext? context = null;
        if (behavior is ResourceBehavior.Fixed or ResourceBehavior.Variable)
        {
            if (CompositionUnit is not { } unit || string.IsNullOrWhiteSpace(CompositionMinimum) ||
                string.IsNullOrWhiteSpace(CompositionMaximum))
            { statusKey = "CompositionContextRequired"; Refresh(); return; }
            try
            {
                var min = Parse(CompositionMinimum);
                var max = Parse(CompositionMaximum);
                context = new(unit, workspace.Resources.Study.PeriodStart.Value,
                    workspace.Resources.Study.PeriodEnd.Value,
                    new Quantity(min, unit, workspace.Resources.Precision),
                    new Quantity(max, unit, workspace.Resources.Precision));
            }
            catch (Exception e) when (e is DomainRuleException or ArgumentException or OverflowException)
            { statusKey = "CompositionContextRequired"; Refresh(); return; }
        }
        ResourcePartReplacement[] changes;
        try
        {
            changes = workspace.Resources.Resources.Where(r => ids.Contains(r.Id) && r.Parts.Count == 1)
                .Select(r => new ResourcePartReplacement(r.Id, r.Parts.Select(p =>
                    p.WithClassification(p.Classification with { Behavior = behavior, Context = context, VariablePercentage = null })).ToArray()))
                .Where(r => r.Parts[0].Classification != workspace.Resources.Resources.Single(x => x.Id == r.ResourceId).Parts[0].Classification)
                .ToArray();
        }
        catch (Exception e) when (e is DomainRuleException or ArgumentException or OverflowException)
        { statusKey = "CompositionContextRequired"; Refresh(); return; }
        if (changes.Length == 0) { statusKey = "CompositionUnchanged"; Refresh(); return; }
        await SaveCompositionAsync(changes, this["CompositionEditReason"]);
    }

    private async Task ApplyCompositionWeightsAsync()
    {
        if (!CanEditClassification || workspace is null) return;
        decimal fixedWeight, variableWeight;
        try { fixedWeight = Parse(CompositionFixedPercentage); variableWeight = Parse(CompositionVariablePercentage); }
        catch (Exception ex) when (ex is ArgumentException or DomainRuleException or OverflowException)
        { statusKey = "CompositionWeightsInvalid"; Refresh(); return; }
        if (fixedWeight < 0m || fixedWeight > 100m || variableWeight < 0m || variableWeight > 100m || fixedWeight + variableWeight != 100m)
        { statusKey = "CompositionWeightsInvalid"; Refresh(); return; }
        var ids = CompositionSelection.Select(r => r.Id).ToHashSet();
        var behavior = variableWeight == 0m ? ResourceBehavior.Fixed : variableWeight == 100m ? ResourceBehavior.Variable : ResourceBehavior.Mixed;
        var changes = workspace.Resources.Resources.Where(r => ids.Contains(r.Id) && r.Parts.Count == 1)
            .Select(r => new ResourcePartReplacement(r.Id, r.Parts.Select(p => p.WithClassification(p.Classification with
            { Behavior = behavior, VariablePercentage = variableWeight, Context = null })).ToArray())).ToArray();
        await SaveCompositionAsync(changes, this["CompositionEditReason"]);
    }

    private async Task<bool> SaveCompositionAsync(IReadOnlyList<ResourcePartReplacement> changes, string reason)
    {
        if (!CanEditClassification || workspace is null || changes.Count == 0) return false;
        return await Run(async () =>
        {
            var study = workspace.Resources.Study;
            var command = new ResourceMutation(ResourceMutationKind.ClassifyBatch, study.CompanyId, study.Id,
                study.Revision, new EntityId(Guid.NewGuid()), "local-user", reason, null,
                [], [], null, null, [], changes);
            _ = ResourceWorkspaceRules.Apply(workspace.Resources, command);
            var receipt = await Task.Run(() => company.ActiveResources!.ChangeResources(command));
            statusKey = "CompositionSaved";
            try { await studies.ApplyResourceReceiptAsync(receipt.ConfirmedStudy); }
            catch (Exception e) when (e is IOException or CompanyFileException or InvalidOperationException)
            { statusKey = "SavedRefreshPending"; }
            workspace = await Task.Run(() => company.ActiveAllocations!.ReadAllocations(study.CompanyId, study.Id));
            Rebuild();
            if (statusKey != "SavedRefreshPending") statusKey = "CompositionSaved";
        });
    }

    private async Task<bool> ConfirmCompositionDefaultsAsync()
    {
        if (!CanEditClassification || workspace is null) return false;
        if (workspace.Resources.Resources.SelectMany(r => r.Parts).Any(p => p.Classification.Behavior == ResourceBehavior.MixedPending))
        { statusKey = "CompositionMixedBlock"; Refresh(); return false; }
        var changes = workspace.Resources.Resources
            .Where(r => r.Parts.Any(p => p.Classification.Behavior == ResourceBehavior.Unclassified))
            .Select(r => new ResourcePartReplacement(r.Id, r.Parts.Select(p =>
                p.Classification.Behavior == ResourceBehavior.Unclassified ?
                    p.WithClassification(p.Classification with { Behavior = ResourceBehavior.AssumedFixed }) : p).ToArray()))
            .ToArray();
        return changes.Length == 0 || await SaveCompositionAsync(changes, this["CompositionCloseReason"]);
    }
}
