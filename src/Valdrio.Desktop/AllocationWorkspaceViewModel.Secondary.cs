using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using Valdrio.Application;
using Valdrio.Calculation;
using Valdrio.Domain;
using Valdrio.Localization;

namespace Valdrio.Desktop;

public sealed record SecondaryAllocationRow(
    EntityId PrimaryNodeId, EntityId TerminalNodeId, ResourceBehavior SourceBehavior,
    string Source, string Destination, string Amount, string Behavior, decimal PostedAmount);
public sealed record PrincipalCostRow(string Node, string Primary, string Auxiliary, string Total,
    decimal PrimaryAmount, decimal AuxiliaryAmount, decimal TotalAmount);
public sealed record SecondarySourceDetailRow(string Account, string Primary, string Share, string Amount, string Keys) { public string TechnicalDetail { get; init; } = ""; }

public sealed record SecondaryGroupRow(string Source, string Group);

public sealed record SecondaryBridgeRow(
    string Resource, string Effective, string Direct, string Excluded, string Terminal, string Difference);

// The secondary workspace reuses the T08 key library and T09 primary facts.
// It does not post an economic run in the UI until the user explicitly calculates.
public sealed partial class AllocationWorkspaceViewModel
{
    private SecondaryAllocationResult? secondaryPreview;
    private IReadOnlyList<string> requiredSecondaryGroups = [];
    private bool secondaryCalculated;
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
    public bool CanManageSecondary => CanManagePrimary && !IsSecondaryClosed;
    public bool CanEditSecondaryConfiguration => CanManageSecondary && !keyDirty;
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
        SecondaryResults.Clear();
        PrincipalCosts.Clear();
        secondaryContributions.Clear();
        SecondaryExplanationDetails.Clear();
        SecondaryBridges.Clear();
        SecondaryGroupAssignments.Clear();
        SecondaryCirculation.Clear();
        SelectedSecondaryResultRow = null;
        SecondaryExplanation = this["SecondarySelectCost"];
        if (workspace is null)
        {
            SecondarySummary = "";
            SecondaryDiagnostic = this["SecondaryNeedStudy"];
            return;
        }
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
            return;
        }
        var nodes = workspace.Resources.Model.Structure.Nodes.ToDictionary(n => n.Id);
        PopulateSecondaryResults(published.Facts, published.ExplanationLinks);
        PopulateSecondaryBridges(published.ResourceBridges);
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
            PopulateSecondaryBridges(result.ResourceBridges);
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
        SecondaryResults.Clear();
        PrincipalCosts.Clear();
        secondaryContributions.Clear();
        SecondaryExplanationDetails.Clear();
        SecondaryBridges.Clear();
        SecondaryCirculation.Clear();
        SelectedSecondaryResultRow = null;
        SecondaryExplanation = this["SecondarySelectCost"];
        SecondarySummary = "";
        SecondaryDiagnostic = this["SecondaryCalculating"];
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
                return;
            }
            secondaryPreview = result;
            secondaryCalculated = true;
            PresentSecondaryPreview(result);
        }
        catch (OperationCanceledException)
        {
            SecondaryDiagnostic = this["SecondaryCancelled"];
        }
        catch (DomainRuleException ex)
        {
            SecondarySummary = "";
            var diagnostic = MakeReciprocalDiagnosticReadable(ex.Diagnostic);
            SecondaryDiagnostic = new DomainDiagnosticFormatter(texts)
                .Format(diagnostic, System.Globalization.CultureInfo.GetCultureInfo(preferences.Current.UiLanguageTag), Format).Message;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OverflowException)
        {
            SecondarySummary = "";
            SecondaryDiagnostic = this["SecondaryInvalid"];
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
        string Amount(decimal value) => MoneyText(new Money(value,
            workspace.Resources.Study.Currency.FunctionalCurrencyCode, workspace.Resources.Precision));
        foreach (var group in secondaryContributions.GroupBy(c =>
            (c.PrimaryNodeId, c.Fact.TerminalNodeId, c.Fact.Behavior))
            .OrderBy(g => nodes[g.Key.TerminalNodeId].Code, StringComparer.Ordinal)
            .ThenBy(g => nodes[g.Key.PrimaryNodeId].Code, StringComparer.Ordinal).ThenBy(g => g.Key.Behavior))
        {
            var amount = group.Sum(c => c.PostedAmount);
            SecondaryResults.Add(new(group.Key.PrimaryNodeId, group.Key.TerminalNodeId, group.Key.Behavior,
                NodeLabel(nodes[group.Key.PrimaryNodeId]), NodeLabel(nodes[group.Key.TerminalNodeId]),
                Amount(amount), this["SecondaryBehavior" + group.Key.Behavior], amount));
        }
        foreach (var node in nodes.Values.Where(n => n.IsActive && n.Role == NodeRole.Principal)
            .OrderBy(n => n.Code, StringComparer.Ordinal))
        {
            var rows = secondaryContributions.Where(c => c.Fact.TerminalNodeId == node.Id).ToArray();
            var primary = rows.Where(c => nodes[c.PrimaryNodeId].Role == NodeRole.Principal).Sum(c => c.PostedAmount);
            var auxiliary = rows.Where(c => nodes[c.PrimaryNodeId].Role == NodeRole.Auxiliary).Sum(c => c.PostedAmount);
            PrincipalCosts.Add(new(NodeLabel(node), Amount(primary), Amount(auxiliary), Amount(primary + auxiliary),
                primary, auxiliary, primary + auxiliary));
        }
    }

    private void ShowSecondaryExplanation()
    {
        SecondaryExplanationDetails.Clear();
        if (SelectedSecondaryResultRow is not { } row || workspace is null)
        {
            SecondaryExplanation = this["SecondarySelectCost"];
            OnPropertyChanged(nameof(SecondaryExplanation));
            return;
        }
        var nodes = workspace.Resources.Model.Structure.Nodes.ToDictionary(n => n.Id);
        var origins = workspace.Resources.Origins.ToDictionary(o => o.Id);
        var auxiliary = nodes[row.PrimaryNodeId].Role == NodeRole.Auxiliary;
        SecondaryExplanation = string.Format(Format, this[auxiliary ? "SecondaryExplainAuxiliary" : "SecondaryExplainDirect"],
            row.Source, row.Destination, row.Amount, row.Behavior);
        foreach (var contribution in secondaryContributions.Where(c => c.PrimaryNodeId == row.PrimaryNodeId &&
            c.Fact.TerminalNodeId == row.TerminalNodeId && c.Fact.Behavior == row.SourceBehavior)
            .OrderBy(c => c.Fact.Account, StringComparer.Ordinal))
        {
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
                MoneyText(new Money(contribution.PostedAmount, fact.PostedAmount.Currency, fact.PostedAmount.PrecisionPolicy)),
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
