using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using Valdrio.Application;
using Valdrio.Domain;
using Valdrio.Localization;

namespace Valdrio.Desktop;

public sealed record SecondaryAllocationRow(
    EntityId ResourceId, EntityId PartId, EntityId TerminalNodeId,
    string Source, string Destination, string Amount, string Behavior);

// The secondary workspace reuses the T08 key library and T09 primary facts.
// It does not post an economic run in the UI until the user explicitly calculates.
public sealed partial class AllocationWorkspaceViewModel
{
    private SecondaryAllocationResult? secondaryPreview;
    private bool secondaryCalculated;
    public ObservableCollection<SecondaryAllocationRow> SecondaryResults { get; } = [];
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

    private IReadOnlyList<CalculationNodeDefinition> MissingSecondaryNodes()
    {
        if (workspace is null) return [];
        var model = workspace.Resources.Model.Structure;
        return model.Nodes.Where(n => n.IsActive && n.Role == NodeRole.Auxiliary &&
            !workspace.Configuration.SecondaryRules.Any(r => r.IsActive && r.AuxiliaryNodeId == n.Id &&
                workspace.Keys.Any(k => k.Id == r.KeyVersionId && k.Status == ModelStatus.Published &&
                    k.Stage == AllocationStage.Secondary))).OrderBy(n => n.Id).ToArray();
    }
    // Assignment completeness is intentionally distinct from mathematical feasibility:
    // complete but closed/unstable graphs must still allow Calculate to report the error.
    public bool CanCalculateSecondary => !busy && !IsDirty && CanManageSecondary && primaryComplete &&
        workspace is not null && workspace.Resources.Model.Structure.Nodes.Any(n => n.IsActive && n.Role == NodeRole.Auxiliary) &&
        MissingSecondaryNodes().Count == 0;

    private void RebuildSecondary()
    {
        secondaryPreview = null;
        secondaryCalculated = false;
        SecondaryResults.Clear();
        SecondaryCirculation.Clear();
        SelectedSecondaryResultRow = null;
        SecondaryExplanation = "";
        if (workspace is null)
        {
            SecondarySummary = "";
            SecondaryDiagnostic = this["SecondaryNeedStudy"];
            return;
        }
        if (IsSecondaryClosed)
        {
            // Closed results are reconstructed from the same immutable primary
            // facts and published keys; never display a stale cache as approved.
            CalculateSecondaryCore(true);
            return;
        }
        var missing = MissingSecondaryNodes();
        SecondarySummary = this["SecondaryOpenState"];
        SecondaryDiagnostic = !primaryComplete ? this["SecondaryPrimaryIncomplete"] :
            missing.Count == 0 ? this["SecondaryReady"] :
            string.Format(Format, this["SecondaryMissingAssignments"],
                string.Join(", ", missing.Select(NodeLabel)));
    }

    private void CalculateSecondary()
    {
        if (!CanCalculateSecondary || workspace is null) return;
        CalculateSecondaryCore(false);
        Refresh();
    }

    private void CalculateSecondaryCore(bool restoringClosed)
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
            if (restoringClosed && !StringComparer.Ordinal.Equals(
                workspace.Configuration.SecondaryCalculationHash,
                SecondaryAllocationService.ResultFingerprint(result, workspace.Configuration)))
                throw new DomainRuleException(new DomainDiagnostic(
                    "SECONDARY_RESULT_STALE", "Secondary.ResultStale", []));
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
            for (var i = 0; i < result.Circulation.AuxiliaryIds.Count; i++)
            {
                var id = result.Circulation.AuxiliaryIds[i];
                var name = nodes.Values.FirstOrDefault(n => n.Id.ToString() == id);
                SecondaryCirculation.Add(new(name is null ? id : NodeLabel(name),
                    this["SecondaryGross"], result.Circulation.Gross[i].ToString("0.########", Format)));
            }
            SecondarySummary = string.Format(Format, this["SecondaryTotals"],
                result.Facts.Count, result.Totals.Count, result.Primary.Currency);
            SecondaryDiagnostic = this[restoringClosed ? "SecondaryClosedState" : "SecondaryCalculationReady"];
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

    private void ShowSecondaryExplanation()
    {
        if (SelectedSecondaryResultRow is not { } row || secondaryPreview is null || workspace is null)
        {
            SecondaryExplanation = "";
            OnPropertyChanged(nameof(SecondaryExplanation));
            return;
        }
        var nodes = workspace.Resources.Model.Structure.Nodes.ToDictionary(n => n.Id);
        var links = secondaryPreview.ExplanationLinks.Where(l =>
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
