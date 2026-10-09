using System.Collections.ObjectModel;
using Valdrio.Application;
using Valdrio.Domain;

namespace Valdrio.Desktop;

public sealed record PrimaryAllocationRow(string Source, string Destination, string Amount, string AppliedRule);

// The preview belongs to the T08 workspace: never invent a second, editable set of rules.
public sealed partial class AllocationWorkspaceViewModel
{
    public ObservableCollection<PrimaryAllocationRow> PrimaryResults { get; } = [];
    public string PrimarySummary { get; private set; } = "";
    public string PrimaryDiagnostic { get; private set; } = "";

    private bool primaryComplete;
    private void RebuildPrimary()
    {
        primaryComplete = false;
        PrimaryResults.Clear();
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
            var nodeById = workspace.Resources.Model.Structure.Nodes.ToDictionary(n => n.Id);
            var resourceById = workspace.Resources.Origins.ToDictionary(o => o.Id);
            foreach (var fact in primary.Facts)
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
                PrimaryResults.Add(new(source, destination, MoneyText(fact.PostedAmount), rule));
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
