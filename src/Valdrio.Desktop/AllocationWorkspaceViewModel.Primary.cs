using System.Collections.ObjectModel;
using Valdrio.Application;
using Valdrio.Domain;

namespace Valdrio.Desktop;

public sealed record PrimaryAllocationRow(string Source, string Destination, string Amount, string AppliedRule, string Behavior)
{
    public decimal PresentedAmount { get; init; }
}
public sealed record PrimaryNodeTotalRow(string Center, string Node, string Amount, decimal NumericAmount)
{
    public decimal PresentedAmount { get; init; }
}

// The preview belongs to the T08 workspace: never invent a second, editable set of rules.
public sealed partial class AllocationWorkspaceViewModel
{
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private int primaryResultCategoryIndex;
    public ObservableCollection<PrimaryAllocationRow> PrimaryResults { get; } = [];
    public ObservableCollection<PrimaryNodeTotalRow> PrimaryNodeTotals { get; } = [];
    // Two read-only presentations of the same primary facts; never independent ledgers.
    public ObservableCollection<PrimaryAllocationRow> GeneralPrimaryResults { get; } = [];
    public ObservableCollection<PrimaryAllocationRow> DirectPrimaryResults { get; } = [];
    public ObservableCollection<PrimaryNodeTotalRow> GeneralPrimaryNodeTotals { get; } = [];
    public ObservableCollection<PrimaryNodeTotalRow> DirectPrimaryNodeTotals { get; } = [];
    public string PrimarySummary { get; private set; } = "";
    public string PrimaryDiagnostic { get; private set; } = "";

    private bool primaryComplete;
    private PrimaryAllocationResult? primaryForOverview;
    private decimal primaryPostedTotal;
    public System.Globalization.CultureInfo ParetoFormat => Format;
    public string ParetoMoney(decimal amount) => StudyMoney(amount);
    private string StudyMoney(decimal amount) => workspace is null ? "" :
        DisplayMoney(amount, workspace.Resources.Study.Currency.FunctionalCurrencyCode);
    public string PrimaryResultsTotal => PrimaryResults.Count == 0 ? "" : StudyMoney(primaryPostedTotal);
    public string PrimaryNodeTotalsTotal => PrimaryNodeTotals.Count == 0 ? "" :
        StudyMoney(PrimaryNodeTotals.Sum(row => row.PresentedAmount));
    public string PrimaryCoverageTotal => Coverage.Count == 0 ? "" :
        StudyMoney(Coverage.Sum(row => row.NumericAmount));
    public string PrimaryCompositionTotal => workspace?.Resources.Preparation.Coverage is not { Count: > 0 } coverage ? "" :
        StudyMoney(coverage.Sum(row => row.Effective.Amount));
    public string PrimaryAbsoluteTotal
    {
        get
        {
            try
            {
                var selected = AbsoluteLines.Where(line => line.Included).ToArray();
                if (selected.Any(line => !numbers.TryCommit(line.Value, out _))) return this["CheckNumbers"];
                return selected.Length == 0 ? "" : StudyMoney(selected.Sum(line => Parse(line.Value)));
            }
            catch (Exception ex) when (ex is DomainRuleException or ArgumentException or OverflowException)
            {
                return this["CheckNumbers"];
            }
        }
    }


