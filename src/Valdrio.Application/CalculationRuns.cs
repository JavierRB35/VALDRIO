using Valdrio.Calculation;
using Valdrio.Domain;

namespace Valdrio.Application;

public enum CalculationRunStatus { Started, Completed, Failed, Cancelled }
public sealed record RunTransfer(string Group, EntityId SourceNodeId, EntityId DestinationNodeId,
    EntityId KeyId, decimal Share, decimal Amount, bool Internal)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool MeasuredServices { get; init; }
}
public sealed record RunOutput(PrimaryAllocationResult? Primary, SecondaryCalculationPublication? Secondary,
    IReadOnlyList<RunTransfer> Transfers, IReadOnlyList<DomainDiagnostic> Diagnostics);
public sealed record CalculationRun(EntityId Id, StudySnapshotReceipt Snapshot, string AlgorithmVersion,
    CalculationRunStatus Status, UtcInstant StartedAtUtc, UtcInstant? CompletedAtUtc, RunOutput? Output)
{
    public const string Algorithm = "cost-flow-t12-v1";
    public string FullSnapshotHash => Snapshot.FullSnapshotHash;
    public string EconomicInputHash => PublishedModelStructure.Hash(CanonicalSnapshotJson.Serialize(new
    {
        Version = "run-economy-t12-v1", Snapshot.EconomicInputHash,
        SecondaryKeys = (Snapshot.Input as AllocationStudySnapshotInput)?.Allocations.Keys
            .Where(k => k.Stage == AllocationStage.Secondary && k.Status == ModelStatus.Published)
            .Select(k => new { k.Id, k.VersionNumber, k.Mode, k.UnitId, k.SelfAllocation, k.Lines }).ToArray(),
        Groups = (Snapshot.Input as AllocationStudySnapshotInput)?.Allocations.Configuration.SecondaryPartRoutingGroups
    }));
    public bool IsCurrentFor(long revision) => Status == CalculationRunStatus.Completed &&
        Snapshot.InputRevision == revision && AlgorithmVersion == Algorithm;
}
public sealed record RunHistory(long StudyRevision, IReadOnlyList<CalculationRun> Runs)
{
    public IReadOnlyList<EntityId> StalePhysicalRuns { get; init; } = [];
    public EntityId? CurrentRunId => Runs.LastOrDefault(r => r.IsCurrentFor(StudyRevision) && !StalePhysicalRuns.Contains(r.Id))?.Id;

    // A completed calculation for the identical, frozen revision/algorithm
    // is reusable; failed/cancelled/in-progress and historical runs are not.
    public CalculationRun? MatchingCompleted(long revision) =>
        StudyRevision == revision
            ? Runs.LastOrDefault(r => r.IsCurrentFor(revision) && !StalePhysicalRuns.Contains(r.Id))
            : null;
}
public interface IRunStore
{
    CalculationRun PrepareRun(CaptureStudySnapshot request, CancellationToken token = default);
    CalculationRun PublishRun(EntityId runId, RunOutput output, CancellationToken token = default);
    CalculationRun CancelRun(EntityId runId);
    RunHistory ReadRuns(EntityId companyId, EntityId studyId, CancellationToken token = default);
}

// The only calculation entry point for T12. Reading a run never calls this service.
public static class CalculationRunService
{
    public static RunOutput Calculate(CalculationRun run, CancellationToken token = default)
    {
        PrimaryAllocationResult? primary = null;
        try
        {
            token.ThrowIfCancellationRequested();
            if (run.Snapshot.Input is not AllocationStudySnapshotInput input)
                throw new DomainRuleException(new("RUN_INPUT_REQUIRED", "Run.InputRequired", []));
            primary = PrimaryAllocationService.Calculate(input);
            if (!primary.IsComplete)
                return new(primary, null, [], primary.Gaps.Select(g => g.Diagnostic)
                    .Append(new DomainDiagnostic("RUN_INCOMPLETE", "Run.Incomplete", [])).ToArray());
            var result = SecondaryAllocationService.Calculate(input, token);
            var transfers = new List<RunTransfer>();
            foreach (var group in result.GroupCirculations)
            {
                var w = input.Allocations;
                var graph = AuxiliaryCapacityService.Graph(w, group.ComponentRoutingGroup);
                for (var i = 0; i < graph.AuxiliaryIds.Length; i++)
                {
                    token.ThrowIfCancellationRequested();
                    var source = EntityId.Parse(graph.AuxiliaryIds[i]);
                    var gross = group.Solution.Gross[group.Solution.AuxiliaryIds.ToList().IndexOf(graph.AuxiliaryIds[i])];
                    var measured = w.Configuration.AuxiliaryCapacity?.Rows.Any(r => r.NodeId == source && r.Mode == AuxiliaryCapacityMode.OwnerOnceCapacity) == true;
                    var key = w.Configuration.SecondaryRules.Single(r => r.IsActive && r.AuxiliaryNodeId == source && r.ComponentRoutingGroup == group.ComponentRoutingGroup).KeyVersionId;
                    for (var j = 0; j < graph.AuxiliaryIds.Length; j++)
                        if (graph.Q[i][j] != 0) transfers.Add(new(group.ComponentRoutingGroup, source, EntityId.Parse(graph.AuxiliaryIds[j]), key, graph.Q[i][j], checked(gross * graph.Q[i][j]), true) { MeasuredServices = measured });
                    for (var j = 0; j < graph.TerminalIds.Length; j++)
                        if (graph.B[i][j] != 0) transfers.Add(new(group.ComponentRoutingGroup, source, EntityId.Parse(graph.TerminalIds[j]), key, graph.B[i][j], checked(gross * graph.B[i][j]), false) { MeasuredServices = measured });
                }
            }
            return new(primary, SecondaryAllocationService.Publish(result, input.Allocations.Configuration, run.Snapshot.InputRevision),
                transfers, result.GroupCirculations.SelectMany(g => g.Solution.Warnings).ToArray());
        }
        catch (DomainRuleException ex) { return new(primary, null, [], [ex.Diagnostic]); }
        catch (OverflowException) { return new(primary, null, [], [new("RUN_OVERFLOW", "Run.Overflow", [])]); }
    }
}
