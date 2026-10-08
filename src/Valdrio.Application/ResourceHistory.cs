using Valdrio.Domain;
namespace Valdrio.Application;

public static class ResourceHistory
{
    public static long EconomicRevision(StudyDefinition study, IEnumerable<ImportedBatch> imports, IEnumerable<ResourceMutationReceipt> history) =>
        imports.Where(b => b.ConfirmedStudy.Revision <= study.Revision).Select(b => b.ConfirmedStudy.Revision)
            .Concat(history.Where(h => h.ConfirmedStudy.Revision <= study.Revision).Select(h => h.ConfirmedStudy.Revision)).DefaultIfEmpty(0).Max();
    public static ResourceLedger Replay(StudyDefinition study, PrecisionPolicy precision, PublishedModelStructure model,
        IReadOnlyList<ImportedBatch> imports, IReadOnlyList<ResourceMutationReceipt> history)
    {
        var ledger = ResourceLedger.Empty; long revision = -1;
        var requests = new HashSet<EntityId>(); var adjustments = new HashSet<EntityId>();
        foreach (var change in history)
        {
            var c = change.Request; var before = change.PreviousStudy; var after = change.ConfirmedStudy;
            if (before.Id != study.Id || before.CompanyId != study.CompanyId || before.SelectedModelVersionId != model.Structure.Id ||
                before.Revision < revision || after.Revision > study.Revision || !requests.Add(c.RequestId) ||
                !ImportConfirmationRules.IsImportRevision(before, after))
                throw ResourceWorkspaceRules.Error("RES_HISTORY_INVALID", "Resource.HistoryInvalid");
            var priorImports = imports.Where(b => b.ConfirmedStudy.Revision <= before.Revision).ToArray();
            var workspace = new ResourceWorkspaceSnapshot(before, precision, model, ResourceOrigins.FromImports(before, priorImports), ledger);
            foreach (var delta in c.Adjustments)
                if (!adjustments.Add(delta.Id)) throw ResourceWorkspaceRules.Error("RES_DUPLICATE_ID", "Resource.DuplicateId");
            var computed = ResourceWorkspaceRules.Apply(workspace, c);
            if (CanonicalSnapshotJson.Serialize(computed) != CanonicalSnapshotJson.Serialize(change.Ledger))
                throw ResourceWorkspaceRules.Error("RES_HISTORY_INVALID", "Resource.HistoryInvalid");
            ledger = computed; revision = after.Revision;
        }
        var current = new ResourceWorkspaceSnapshot(study, precision, model, ResourceOrigins.FromImports(study, imports), ledger);
        if (!current.Preparation.IsValid) throw new DomainRuleException(current.Preparation.Diagnostics[0]);
        return ledger;
    }
}
