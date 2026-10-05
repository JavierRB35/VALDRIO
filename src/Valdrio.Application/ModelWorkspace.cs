using System.Security.Cryptography;
using System.Text;
using Valdrio.Domain;

namespace Valdrio.Application;

public sealed class PublishedModelStructure
{
    public PublishedModelStructure(ModelStructure structure, IReadOnlyList<PhysicalCenter> physicalCenters,
        IReadOnlyList<UnitDefinition> driverUnits)
    {
        ArgumentNullException.ThrowIfNull(structure);
        Structure = structure; PhysicalCenters = FrozenList.Copy(physicalCenters.OrderBy(x => x.Id));
        DriverUnits = FrozenList.Copy(driverUnits.OrderBy(x => x.Id));
        foreach (var center in PhysicalCenters) center.EnsureValid();
        if (PhysicalCenters.Select(x => x.Id).Distinct().Count() != PhysicalCenters.Count
            || DriverUnits.Select(x => x.Id).Distinct().Count() != DriverUnits.Count
            || structure.Nodes.Any(x => !PhysicalCenters.Any(c => c.Id == x.PhysicalCenterId && c.CompanyId == structure.CompanyId))
            || structure.Pools.Any(x => !DriverUnits.Any(u => u.Id == x.DriverUnitId)))
            throw ModelRules.Error("MODEL_SNAPSHOT_INVALID", "Model.SnapshotInvalid", structure.Id);
    }
    public ModelStructure Structure { get; }
    public IReadOnlyList<PhysicalCenter> PhysicalCenters { get; }
    public IReadOnlyList<UnitDefinition> DriverUnits { get; }
    public const string PayloadSchema = "valdrio-model-structure-v1";
    public static string Hash(string canonicalJson) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson))).ToLowerInvariant();
}

public sealed record ModelVersionSnapshot(ModelStructure Structure, ModelStatus Status, long Revision,
    UtcInstant? PublishedAtUtc, string? StructuralHash, string? PublishedCanonicalJson)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public PublishedModelStructure? Publication => PublishedCanonicalJson is null ? null
        : CanonicalSnapshotJson.Deserialize<PublishedModelStructure>(PublishedCanonicalJson);
}
public sealed record CreatePhysicalCenter(EntityId CompanyId, string Code, string Name, string Actor);
public sealed record ChangePhysicalCenter(EntityId CompanyId, EntityId Id, long ExpectedRevision,
    string Code, string Name, bool IsActive, string Actor);
public sealed record ChangeDriverUnitState(EntityId CompanyId, UnitId Id, long ExpectedRevision, bool IsActive, string Actor);
public sealed record CreateModelDraft(EntityId CompanyId, string Description, string Actor);
public sealed record SaveModelDraft(ModelStructure Structure, long ExpectedRevision, string Actor);
public sealed record PublishModel(EntityId CompanyId, EntityId Id, long ExpectedRevision, string Actor);
public sealed record CloneModel(EntityId CompanyId, EntityId Id, long ExpectedRevision, EntityId RequestId, string Description, string Actor);

// The workspace shares the lifetime and serialization gate of its company file session.
public interface IModelWorkspace
{
    IReadOnlyList<PhysicalCenter> ReadCenters(CancellationToken token = default);
    IReadOnlyList<DriverUnitState> ReadDriverUnits(CancellationToken token = default);
    IReadOnlyList<ModelVersionSnapshot> ReadModels(CancellationToken token = default);
    PhysicalCenter CreateCenter(CreatePhysicalCenter command, CancellationToken token = default);
    PhysicalCenter ChangeCenter(ChangePhysicalCenter command, CancellationToken token = default);
    DriverUnitState ChangeUnitState(ChangeDriverUnitState command, CancellationToken token = default);
    ModelVersionSnapshot CreateDraft(CreateModelDraft command, CancellationToken token = default);
    ModelVersionSnapshot SaveDraft(SaveModelDraft command, CancellationToken token = default);
    IReadOnlyList<DomainDiagnostic> ValidateModel(EntityId companyId, EntityId modelId, CancellationToken token = default);
    ModelVersionSnapshot Publish(PublishModel command, CancellationToken token = default);
    ModelVersionSnapshot Clone(CloneModel command, CancellationToken token = default);
}
