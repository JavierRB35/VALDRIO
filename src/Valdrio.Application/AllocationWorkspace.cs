using System.Text.Json.Serialization;
using Valdrio.Calculation;
using Valdrio.Domain;

namespace Valdrio.Application;

public sealed record SaveAllocationKey(EntityId CompanyId, EntityId ModelVersionId, EntityId KeyId,
    EntityId? ParentVersionId, long ExpectedRevision, string Name, AllocationStage Stage, AllocationKeyMode Mode,
    UnitId? UnitId, SelfAllocationPolicy SelfAllocation, IReadOnlyList<AllocationKeyLine> Lines,
    bool Publish, EntityId RequestId, string Actor)
{
    private IReadOnlyList<AllocationKeyLine> lines = FrozenList.Copy(Lines);
    public IReadOnlyList<AllocationKeyLine> Lines { get => lines; init => lines = FrozenList.Copy(value); }
}
public sealed record AllocationKeyReceipt(SaveAllocationKey Request, AllocationKeyVersion? Before,
    AllocationKeyVersion After, UtcInstant AtUtc);
public sealed record AccountDescriptionChoice(string Account, string Description);
public sealed class AllocationConfiguration
{
    public AllocationConfiguration(IReadOnlyList<PrimaryRule> primaryRules, IReadOnlyList<SecondaryRule> secondaryRules,
        IReadOnlyList<ExplicitPrimaryAssignment> explicitAssignments, IReadOnlyList<AbsolutePartAllocation> absoluteAssignments,
        IReadOnlyList<AccountDescriptionChoice> descriptions)
    {
        if (primaryRules is null || secondaryRules is null || explicitAssignments is null || absoluteAssignments is null || descriptions is null ||
            primaryRules.Any(r => r is null) || secondaryRules.Any(r => r is null) || explicitAssignments.Any(r => r is null) ||
            absoluteAssignments.Any(r => r is null) || descriptions.Any(r => r is null))
            throw AllocationDiagnostics.Error("KEY_RULE_INVALID", "Allocation.RuleInvalid");
        PrimaryRules = FrozenList.Copy(primaryRules.OrderBy(r => r.Id));
        SecondaryRules = FrozenList.Copy(secondaryRules.OrderBy(r => r.Id));
        ExplicitAssignments = FrozenList.Copy(explicitAssignments.OrderBy(r => r.PartId));
        AbsoluteAssignments = FrozenList.Copy(absoluteAssignments.OrderBy(r => r.PartId));
        Descriptions = FrozenList.Copy(descriptions.OrderBy(r => r.Account, StringComparer.Ordinal));
    }
    public IReadOnlyList<PrimaryRule> PrimaryRules { get; }
    public IReadOnlyList<SecondaryRule> SecondaryRules { get; }
    public IReadOnlyList<ExplicitPrimaryAssignment> ExplicitAssignments { get; }
    public IReadOnlyList<AbsolutePartAllocation> AbsoluteAssignments { get; }
    public IReadOnlyList<AccountDescriptionChoice> Descriptions { get; }
    public static AllocationConfiguration Empty => new([], [], [], [], []);
}
public sealed record ChangeAllocations(EntityId CompanyId, EntityId StudyId, long ExpectedRevision,
    EntityId RequestId, string Actor, AllocationConfiguration Configuration);
public sealed record AllocationChangeReceipt(ChangeAllocations Request, StudyDefinition PreviousStudy,
    StudyDefinition ConfirmedStudy, IReadOnlyList<AllocationKeyVersion> ReferencedKeys, UtcInstant AtUtc)
{
    private IReadOnlyList<AllocationKeyVersion> referencedKeys = FrozenList.Copy(ReferencedKeys.OrderBy(k => k.Id));
    public IReadOnlyList<AllocationKeyVersion> ReferencedKeys { get => referencedKeys; init => referencedKeys = FrozenList.Copy(value.OrderBy(k => k.Id)); }
}
public sealed record AppliedAllocationKey(EntityId SourceId, string ComponentGroup, AllocationKeyVersion Key);
public sealed record AllocationCoverage(EntityId SourceId, string Account, EntityId? KeyId, DomainDiagnostic? Diagnostic, string ComponentGroup = "");

