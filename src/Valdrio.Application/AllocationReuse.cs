using Valdrio.Domain;
using Valdrio.Calculation;

namespace Valdrio.Application;

public sealed record AllocationReuseSelection(EntityId SourceStudyId, long SourceRevision,
    string SourceFingerprint, IReadOnlyList<EntityId> PrimaryRuleIds,
    IReadOnlyList<EntityId> SecondaryRuleIds, bool ReplaceConflicts);
public sealed record AllocationReuseItem(EntityId RuleId, AllocationStage Stage, string Selector,
    string KeyName, string Status, bool Compatible, bool Conflict, bool AlreadyPresent, bool Unused);
public sealed record AllocationReusePreview(string SourceFingerprint, IReadOnlyList<AllocationReuseItem> Items,
    int OmittedAssignments);

// Reuses methodology only. Monthly drivers and part identities are evidence of another period.
// A different model remains a reference: new keys must be published explicitly for that model.
public static class AllocationReuse
{
    public static string Fingerprint(AllocationWorkspaceSnapshot source) => PublishedModelStructure.Hash(
        CanonicalSnapshotJson.Serialize(new { source.Resources.Study, source.Configuration, source.Keys }));

    public static AllocationReusePreview Preview(AllocationWorkspaceSnapshot source, AllocationWorkspaceSnapshot target)
    {
        var sameScope = source.Resources.Study.CompanyId == target.Resources.Study.CompanyId &&
            source.Resources.Study.Id != target.Resources.Study.Id &&
            CanonicalSnapshotJson.Serialize(source.Resources.Model.Structure) == CanonicalSnapshotJson.Serialize(target.Resources.Model.Structure);
        var result = new List<AllocationReuseItem>();
        string Compatibility(EntityId keyId, bool active, AllocationStage stage)
        {
            if (!sameScope) return "Model";
            if (!active) return "Inactive";
            var key = target.Keys.SingleOrDefault(k => k.Id == keyId);
            if (key is null || key.IsDeleted || key.Status != ModelStatus.Published || key.Stage != stage) return "Key";
            if (key.Mode == AllocationKeyMode.DriverUnits) return "Driver";
            try { AllocationKeyRules.Validate(key, target.Resources.Model.Structure, target.Resources.Model.DriverUnits, target.Resources.Precision, true); }
            catch (DomainRuleException) { return "Key"; }
            return "Compatible";
        }
        string Name(EntityId id) => source.Keys.SingleOrDefault(k => k.Id == id)?.Name ?? id.ToString();
        foreach (var rule in source.Configuration.PrimaryRules)
        {
            var status = Compatibility(rule.KeyVersionId, rule.IsActive, AllocationStage.Primary);
            var matches = target.Configuration.PrimaryRules.Where(r => r.Id == rule.Id ||
                r.IsActive && r.SelectorKind == rule.SelectorKind && r.SelectorValue == rule.SelectorValue && r.Priority == rule.Priority).ToArray();
            var present = matches.Length == 1 && SamePrimary(matches[0], rule);
            var conflict = matches.Length > 0 && !present;
            var unused = !target.Resources.Origins.Any(o => rule.SelectorKind == AccountSelectorKind.Exact
                ? o.Code == rule.SelectorValue : o.Code.StartsWith(rule.SelectorValue, StringComparison.Ordinal));
            result.Add(new(rule.Id, AllocationStage.Primary, rule.SelectorValue, Name(rule.KeyVersionId),
                status != "Compatible" ? status : present ? "Present" : conflict ? "Conflict" : unused ? "Unused" : status,
                status == "Compatible", conflict, present, unused));
        }
        foreach (var rule in source.Configuration.SecondaryRules)
        {
            var status = Compatibility(rule.KeyVersionId, rule.IsActive, AllocationStage.Secondary);
            if (status == "Compatible" && (!target.Resources.Model.Structure.Nodes.Any(n =>
                n.Id == rule.AuxiliaryNodeId && n.Role == NodeRole.Auxiliary && n.IsActive))) status = "Model";
            // Custom routing groups depend on period-specific PartIds. They need explicit remapping.
            if (status == "Compatible" && rule.ComponentRoutingGroup != "all") status = "Group";
            var matches = target.Configuration.SecondaryRules.Where(r => r.Id == rule.Id ||
                r.IsActive && r.AuxiliaryNodeId == rule.AuxiliaryNodeId && r.ComponentRoutingGroup == rule.ComponentRoutingGroup).ToArray();
            var present = matches.Length == 1 && SameSecondary(matches[0], rule);
            var conflict = matches.Length > 0 && !present;
            var node = source.Resources.Model.Structure.Nodes.SingleOrDefault(n => n.Id == rule.AuxiliaryNodeId);
            result.Add(new(rule.Id, AllocationStage.Secondary, (node?.Code ?? "—") + " / " + rule.ComponentRoutingGroup,
                Name(rule.KeyVersionId), status != "Compatible" ? status : present ? "Present" : conflict ? "Conflict" : status,
                status == "Compatible", conflict, present, false));
        }
        return new(Fingerprint(source), FrozenList.Copy(result), source.Configuration.ExplicitAssignments.Count +
            source.Configuration.AbsoluteAssignments.Count + (source.Configuration.SecondaryPartRoutingGroups?.Count ?? 0));
    }

