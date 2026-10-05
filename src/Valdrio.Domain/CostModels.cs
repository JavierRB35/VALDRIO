namespace Valdrio.Domain;

public enum ModelStatus { Draft, Published }
public enum NodeRole { Principal, Auxiliary }
public enum CostLayer { Unclassified, Fixed, Variable, Mixed }
public enum PolicyResolution { Pending, Accepted }

// A structural publication never resolves an economic decision.
public sealed record ModelPolicyReference(string Id, string Version, PolicyResolution Resolution)
{
    public static ModelPolicyReference PendingMethod => new("method-unresolved", "proposal", PolicyResolution.Pending);
    public static ModelPolicyReference PendingCapacity => new("capacity-unresolved", "proposal", PolicyResolution.Pending);
    public void EnsureValid()
    {
        if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Version) || !Enum.IsDefined(Resolution))
            throw ModelRules.Error("MODEL_POLICY_INVALID", "Model.PolicyInvalid");
        // Accepting economic policies is a later, separately approved use case.
        if (Resolution != PolicyResolution.Pending)
            throw ModelRules.Error("MODEL_POLICY_NOT_IMPLEMENTED", "Model.PolicyNotImplemented");
    }
}

public sealed record PhysicalCenter(EntityId Id, EntityId CompanyId, string Code, string Name, bool IsActive, long Revision)
{
    public void EnsureValid()
    {
        Id.EnsureValid(); CompanyId.EnsureValid();
        if (string.IsNullOrWhiteSpace(Code) || string.IsNullOrWhiteSpace(Name) || Revision < 0)
            throw ModelRules.Error("MODEL_CENTER_INVALID", "Model.CenterInvalid", Id);
    }
}
public sealed record DriverUnitState(UnitDefinition Definition, bool IsActive, long Revision);

public sealed record CalculationNodeDefinition
{
    public CalculationNodeDefinition(EntityId id, EntityId modelVersionId, EntityId physicalCenterId,
        string code, NodeRole role, IReadOnlyList<string> functionTags, bool isActive)
    {
        id.EnsureValid(); modelVersionId.EnsureValid(); physicalCenterId.EnsureValid();
        if (string.IsNullOrWhiteSpace(code) || !Enum.IsDefined(role) || functionTags is null
            || functionTags.Any(string.IsNullOrWhiteSpace) || functionTags.Distinct(StringComparer.Ordinal).Count() != functionTags.Count)
            throw ModelRules.Error("MODEL_NODE_INVALID", "Model.NodeInvalid", id);
        Id = id; ModelVersionId = modelVersionId; PhysicalCenterId = physicalCenterId;
        Code = code; Role = role; FunctionTags = FrozenList.Copy(functionTags.Order(StringComparer.Ordinal)); IsActive = isActive;
    }
    public EntityId Id { get; }
    public EntityId ModelVersionId { get; }
    public EntityId PhysicalCenterId { get; }
    public string Code { get; }
    public NodeRole Role { get; }
    public IReadOnlyList<string> FunctionTags { get; }
    public bool IsActive { get; }
}
public sealed record CostPoolDefinition(EntityId Id, EntityId ModelVersionId, EntityId NodeId,
    UnitId DriverUnitId, string Description, CostLayer CostLayer, ModelPolicyReference CapacityPolicy)
{
    public void EnsureValid()
    {
        Id.EnsureValid(); ModelVersionId.EnsureValid(); NodeId.EnsureValid(); DriverUnitId.EnsureValid();
        if (Description is null || !Enum.IsDefined(CostLayer) || CapacityPolicy is null)
            throw ModelRules.Error("MODEL_POOL_INVALID", "Model.PoolInvalid", Id);
        CapacityPolicy.EnsureValid();
    }
}

