using System.Globalization;
using Valdrio.Application;
using Valdrio.Domain;
using Valdrio.Localization;
namespace Valdrio.Desktop;

public sealed partial class ResourceWorkspaceViewModel
{
    private bool correctionFailed, correctionCommitPending;
    internal bool ReserveCorrectionCommit()
    {
        if (!CanEditCorrections) return false;
        correctionCommitPending = true;
        company.NotifyWorkspaceStateChanged(); Refresh();
        return true;
    }
    public bool CanEditCorrections => CanEdit && !IsDirty && !ShowRevalue;
    public bool CorrectionsReadOnly => !CanEditCorrections;
    public string CorrectionHelp => string.Format(Format, this["CorrectionHelp"], FunctionalCurrency);
    public string CorrectionLockMessage => correctionFailed ? this["CorrectionNotSaved"] :
        !CanEdit && workspace is not null ? Requirement : IsDirty || ShowRevalue ? this["CorrectionDraftBlocked"] : "";

    // The cell represents the cumulative correction, not an additional delta.
    // Persist its difference from the ledger using the usual revisioned transaction.
    public async Task<bool> SaveCorrectionAsync(ResourceGridRow row, string text, CultureInfo capturedCulture)
    {
        if (!CanEditCorrections || workspace is null)
        {
            correctionCommitPending = false; company.NotifyWorkspaceStateChanged(); Refresh(); return false;
        }
        var success = await Run(async token =>
        {
            StudyRules.CheckRevision(row.Revision, workspace.Study.Revision, workspace.Study.Id);
            if (!Rows.Any(r => r.Id == row.Id))
                throw ResourceWorkspaceRules.Error("RES_NOT_FOUND", "Resource.NotFound");
            if (!new DecimalEditSession(capturedCulture).TryCommit(text, out var requested))
                throw ResourceWorkspaceRules.Error("RES_NUMBER_REQUIRED", "Resource.NumberRequired");
            var currency = workspace.Study.Currency.FunctionalCurrencyCode;
            var total = new Money(requested, currency, workspace.Precision);
            var resource = workspace.Resources.Single(r => r.Id == row.Id);
            var coverage = workspace.Preparation.Coverage.Single(c => c.ResourceId == row.Id);
            var delta = total - coverage.AdjustmentTotal;
            if (delta.Amount == 0m) { statusKey = "CorrectionUnchanged"; return; }
            var revisedParts = resource.Parts.ToList();
            var pendingIndex = revisedParts.FindLastIndex(p => p.Routing == ResourceRoutingKind.Pending);
            if (pendingIndex >= 0)
            {
                var pending = revisedParts[pendingIndex];
                revisedParts[pendingIndex] = new(pending.Id, pending.Amount + delta, ResourceRoutingKind.Pending,
                    pending.DirectAssignments, pending.PoolId, pending.ExclusionReason, pending.Classification);
            }
            else
                revisedParts.Add(new(new(Guid.NewGuid()), delta, ResourceRoutingKind.Pending));
            var explanation = "Inline correction: account " + row.Code + "; total " +
                coverage.AdjustmentTotal.Amount.ToString(CultureInfo.InvariantCulture) + " -> " +
                requested.ToString(CultureInfo.InvariantCulture) + " " + currency;
            var adjustment = new ResourceAdjustmentRequest(new(Guid.NewGuid()), delta, null,
                "Inline correction", explanation, null, catalog.GetRequired(currency).Freeze());
            var command = new ResourceMutation(ResourceMutationKind.EditResource, workspace.Study.CompanyId, workspace.Study.Id,
                workspace.Study.Revision, new(Guid.NewGuid()), "local-user", explanation, row.Id, revisedParts.ToArray(),
                [adjustment], null, null, [], []);
            _ = ResourceWorkspaceRules.Apply(workspace, command);
            await Commit(command, token);
            if (statusKey != "SavedRefreshPending") statusKey = "CorrectionSaved";
        });
        correctionCommitPending = false; company.NotifyWorkspaceStateChanged();
        correctionFailed = !success;
        Refresh();
        return success;
    }
}
