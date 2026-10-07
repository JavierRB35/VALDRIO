using System.Text.Json.Serialization;
using Valdrio.Domain;

namespace Valdrio.Application;

public sealed record CreateStudy(EntityId CompanyId, EntityId Id, string Name, BusinessDate PeriodStart,
    BusinessDate PeriodEnd, StudyPurpose Purpose, StudyScenario ScenarioKind,
    EntityId SelectedModelVersionId, CurrencyCode FunctionalCurrency, string Actor);
public sealed record UpdateStudyContext(EntityId CompanyId, EntityId Id, long ExpectedRevision, string Name,
    BusinessDate PeriodStart, BusinessDate PeriodEnd, StudyPurpose Purpose, StudyScenario ScenarioKind,
    EntityId SelectedModelVersionId, CurrencyCode FunctionalCurrency, string Actor);
public sealed record ChangeStudyState(EntityId CompanyId, EntityId Id, long ExpectedRevision, StudyState State, string Actor);
public sealed record CaptureStudySnapshot(EntityId CompanyId, EntityId StudyId, long ExpectedRevision, EntityId RequestId, string Actor);

public interface IStudyWorkspace
{
    IReadOnlyList<StudyDefinition> ReadStudies(CancellationToken token = default);
    StudyDefinition Create(CreateStudy command, CancellationToken token = default);
    StudyDefinition UpdateContext(UpdateStudyContext command, CancellationToken token = default);
    StudyDefinition ChangeState(ChangeStudyState command, CancellationToken token = default);
    StudySnapshotReceipt Capture(CaptureStudySnapshot command, CancellationToken token = default);
    IReadOnlyList<StudySnapshotReceipt> ReadSnapshots(EntityId companyId, EntityId studyId, CancellationToken token = default);
}

// Only empty inputs are supported by context-only-t05-v1. Economic modules need a new schema.
public sealed record ResourceSnapshotInput(EntityId Id, Money Original, Money Functional, FxRateApplication? Conversion);
public sealed record ActivitySnapshotInput(EntityId PoolId, Quantity Quantity);
public sealed record ConsumptionSnapshotInput(EntityId Id, EntityId PoolId, Quantity Quantity);
public sealed record StudySnapshotProvenance(EntityId SnapshotId, EntityId RequestId, UtcInstant CapturedAtUtc, string Actor);

public interface IStudySnapshotInput
{
    string SnapshotSchemaVersion { get; }
    string InputPreparationVersion { get; }
    StudyDefinition StudyContext { get; }
    PrecisionPolicy PrecisionPolicy { get; }
    PublishedModelStructure Model { get; }
    StudySnapshotProvenance Provenance { get; }
    IReadOnlyList<ResourceSnapshotInput> Resources { get; }
    IReadOnlyList<FxRateApplication> AppliedFx { get; }
    IReadOnlyList<ActivitySnapshotInput> Activity { get; }
    IReadOnlyList<ConsumptionSnapshotInput> Consumptions { get; }
}
public sealed class StudySnapshotInput : IStudySnapshotInput
{
    public const string Schema = "valdrio-study-input-v1";
    public const string PreparationVersion = "context-only-t05-v1";
    public StudySnapshotInput(string snapshotSchemaVersion, string inputPreparationVersion, StudyDefinition studyContext,
        PrecisionPolicy precisionPolicy, PublishedModelStructure model, StudySnapshotProvenance provenance,
        IReadOnlyList<ResourceSnapshotInput> resources, IReadOnlyList<FxRateApplication> appliedFx,
        IReadOnlyList<ActivitySnapshotInput> activity, IReadOnlyList<ConsumptionSnapshotInput> consumptions)
    {
        if (snapshotSchemaVersion != Schema || inputPreparationVersion != PreparationVersion)
            throw StudyRules.Error("STUDY_SNAPSHOT_SCHEMA_UNSUPPORTED", "Study.SnapshotSchemaUnsupported");
        ArgumentNullException.ThrowIfNull(studyContext); ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(precisionPolicy); precisionPolicy.EnsureValid();
        ArgumentNullException.ThrowIfNull(provenance); provenance.SnapshotId.EnsureValid(); provenance.RequestId.EnsureValid();
        if (string.IsNullOrWhiteSpace(provenance.Actor) || provenance.SnapshotId != provenance.RequestId)
            throw StudyRules.Error("STUDY_SNAPSHOT_INVALID", "Study.SnapshotInvalid");
        if (model.Structure.CompanyId != studyContext.CompanyId || model.Structure.Id != studyContext.SelectedModelVersionId ||
            model.Structure.PrecisionPolicyVersion != precisionPolicy.Version)
            throw StudyRules.Error("STUDY_SNAPSHOT_INVALID", "Study.SnapshotInvalid", studyContext.Id);
        if (ModelRules.ValidateStructure(model.Structure, model.PhysicalCenters,
            model.DriverUnits.Select(u => new DriverUnitState(u, true, 0)).ToArray()).Count != 0)
            throw StudyRules.Error("STUDY_SNAPSHOT_INVALID", "Study.SnapshotInvalid", studyContext.Id);
        if (resources is null || appliedFx is null || activity is null || consumptions is null ||
            resources.Count != 0 || appliedFx.Count != 0 || activity.Count != 0 || consumptions.Count != 0)
            throw StudyRules.Error("STUDY_MODULE_NOT_AVAILABLE", "Study.ModuleNotAvailable", studyContext.Id);
        SnapshotSchemaVersion = snapshotSchemaVersion; InputPreparationVersion = inputPreparationVersion;
        StudyContext = studyContext; PrecisionPolicy = precisionPolicy; Model = model; Provenance = provenance;
        Resources = FrozenList.Copy(resources); AppliedFx = FrozenList.Copy(appliedFx);
        Activity = FrozenList.Copy(activity); Consumptions = FrozenList.Copy(consumptions);
    }
    public string SnapshotSchemaVersion { get; }
    public string InputPreparationVersion { get; }
    public StudyDefinition StudyContext { get; }
    public PrecisionPolicy PrecisionPolicy { get; }
    public PublishedModelStructure Model { get; }
    public StudySnapshotProvenance Provenance { get; }
    public IReadOnlyList<ResourceSnapshotInput> Resources { get; }
    public IReadOnlyList<FxRateApplication> AppliedFx { get; }
    public IReadOnlyList<ActivitySnapshotInput> Activity { get; }
    public IReadOnlyList<ConsumptionSnapshotInput> Consumptions { get; }
}

