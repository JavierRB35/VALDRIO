using System.Collections.ObjectModel;
using Valdrio.Application;
using Valdrio.Calculation;
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
    // Currency presentation never alters the stored eight-decimal economic ledger.
    private string StudyMoney(decimal amount) => DisplayedMoney(amount);
    private string DisplayedMoney(decimal amount)
    {
        if (workspace is null) return "";
        var currency = workspace.Resources.Study.Currency;
        return amount.ToString("N" + currency.CurrencyMetadataSnapshot.MinorUnitDigits, Format)
            + " " + currency.FunctionalCurrencyCode;
    }
    private decimal[] ProjectForDisplay(IReadOnlyList<decimal> values)
    {
        if (values.Count == 0) return [];
        var entries = values.Select((value, i) => new CurrencyProjectionInputLine(
            new StableRoundingId("view-" + i.ToString("D8", System.Globalization.CultureInfo.InvariantCulture)), value)).ToArray();
        var projected = new CurrencyProjector().Project(values.Sum(), entries,
            workspace!.Resources.Study.Currency.CurrencyMetadataSnapshot.ReportQuantum);
        var byId = projected.Lines.ToDictionary(line => line.DestinationId.Value, line => line.ProjectedAmount, StringComparer.Ordinal);
        return Enumerable.Range(0, values.Count)
            .Select(i => byId["view-" + i.ToString("D8", System.Globalization.CultureInfo.InvariantCulture)]).ToArray();
    }
    public string PrimaryResultsTotal => PrimaryResults.Count == 0 ? "" : StudyMoney(primaryPostedTotal);
    public string PrimaryNodeTotalsTotal => PrimaryNodeTotals.Count == 0 ? "" :
        StudyMoney(PrimaryNodeTotals.Sum(row => row.NumericAmount));
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


    private void RebuildPrimary()
    {
        primaryComplete = false;
        primaryPostedTotal = 0m;
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
            var nodeById = workspace.Resources.Model.Structure.Nodes.ToDictionary(n => n.Id);
            var resourceById = workspace.Resources.Origins.ToDictionary(o => o.Id);
            var presentedFacts = ProjectForDisplay(primary.Facts.Select(f => f.PostedAmount.Amount).ToArray());
            for (var i = 0; i < primary.Facts.Count; i++)
            {
                var fact = primary.Facts[i];
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
                PrimaryResults.Add(new(source, destination, DisplayedMoney(presentedFacts[i]), rule, this["SecondaryBehavior" + fact.Behavior]));
            }
            // Group published primary facts by node, not by account, without changing the ledger.
            var centers = workspace.Resources.Model.PhysicalCenters.ToDictionary(center => center.Id);
            foreach (var group in primary.Facts.GroupBy(fact => fact.NodeId)
                .OrderBy(group => centers[nodeById[group.Key].PhysicalCenterId].Code, StringComparer.Ordinal)
                .ThenBy(group => nodeById[group.Key].Code, StringComparer.Ordinal))
            {
                var node = nodeById[group.Key];
                var center = centers[node.PhysicalCenterId];
                var total = group.Sum(fact => fact.PostedAmount.Amount);
                var displayed = primary.Facts.Select((fact, index) => (fact, index))
                    .Where(x => x.fact.NodeId == group.Key)
                    .Sum(x => presentedFacts[x.index]);
                PrimaryNodeTotals.Add(new(center.Code + " · " + center.Name, node.Code,
                    DisplayedMoney(displayed), total));
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