// Collections are sorted by stable IDs and copied before crossing application boundaries.
public sealed class ModelStructure
{
    public ModelStructure(EntityId id, EntityId companyId, EntityId familyId, int versionNumber,
        EntityId? parentVersionId, string description, string precisionPolicyVersion,
        ModelPolicyReference methodPolicy, IReadOnlyList<CalculationNodeDefinition> nodes,
        IReadOnlyList<CostPoolDefinition> pools)
    {
        id.EnsureValid(); companyId.EnsureValid(); familyId.EnsureValid(); parentVersionId?.EnsureValid();
        if (versionNumber < 1 || string.IsNullOrWhiteSpace(description) || string.IsNullOrWhiteSpace(precisionPolicyVersion)
            || methodPolicy is null || nodes is null || pools is null || nodes.Any(x => x is null) || pools.Any(x => x is null))
            throw ModelRules.Error("MODEL_STRUCTURE_INVALID", "Model.StructureInvalid", id);
        methodPolicy.EnsureValid();
        Id = id; CompanyId = companyId; FamilyId = familyId; VersionNumber = versionNumber;
        ParentVersionId = parentVersionId; Description = description; PrecisionPolicyVersion = precisionPolicyVersion;
        MethodPolicy = methodPolicy; Nodes = FrozenList.Copy(nodes.OrderBy(x => x.Id)); Pools = FrozenList.Copy(pools.OrderBy(x => x.Id));
        EnsureScope();
    }
    public EntityId Id { get; }
    public EntityId CompanyId { get; }
    public EntityId FamilyId { get; }
    public int VersionNumber { get; }
    public EntityId? ParentVersionId { get; }
    public string Description { get; }
    public string PrecisionPolicyVersion { get; }
    public ModelPolicyReference MethodPolicy { get; }
    public IReadOnlyList<CalculationNodeDefinition> Nodes { get; }
    public IReadOnlyList<CostPoolDefinition> Pools { get; }
    public void EnsureScope()
    {
        if (Nodes.Any(x => x.ModelVersionId != Id) || Pools.Any(x => x.ModelVersionId != Id || !Nodes.Any(n => n.Id == x.NodeId)))
            throw ModelRules.Error("MODEL_SCOPE_INVALID", "Model.ScopeInvalid", Id);
        if (Nodes.Select(x => x.Id).Distinct().Count() != Nodes.Count || Pools.Select(x => x.Id).Distinct().Count() != Pools.Count
            || Nodes.Select(x => x.Code).Distinct(StringComparer.Ordinal).Count() != Nodes.Count
            || Pools.Select(x => x.NodeId).Distinct().Count() != Pools.Count)
            throw ModelRules.Error("MODEL_DUPLICATE", "Model.Duplicate", Id);
        foreach (var pool in Pools) pool.EnsureValid();
    }
}

public static class ModelRules
{
    public static DomainRuleException Error(string code, string key, EntityId? entity = null) => new(new(code, key,
        entity.HasValue ? [new("entityId", DiagnosticArgumentType.Identifier, entity.Value.ToString())] : []));

    public static IReadOnlyList<DomainDiagnostic> ValidateStructure(ModelStructure model,
        IReadOnlyList<PhysicalCenter> centers, IReadOnlyList<DriverUnitState> units)
    {
        model.EnsureScope();
        var issues = new List<DomainDiagnostic>();
        void Add(string code, string key, EntityId id) => issues.Add(Error(code, key, id).Diagnostic);
        if (!model.Nodes.Any(x => x.IsActive && x.Role == NodeRole.Principal))
            Add("MODEL_PRINCIPAL_REQUIRED", "Model.PrincipalRequired", model.Id);
        foreach (var node in model.Nodes)
        {
            var center = centers.SingleOrDefault(x => x.Id == node.PhysicalCenterId && x.CompanyId == model.CompanyId);
            if (center is null || (node.IsActive && !center.IsActive)) Add("MODEL_CENTER_UNAVAILABLE", "Model.CenterUnavailable", node.Id);
            var pool = model.Pools.SingleOrDefault(x => x.NodeId == node.Id);
            if (node.IsActive && pool is null) Add("MODEL_POOL_REQUIRED", "Model.PoolRequired", node.Id);
            if (pool is not null && !units.Any(x => x.Definition.Id == pool.DriverUnitId && (!node.IsActive || x.IsActive)))
                Add("MODEL_DRIVER_UNAVAILABLE", "Model.DriverUnavailable", pool.Id);
        }
        return FrozenList.Copy(issues);
    }
}
