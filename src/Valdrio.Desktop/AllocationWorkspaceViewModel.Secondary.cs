using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using Valdrio.Application;
using Valdrio.Domain;
using Valdrio.Localization;

namespace Valdrio.Desktop;

public sealed record SecondaryAllocationRow(
    EntityId ResourceId, EntityId PartId, EntityId TerminalNodeId,
    string Source, string Destination, string Amount, string Behavior);

public sealed record SecondaryGroupRow(string Source, string Group);

public sealed record SecondaryBridgeRow(
    string Resource, string Effective, string Direct, string Excluded, string Terminal, string Difference);

// The secondary workspace reuses the T08 key library and T09 primary facts.
// It does not post an economic run in the UI until the user explicitly calculates.
public sealed partial class AllocationWorkspaceViewModel
{
    private SecondaryAllocationResult? secondaryPreview;
    private bool secondaryCalculated;
    public ObservableCollection<SecondaryAllocationRow> SecondaryResults { get; } = [];
    public ObservableCollection<SecondaryBridgeRow> SecondaryBridges { get; } = [];
    public ObservableCollection<SecondaryGroupRow> SecondaryGroupAssignments { get; } = [];
    private EntityId? secondaryGroupPart;
    private string secondaryGroupName = "all";
    public EntityId? SecondaryGroupPart { get => secondaryGroupPart;
        set { if (SetProperty(ref secondaryGroupPart, value)) Refresh(); } }
    public string SecondaryGroupName { get => secondaryGroupName;
        set { if (SetProperty(ref secondaryGroupName, value ?? "all")) Refresh(); } }
    public IReadOnlyList<ResourceChoice<string>> SecondaryGroupChoices =>
        (workspace?.Configuration.SecondaryRules.Where(r => r.IsActive)
            .Select(r => r.ComponentRoutingGroup).Append("all")
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)
            .Select(g => new ResourceChoice<string>(g,
                g == "all" ? this["AllComponents"] : g)).ToArray()) ?? [];
    public IAsyncRelayCommand SaveSecondaryGroupCommand { get; private set; } = null!;
    public bool CanSaveSecondaryGroup => CanEditSecondaryConfiguration && SecondaryGroupPart.HasValue &&
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
            if (SetProperty(ref selectedSecondaryResult, value)) { SecondaryExplanation = ""; OnPropertyChanged(nameof(SecondaryExplanation)); ExplainSecondaryCommand.NotifyCanExecuteChanged(); }
        }
    }
    private SecondaryAllocationRow? selectedSecondaryResult;
    public IRelayCommand ExplainSecondaryCommand { get; private set; } = null!;
    public IRelayCommand CalculateSecondaryCommand { get; private set; } = null!;
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
        var usedGroups = RequiredSecondaryGroups();
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
    private IReadOnlyList<string> RequiredSecondaryGroups()
    {
        if (workspace is null) return [];
        var map = (workspace.Configuration.SecondaryPartRoutingGroups ?? [])
            .ToDictionary(p => p.PartId, p => p.ComponentRoutingGroup);
        try
        {
            var primary = PrimaryAllocationService.Calculate(workspace);
            return primary.Facts.Select(f => map.TryGetValue(f.PartId, out var group) ? group : "all")
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        }
        catch (DomainRuleException) { return ["all"]; }
    }

    private Task SaveSecondaryPartGroupAsync() => CommitConfig(c =>
    {
        if (!SecondaryGroupPart.HasValue || !SecondaryGroupChoices.Any(g => g.Value == SecondaryGroupName))
            throw AllocationDiagnostics.Error("KEY_RULE_INVALID", "Allocation.RuleInvalid");
        var existing = c.SecondaryPartRoutingGroups ?? [];
        var groups = existing.Where(g => g.PartId != SecondaryGroupPart.Value)
            .Concat(SecondaryGroupName == "all" ? [] :
                [new SecondaryPartRoutingGroup(SecondaryGroupPart.Value, SecondaryGroupName)])
            .ToArray();
        return c.WithSecondaryPartRoutingGroups(groups);
    }, true);

    // Assignment completeness is intentionally distinct from mathematical feasibility:
    // complete but closed/unstable graphs must still allow Calculate to report the error.
    public bool CanCalculateSecondary => !busy && !IsDirty && CanManageSecondary && primaryComplete &&
        workspace is not null &&
        MissingSecondaryNodes().Count == 0;

    private void RebuildSecondary()
    {
        secondaryPreview = null;
        secondaryCalculated = false;
        SecondaryResults.Clear();
        SecondaryBridges.Clear();
        SecondaryGroupAssignments.Clear();
        SecondaryCirculation.Clear();
        SelectedSecondaryResultRow = null;
        SecondaryExplanation = "";
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
        var origins = workspace.Resources.Origins.ToDictionary(o => o.Id);
        foreach (var fact in published.Facts)
        {
            SecondaryResults.Add(new(fact.ResourceId, fact.PartId, fact.TerminalNodeId,
                origins.TryGetValue(fact.ResourceId, out var origin) ? AccountLabel(origin) : fact.Account,
                nodes.TryGetValue(fact.TerminalNodeId, out var node) ? NodeLabel(node) : fact.TerminalNodeId.ToString(),
                MoneyText(fact.PostedAmount), this["SecondaryBehavior" + fact.Behavior]));
        }
        PopulateSecondaryBridges(published.ResourceBridges);
        foreach (var flow in published.AuxiliaryGrossFlows)
        {
            var name = nodes.Values.FirstOrDefault(n => n.Id.ToString() == flow.AuxiliaryId);
            SecondaryCirculation.Add(new(name is null ? flow.AuxiliaryId : NodeLabel(name),
                this["SecondaryGross"], flow.GrossAmount.ToString("0.########", Format)));
        }
        SecondarySummary = string.Format(Format, this["SecondaryTotals"],
            published.Facts.Count, published.Totals.Count, workspace.Resources.Study.Currency.FunctionalCurrencyCode);
        SecondaryDiagnostic = this["SecondaryClosedState"];
    }

    private void CalculateSecondary()
    {
        if (!CanCalculateSecondary || workspace is null) return;
        CalculateSecondaryCore();
        Refresh();
    }

    private void CalculateSecondaryCore()
    {
        if (workspace is null) return;
        secondaryPreview = null;
        secondaryCalculated = false;
        SecondaryResults.Clear();
        SecondaryCirculation.Clear();
        SecondaryExplanation = "";
        try
        {
            var result = SecondaryAllocationService.Calculate(workspace);
            secondaryPreview = result;
            secondaryCalculated = true;
            var nodes = workspace.Resources.Model.Structure.Nodes.ToDictionary(n => n.Id);
            var origins = workspace.Resources.Origins.ToDictionary(o => o.Id);
            foreach (var fact in result.Facts)
            {
                SecondaryResults.Add(new(fact.ResourceId, fact.PartId, fact.TerminalNodeId,
                    AccountLabel(origins[fact.ResourceId]), NodeLabel(nodes[fact.TerminalNodeId]),
                    MoneyText(fact.PostedAmount), this["SecondaryBehavior" + fact.Behavior]));
            }
            PopulateSecondaryBridges(result.ResourceBridges);
            for (var i = 0; i < result.Circulation.AuxiliaryIds.Count; i++)
            {
                var id = result.Circulation.AuxiliaryIds[i];
                var name = nodes.Values.FirstOrDefault(n => n.Id.ToString() == id);
                SecondaryCirculation.Add(new(name is null ? id : NodeLabel(name),
                    this["SecondaryGross"], result.Circulation.Gross[i].ToString("0.########", Format)));
            }
            SecondarySummary = string.Format(Format, this["SecondaryTotals"],
                result.Facts.Count, result.Totals.Count, result.Primary.Currency);
            SecondaryDiagnostic = this["SecondaryCalculationReady"];
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
    }

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

    private void ShowSecondaryExplanation()
    {
        if (SelectedSecondaryResultRow is not { } row || workspace is null ||
            (secondaryPreview is null && workspace.SecondaryPublication is null))
        {
            SecondaryExplanation = "";
            OnPropertyChanged(nameof(SecondaryExplanation));
            return;
        }
        var nodes = workspace.Resources.Model.Structure.Nodes.ToDictionary(n => n.Id);
        var links = (secondaryPreview?.ExplanationLinks ??
            workspace.SecondaryPublication!.ExplanationLinks).Where(l =>
            l.ResourceId == row.ResourceId && l.PartId == row.PartId &&
            l.TerminalNodeId == row.TerminalNodeId);
        SecondaryExplanation = string.Join(Environment.NewLine, links.Select(l =>
            NodeLabel(nodes[l.PrimaryNodeId]) + " → " + NodeLabel(nodes[l.TerminalNodeId]) +
            " · " + l.RawContribution.ToString("0.########", Format) + " " + Currency +
            " · " + l.EffectiveShare.ToString("0.########", Format)));
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
