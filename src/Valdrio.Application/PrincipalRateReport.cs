using Valdrio.Calculation;
using Valdrio.Domain;

namespace Valdrio.Application;

public sealed record PrincipalRateRow(EntityId NodeId, EntityId PoolId, string Name, UnitId UnitId,
    PhysicalObservation? Observation, PrincipalRateResult? Result, IReadOnlyList<string> Diagnostics, decimal? Cost);
public sealed record PrincipalRateReport(StudyDefinition Study, EntityId? EconomicRunId, string? EconomicHash,
    string Algorithm, string Version, IReadOnlyList<PrincipalRateRow> Rows);

// Read-only projection of immutable T12 and T14 versions. Never recalculates the upstream ledger.
public static class PrincipalRateReportService
{
    // A downstream estimate can freeze this reference and restore exactly those versions,
    // even after new hours or a new economic revision. Never substitute the latest inputs.
    public static PrincipalRateReport RestoreVersion(PrincipalRateReport reference, IRunStore runs, IPhysicalActivityWorkspace activity)
    {
        var run = runs.ReadRuns(reference.Study.CompanyId, reference.Study.Id).Runs.SingleOrDefault(r => r.Id == reference.EconomicRunId);
        if (run is null || run.Status != CalculationRunStatus.Completed || reference.Algorithm != PrincipalRateCalculator.Version ||
            run.Snapshot.Input is not AllocationStudySnapshotInput input)
            throw new InvalidOperationException("Rate reference is not reproducible");
        var physical = reference.Rows.Where(r => r.Observation is not null).Select(r =>
            activity.ReadHistory(reference.Study.CompanyId, reference.Study.Id, r.PoolId)
                .Single(o => o.Id == r.Observation!.Id && o.Version == r.Observation.Version)).ToArray();
        var restored = Read(reference.Study, input.Model, new(reference.Study.Revision, [run]), physical);
        if (CanonicalSnapshotJson.Serialize(restored) != CanonicalSnapshotJson.Serialize(reference))
            throw new InvalidOperationException("Rate reference differs from its saved sources");
        return restored;
    }
    public static PrincipalRateReport Read(StudyDefinition study, PublishedModelStructure model, RunHistory? history,
        IReadOnlyList<PhysicalObservation> observations)
    {
        var run = history?.MatchingCompleted(study.Revision);
        var input = run?.Snapshot.Input as AllocationStudySnapshotInput;
        if (run?.Snapshot.StudyId != study.Id || run.Snapshot.CompanyId != study.CompanyId ||
            input?.Model.Structure.Id != study.SelectedModelVersionId) { run = null; input = null; }
        var publication = run?.Output?.Secondary;
        var structure = input?.Allocations.Resources.Model.Structure ?? model.Structure;
        var rows = new List<PrincipalRateRow>();
        foreach (var node in structure.Nodes.Where(n => n.IsActive && n.Role == NodeRole.Principal).OrderBy(n => n.Code))
        {
            foreach (var pool in structure.Pools.Where(p => p.NodeId == node.Id))
            {
                var observation = observations.Where(o => o.CompanyId == study.CompanyId && o.StudyId == study.Id &&
                    o.ModelVersionId == study.SelectedModelVersionId && o.NodeId == node.Id && o.PoolId == pool.Id &&
                    o.UnitId == pool.DriverUnitId && o.PeriodStart == study.PeriodStart && o.PeriodEnd == study.PeriodEnd)
                    .OrderByDescending(o => o.Version).FirstOrDefault();
                var errors = new List<string>();
                if (publication is null) errors.Add("Rates.MissingCosts");
                if (observation?.State != PhysicalObservationState.Confirmed) errors.Add("Rates.UnconfirmedActivity");
                if (structure.Pools.Count(p => p.NodeId == node.Id) != 1) errors.Add("Rates.MultiplePools");
                var sources = new List<RateSource>();
                if (publication is not null && input is not null)
                {
                    var parts = input.Allocations.Resources.Resources.SelectMany(r => r.Parts).ToDictionary(p => p.Id);
                    foreach (var fact in publication.Facts.Where(f => f.TerminalNodeId == node.Id))
                    {
                        var classification = parts.GetValueOrDefault(fact.PartId)?.Classification;
                        if (classification is null || fact.PostedAmount.Currency != study.Currency.FunctionalCurrencyCode)
                        { errors.Add("Rates.SourceMismatch"); continue; }
                        var kind = RateComponentKind.Consumed;
                        if (fact.CapacityTreatment == ResourceCapacityTreatment.PeriodCost) kind = RateComponentKind.Period;
                        else if (fact.Behavior is ResourceBehavior.Unclassified or ResourceBehavior.MixedPending or ResourceBehavior.Mixed)
                        { errors.Add("Rates.ClassificationRequired"); continue; }
                        else if (fact.Behavior == ResourceBehavior.Variable) kind = RateComponentKind.Variable;
                        else if (fact.CapacityTreatment == ResourceCapacityTreatment.AssessAtPool)
                        {
                            // T11 transfers do not certify an auxiliary capacity assessment. Never silently assess it again.
                            if (classification.CapacityOwner?.NodeId != node.Id || classification.CapacityOwner.PoolId != pool.Id ||
                                classification.CapacityOwner.UnitId != pool.DriverUnitId)
                            { errors.Add("Rates.CapacityOriginRequired"); continue; }
                            kind = RateComponentKind.OwnFixed;
                        }
                        sources.Add(new(fact.PartId + ":" + fact.Behavior + ":" + fact.CostLayer + ":" + fact.CapacityTreatment,
                            fact.Behavior, kind, fact.PostedAmount.Amount, fact.Account));
                    }
                    if (publication.Totals.Where(t => t.TerminalNodeId == node.Id).Sum(t => t.PostedAmount.Amount) != sources.Sum(s => s.Amount) && errors.Count == 0)
                        errors.Add("Rates.SourceMismatch");
                }
                PrincipalRateResult? result = null;
                if (errors.Count == 0)
                {
                    try { result = PrincipalRateCalculator.Calculate(sources, observation!.NormalQuantity, observation.RealQuantity, precision: input!.PrecisionPolicy); }
                    catch (OverflowException) { errors.Add("Rates.Overflow"); }
                    catch (DomainRuleException) { errors.Add("Rates.SourceMismatch"); }
                    if (result is not null) errors.AddRange(result.Diagnostics);
                }
                var name = model.PhysicalCenters.FirstOrDefault(c => c.Id == node.PhysicalCenterId)?.Name ?? node.Code;
                rows.Add(new(node.Id, pool.Id, name + " · " + node.Code, pool.DriverUnitId, observation, result, errors.Distinct().ToArray(),
                    publication?.Totals.Where(t => t.TerminalNodeId == node.Id).Sum(t => t.PostedAmount.Amount)));
            }
        }
        var version = PublishedModelStructure.Hash(CanonicalSnapshotJson.Serialize(new { Algorithm = PrincipalRateCalculator.Version,
            study.CompanyId, Study = study.Id, study.Revision, study.SelectedModelVersionId, study.PeriodStart, study.PeriodEnd,
            Run = run?.Id, Rows = rows.Select(r => new { r.NodeId, r.PoolId, Physical = r.Observation?.Id, r.Observation?.Version }).ToArray() }));
        return new(study, run?.Id, run?.EconomicInputHash, PrincipalRateCalculator.Version, version, FrozenList.Copy(rows));
    }
}
