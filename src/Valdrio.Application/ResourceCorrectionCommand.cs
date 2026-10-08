using System.Globalization;
using Valdrio.Calculation;
using Valdrio.Domain;

namespace Valdrio.Application;

public static class ResourceCorrectionCommand
{
    // Return null for an unchanged total. The existing mutation/receipt format remains unchanged.
    public static ResourceMutation? Prepare(ResourceWorkspaceSnapshot before, EntityId resourceId, long expectedRevision,
        Money requestedTotal, CurrencyMetadataSnapshot metadata, EntityId requestId, EntityId adjustmentId,
        EntityId pendingPartId, string actor)
    {
        ArgumentNullException.ThrowIfNull(before);
        StudyRules.CheckRevision(expectedRevision, before.Study.Revision, before.Study.Id);
        if (before.Study.State != StudyState.Open)
            throw StudyRules.Error("STUDY_CLOSED", "Study.ClosedDiagnostic", before.Study.Id);
        var currency = before.Study.Currency.FunctionalCurrencyCode;
        if (requestedTotal.Currency != currency || metadata.AlphaCode != currency || !metadata.IsMonetary ||
            requestedTotal.PrecisionPolicy != before.Precision)
            throw ResourceWorkspaceRules.Error("RES_COMMAND_INVALID", "Resource.CommandInvalid");
        metadata.EnsureValid();
        var resource = before.Resources.SingleOrDefault(r => r.Id == resourceId)
            ?? throw ResourceWorkspaceRules.Error("RES_NOT_FOUND", "Resource.NotFound");
        var correction = ResourceCorrectionRules.Prepare(resource, requestedTotal, pendingPartId);
        if (correction is null) return null;

        var previous = before.Preparation.Coverage.Single(c => c.ResourceId == resourceId).AdjustmentTotal;
        var code = before.Origins.Single(o => o.Id == resourceId).Code;
        var explanation = "Inline correction: account " + code + "; total " +
            previous.Amount.ToString(CultureInfo.InvariantCulture) + " -> " +
            requestedTotal.Amount.ToString(CultureInfo.InvariantCulture) + " " + currency;
        var adjustment = new ResourceAdjustmentRequest(adjustmentId, correction.Delta, null,
            "Inline correction", explanation, null, metadata);
        var command = new ResourceMutation(ResourceMutationKind.EditResource, before.Study.CompanyId, before.Study.Id,
            expectedRevision, requestId, actor, explanation, resourceId, correction.Parts,
            [adjustment], null, null, [], []);
        _ = ResourceWorkspaceRules.Apply(before, command);
        return command;
    }
}