public interface IAllocationWorkspace
{
    IReadOnlyList<AllocationKeyVersion> ReadAllocationKeys(EntityId companyId, EntityId modelId, CancellationToken token = default);
    AllocationKeyReceipt SaveAllocationKey(SaveAllocationKey request, CancellationToken token = default);
    AllocationWorkspaceSnapshot ReadAllocations(EntityId companyId, EntityId studyId, CancellationToken token = default);
    AllocationChangeReceipt ChangeAllocations(ChangeAllocations request, CancellationToken token = default);
    IReadOnlyList<AllocationChangeReceipt> ReadAllocationHistory(EntityId companyId, EntityId studyId, CancellationToken token = default);
}
public sealed class AllocationWorkspaceSnapshot
{
    public AllocationWorkspaceSnapshot(ResourceWorkspaceSnapshot resources, IReadOnlyList<AllocationKeyVersion> keys,
        AllocationConfiguration configuration, long configurationRevision, bool includeUnroutedAccounts = true,
        IReadOnlyList<EntityId>? usedKeyIds = null)
    {
        Resources = resources; Keys = FrozenList.Copy(keys.OrderBy(k => k.Id));
        Configuration = configuration; ConfigurationRevision = configurationRevision;
        IncludeUnroutedAccounts = includeUnroutedAccounts;
        UsedKeyIds = FrozenList.Copy(usedKeyIds ?? []);
    }
    public ResourceWorkspaceSnapshot Resources { get; }
    public IReadOnlyList<AllocationKeyVersion> Keys { get; }
    public AllocationConfiguration Configuration { get; }
    public long ConfigurationRevision { get; }
    public bool IncludeUnroutedAccounts { get; }
    [JsonIgnore] public IReadOnlyList<EntityId> UsedKeyIds { get; }
    [JsonIgnore] public IReadOnlyList<AllocationCoverage> Coverage => AllocationWorkspaceRules.Coverage(this);
}

public static class AllocationWorkspaceRules
{
    public static AllocationKeyVersion SaveKey(SaveAllocationKey request, PublishedModelStructure model,
        PrecisionPolicy precision, IReadOnlyList<AllocationKeyVersion> existing, UtcInstant now, bool allowUnusedPublishedEdit = false)
    {
        request.RequestId.EnsureValid(); request.KeyId.EnsureValid();
        if (request.CompanyId != model.Structure.CompanyId || request.ModelVersionId != model.Structure.Id || string.IsNullOrWhiteSpace(request.Actor))
            throw AllocationDiagnostics.Error("KEY_SCOPE_INVALID", "Allocation.ScopeInvalid", request.KeyId);
        var before = existing.SingleOrDefault(k => k.Id == request.KeyId);
        if (before is not null && (before.CompanyId != request.CompanyId || before.ModelVersionId != request.ModelVersionId))
            throw AllocationDiagnostics.Error("KEY_SCOPE_INVALID", "Allocation.ScopeInvalid", request.KeyId);
        if (before?.Status == ModelStatus.Published && (!request.Publish || !allowUnusedPublishedEdit &&
            (before.Stage != request.Stage || before.Mode != request.Mode || before.UnitId != request.UnitId ||
             before.SelfAllocation != request.SelfAllocation || !before.Lines.SequenceEqual(request.Lines.OrderBy(l => l.DestinationNodeId)))))
            throw AllocationDiagnostics.Error("KEY_IMMUTABLE", "Allocation.Immutable", request.KeyId);
        if ((before?.Revision ?? -1) != request.ExpectedRevision)
            throw AllocationDiagnostics.Error("KEY_REVISION_CHANGED", "Allocation.RevisionChanged", request.KeyId);
        var parent = request.ParentVersionId is { } parentId ? existing.SingleOrDefault(k => k.Id == parentId && k.CompanyId == request.CompanyId && k.ModelVersionId == request.ModelVersionId && k.Status == ModelStatus.Published) : null;
        if (request.ParentVersionId.HasValue && parent is null || before is not null && before.ParentVersionId != request.ParentVersionId)
            throw AllocationDiagnostics.Error("KEY_PARENT_INVALID", "Allocation.ParentInvalid", request.KeyId);
        var family = before?.FamilyId ?? parent?.FamilyId ?? request.KeyId;
        var version = before?.VersionNumber ?? (existing.Where(k => k.FamilyId == family).Select(k => k.VersionNumber).DefaultIfEmpty(0).Max() + 1);
        var result = new AllocationKeyVersion(request.KeyId, request.CompanyId, request.ModelVersionId, family,
            version, request.ParentVersionId, (before?.Revision ?? -1) + 1, request.Name, request.Stage, request.Mode,
            request.UnitId, request.SelfAllocation, request.Publish ? ModelStatus.Published : ModelStatus.Draft,
            request.Publish ? now : null, request.Lines);
        AllocationKeyRules.Validate(result, model.Structure, model.DriverUnits, precision, request.Publish);
        return result;
    }

