using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using Valdrio.Calculation;
using Valdrio.Domain;
using Valdrio.Localization;

namespace Valdrio.Desktop;

public sealed record ReciprocalCheckRow(string Group, string State, string Detail);

// Read-only feasibility diagnostic over persisted/published T08 routing keys.
// This never posts a secondary allocation or alters the economic snapshot.
public sealed partial class AllocationWorkspaceViewModel
{
    public ObservableCollection<ReciprocalCheckRow> ReciprocalChecks { get; } = [];
    public string ReciprocalSummary { get; private set; } = "";
    public IRelayCommand CheckReciprocalCommand { get; private set; } = null!;

    private void ResetReciprocal()
    {
        ReciprocalChecks.Clear();
        ReciprocalSummary = this[workspace is null ? "ReciprocalNeedStudy" : "ReciprocalNotChecked"];
    }

    private void CheckReciprocal()
    {
        if (workspace is null || busy) return;
        ReciprocalChecks.Clear();
        var model = workspace.Resources.Model.Structure;
        if (!model.Nodes.Any(n => n.IsActive && n.Role == NodeRole.Auxiliary))
        {
            ReciprocalSummary = this["ReciprocalNoAuxiliaries"];
            Refresh();
            return;
        }
        var groups = workspace.Configuration.SecondaryRules.Where(r => r.IsActive)
            .Select(r => r.ComponentRoutingGroup)
            .DefaultIfEmpty("all").Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).ToArray();

        var formatter = new DomainDiagnosticFormatter(texts);
        var ui = System.Globalization.CultureInfo.GetCultureInfo(preferences.Current.UiLanguageTag);
        var valid = 0;
        foreach (var group in groups)
        {
            var display = group == "all" ? this["AllComponents"] : group;
            try
            {
                var routes = ReciprocalGraphBuilder.FromPublishedSecondaryKeys(
                    model, workspace.Keys, workspace.Configuration.SecondaryRules,
                    workspace.Resources.Model.DriverUnits, workspace.Resources.Precision, group);
                var factor = ReciprocalFactorization.Prepare(routes);
                // Graph feasibility is independent from financial values: zero RHS
                // is meaningful only after the graph has passed reachability.
                var report = factor.Solve(
                    routes.AuxiliaryIds.ToDictionary(id => id, _ => 0m, StringComparer.Ordinal),
                    routes.TerminalIds.ToDictionary(id => id, _ => 0m, StringComparer.Ordinal));
                var details = string.Format(Format, this["ReciprocalMetrics"],
                    report.MinNormalizedPivot, report.MaxCirculationAmplification);
                if (report.Warnings.Count != 0)
                    details += " · " + string.Join("; ", report.Warnings.Select(d =>
                        formatter.Format(d, ui, Format).Message));
                ReciprocalChecks.Add(new(display, this["ReciprocalViable"], details));
                valid++;
            }
            catch (DomainRuleException ex)
            {
                var adjusted = MakeReciprocalDiagnosticReadable(ex.Diagnostic);
                ReciprocalChecks.Add(new(display, this["ReciprocalBlocked"],
                    formatter.Format(adjusted, ui, Format).Message));
            }
            catch (Exception ex) when (ex is ArgumentException or OverflowException or InvalidOperationException)
            {
                ReciprocalChecks.Add(new(display, this["ReciprocalBlocked"], this["ReciprocalInvalid"]));
            }
        }
        ReciprocalSummary = string.Format(Format, this["ReciprocalSummary"], valid, groups.Length);
        Refresh();
    }

    private DomainDiagnostic MakeReciprocalDiagnosticReadable(DomainDiagnostic diagnostic)
    {
        var model = workspace?.Resources.Model.Structure;
        if (model is null) return diagnostic;
        var names = model.Nodes.ToDictionary(n => n.Id.ToString(), n => NodeLabel(n), StringComparer.Ordinal);
        var args = diagnostic.Arguments.Select(argument =>
        {
            if (argument.Name is not ("auxiliary" or "auxiliaries")) return argument;
            var human = string.Join(", ", argument.CanonicalValue.Split(',').Select(id =>
                names.TryGetValue(id, out var label) ? label : id));
            return argument with { CanonicalValue = human };
        }).ToArray();
        return new DomainDiagnostic(diagnostic.Code, diagnostic.MessageKey, args);
    }
}