    private void RebuildPrimary()
    {
        primaryComplete = false;
        primaryForOverview = null;
        ClearPrimaryAccountOverview();
        primaryPostedTotal = 0m;
        PrimaryResults.Clear();
        PrimaryNodeTotals.Clear();
        GeneralPrimaryResults.Clear();
        DirectPrimaryResults.Clear();
        GeneralPrimaryNodeTotals.Clear();
        DirectPrimaryNodeTotals.Clear();
        if (workspace is null)
        {
            PrimarySummary = "";
            PrimaryDiagnostic = this["PrimaryNotLoaded"];
            return;
        }

        try
        {
            var primary = PrimaryAllocationService.Calculate(workspace);
            primaryForOverview = primary;
            primaryComplete = workspace.Resources.Origins.Count > 0 && primary.IsComplete;
            primaryPostedTotal = primary.Facts.Sum(f => f.PostedAmount.Amount);
            var nodeById = workspace.Resources.Model.Structure.Nodes.ToDictionary(n => n.Id);
            var resourceById = workspace.Resources.Origins.ToDictionary(o => o.Id);
            var facts = primary.Facts.ToArray();
            var shown = AllocationCurrencyDisplay.Project(
                facts.Select(fact => fact.PostedAmount.Amount).ToArray(),
                DisplayCurrencyQuantum, workspace.Resources.Precision);
            var displayed = facts.Select((fact, i) => (Fact: fact, Amount: shown[i])).ToArray();
            BuildPrimaryAccountOverview(primary, displayed);
            foreach (var (fact, displayAmount) in displayed)
            {
                var source = AccountLabel(resourceById[fact.ResourceId]);
                var destination = NodeLabel(nodeById[fact.NodeId]);
                var rule = fact.Selection switch
                {
                    "absolute" => this["PrimaryAbsolute"],
                    "manual" => this["PrimaryManual"],
                    _ => this["PrimaryRuleSource"]
                };
                if (fact.KeyVersionId is { } key)
                {
                    var applied = workspace.Keys.Single(k => k.Id == key);
                    rule += ": " + KeyLabel(applied);
                }
                var row = new PrimaryAllocationRow(source, destination,
                    DisplayMoney(displayAmount, fact.PostedAmount.Currency),
                    rule, this["SecondaryBehavior" + fact.Behavior])
                { PresentedAmount = displayAmount };
                PrimaryResults.Add(row);
                (NodeCostClassification.IsDirectFactor(nodeById[fact.NodeId])
                    ? DirectPrimaryResults : GeneralPrimaryResults).Add(row);
            }
            // Group published primary facts by node, not by account, without changing the ledger.
            var centers = workspace.Resources.Model.PhysicalCenters.ToDictionary(center => center.Id);
            foreach (var group in displayed.GroupBy(item => item.Fact.NodeId)
                .OrderBy(group => centers[nodeById[group.Key].PhysicalCenterId].Code, StringComparer.Ordinal)
                .ThenBy(group => nodeById[group.Key].Code, StringComparer.Ordinal))
            {
                var node = nodeById[group.Key];
                var center = centers[node.PhysicalCenterId];
                var ledgerTotal = group.Sum(item => item.Fact.PostedAmount.Amount);
                var shownTotal = group.Sum(item => item.Amount);
                var row = new PrimaryNodeTotalRow(center.Code + " · " + center.Name, node.Code,
                    DisplayMoney(shownTotal, primary.Currency), ledgerTotal)
                { PresentedAmount = shownTotal };
                PrimaryNodeTotals.Add(row);
                (NodeCostClassification.IsDirectFactor(node)
                    ? DirectPrimaryNodeTotals : GeneralPrimaryNodeTotals).Add(row);
            }
            PrimarySummary = string.Format(Format, this["PrimarySummary"], primary.Facts.Count,
                primary.Gaps.Count, primary.Pools.Count, primary.Currency);
            PrimaryDiagnostic = primary.IsComplete ? this["PrimaryReady"] : this["PrimaryIncomplete"];
            if (primary.Gaps.Count > 0)
            {
                var formatter = new Valdrio.Localization.DomainDiagnosticFormatter(texts);
                var culture = System.Globalization.CultureInfo.GetCultureInfo(preferences.Current.UiLanguageTag);
                PrimaryDiagnostic += Environment.NewLine + string.Join(Environment.NewLine,
                    primary.Gaps.Select(g => formatter.Format(g.Diagnostic, culture, Format).Message).Distinct());
            }
        }
        catch (DomainRuleException ex)
        {
            PrimarySummary = "";
            PrimaryDiagnostic = new Valdrio.Localization.DomainDiagnosticFormatter(texts)
                .Format(ex.Diagnostic, System.Globalization.CultureInfo.GetCultureInfo(preferences.Current.UiLanguageTag), Format).Message;
        }
    }
}
