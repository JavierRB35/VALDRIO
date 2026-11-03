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
        if (!new DecimalEditSession(capturedCulture).TryCommit(text, out var requested))
        {
            correctionCommitPending = false; correctionFailed = true;
            RejectInput("RES_NUMBER_REQUIRED", "Resource.NumberRequired"); company.NotifyWorkspaceStateChanged(); return false;
        }
        var declined = false;
        var success = await Run(async token =>
        {
            StudyRules.CheckRevision(row.Revision, workspace.Study.Revision, workspace.Study.Id);
            if (!Rows.Any(r => r.Id == row.Id))
                throw ResourceWorkspaceRules.Error("RES_NOT_FOUND", "Resource.NotFound");
            var currency = workspace.Study.Currency.FunctionalCurrencyCode;
            var total = new Money(requested, currency, workspace.Precision);
            var command = ResourceCorrectionCommand.Prepare(workspace, row.Id, row.Revision, total,
                catalog.GetRequired(currency).Freeze(), new(Guid.NewGuid()), new(Guid.NewGuid()),
                new(Guid.NewGuid()), "local-user");
            if (command is null) { statusKey = "CorrectionUnchanged"; return; }
            var affected = workspace.Ledger.AdjustmentApplications?.Where(a => a.ReversedBy is null &&
                a.Lines.Any(l => l.ResourceId == row.Id)).ToArray() ?? [];
            if (affected.Length > 0)
            {
                var coverage = workspace.Preparation.Coverage.Single(c => c.ResourceId == row.Id);
                var priorManual = coverage.AdjustmentTotal - AdjustmentRuleEngine.RuleTotal(workspace, row.Id);
                var after = coverage.Effective + total - priorManual;
                var explanation = string.Format(Format, RuleText("ManualOnRule"), row.DisplayName,
                    string.Join(", ", affected.Select(a => a.Rule.Name)), MoneyText(priorManual), MoneyText(total),
                    MoneyText(coverage.Effective), MoneyText(after));
                if (!dialogs.ConfirmEconomicChange(RuleText("ManualTotal"), explanation, RuleText("Confirm"), RuleText("Cancel")))
                { declined = true; RebuildRows(); return; }
            }
            await Commit(command, token);
            if (statusKey != "SavedRefreshPending") statusKey = "CorrectionSaved";
        });
        correctionCommitPending = false; company.NotifyWorkspaceStateChanged();
        correctionFailed = !success;
        Refresh();
        return success && !declined;
    }
}
