using Valdrio.Domain;

namespace Valdrio.Desktop;

public sealed partial class MainWindowViewModel
{
    public string AnotherImportLabel => Guide("AnotherImport");
    public string Guide(string key) => Text("Guide." + key);
}

public sealed partial class CompanyFileViewModel
{
    public string AnotherImportLabel => Guide("AnotherImport");
    public string Guide(string key) => Text("Guide." + key);
}

public sealed partial class ModelWorkspaceViewModel
{
    public string Guide(string key) => Text("Guide." + key);
    public string OtherOperationsLabel => Guide("OtherModel");
    public bool HasPendingCenter => CenterFormEdited;
    public bool HasPendingNode => nodeFormEdited;
    public bool HasActiveCenter => Centers.Any(c => c.Value.IsActive);
    public bool HasActiveUnit => Units.Any(u => u.Value.IsActive);
    public bool HasPrincipal => draftNodes.Any(n => n.Role == NodeRole.Principal && n.IsActive);
    public bool CanWriteInGuide => company.CanWrite;
    public string NextRequirement => Guide(company.Snapshot is null ? "NeedCompany" :
        !company.CanWrite ? "ReadOnly" : IsBusy || !company.WorkspaceIdle ? "Working" :
        MasterEdited ? "PendingCenter" : Current is null ? "NeedDraft" :
        IsPublished ? "ModelComplete" : !HasActiveCenter ? "NeedCenter" : !HasActiveUnit ? "NeedUnit" :
        nodeFormEdited && string.IsNullOrWhiteSpace(NodeCode) ? "NeedNodeCode" :
        nodeFormEdited && Physical is null ? "NeedNodeCenter" :
        nodeFormEdited && DriverUnit is null ? "NeedNodeUnit" :
        nodeFormEdited ? "ApplyNode" : !HasPrincipal ? "NeedPrincipal" :
        dirty ? "SaveModel" : "PublishModel");
}

public sealed partial class StudyWorkspaceViewModel
{
    public string Guide(string key) => Text("Guide." + key);
    public string OtherOperationsLabel => Guide("OtherStudy");
    public string CreateVisibility => Current is null ? "Visible" : "Collapsed";
    public string SaveVisibility => Current is null ? "Collapsed" : "Visible";
    public string InspectFileLabel => Guide("Preview");
    public bool HasImportDestination => Current?.State == StudyState.Open && !IsDirty && company.CanWrite;
    public string NextRequirement => Guide(company.Snapshot is null ? "NeedCompany" :
        !company.CanWrite ? "ReadOnly" : !Ready ? "Working" : !HasPublishedModels ? "NeedPublished" :
        string.IsNullOrWhiteSpace(Name) ? "NeedStudyName" : ModelChoice is null ? "NeedStudyModel" :
        PeriodStart is null || PeriodEnd is null ? "NeedDates" : PeriodStart > PeriodEnd ? "InvalidDates" :
        Current?.State == StudyState.Closed ? "ClosedStudy" : Current is null || IsDirty ? "SaveStudy" : "StudyComplete");
    public async Task<bool> SaveForImport()
    {
        if (!Ready || !company.CanWrite) return false;
        if (Current is null) await CreateCommand.ExecuteAsync(null);
        else if (SaveCommand.CanExecute(null)) await SaveCommand.ExecuteAsync(null);
        return HasImportDestination;
    }
}

public sealed partial class ImportColumnMappingViewModel
{
    public string Guide(string key) => Text("Guide." + key);
    public bool HasImportDestination => targetStudy?.State == StudyState.Open && importPort is not null && writable;
    public string MeaningChosenLabel => Guide("MeaningChosen");
    public string MeaningRequiredMessage => Guide("NeedMeaning");
    private bool hasAmountMeaning;
    public bool HasAmountMeaning
    {
        get => hasAmountMeaning;
        set { if (SetProperty(ref hasAmountMeaning, value)) { InvalidatePreparation(false); Refresh(); } }
    }
    public string? AmountMeaningSelection
    {
        get => HasAmountMeaning ? amountMode : null;
        set { if (value is "period" or "closing-full" or "cumulative") { AmountMode = value; AcknowledgeAmountMeaning(); OnPropertyChanged(nameof(AmountMeaningSelection)); } }
    }
    public void AcknowledgeAmountMeaning() => HasAmountMeaning = true;
    public string ReviewRequirement => Guide(!HasImportDestination ? "PreviewOnly" : !IsReady ? "NeedMapping" :
        !IsAccountSelectionReady ? "NeedRecords" : !HasAmountMeaning ? "NeedMeaning" :
        !(international && currencyFromColumn) && string.IsNullOrWhiteSpace(sourceCurrency) ? "NeedCurrency" :
        amountMode == "cumulative" && comparison is null ? "NeedComparison" : "ReadyReview");
    public bool CanOpenReview => HasPreparedReview || CanReviewInGuide;
    public bool CanReviewInGuide => HasImportDestination && IsReady && IsAccountSelectionReady && HasAmountMeaning &&
        (international && currencyFromColumn || !string.IsNullOrWhiteSpace(sourceCurrency)) &&
        (amountMode != "cumulative" || comparison is not null);
}