    private static bool SamePrimary(PrimaryRule a, PrimaryRule b) =>
        a.SelectorKind == b.SelectorKind && a.SelectorValue == b.SelectorValue && a.Priority == b.Priority &&
        a.KeyVersionId == b.KeyVersionId && a.IsActive == b.IsActive;
    private static bool SameSecondary(SecondaryRule a, SecondaryRule b) =>
        a.AuxiliaryNodeId == b.AuxiliaryNodeId && a.ComponentRoutingGroup == b.ComponentRoutingGroup &&
        a.KeyVersionId == b.KeyVersionId && a.IsActive == b.IsActive;

    public static AllocationConfiguration Adopt(AllocationWorkspaceSnapshot source, AllocationWorkspaceSnapshot target,
        AllocationReuseSelection selection)
    {
        if (source.Resources.Study.Id != selection.SourceStudyId || source.Resources.Study.Revision != selection.SourceRevision ||
            Fingerprint(source) != selection.SourceFingerprint)
            throw PhysicalActivityRules.Error("REUSE_SOURCE_CHANGED", "Reuse.SourceChanged");
        if (target.Resources.Study.State != StudyState.Open || target.Configuration.PrimaryClosed || target.Configuration.SecondaryClosed)
            throw PhysicalActivityRules.Error("REUSE_TARGET_CLOSED", "Reuse.TargetClosed");
        var preview = Preview(source, target);
        var selected = selection.PrimaryRuleIds.Select(id => (id, AllocationStage.Primary))
            .Concat(selection.SecondaryRuleIds.Select(id => (id, AllocationStage.Secondary))).ToArray();
        if (selected.Length == 0 || selected.Distinct().Count() != selected.Length)
            throw PhysicalActivityRules.Error("REUSE_SELECTION_INVALID", "Reuse.SelectionInvalid");
        foreach (var (id, stage) in selected)
        {
            var item = preview.Items.SingleOrDefault(i => i.RuleId == id && i.Stage == stage);
            if (item is null || !item.Compatible || item.Conflict && !selection.ReplaceConflicts)
                throw PhysicalActivityRules.Error("REUSE_SELECTION_INVALID", "Reuse.SelectionInvalid");
        }
        var primary = target.Configuration.PrimaryRules.ToList();
        var secondary = target.Configuration.SecondaryRules.ToList();
        foreach (var id in selection.PrimaryRuleIds)
        {
            var item = preview.Items.Single(i => i.RuleId == id && i.Stage == AllocationStage.Primary);
            if (item.AlreadyPresent) continue;
            var rule = source.Configuration.PrimaryRules.Single(r => r.Id == id);
            primary.RemoveAll(r => r.Id == id || r.IsActive && r.SelectorKind == rule.SelectorKind &&
                r.SelectorValue == rule.SelectorValue && r.Priority == rule.Priority);
            primary.Add(rule with { DraftOfId = null });
        }
        foreach (var id in selection.SecondaryRuleIds)
        {
            var item = preview.Items.Single(i => i.RuleId == id && i.Stage == AllocationStage.Secondary);
            if (item.AlreadyPresent) continue;
            var rule = source.Configuration.SecondaryRules.Single(r => r.Id == id);
            secondary.RemoveAll(r => r.Id == id || r.IsActive && r.AuxiliaryNodeId == rule.AuxiliaryNodeId &&
                r.ComponentRoutingGroup == rule.ComponentRoutingGroup);
            secondary.Add(rule);
        }
        var current = target.Configuration;
        return new(primary, secondary, current.ExplicitAssignments, current.AbsoluteAssignments,
            current.Descriptions, secondaryPartRoutingGroups: current.SecondaryPartRoutingGroups);
    }
}
