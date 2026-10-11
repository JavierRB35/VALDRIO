using System.Collections.ObjectModel;
using Valdrio.Domain;
using Valdrio.Localization;

namespace Valdrio.Desktop;

public sealed record ReciprocalCheckRow(string Group, string State, string Detail) { public string TechnicalDetail { get; init; } = ""; public bool HasError { get; init; } }

// Feasibility is projected from the calculation or its frozen publication.
// Never factorize the graph again on the UI thread.
public sealed partial class AllocationWorkspaceViewModel
{
    public ObservableCollection<ReciprocalCheckRow> ReciprocalChecks { get; } = [];
    public string ReciprocalSummary { get; private set; } = "";
    public bool ReciprocalHasError => ReciprocalChecks.Any(r => r.HasError);

    private void ResetReciprocal()
    {
        ReciprocalChecks.Clear();
        ReciprocalSummary = this[workspace is null ? "ReciprocalNeedStudy" : "ReciprocalNotChecked"];
    }

    private void PresentReciprocal(IReadOnlyList<Valdrio.Application.SecondaryGroupCirculation> groups)
    {
        ReciprocalChecks.Clear();
        var formatter = new DomainDiagnosticFormatter(texts);
        var ui = System.Globalization.CultureInfo.GetCultureInfo(preferences.Current.UiLanguageTag);
        foreach (var group in groups)
        {
            var report = group.Solution;
            var details = this["ReciprocalViableMeaning"];
            if (report.Warnings.Count != 0)
                details += " · " + string.Join("; ", report.Warnings.Select(d => formatter.Format(d, ui, Format).Message));
            ReciprocalChecks.Add(new(group.ComponentRoutingGroup == "all" ? this["AllComponents"] : group.ComponentRoutingGroup,
                this["ReciprocalViable"], details)
            {
                TechnicalDetail = string.Format(Format, this["ReciprocalMetrics"], report.MinNormalizedPivot, report.MaxCirculationAmplification)
            });
        }
        ReciprocalSummary = groups.Count == 0 ? this["ReciprocalNoAuxiliaries"] :
            string.Format(Format, this["ReciprocalSummary"], groups.Count, groups.Count);
    }

    private void PresentReciprocalFailure(string message)
    {
        ReciprocalChecks.Clear();
        ReciprocalSummary = this["ReciprocalCalculationFailed"];
        ReciprocalChecks.Add(new(this["SecondaryTitle"], this["ReciprocalCalculationFailed"], message) { HasError = true });
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
