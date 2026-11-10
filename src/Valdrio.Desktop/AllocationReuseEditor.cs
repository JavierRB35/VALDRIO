using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Valdrio.Application;
using Valdrio.Domain;
using Valdrio.Localization;

namespace Valdrio.Desktop;

public sealed partial class AllocationReuseChoice(AllocationReuseItem item, string status, string selector) : ObservableObject
{
    public AllocationReuseItem Item { get; } = item;
    public string Selector { get; } = selector;
    public string Status { get; } = status;
    public bool CanSelect => Item.Compatible && !Item.AlreadyPresent;
    [ObservableProperty] private bool selected;
}

public sealed partial class AllocationReuseEditor : ObservableObject
{
    private readonly IAllocationWorkspace port;
    private readonly IStudyWorkspace studies;
    private readonly StudyDefinition target;
    private readonly CultureInfo culture;
    private readonly UiTextCatalog texts = new();
    private AllocationWorkspaceSnapshot? sourceSnapshot, targetSnapshot;
    private bool previewSucceeded;
    [ObservableProperty] private StudyOption? source;
    [ObservableProperty] private bool replaceConflicts;
    [ObservableProperty] private string feedback = "";
    public AllocationChangeReceipt? Receipt { get; private set; }
    public ObservableCollection<StudyOption> Sources { get; } = [];
    public ObservableCollection<AllocationReuseChoice> Items { get; } = [];
    public string Title => L("Reuse.Title");
    public string Help => L("Reuse.Help");
    public string SourceLabel => L("Reuse.Source");
    public string PreviewLabel => L("Activity.Preview");
    public string AdoptLabel => L("Reuse.Adopt");
    public string CancelLabel => L("Activity.Cancel");
    public string ReplaceLabel => L("Reuse.Replace");
    public bool CanPreview => Source is not null;
    public bool HasPendingSourcePreview => CanPreview && !previewSucceeded;
    public bool CanAdopt => sourceSnapshot is not null && Items.Any(i => i.Selected && i.CanSelect) &&
        (ReplaceConflicts || !Items.Any(i => i.Selected && i.Item.Conflict));
    public string TargetLabel => target.Name + " · " + target.PeriodStart + " – " + target.PeriodEnd;
    public string L(string key) => texts.Get(key, culture);
    public AllocationReuseEditor(IAllocationWorkspace port, IStudyWorkspace studies, StudyDefinition target, CultureInfo culture)
    {
        this.port = port; this.studies = studies; this.target = target; this.culture = culture;
        foreach (var study in studies.ReadStudies().Where(s => s.CompanyId == target.CompanyId && s.Id != target.Id)
            .OrderByDescending(s => s.PeriodStart.Value))
            Sources.Add(new(study, study.Name + " · " + study.PeriodStart + " – " + study.PeriodEnd));
        if (Sources.Count == 0) Feedback = L("Reuse.NoSources");
    }
    partial void OnSourceChanged(StudyOption? value)
    {
        Items.Clear(); sourceSnapshot = null; targetSnapshot = null; previewSucceeded = false; ReplaceConflicts = false; Feedback = "";
        OnPropertyChanged(nameof(CanPreview)); OnPropertyChanged(nameof(CanAdopt));
        OnPropertyChanged(nameof(HasPendingSourcePreview));
    }
    partial void OnReplaceConflictsChanged(bool value) => OnPropertyChanged(nameof(CanAdopt));
    public void Preview()
    {
        Items.Clear(); sourceSnapshot = null; targetSnapshot = null; previewSucceeded = false;
        if (Source is null) { Feedback = L("Reuse.NoSources"); return; }
        try
        {
            sourceSnapshot = port.ReadAllocations(target.CompanyId, Source.Value.Id);
            targetSnapshot = port.ReadAllocations(target.CompanyId, target.Id);
            var result = AllocationReuse.Preview(sourceSnapshot, targetSnapshot);
            foreach (var item in result.Items)
            {
                var selector = item.Selector;
                if (item.Stage == AllocationStage.Primary)
                {
                    var rule = sourceSnapshot.Configuration.PrimaryRules.Single(r => r.Id == item.RuleId);
                    if (rule.SelectorKind == AccountSelectorKind.Exact)
                    {
                        var descriptions = sourceSnapshot.Resources.Origins.Where(o => o.Code == rule.SelectorValue)
                            .SelectMany(o => o.Descriptions).Distinct(StringComparer.Ordinal).ToArray();
                        selector += " · " + (descriptions.Length == 0 ? L("Reuse.NoDescription") : string.Join(" / ", descriptions));
                    }
                    else selector += "…";
                }
                var choice = new AllocationReuseChoice(item, L("Reuse.Status." + item.Status), selector);
                choice.PropertyChanged += (_, _) => OnPropertyChanged(nameof(CanAdopt));
                Items.Add(choice);
            }
            Feedback = string.Format(culture, L("Reuse.Summary"), result.Items.Count(i => i.Compatible && !i.AlreadyPresent),
                result.Items.Count(i => !i.Compatible), result.Items.Count(i => i.Conflict),
                result.Items.Count(i => i.Unused), result.OmittedAssignments);
            previewSucceeded = true;
        }
        catch (DomainRuleException e) { Feedback = L(e.Diagnostic.MessageKey); }
        catch (CompanyFileException e) { Feedback = L(e.Diagnostic.MessageKey); }
        finally { OnPropertyChanged(nameof(CanAdopt)); OnPropertyChanged(nameof(HasPendingSourcePreview)); }
    }
    public bool Adopt()
    {
        if (!CanAdopt || sourceSnapshot is null || targetSnapshot is null) return false;
        try
        {
            var selection = new AllocationReuseSelection(sourceSnapshot.Resources.Study.Id, sourceSnapshot.Resources.Study.Revision,
                AllocationReuse.Fingerprint(sourceSnapshot),
                Items.Where(i => i.Selected && i.Item.Stage == AllocationStage.Primary).Select(i => i.Item.RuleId).ToArray(),
                Items.Where(i => i.Selected && i.Item.Stage == AllocationStage.Secondary).Select(i => i.Item.RuleId).ToArray(), ReplaceConflicts);
            var config = AllocationReuse.Adopt(sourceSnapshot, targetSnapshot, selection);
            Receipt = port.ChangeAllocations(new(target.CompanyId, target.Id, targetSnapshot.Resources.Study.Revision,
                new(Guid.NewGuid()), "local-user", config) { Reuse = selection });
            return true;
        }
        catch (DomainRuleException e) { Feedback = L(e.Diagnostic.MessageKey); return false; }
        catch (CompanyFileException e) { Feedback = L(e.Diagnostic.MessageKey); return false; }
    }
}