    public static IReadOnlyList<AllocationKeyVersion> ValidateConfiguration(ResourceWorkspaceSnapshot resources,
        IReadOnlyList<AllocationKeyVersion> keys, ChangeAllocations request)
    {
        var study = resources.Study; var model = resources.Model.Structure; var config = request.Configuration;
        request.RequestId.EnsureValid();
        if (request.CompanyId != study.CompanyId || request.StudyId != study.Id || string.IsNullOrWhiteSpace(request.Actor))
            throw AllocationDiagnostics.Error("KEY_SCOPE_INVALID", "Allocation.ScopeInvalid");
        StudyRules.CheckRevision(request.ExpectedRevision, study.Revision, study.Id);
        if (study.State != StudyState.Open) throw StudyRules.Error("STUDY_CLOSED", "Study.ClosedDiagnostic", study.Id);
        void Invalid(bool test) { if (test) throw AllocationDiagnostics.Error("KEY_RULE_INVALID", "Allocation.RuleInvalid"); }
        Invalid(config.PrimaryRules.Select(r => r.Id).Distinct().Count() != config.PrimaryRules.Count ||
            config.SecondaryRules.Select(r => r.Id).Distinct().Count() != config.SecondaryRules.Count ||
            config.ExplicitAssignments.Select(a => a.PartId).Distinct().Count() != config.ExplicitAssignments.Count ||
            config.AbsoluteAssignments.Select(a => a.PartId).Distinct().Count() != config.AbsoluteAssignments.Count ||
            config.Descriptions.Select(d => d.Account).Distinct(StringComparer.Ordinal).Count() != config.Descriptions.Count);
        var referenced = new Dictionary<EntityId, AllocationKeyVersion>();
        AllocationKeyVersion Key(EntityId id, AllocationStage stage)
        {
            var key = keys.SingleOrDefault(k => k.Id == id && k.CompanyId == study.CompanyId && k.ModelVersionId == model.Id &&
                k.Status == ModelStatus.Published && k.Stage == stage)
                ?? throw AllocationDiagnostics.Error("KEY_PUBLICATION_REQUIRED", "Allocation.PublicationRequired", id);
            AllocationKeyRules.Validate(key, model, resources.Model.DriverUnits, resources.Precision, true);
            referenced[id] = key; return key;
        }
        foreach (var rule in config.PrimaryRules)
        {
            rule.Id.EnsureValid();
            Invalid(rule.CompanyId != study.CompanyId || rule.ModelVersionId != model.Id || !Enum.IsDefined(rule.SelectorKind) || string.IsNullOrWhiteSpace(rule.SelectorValue));
            _ = Key(rule.KeyVersionId, AllocationStage.Primary);
        }
        if (config.PrimaryRules.Where(r => r.IsActive).GroupBy(r => (r.SelectorKind, r.SelectorValue, r.Priority)).Any(g => g.Count() > 1))
            throw AllocationDiagnostics.Error("KEY_RULE_AMBIGUOUS", "Allocation.RuleAmbiguous");
        foreach (var rule in config.SecondaryRules)
        {
            rule.Id.EnsureValid();
            Invalid(rule.CompanyId != study.CompanyId || rule.ModelVersionId != model.Id || string.IsNullOrWhiteSpace(rule.ComponentRoutingGroup));
            _ = Key(rule.KeyVersionId, AllocationStage.Secondary);
            if (rule.IsActive) _ = AllocationKeyRules.ResolveSecondary(rule.AuxiliaryNodeId, rule.ComponentRoutingGroup, config.SecondaryRules, keys, model);
        }
        var eligible = resources.Resources.SelectMany(r => r.Parts).Where(p => p.Routing is ResourceRoutingKind.AllocationPool or ResourceRoutingKind.Pending).ToDictionary(p => p.Id);
        foreach (var item in config.ExplicitAssignments)
        {
            Invalid(!eligible.ContainsKey(item.PartId) || config.AbsoluteAssignments.Any(a => a.PartId == item.PartId));
            _ = Key(item.KeyVersionId, AllocationStage.Primary);
        }
        foreach (var absolute in config.AbsoluteAssignments)
        {
            Invalid(!eligible.ContainsKey(absolute.PartId));
            AllocationKeyRules.ValidateAbsolute(absolute, eligible[absolute.PartId], resources.EconomicRevision, model);
        }
        foreach (var name in config.Descriptions)
            Invalid(!resources.Origins.Any(o => StringComparer.Ordinal.Equals(o.Code, name.Account) && o.Descriptions.Contains(name.Description, StringComparer.Ordinal)));
        return FrozenList.Copy(referenced.Values.OrderBy(k => k.Id));
    }