public sealed record StudySnapshotReceipt(EntityId Id, EntityId CompanyId, EntityId StudyId, long InputRevision,
    UtcInstant CapturedAtUtc, string PayloadSchema, string EconomicInputHash, string FullSnapshotHash, string CanonicalJson)
{
    [JsonIgnore] public IStudySnapshotInput Input => StudySnapshotJson.Read(CanonicalJson);
    public void EnsureValid()
    {
        var input = Input;
        if (PayloadSchema != input.SnapshotSchemaVersion || Id != input.Provenance.SnapshotId || CompanyId != input.StudyContext.CompanyId ||
            StudyId != input.StudyContext.Id || InputRevision != input.StudyContext.Revision || CapturedAtUtc != input.Provenance.CapturedAtUtc ||
            CanonicalJson != StudySnapshotJson.Serialize(input) || EconomicInputHash != StudySnapshotJson.EconomicHash(input) ||
            FullSnapshotHash != PublishedModelStructure.Hash(CanonicalJson))
            throw StudyRules.Error("STUDY_SNAPSHOT_INVALID", "Study.SnapshotInvalid", Id);
    }
    public bool IsCurrentFor(StudyDefinition study) => StudyId == study.Id && CompanyId == study.CompanyId && InputRevision == study.Revision &&
        CanonicalSnapshotJson.Serialize(Input.StudyContext) == CanonicalSnapshotJson.Serialize(study);
}

public sealed record StudyPreparation(bool ReadyForCalculation, IReadOnlyList<DomainDiagnostic> Diagnostics);
public static class StudyPreparationService
{
    public static StudyPreparation Evaluate(IStudySnapshotInput input)
    {
        var id = input.StudyContext.Id;
        var diagnostics = new List<DomainDiagnostic>();
        void Add(string code, string key) => diagnostics.Add(StudyRules.Error(code, key, id).Diagnostic);
        if (input.Model.Structure.MethodPolicy.Resolution == PolicyResolution.Pending ||
            input.Model.Structure.Pools.Any(p => p.CapacityPolicy.Resolution == PolicyResolution.Pending))
            Add("STUDY_POLICY_PENDING", "Study.PolicyPending");
        if (input.Resources.Count == 0) Add("STUDY_RESOURCES_PENDING", "Study.ResourcesPending");
        else if (input is ResourceStudySnapshotInput prepared)
        {
            diagnostics.AddRange(prepared.Workspace.Preparation.Diagnostics);
            if (prepared.Workspace.Preparation.HasPendingParts) Add("STUDY_PARTITIONS_PENDING", "Study.PartitionsPending");
            diagnostics.AddRange(prepared.Workspace.Preparation.Warnings);
        }
        else Add("STUDY_PARTITIONS_PENDING", "Study.PartitionsPending");
        if (input.Activity.Count == 0) Add("STUDY_ACTIVITY_PENDING", "Study.ActivityPending");
        if (input.Consumptions.Count == 0) Add("STUDY_CONSUMPTION_PENDING", "Study.ConsumptionPending");
        if (input.StudyContext.State == StudyState.Closed) Add("STUDY_CLOSED", "Study.ClosedDiagnostic");
        return new(diagnostics.Count == 0, FrozenList.Copy(diagnostics));
    }
}
