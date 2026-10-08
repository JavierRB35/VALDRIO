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
            var command = ResourceCorrectionCommand.Prepare(workspace, row.Id, row.Revision, total,
                catalog.GetRequired(currency).Freeze(), new(Guid.NewGuid()), new(Guid.NewGuid()),
                new(Guid.NewGuid()), "local-user");
            if (command is null) { statusKey = "CorrectionUnchanged"; return; }
            await Commit(command, token);
            if (statusKey != "SavedRefreshPending") statusKey = "CorrectionSaved";
        });
        correctionCommitPending = false; company.NotifyWorkspaceStateChanged();
        correctionFailed = !success;
        Refresh();
        return success;
    }
}
