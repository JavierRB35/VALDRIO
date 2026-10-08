namespace Valdrio.Domain;

public enum AllocationStage { Primary, Secondary }
public enum AllocationKeyMode { Percentage, DriverUnits, EqualShares }
public enum SelfAllocationPolicy { Forbidden, Allowed }
public enum AccountSelectorKind { Exact, Prefix }
public sealed record AllocationKeyLine(EntityId DestinationNodeId, decimal Value);

// A version is an immutable value. Saving a draft replaces the aggregate under a revision check;
// editing a publication creates a new version instead of changing this value.
public sealed class AllocationKeyVersion
{
    public AllocationKeyVersion(EntityId id, EntityId companyId, EntityId modelVersionId, EntityId familyId,
        int versionNumber, EntityId? parentVersionId, long revision, string name, AllocationStage stage,
        AllocationKeyMode mode, UnitId? unitId, SelfAllocationPolicy selfAllocation,
        ModelStatus status, UtcInstant? publishedAtUtc, IReadOnlyList<AllocationKeyLine> lines)
    {
        id.EnsureValid(); companyId.EnsureValid(); modelVersionId.EnsureValid(); familyId.EnsureValid();
        parentVersionId?.EnsureValid(); unitId?.EnsureValid();
        if (versionNumber < 1 || revision < 0 || string.IsNullOrWhiteSpace(name) || !Enum.IsDefined(stage) ||
            !Enum.IsDefined(mode) || !Enum.IsDefined(selfAllocation) || !Enum.IsDefined(status) ||
            (status == ModelStatus.Published) != publishedAtUtc.HasValue || lines is null ||
            lines.Any(l => l is null || !l.DestinationNodeId.IsValid) ||
            lines.Select(l => l.DestinationNodeId).Distinct().Count() != lines.Count ||
            (mode == AllocationKeyMode.DriverUnits) != unitId.HasValue)
            throw AllocationDiagnostics.Error("KEY_INVALID", "Allocation.Invalid");
        Id = id; CompanyId = companyId; ModelVersionId = modelVersionId; FamilyId = familyId;
        VersionNumber = versionNumber; ParentVersionId = parentVersionId; Revision = revision; Name = name;
        Stage = stage; Mode = mode; UnitId = unitId; SelfAllocation = selfAllocation; Status = status;
        PublishedAtUtc = publishedAtUtc; Lines = FrozenList.Copy(lines.OrderBy(l => l.DestinationNodeId));
    }
    public EntityId Id { get; }
    public EntityId CompanyId { get; }
    public EntityId ModelVersionId { get; }
    public EntityId FamilyId { get; }
    public int VersionNumber { get; }
    public EntityId? ParentVersionId { get; }
    public long Revision { get; }
    public string Name { get; }
    public AllocationStage Stage { get; }
    public AllocationKeyMode Mode { get; }
    public UnitId? UnitId { get; }
    public SelfAllocationPolicy SelfAllocation { get; }
    public ModelStatus Status { get; }
    public UtcInstant? PublishedAtUtc { get; }
    public IReadOnlyList<AllocationKeyLine> Lines { get; }
}

public sealed record PrimaryRule(EntityId Id, EntityId CompanyId, EntityId ModelVersionId,
    AccountSelectorKind SelectorKind, string SelectorValue, int Priority, EntityId KeyVersionId, bool IsActive);
public sealed record SecondaryRule(EntityId Id, EntityId CompanyId, EntityId ModelVersionId,
    EntityId AuxiliaryNodeId, string ComponentRoutingGroup, EntityId KeyVersionId, bool IsActive);
public sealed record ExplicitPrimaryAssignment(EntityId PartId, EntityId KeyVersionId);
public sealed record AbsoluteAllocationLine(EntityId DestinationNodeId, Money Amount);
public sealed class AbsolutePartAllocation
{
    public AbsolutePartAllocation(EntityId partId, long sourceRevision, Money sourceAmount, IReadOnlyList<AbsoluteAllocationLine> lines)
    {
        partId.EnsureValid();
        if (sourceRevision < 0 || lines is null || lines.Any(l => l is null || !l.DestinationNodeId.IsValid) ||
            lines.Select(l => l.DestinationNodeId).Distinct().Count() != lines.Count)
            throw AllocationDiagnostics.Error("KEY_ABSOLUTE_INVALID", "Allocation.AbsoluteInvalid");
        PartId = partId; SourceRevision = sourceRevision; SourceAmount = sourceAmount;
        Lines = FrozenList.Copy(lines.OrderBy(l => l.DestinationNodeId));
    }
    public EntityId PartId { get; }
    public long SourceRevision { get; }
    public Money SourceAmount { get; }
    public IReadOnlyList<AbsoluteAllocationLine> Lines { get; }
}
public static class AllocationDiagnostics
{
    public static DomainRuleException Error(string code, string key, EntityId? entity = null) =>
        new(new(code, key, entity.HasValue ? [new("entityId", DiagnosticArgumentType.Identifier, entity.Value.ToString())] : []));
}
