using Valdrio.Domain;
namespace Valdrio.Application;

public sealed record AuxiliaryServiceReportRow(EntityId NodeId, string Center, string Policy, string Unit,
    decimal? Normal, decimal? Real, decimal? Distributed, decimal? Unassigned, decimal? Idle,
    string Reason, IReadOnlyList<AuxiliaryServiceQuantity> Destinations);

// Read-only physical projection. Multiple economic groups must not multiply physical service.
public static class AuxiliaryServiceReport
{
    public static IReadOnlyList<AuxiliaryServiceReportRow> Read(AllocationWorkspaceSnapshot workspace,
        IReadOnlyList<PhysicalObservation> observations)
    {
        var model = workspace.Resources.Model;
        var study = workspace.Resources.Study;
        var rows = new List<AuxiliaryServiceReportRow>();
        foreach (var node in model.Structure.Nodes.Where(n => n.IsActive && n.Role == NodeRole.Auxiliary))
        {
            var pool = model.Structure.Pools.Single(p => p.NodeId == node.Id);
            var unit = model.DriverUnits.Single(u => u.Id == pool.DriverUnitId);
            var observation = observations.SingleOrDefault(o => o.PoolId == pool.Id && o.CompanyId == study.CompanyId && o.StudyId == study.Id &&
                o.ModelVersionId == model.Structure.Id && o.PeriodStart == study.PeriodStart && o.PeriodEnd == study.PeriodEnd &&
                o.UnitId == pool.DriverUnitId && o.State == PhysicalObservationState.Confirmed);
            var saved = workspace.Configuration.AuxiliaryCapacity?.Rows.SingleOrDefault(r => r.NodeId == node.Id);
            var sets = new List<IReadOnlyList<AuxiliaryServiceQuantity>>();
            var reason = observation?.RealQuantity is null ? "MissingActivity" : "UnknownBasis";
            if (saved?.Mode == AuxiliaryCapacityMode.OwnerOnceCapacity)
            {
                // Frozen measured service is economic evidence; never substitute current hours.
                observation = saved.Observation;
                sets.AddRange(saved.ServiceSets.Select(s => s.Services));
            }
            else if (observation?.RealQuantity is { } real)
            {
                foreach (var rule in workspace.Configuration.SecondaryRules.Where(r => r.IsActive && r.AuxiliaryNodeId == node.Id))
                {
                    var key = workspace.Keys.Single(k => k.Id == rule.KeyVersionId);
                    if (key.Mode != AllocationKeyMode.DriverUnits || key.UnitId != pool.DriverUnitId)
                    { sets.Clear(); break; }
                    var total = key.Lines.Sum(l => l.Value);
                    if (total <= 0) { sets.Clear(); break; }
                    var lines = key.Lines.Where(l => l.Value > 0).OrderBy(l => l.DestinationNodeId).ToArray();
                    decimal remainder = real;
                    sets.Add(lines.Select((l, index) =>
                    {
                        var quantity = index == lines.Length - 1 ? remainder : real * (l.Value / total);
                        remainder -= quantity;
                        return new AuxiliaryServiceQuantity(l.DestinationNodeId, quantity);
                    }).ToArray());
                }
            }
            IReadOnlyList<AuxiliaryServiceQuantity> destinations = [];
            if (sets.Count > 0)
            {
                string Signature(IReadOnlyList<AuxiliaryServiceQuantity> set) => CanonicalSnapshotJson.Serialize(set.Where(s => s.Quantity != 0).OrderBy(s => s.DestinationNodeId).ToArray());
                if (sets.Select(Signature).Distinct().Count() == 1) { destinations = sets[0]; reason = "Derived"; }
                else reason = "MultipleGroups";
            }
            decimal? distributed = reason == "Derived" ? destinations.Sum(s => s.Quantity) : null;
            var normal = observation?.NormalQuantity; var actual = observation?.RealQuantity;
            rows.Add(new(node.Id, node.Code + " · " + model.PhysicalCenters.Single(c => c.Id == node.PhysicalCenterId).Name,
                (saved?.Mode ?? AuxiliaryCapacityMode.ThroughFlow).ToString(), unit.Symbol, normal, actual, distributed,
                distributed.HasValue && actual.HasValue ? actual - distributed : null,
                normal.HasValue && actual.HasValue ? Math.Max(0, normal.Value - actual.Value) : null, reason, destinations));
        }
        return rows;
    }
}
