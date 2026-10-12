using System.Collections.ObjectModel;
using Valdrio.Application;
using Valdrio.Domain;

namespace Valdrio.Desktop;

public sealed record PrimaryAllocationRow(string Source, string Destination, string Amount, string AppliedRule, string Behavior);
public sealed record PrimaryNodeTotalRow(string Center, string Node, string Amount, decimal NumericAmount);

// The preview belongs to the T08 workspace: never invent a second, editable set of rules.
public sealed partial class AllocationWorkspaceViewModel
{
    public ObservableCollection<PrimaryAllocationRow> PrimaryResults { get; } = [];
    public ObservableCollection<PrimaryNodeTotalRow> PrimaryNodeTotals { get; } = [];
    public string PrimarySummary { get; private set; } = "";
    public string PrimaryDiagnostic { get; private set; } = "";

    private bool primaryComplete;
    private decimal primaryPostedTotal;
    private decimal primaryShownTotal;
    private string StudyMoney(decimal amount) => workspace is null ? "" :
        amount.ToString("0.########", Format) + " " + workspace.Resources.Study.Currency.FunctionalCurrencyCode;
    public string PrimaryResultsTotal => PrimaryResults.Count == 0 ? "" : DisplayStudyMoney(primaryShownTotal);
    public string PrimaryNodeTotalsTotal => PrimaryNodeTotals.Count == 0 ? "" :
        DisplayStudyMoney(primaryShownTotal);
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
                return selected.Length == 0 ? "" : StudyMoney(selected.Sum(line => Parse(line.Value)));
            }
            catch (Exception ex) when (ex is DomainRuleException or ArgumentException or OverflowException)
            {
                return this["CheckNumbers"];
            }
        }
    }


    private string DisplayStudyMoney(decimal amount) => workspace is null ? "" :
        FinancialDisplayProjection.Format(amount, workspace.Resources.Study.Currency.FunctionalCurrencyCode.ToString(),
            workspace.Resources.Study.Currency.CurrencyMetadataSnapshot.ReportQuantum, Format);

    private void RebuildPrimary()
    {
        primaryComplete = false;
        primaryPostedTotal = 0m;
        primaryShownTotal = 0m;
        PrimaryResults.Clear();
        PrimaryNodeTotals.Clear();
        if (workspace is null)
        {
            PrimarySummary = "";
            PrimaryDiagnostic = this["PrimaryNotLoaded"];
            return;
        }

        try
        {
            var primary = PrimaryAllocationService.Calculate(workspace);
            primaryComplete = workspace.Resources.Origins.Count > 0 && primary.IsComplete;
            primaryPostedTotal = primary.Facts.Sum(f => f.PostedAmount.Amount);
            var projection = FinancialDisplayProjection.Project(primary.Facts.Select(f => f.PostedAmount.Amount).ToArray(),
                workspace.Resources.Study.Currency.CurrencyMetadataSnapshot.ReportQuantum);
            primaryShownTotal = projection.Sum();
            var nodeById = workspace.Resources.Model.Structure.Nodes.ToDictionary(n => n.Id);
            var resourceById = workspace.Resources.Origins.ToDictionary(o => o.Id);
            for (var index=0; index<primary.Facts.Count; index++)
            {
                var fact=primary.Facts[index];
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
                PrimaryResults.Add(new(source, destination, DisplayStudyMoney(projection[index]), rule, this["SecondaryBehavior" + fact.Behavior]));
            }
            // Group published primary facts by node, not by account, without changing the ledger.
            var centers = workspace.Resources.Model.PhysicalCenters.ToDictionary(center => center.Id);
            foreach (var group in primary.Facts.Select((fact,index) => (fact,index)).GroupBy(item => item.fact.NodeId)
                .OrderBy(group => centers[nodeById[group.Key].PhysicalCenterId].Code, StringComparer.Ordinal)
                .ThenBy(group => nodeById[group.Key].Code, StringComparer.Ordinal))
            {
                var node = nodeById[group.Key];
                var center = centers[node.PhysicalCenterId];
                var total = group.Sum(item => item.fact.PostedAmount.Amount);
                var presented = group.Sum(item => projection[item.index]);
                PrimaryNodeTotals.Add(new(center.Code + " · " + center.Name, node.Code,
                    DisplayStudyMoney(presented), total));
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
