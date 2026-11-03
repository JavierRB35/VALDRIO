using System.Collections.ObjectModel;
using Valdrio.Application;
namespace Valdrio.Desktop;
public sealed record AuxiliaryReportDisplay(string Center, string Policy, string Unit, string Normal, string Real,
    string Distributed, string Unassigned, string Idle, string Detail)
{ public string CenterSummary => Center + " · " + Unit + "\n" + Policy; }
public sealed partial class CostFlowViewModel
{
    public ObservableCollection<AuxiliaryReportDisplay> AuxiliaryReportRows { get; } = [];
    private bool showAuxiliaryReport;
    public bool ShowAuxiliaryReport { get => showAuxiliaryReport; set => SetProperty(ref showAuxiliaryReport, value); }
    private void RebuildAuxiliaryReport()
    {
        AuxiliaryReportRows.Clear();
        if (liveWorkspace is null || studies.Current is not { } study) return;
        var input = SelectedRun?.Run.Snapshot.Input as AllocationStudySnapshotInput;
        var workspace = input?.Allocations ?? liveWorkspace;
        var observations = company.ActivePhysicalActivity!.ReadCurrent(study.CompanyId, study.Id);
        string Q(decimal? value) => value?.ToString("0.############", Format) ?? "—";
        foreach (var row in AuxiliaryServiceReport.Read(workspace, observations))
        {
            var details = this["Aux." + row.Reason];
            if (row.Destinations.Count > 0)
                details += " " + string.Join("; ", row.Destinations.Select(d =>
                    workspace.Resources.Model.Structure.Nodes.Single(n => n.Id == d.DestinationNodeId).Code + ": " + Q(d.Quantity) + " " + row.Unit));
            AuxiliaryReportRows.Add(new(row.Center, texts.Get("Aux." + row.Policy, studies.PhysicalUiCulture), row.Unit,
                Q(row.Normal), Q(row.Real), Q(row.Distributed), Q(row.Unassigned), Q(row.Idle), details));
        }
    }
}