    public static IReadOnlyList<AllocationCoverage> Coverage(AllocationWorkspaceSnapshot workspace)
    {
        var result = new List<AllocationCoverage>(); var resources = workspace.Resources;
        foreach (var resource in resources.Resources)
        foreach (var part in resource.Parts.Where(p => p.Routing is ResourceRoutingKind.AllocationPool or ResourceRoutingKind.Pending))
        {
            var account = resources.Origins.Single(o => o.Id == resource.Id).Code;
            try
            {
                if (part.Routing == ResourceRoutingKind.Pending && !workspace.IncludeUnroutedAccounts) throw AllocationDiagnostics.Error("KEY_PART_PENDING", "Allocation.PartPending", part.Id);
                if (workspace.Configuration.AbsoluteAssignments.SingleOrDefault(a => a.PartId == part.Id) is { } absolute)
                {
                    // Descriptive/rule changes do not invalidate amounts. A new import or resource revision does.
                    if (absolute.SourceRevision != resources.EconomicRevision) throw AllocationDiagnostics.Error("KEY_ABSOLUTE_STALE", "Allocation.AbsoluteStale", part.Id);
                    AllocationKeyRules.ValidateAbsolute(absolute, part, resources.EconomicRevision, resources.Model.Structure);
                    result.Add(new(part.Id, account, null, null)); continue;
                }
                var key = AllocationKeyRules.ResolvePrimary(account,
                    workspace.Configuration.ExplicitAssignments.SingleOrDefault(a => a.PartId == part.Id)?.KeyVersionId,
                    workspace.Configuration.PrimaryRules, workspace.Keys, resources.Model.Structure);
                AllocationKeyRules.Validate(key, resources.Model.Structure, resources.Model.DriverUnits, resources.Precision, true);
                result.Add(new(part.Id, account, key.Id, null));
            }
            catch (DomainRuleException ex) { result.Add(new(part.Id, account, null, ex.Diagnostic)); }
        }
        var eligible = resources.Resources.SelectMany(r => r.Parts).Where(p => p.Routing == ResourceRoutingKind.AllocationPool || workspace.IncludeUnroutedAccounts && p.Routing == ResourceRoutingKind.Pending).Select(p => p.Id).ToHashSet();
        foreach (var orphan in workspace.Configuration.ExplicitAssignments.Select(a => a.PartId).Concat(workspace.Configuration.AbsoluteAssignments.Select(a => a.PartId)).Where(id => !eligible.Contains(id)).Distinct())
            result.Add(new(orphan, "", null, AllocationDiagnostics.Error("KEY_PART_CHANGED", "Allocation.PartChanged", orphan).Diagnostic));
        foreach (var node in resources.Model.Structure.Nodes.Where(n => n.IsActive && n.Role == NodeRole.Auxiliary))
        {
            var groups = workspace.Configuration.SecondaryRules.Where(r => r.IsActive && r.AuxiliaryNodeId == node.Id)
                .Select(r => r.ComponentRoutingGroup).DefaultIfEmpty("all").Distinct(StringComparer.Ordinal);
            foreach (var group in groups)
            {
                try
                {
                    var key = AllocationKeyRules.ResolveSecondary(node.Id, group, workspace.Configuration.SecondaryRules, workspace.Keys, resources.Model.Structure);
                    AllocationKeyRules.Validate(key, resources.Model.Structure, resources.Model.DriverUnits, resources.Precision, true);
                    result.Add(new(node.Id, node.Code, key.Id, null, group));
                }
                catch (DomainRuleException ex) { result.Add(new(node.Id, node.Code, null, ex.Diagnostic, group)); }
            }
        }
        return FrozenList.Copy(result);
    }
}
