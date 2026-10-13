using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Valdrio.Application;
using Valdrio.Calculation;
using Valdrio.Domain;
using Valdrio.Localization;

namespace Valdrio.Desktop;

public sealed record SecondaryAllocationRow(
    EntityId PrimaryNodeId, EntityId TerminalNodeId, ResourceBehavior SourceBehavior,
    string Source, string Destination, string Amount, string Behavior, decimal PostedAmount)
{
    public decimal PresentedAmount { get; init; }
}
public sealed record PrincipalCostRow(string Node, string Primary, string Auxiliary, string Total,
    decimal PrimaryAmount, decimal AuxiliaryAmount, decimal TotalAmount)
{
    public decimal PresentedPrimaryAmount { get; init; }
    public decimal PresentedAuxiliaryAmount { get; init; }
    public decimal PresentedTotalAmount { get; init; }
}
public sealed record SecondarySourceDetailRow(string Account, string Primary, string Share, string Amount, string Keys) { public string TechnicalDetail { get; init; } = ""; }

public sealed record SecondaryGroupRow(string Source, string Group);
public sealed class SecondaryNodeAssignmentRow(EntityId nodeId, string source, EntityId? selectedKeyId) : ObservableObject
{
    private EntityId? selected = selectedKeyId;
    private string assignmentError = "";
    public string AssignmentError
    {
        get => assignmentError;
        set { if (SetProperty(ref assignmentError, value)) OnPropertyChanged(nameof(HasAssignmentError)); }
    }
    public bool HasAssignmentError => !string.IsNullOrWhiteSpace(AssignmentError);
    public EntityId NodeId { get; } = nodeId;
    public string Source { get; } = source;
    // Keep the committed selection separate from the ComboBox's editable value.
    // On a failed save the control returns to the last persisted key, never a blank.
    public EntityId? PersistedKeyId { get; private set; } = selectedKeyId;
    public EntityId? SelectedKeyId { get => selected; set => SetProperty(ref selected, value); }
    public void AcceptCommitted(EntityId? keyId)
    {
        PersistedKeyId = keyId;
        SelectedKeyId = keyId;
        AssignmentError = "";
    }
    public void RestoreCommitted() => SelectedKeyId = PersistedKeyId;
}

public sealed record SecondaryBridgeRow(
    string Resource, string Effective, string Direct, string Excluded, string Terminal, string Difference);

