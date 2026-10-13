using Valdrio.Application;
using Valdrio.Domain;

namespace Valdrio.Desktop;

// A preview is read-only. An explicit user confirmation in WPF is required
// before the selected, currently unassigned parts are saved in one transaction.
public sealed record CoverageBatchPreview(
    int SelectedCount, IReadOnlyList<EntityId> EligiblePartIds, decimal EligibleAmount)
{
    public int EligibleCount => EligiblePartIds.Count;
    public int SkippedCount => SelectedCount - EligibleCount;
}

public sealed partial class AllocationWorkspaceViewModel
{
    public CoverageBatchPreview PreviewCoverageBatch(IReadOnlyList<EntityId> partIds, EntityId keyId)
    {
        var selected = partIds.Distinct().ToHashSet();
        if (workspace is null || !CanShowCoverageEditor || !CanEditConfiguration ||
            !workspace.Keys.Any(k => k.Id == keyId && k.Stage == AllocationStage.Primary &&
                                     k.Status == ModelStatus.Published))
            return new(selected.Count, [], 0m);

        var configuration = workspace.Configuration;
        var manual = configuration.ExplicitAssignments.Select(a => a.PartId).ToHashSet();
        var absolute = configuration.AbsoluteAssignments.Select(a => a.PartId).ToHashSet();
        var parts = workspace.Resources.Resources.SelectMany(r => r.Parts)
            .ToDictionary(p => p.Id, p => p.Amount.Amount);
        var eligible = workspace.Coverage
            .Where(c => selected.Contains(c.SourceId) && parts.ContainsKey(c.SourceId) &&
                        c.KeyId is null && c.Diagnostic is not null &&
                        !manual.Contains(c.SourceId) && !absolute.Contains(c.SourceId) &&
                        !configuration.PrimaryRules.Any(r => r.IsActive &&
                            (r.SelectorKind == AccountSelectorKind.Exact
                                ? r.SelectorValue == c.Account
                                : c.Account.StartsWith(r.SelectorValue, StringComparison.Ordinal))))
            .Select(c => c.SourceId).Distinct()
            .OrderBy(id => id.ToString(), StringComparer.Ordinal).ToArray();
        return new(selected.Count, eligible, eligible.Sum(id => parts[id]));
    }

    public async Task<bool> AssignCoverageBatchAsync(IReadOnlyList<EntityId> partIds, EntityId keyId)
    {
        var preview = PreviewCoverageBatch(partIds, keyId);
        if (preview.EligibleCount == 0) return false;
        var selected = preview.EligiblePartIds.ToHashSet();
        // No deletion or replacement: explicit manual assignments, absolute
        // allocations and account/prefix rules were filtered in the preview.
        return await CommitConfig(c => new(c.PrimaryRules, c.SecondaryRules,
            c.ExplicitAssignments.Concat(selected.OrderBy(id => id.ToString(), StringComparer.Ordinal)
                .Select(id => new ExplicitPrimaryAssignment(id, keyId))).ToArray(),
            c.AbsoluteAssignments, c.Descriptions));
    }
}