// The secondary workspace reuses the T08 key library and T09 primary facts.
// It does not post an economic run in the UI until the user explicitly calculates.
public sealed partial class AllocationWorkspaceViewModel
{
    private SecondaryAllocationResult? secondaryPreview;
    private IReadOnlyList<string> requiredSecondaryGroups = [];
    private bool secondaryCalculated;
    private bool secondaryCalculationFailed;
    public bool SecondaryCalculationFailed => secondaryCalculationFailed && !IsSecondaryClosed;
    private CancellationTokenSource? secondaryCalculationCts;
    public ObservableCollection<SecondaryAllocationRow> SecondaryResults { get; } = [];
    public ObservableCollection<PrincipalCostRow> PrincipalCosts { get; } = [];
    public ObservableCollection<SecondarySourceDetailRow> SecondaryExplanationDetails { get; } = [];
    private readonly List<SecondaryPresentedContribution> secondaryContributions = [];
    public ObservableCollection<SecondaryBridgeRow> SecondaryBridges { get; } = [];
    public ObservableCollection<SecondaryGroupRow> SecondaryGroupAssignments { get; } = [];
    private EntityId? secondaryGroupPart;
    private string secondaryGroupName = "all";
    public EntityId? SecondaryGroupPart { get => secondaryGroupPart;
        set
        {
            if (SetProperty(ref secondaryGroupPart, value))
            {
                SecondaryGroupName = value is { } id ?
                    workspace?.Configuration.SecondaryPartRoutingGroups?
                        .FirstOrDefault(g => g.PartId == id)?.ComponentRoutingGroup ?? "all" : "all";
                Refresh();
            }
        } }
    public string SecondaryGroupName { get => secondaryGroupName;
        set { if (SetProperty(ref secondaryGroupName, value ?? "all")) Refresh(); } }
    public IReadOnlyList<ResourceChoice<string>> SecondaryGroupChoices =>
        (workspace?.Configuration.SecondaryRules.Where(r => r.IsActive)
            .Select(r => r.ComponentRoutingGroup).Append("all")
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)
            .Select(g => new ResourceChoice<string>(g,
                g == "all" ? this["AllComponents"] : g)).ToArray()) ?? [];
    public IAsyncRelayCommand AssignSecondaryKeyToMissingCommand { get; private set; } = null!;
    public bool CanAssignSecondaryKeyToMissing =>
        CanEditSecondaryConfiguration && !HasUnappliedRule && SecondaryKey.HasValue && workspace is not null &&
        workspace.Keys.Any(k => k.Id == SecondaryKey.Value &&
            k.Status == ModelStatus.Published && k.Stage == AllocationStage.Secondary) &&
        workspace.Resources.Model.Structure.Nodes.Any(n => n.IsActive && n.Role == NodeRole.Auxiliary &&
            !workspace.Configuration.SecondaryRules.Any(r => r.IsActive &&
                r.AuxiliaryNodeId == n.Id &&
                StringComparer.Ordinal.Equals(r.ComponentRoutingGroup,
                    string.IsNullOrWhiteSpace(GroupName) ? "all" : GroupName.Trim())));

    public IAsyncRelayCommand SaveSecondaryGroupCommand { get; private set; } = null!;
    public IAsyncRelayCommand ClearObsoleteSecondaryGroupsCommand { get; private set; } = null!;
    public bool HasObsoleteSecondaryGroups => workspace?.Configuration.SecondaryPartRoutingGroups?
        .Any(g => !Parts.Any(p => p.Value == g.PartId)) == true;
    public bool CanSaveSecondaryGroup => CanEditSecondaryConfiguration && !HasUnappliedRule && SecondaryGroupPart.HasValue &&
        SecondaryGroupChoices.Any(g => g.Value == SecondaryGroupName);

    public ObservableCollection<ReciprocalCheckRow> SecondaryCirculation { get; } = [];
    private decimal secondaryGrossTotal;
    public string PrincipalCostsPrimaryTotal => PrincipalCosts.Count == 0 ? "" :
        StudyMoney(PrincipalCosts.Sum(row => row.PresentedPrimaryAmount));
    public string PrincipalCostsAuxiliaryTotal => PrincipalCosts.Count == 0 ? "" :
        StudyMoney(PrincipalCosts.Sum(row => row.PresentedAuxiliaryAmount));
    public string PrincipalCostsFinalTotal => PrincipalCosts.Count == 0 ? "" :
        StudyMoney(PrincipalCosts.Sum(row => row.PresentedTotalAmount));
    public string SecondaryResultsTotal => SecondaryResults.Count == 0 ? "" :
        StudyMoney(SecondaryResults.Sum(row => row.PresentedAmount));
    public string SecondaryExplanationTotal => SecondaryExplanationDetails.Count == 0 ||
        SelectedSecondaryResultRow is null ? "" : StudyMoney(SelectedSecondaryResultRow.PresentedAmount);
    // The gross internal circulation is diagnostic only, never added to terminal costs.
    public string SecondaryGrossTotal => SecondaryCirculation.Count == 0 ? "" : StudyMoney(secondaryGrossTotal);
    public string SecondarySummary { get; private set; } = "";
    public string SecondaryDiagnostic { get; private set; } = "";
    public string SecondaryExplanation { get; private set; } = "";
    public SecondaryAllocationRow? SelectedSecondaryResultRow
    {
        get => selectedSecondaryResult;
        set
        {
            if (SetProperty(ref selectedSecondaryResult, value)) { ShowSecondaryExplanation(); ExplainSecondaryCommand.NotifyCanExecuteChanged(); }
        }
    }
    private SecondaryAllocationRow? selectedSecondaryResult;
    public IRelayCommand ExplainSecondaryCommand { get; private set; } = null!;
    public IAsyncRelayCommand CalculateSecondaryCommand { get; private set; } = null!;
    public IRelayCommand CancelSecondaryCommand { get; private set; } = null!;
    public IAsyncRelayCommand ToggleSecondaryCommand { get; private set; } = null!;
    public bool IsSecondaryClosed => workspace?.Configuration.SecondaryClosed == true;
    public bool CanManageSecondary => CanManagePrimary && IsPrimaryClosed && !IsSecondaryClosed;
    public bool CanEditSecondaryConfiguration => CanManageSecondary && !keyDirty;
    public bool CanShowSecondaryEditor => company.CanWrite && workspace?.Resources.Study.State == StudyState.Open &&
        IsPrimaryClosed && !IsSecondaryClosed && !IsDirty;
    public ObservableCollection<SecondaryNodeAssignmentRow> SecondaryNodeAssignments { get; } = [];
    public string SecondaryAssignmentFailure =>
        diagnostic is null ? this["SecondaryAssignmentSaveFailed"] : Status;
    public string SecondaryState => this[IsSecondaryClosed ? "SecondaryClosedState" : "SecondaryOpenState"];
    public string SecondaryToggleLabel => this[IsSecondaryClosed ? "ReopenSecondary" : "CloseSecondary"];

    private IReadOnlyList<(string Group, CalculationNodeDefinition Node)> MissingSecondaryNodes()
    {
        if (workspace is null) return [];
        var model = workspace.Resources.Model.Structure;
        var usedGroups = requiredSecondaryGroups;
        return usedGroups.SelectMany(group => model.Nodes.Where(n =>
            n.IsActive && n.Role == NodeRole.Auxiliary &&
            !workspace.Configuration.SecondaryRules.Any(r => r.IsActive &&
                r.AuxiliaryNodeId == n.Id &&
                StringComparer.Ordinal.Equals(r.ComponentRoutingGroup, group) &&
                workspace.Keys.Any(k => k.Id == r.KeyVersionId &&
                    k.Status == ModelStatus.Published && k.Stage == AllocationStage.Secondary)))
            .Select(node => (Group: group, Node: node)))
            .OrderBy(issue => issue.Group, StringComparer.Ordinal)
            .ThenBy(issue => issue.Node.Id).ToArray();
    }
    private IReadOnlyList<string> DetermineRequiredSecondaryGroups()
    {
        if (workspace is null) return [];
        var map = (workspace.Configuration.SecondaryPartRoutingGroups ?? [])
            .ToDictionary(p => p.PartId, p => p.ComponentRoutingGroup);
        try
        {
            var primary = PrimaryAllocationService.Calculate(workspace);
            return primary.Facts.Select(f => map.TryGetValue(f.PartId, out var group) ? group : "all")
                .DefaultIfEmpty("all").Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        }
        catch (DomainRuleException) { return ["all"]; }
    }

    // One optional convenience operation for large models. Never overwrites
    // manual rules and validates every proposed route before a single save.
    // One effective key per auxiliary in the default routing group. Existing explicit
    // component groups remain intact for backwards compatibility.
    public Task<bool> AssignSecondaryForNodeAsync(EntityId auxiliaryId, EntityId keyId)
    {
        if (!CanManageSecondary || workspace is null ||
            !workspace.Resources.Model.Structure.Nodes.Any(n => n.Id == auxiliaryId && n.IsActive && n.Role == NodeRole.Auxiliary) ||
            !workspace.Keys.Any(k => k.Id == keyId && k.Status == ModelStatus.Published && k.Stage == AllocationStage.Secondary) || IsDirty)
            return Task.FromResult(false);
        return CommitConfig(configuration =>
        {
            var model = workspace.Resources.Model.Structure;
            if (configuration.SecondaryRules.Any(rule => rule.IsActive && rule.AuxiliaryNodeId == auxiliaryId &&
                    rule.ComponentRoutingGroup != "all"))
                throw AllocationDiagnostics.Error("KEY_AUXILIARY_MULTIPLE_KEYS", "Allocation.AuxiliaryMultipleKeys", auxiliaryId);
            var proposed = configuration.SecondaryRules
                .Where(rule => rule.AuxiliaryNodeId != auxiliaryId || rule.ComponentRoutingGroup != "all")
                .Append(new SecondaryRule(new EntityId(Guid.NewGuid()), model.CompanyId, model.Id, auxiliaryId, "all", keyId, true))
                .ToArray();
            _ = AllocationKeyRules.ResolveSecondary(auxiliaryId, "all", proposed, workspace.Keys, model);
            return configuration.WithSecondaryRules(proposed);
        }, true);
    }

    private Task AssignSecondaryKeyToMissingAsync() => CommitConfig(c =>
    {
        if (workspace is null || !SecondaryKey.HasValue)
            throw AllocationDiagnostics.Error("KEY_RULE_INVALID", "Allocation.RuleInvalid");
        var model = workspace.Resources.Model.Structure;
        var group = string.IsNullOrWhiteSpace(GroupName) ? "all" : GroupName.Trim();
        var key = workspace.Keys.SingleOrDefault(k => k.Id == SecondaryKey.Value &&
            k.Status == ModelStatus.Published && k.Stage == AllocationStage.Secondary)
            ?? throw AllocationDiagnostics.Error("KEY_PUBLICATION_REQUIRED", "Allocation.PublicationRequired");
        var missing = model.Nodes.Where(n => n.IsActive && n.Role == NodeRole.Auxiliary &&
            !c.SecondaryRules.Any(r => r.IsActive && r.AuxiliaryNodeId == n.Id &&
                StringComparer.Ordinal.Equals(r.ComponentRoutingGroup, group)))
            .OrderBy(n => n.Id).ToArray();
        if (missing.Length == 0) return c;
        foreach (var node in missing)
            if (c.SecondaryRules.Any(rule => rule.IsActive && rule.AuxiliaryNodeId == node.Id &&
                rule.ComponentRoutingGroup != group))
                throw AllocationDiagnostics.Error("KEY_AUXILIARY_MULTIPLE_KEYS", "Allocation.AuxiliaryMultipleKeys", node.Id);
        // Driver mode cannot be copied to auxiliaries with different units.
        foreach (var node in missing)
            if (key.Mode == AllocationKeyMode.DriverUnits &&
                model.Pools.Single(p => p.NodeId == node.Id).DriverUnitId != key.UnitId)
                throw new DomainRuleException(new DomainDiagnostic("SECONDARY_BATCH_UNIT_MISMATCH",
                    "Secondary.BatchUnitMismatch",
                    [new DiagnosticArgument("source", DiagnosticArgumentType.Identifier, NodeLabel(node))]));
        var proposed = c.SecondaryRules.Concat(missing.Select(n => new SecondaryRule(
            new EntityId(Guid.NewGuid()), model.CompanyId, model.Id, n.Id, group, key.Id, true)))
            .ToArray();
        foreach (var node in missing)
            _ = AllocationKeyRules.ResolveSecondary(node.Id, group, proposed, workspace.Keys, model);
        return c.WithSecondaryRules(proposed);
    }, true);

    private Task SaveSecondaryPartGroupAsync() => CommitConfig(c =>
    {
        if (!SecondaryGroupPart.HasValue || !SecondaryGroupChoices.Any(g => g.Value == SecondaryGroupName))
            throw AllocationDiagnostics.Error("KEY_RULE_INVALID", "Allocation.RuleInvalid");
        var existing = c.SecondaryPartRoutingGroups ?? [];
        var groups = existing.Where(g => g.PartId != SecondaryGroupPart.Value)
            .Concat(SecondaryGroupName == "all"
                ? Array.Empty<SecondaryPartRoutingGroup>()
                : new[] { new SecondaryPartRoutingGroup(SecondaryGroupPart.Value, SecondaryGroupName) })
            .ToArray();
        return c.WithSecondaryPartRoutingGroups(groups);
    }, true);

    private Task ClearObsoleteSecondaryGroupsAsync() => CommitConfig(c =>
        c.WithSecondaryPartRoutingGroups((c.SecondaryPartRoutingGroups ?? [])
            .Where(g => Parts.Any(p => p.Value == g.PartId)).ToArray()), true);

    // Assignment completeness is intentionally distinct from mathematical feasibility:
    // complete but closed/unstable graphs must still allow Calculate to report the error.
    public bool CanCalculateSecondary => !busy && !IsDirty && CanManageSecondary && primaryComplete &&
        workspace is not null && !HasObsoleteSecondaryGroups &&
        MissingSecondaryNodes().Count == 0;

    private void RebuildSecondary()
    {
        requiredSecondaryGroups = DetermineRequiredSecondaryGroups();
        secondaryPreview = null;
        secondaryCalculated = false;
        secondaryCalculationFailed = false;
        SecondaryResults.Clear();
        PrincipalCosts.Clear();
        secondaryContributions.Clear();
        SecondaryExplanationDetails.Clear();
        SecondaryBridges.Clear();
        SecondaryGroupAssignments.Clear();
        // Preserve rows and ComboBox focus while saving; only update their selected keys.
        SecondaryCirculation.Clear();
        secondaryGrossTotal = 0m;
        SelectedSecondaryResultRow = null;
        SecondaryExplanation = this["SecondarySelectCost"];
        if (workspace is null)
        {
            SecondaryNodeAssignments.Clear();
            SecondarySummary = "";
            SecondaryDiagnostic = this["SecondaryNeedStudy"];
            return;
        }
        foreach (var node in workspace.Resources.Model.Structure.Nodes
            .Where(n => n.IsActive && n.Role == NodeRole.Auxiliary)
            .OrderBy(n => NodeLabel(n), StringComparer.Ordinal))
        {
            var route = workspace.Configuration.SecondaryRules.FirstOrDefault(rule =>
                rule.IsActive && rule.AuxiliaryNodeId == node.Id && rule.ComponentRoutingGroup == "all");
            var oldRow = SecondaryNodeAssignments.FirstOrDefault(row => row.NodeId == node.Id);
            if (oldRow is null) SecondaryNodeAssignments.Add(new(node.Id, NodeLabel(node), route?.KeyVersionId));
            else oldRow.AcceptCommitted(route?.KeyVersionId);
        }
        var currentNodeIds = workspace.Resources.Model.Structure.Nodes
            .Where(node => node.IsActive && node.Role == NodeRole.Auxiliary).Select(node => node.Id).ToHashSet();
        foreach (var obsolete in SecondaryNodeAssignments.Where(row => !currentNodeIds.Contains(row.NodeId)).ToArray())
            SecondaryNodeAssignments.Remove(obsolete);
        foreach (var part in Parts)
        {
            var group = workspace.Configuration.SecondaryPartRoutingGroups?
                .FirstOrDefault(g => g.PartId == part.Value)?.ComponentRoutingGroup ?? "all";
            SecondaryGroupAssignments.Add(new(part.Label,
                group == "all" ? this["AllComponents"] : group));
        }
        foreach (var orphan in workspace.Configuration.SecondaryPartRoutingGroups ?? [])
            if (!Parts.Any(p => p.Value == orphan.PartId))
                SecondaryGroupAssignments.Add(new(orphan.PartId.ToString(),
                    orphan.ComponentRoutingGroup + " · " + this["SecondaryObsoleteGroup"]));
        if (IsSecondaryClosed)
        {
            // Closing already published immutable ledger facts. Reopening a study
            // displays that receipt; it MUST NOT rerun the full solver implicitly.
            RestorePublishedSecondary();
            return;
        }
        var missing = MissingSecondaryNodes();
        SecondarySummary = this["SecondaryOpenState"];
        SecondaryDiagnostic = !primaryComplete ? this["SecondaryPrimaryIncomplete"] :
            HasObsoleteSecondaryGroups ? this["SecondaryObsoleteMappingsBlock"] :
            missing.Count == 0 ? this["SecondaryReady"] :
            string.Format(Format, this["SecondaryMissingAssignments"],
                string.Join(", ", missing.Select(x => x.Group + ": " + NodeLabel(x.Node))));
    }

    private void RestorePublishedSecondary()
    {
        var published = workspace?.SecondaryPublication;
        if (workspace is null || published is null ||
            !StringComparer.Ordinal.Equals(published.CalculationFingerprint,
                workspace.Configuration.SecondaryCalculationHash))
        {
            SecondarySummary = "";
            SecondaryDiagnostic = this["SecondaryPublishedMissing"];
            PresentReciprocalFailure(SecondaryDiagnostic);
            return;
        }
        var nodes = workspace.Resources.Model.Structure.Nodes.ToDictionary(n => n.Id);
        PopulateSecondaryResults(published.Facts, published.ExplanationLinks);
        PresentReciprocal(published.GroupCirculations);
        PopulateSecondaryBridges(published.ResourceBridges);
        secondaryGrossTotal = published.AuxiliaryGrossFlows.Sum(flow => flow.GrossAmount);
        foreach (var flow in published.AuxiliaryGrossFlows)
        {
            var name = nodes.Values.FirstOrDefault(n => n.Id.ToString() == flow.AuxiliaryId);
            SecondaryCirculation.Add(new(name is null ? flow.AuxiliaryId : NodeLabel(name),
                this["SecondaryGross"], flow.GrossAmount.ToString("0.########", Format)));
        }
        SecondarySummary = string.Format(Format, this["SecondaryTotals"],
            SecondaryResults.Count, PrincipalCosts.Count, workspace.Resources.Study.Currency.FunctionalCurrencyCode);
        SecondaryDiagnostic = this["SecondaryClosedState"];
    }

    private void PresentSecondaryPreview(SecondaryAllocationResult result)
    {
            if (workspace is null) return;
            var nodes = workspace.Resources.Model.Structure.Nodes.ToDictionary(n => n.Id);
            PopulateSecondaryResults(result.Facts, result.ExplanationLinks);
            PresentReciprocal(result.GroupCirculations);
            PopulateSecondaryBridges(result.ResourceBridges);
            secondaryGrossTotal = result.Circulation.Gross.Sum();
            for (var i = 0; i < result.Circulation.AuxiliaryIds.Count; i++)
            {
                var id = result.Circulation.AuxiliaryIds[i];
                var name = nodes.Values.FirstOrDefault(n => n.Id.ToString() == id);
                SecondaryCirculation.Add(new(name is null ? id : NodeLabel(name),
                    this["SecondaryGross"], result.Circulation.Gross[i].ToString("0.########", Format)));
            }
            SecondarySummary = string.Format(Format, this["SecondaryTotals"],
                SecondaryResults.Count, PrincipalCosts.Count, result.Primary.Currency);
            SecondaryDiagnostic = this["SecondaryCalculationReady"];
    }

    private async Task CalculateSecondaryAsync()
    {
        if (!CanCalculateSecondary || workspace is null) return;
        var input = workspace;
        using var cancellation = new CancellationTokenSource();
        secondaryCalculationCts = cancellation;
        busy = true;
        secondaryPreview = null;
        secondaryCalculated = false;
        secondaryCalculationFailed = false;
        SecondaryResults.Clear();
        PrincipalCosts.Clear();
        secondaryContributions.Clear();
        SecondaryExplanationDetails.Clear();
        SecondaryBridges.Clear();
        SecondaryCirculation.Clear();
        secondaryGrossTotal = 0m;
        SelectedSecondaryResultRow = null;
        SecondaryExplanation = this["SecondarySelectCost"];
        SecondarySummary = "";
        SecondaryDiagnostic = this["SecondaryCalculating"];
        ResetReciprocal();
        ReciprocalSummary = SecondaryDiagnostic;
        company.NotifyWorkspaceStateChanged();
        Refresh();
        try
        {
            // Heavy reciprocal factorization is never performed on the WPF
            // dispatcher. Only the result projection touches bound collections.
            var result = await Task.Run(
                () => SecondaryAllocationService.Calculate(input, cancellation.Token),
                cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            if (!ReferenceEquals(workspace, input))
            {
                SecondaryDiagnostic = this["SecondaryResultStale"];
                ReciprocalSummary = SecondaryDiagnostic;
                return;
            }
            secondaryPreview = result;
            secondaryCalculated = true;
            PresentSecondaryPreview(result);
        }
        catch (OperationCanceledException)
        {
            SecondaryDiagnostic = this["SecondaryCancelled"];
            ReciprocalSummary = SecondaryDiagnostic;
        }
        catch (DomainRuleException ex)
        {
            secondaryCalculationFailed = true;
            SecondarySummary = "";
            // Select the saved route associated with the failing auxiliary when known.
            var troublesome = ex.Diagnostic.Arguments
                .Where(a => a.Name is "auxiliary" or "auxiliaries")
                .SelectMany(a => a.CanonicalValue.Split(','))
                .Select(id => id.Trim()).ToHashSet(StringComparer.Ordinal);
            SelectedSecondary = SecondaryRules.FirstOrDefault(r =>
                workspace?.Configuration.SecondaryRules.Any(route => route.Id == r.Id &&
                    troublesome.Contains(route.AuxiliaryNodeId.ToString())) == true)
                ?? SecondaryRules.FirstOrDefault();
            var diagnostic = MakeReciprocalDiagnosticReadable(ex.Diagnostic);
            SecondaryDiagnostic = new DomainDiagnosticFormatter(texts)
                .Format(diagnostic, System.Globalization.CultureInfo.GetCultureInfo(preferences.Current.UiLanguageTag), Format).Message;
            PresentReciprocalFailure(SecondaryDiagnostic);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OverflowException)
        {
            secondaryCalculationFailed = true;
            SelectedSecondary = SecondaryRules.FirstOrDefault();
            SecondarySummary = "";
            SecondaryDiagnostic = this["SecondaryInvalid"];
            PresentReciprocalFailure(SecondaryDiagnostic);
        }
        finally
        {
            secondaryCalculationCts = null;
            busy = false;
            company.NotifyWorkspaceStateChanged();
            Refresh();
        }
    }

    private void CancelSecondary() => secondaryCalculationCts?.Cancel();

    private void PopulateSecondaryBridges(IReadOnlyList<SecondaryEconomicBridge> bridges)
    {
        if (workspace is null) return;
        var origins = workspace.Resources.Origins.ToDictionary(x => x.Id);
        foreach (var b in bridges)
            SecondaryBridges.Add(new(
                origins.TryGetValue(b.ResourceId, out var r) ? AccountLabel(r) : b.ResourceId.ToString(),
                MoneyText(b.Effective), MoneyText(b.Direct), MoneyText(b.Excluded),
                MoneyText(b.Terminal), MoneyText(b.Difference)));
    }

    private void PopulateSecondaryResults(IReadOnlyList<SecondaryAllocationFact> facts,
        IReadOnlyList<SecondaryExplanationLink> links)
    {
        if (workspace is null) return;
        var nodes = workspace.Resources.Model.Structure.Nodes.ToDictionary(n => n.Id);
        secondaryContributions.AddRange(SecondaryResultPresentation.Build(facts, links, nodes));
        var currency = workspace.Resources.Study.Currency.FunctionalCurrencyCode;
        var precision = workspace.Resources.Precision;
        var quantum = DisplayCurrencyQuantum;

        // Project each principal's direct/auxiliary split and the grand totals as
        // one table, so every visible row AND column reconciles to minor units.
        var principals = nodes.Values.Where(n => n.IsActive && n.Role == NodeRole.Principal)
            .OrderBy(n => n.Code, StringComparer.Ordinal).ToArray();
        var ledger = principals.Select(node =>
        {
            var rows = secondaryContributions.Where(c => c.Fact.TerminalNodeId == node.Id).ToArray();
            return (Direct: rows.Where(c => nodes[c.PrimaryNodeId].Role == NodeRole.Principal)
                    .Sum(c => c.PostedAmount),
                Auxiliary: rows.Where(c => nodes[c.PrimaryNodeId].Role == NodeRole.Auxiliary)
                    .Sum(c => c.PostedAmount));
        }).ToArray();
        var presented = AllocationCurrencyDisplay.ProjectBreakdown(ledger, quantum, precision);
        var shownByNode = new Dictionary<EntityId, (decimal Direct, decimal Auxiliary)>();
        for (var i = 0; i < principals.Length; i++)
        {
            var original = ledger[i];
            var shown = presented[i];
            var originalTotal = checked(original.Direct + original.Auxiliary);
            shownByNode.Add(principals[i].Id, (shown.Direct, shown.Auxiliary));
            PrincipalCosts.Add(new(NodeLabel(principals[i]),
                DisplayMoney(shown.Direct, currency), DisplayMoney(shown.Auxiliary, currency),
                DisplayMoney(shown.Final, currency), original.Direct, original.Auxiliary, originalTotal)
            {
                PresentedPrimaryAmount = shown.Direct,
                PresentedAuxiliaryAmount = shown.Auxiliary,
                PresentedTotalAmount = shown.Final
            });
        }

        var resultGroups = secondaryContributions.GroupBy(c =>
            (c.PrimaryNodeId, c.Fact.TerminalNodeId, c.Fact.Behavior))
            .OrderBy(g => nodes[g.Key.TerminalNodeId].Code, StringComparer.Ordinal)
            .ThenBy(g => nodes[g.Key.PrimaryNodeId].Code, StringComparer.Ordinal)
            .ThenBy(g => g.Key.Behavior)
            .Select(g => (g.Key, LedgerAmount: g.Sum(c => c.PostedAmount))).ToArray();
        var shownByGroup = new Dictionary<(EntityId PrimaryNodeId, EntityId TerminalNodeId,
            ResourceBehavior Behavior), decimal>();
        foreach (var terminalGroup in resultGroups.GroupBy(g => g.Key.TerminalNodeId))
        {
            var totals = shownByNode[terminalGroup.Key];
            foreach (var isDirect in new[] { true, false })
            {
                var groups = terminalGroup.Where(g =>
                    (nodes[g.Key.PrimaryNodeId].Role == NodeRole.Principal) == isDirect).ToArray();
                var target = isDirect ? totals.Direct : totals.Auxiliary;
                var projected = AllocationCurrencyDisplay.ProjectToTarget(
                    groups.Select(g => g.LedgerAmount).ToArray(), target, quantum, precision);
                for (var i = 0; i < groups.Length; i++)
                    shownByGroup.Add(groups[i].Key, projected[i]);
            }
        }
        foreach (var group in resultGroups)
        {
            var shown = shownByGroup[group.Key];
            SecondaryResults.Add(new(group.Key.PrimaryNodeId, group.Key.TerminalNodeId,
                group.Key.Behavior, NodeLabel(nodes[group.Key.PrimaryNodeId]),
                NodeLabel(nodes[group.Key.TerminalNodeId]), DisplayMoney(shown, currency),
                this["SecondaryBehavior" + group.Key.Behavior], group.LedgerAmount)
            {
                PresentedAmount = shown
            });
        }
    }

    private void ShowSecondaryExplanation()
    {
        SecondaryExplanationDetails.Clear();
        if (SelectedSecondaryResultRow is not { } row || workspace is null)
        {
            SecondaryExplanation = this["SecondarySelectCost"];
            OnPropertyChanged(nameof(SecondaryExplanation));
            OnPropertyChanged(nameof(SecondaryExplanationTotal));
            return;
        }
        var nodes = workspace.Resources.Model.Structure.Nodes.ToDictionary(n => n.Id);
        var origins = workspace.Resources.Origins.ToDictionary(o => o.Id);
        var auxiliary = nodes[row.PrimaryNodeId].Role == NodeRole.Auxiliary;
        SecondaryExplanation = string.Format(Format, this[auxiliary ? "SecondaryExplainAuxiliary" : "SecondaryExplainDirect"],
            row.Source, row.Destination, row.Amount, row.Behavior);
        var contributions = secondaryContributions.Where(c => c.PrimaryNodeId == row.PrimaryNodeId &&
            c.Fact.TerminalNodeId == row.TerminalNodeId && c.Fact.Behavior == row.SourceBehavior)
            .OrderBy(c => c.Fact.Account, StringComparer.Ordinal).ToArray();
        var amounts = AllocationCurrencyDisplay.ProjectToTarget(
            contributions.Select(c => c.PostedAmount).ToArray(), row.PresentedAmount,
            DisplayCurrencyQuantum, workspace.Resources.Precision);
        for (var i = 0; i < contributions.Length; i++)
        {
            var contribution = contributions[i];
            var fact = contribution.Fact;
            var initial = contribution.Links.Sum(l => l.PrimaryPostedAmount);
            var raw = contribution.Links.Sum(l => l.RawContribution);
            var keys = contribution.Links.SelectMany(l => (l.SecondaryKeyVersionIds ?? [])
                .Concat(l.PrimaryKeyVersionId is { } id ? new[] { id } : Array.Empty<EntityId>()))
                .Distinct().Select(id => workspace.Keys.FirstOrDefault(k => k.Id == id))
                .Where(k => k is not null).Select(k => KeyLabel(k!));
            SecondaryExplanationDetails.Add(new(
                origins.TryGetValue(fact.ResourceId, out var origin) ? AccountLabel(origin) : fact.Account,
                MoneyText(new Money(initial, fact.PostedAmount.Currency, fact.PostedAmount.PrecisionPolicy)),
                initial == 0m ? "—" : (raw / initial * 100m).ToString("0.####", Format) + " %",
                DisplayMoney(amounts[i], fact.PostedAmount.Currency),
                string.Join(", ", keys))
            {
                TechnicalDetail = string.Format(Format, this["ExplainContributionAdjustment"],
                    raw.ToString("0.########", Format) + " " + Currency,
                    (contribution.PostedAmount - raw).ToString("0.########", Format) + " " + Currency)
                    + Environment.NewLine + this["ExplainTerminalDetail"] + " " +
                    string.Format(Format, this["SecondaryExplainAmounts"], MoneyText(fact.PostedAmount),
                        fact.RawAmount.ToString("0.########", Format) + " " + Currency,
                        fact.NumericAdjustment.ToString("0.########", Format) + " " + Currency,
                        fact.RoundingAdjustment.ToString("0.########", Format) + " " + Currency)
            });
        }
        OnPropertyChanged(nameof(SecondaryExplanation));
        OnPropertyChanged(nameof(SecondaryExplanationTotal));
    }

    private async Task ToggleSecondaryAsync()
    {
        if (workspace is null || !CanManagePrimary || IsDirty) return;
        if (IsSecondaryClosed)
        {
            if (await CommitConfig(c => c.WithSecondaryClosed(false), true))
            { statusKey = "SecondaryOpenState"; Refresh(); }
            return;
        }
        if (!secondaryCalculated || secondaryPreview is null || !secondaryPreview.IsComplete) return;
        var fingerprint = SecondaryAllocationService.ResultFingerprint(secondaryPreview, workspace.Configuration);
        if (await CommitConfig(c => c.WithSecondaryClosed(true, fingerprint), true))
        { statusKey = "SecondaryClosedState"; Refresh(); }
    }
}
