using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Valdrio.Application;
using Valdrio.Domain;
using Valdrio.Localization;

namespace Valdrio.Desktop;

public sealed partial class ResourceWorkspaceViewModel
{
    public bool HasPendingRuleEdits => RuleCards.Any(c => c.IsDirty);
    public string RuleTotalLabel => RuleText("RuleTotal");
    public string ResourceListTitle => HasRuleAdjustments ? RuleText("AccountsAndCosts") : this["ListTitle"];
    public string ResourceListHelp => HasRuleAdjustments ? RuleText("ManualHelp") : this["ListHelp"];
    public string ResourcesTotalRuleAdjustments => SumResourceCoverage(c => AdjustmentRuleEngine.RuleTotal((preview ?? workspace)!, c.ResourceId));
    public bool HasRuleAdjustments => workspace?.Ledger.AdjustmentApplications?.Count > 0;
    public string RuleSectionTitle => RuleText("Title");
    public string RuleSectionHelp => RuleText("Help") + " " + FunctionalCurrency;
    public string RuleAddLabel => RuleText("New");
    public ObservableCollection<AdjustmentRuleCard> RuleCards { get; } = [];
    private IReadOnlyList<ReusableAdjustmentRule> savedRules = [];
    public IRelayCommand AddRuleCommand { get; private set; } = null!;
    private void InitializeRuleCommands() => AddRuleCommand = new RelayCommand(() =>
        { RuleCards.Add(new(this, null, Format)); RuleEdited(); }, () => CanEdit && !IsRevaluing);
    internal string RuleText(string key) => Text("Adjustment." + key);
    internal string RuleFeedback => diagnostic is null ? "" : Status;
    internal string RuleSelectionSummary(string input)
    {
        var pattern = input.Trim();
        if (workspace is null) return RuleText("SelectionHint");
        if (pattern.Length == 0) return RuleText("NoAccounts");
        if (pattern == "*" || pattern.Count(c => c == '*') > 1 || pattern.TrimEnd('*').Contains('*'))
            return RuleText("SelectionHint");
        var prefix = pattern.EndsWith('*');
        var match = prefix ? pattern[..^1] : pattern;
        var ids = workspace.Origins.Where(o => o.AnalyticalName is null &&
            (prefix ? o.Code.StartsWith(match, StringComparison.Ordinal) : o.Code == match)).Select(o => o.Id).ToHashSet();
        if (ids.Count == 0) return RuleText("NoAccounts");
        var amounts = workspace.Preparation.Coverage.Where(c => ids.Contains(c.ResourceId)).ToArray();
        var currency = workspace.Study.Currency.FunctionalCurrencyCode;
        return string.Format(Format, RuleText("SelectionSummary"), ids.Count,
            MoneyText(new Money(amounts.Sum(c => c.ConvertedOriginal.Amount), currency, workspace.Precision)),
            MoneyText(new Money(amounts.Sum(c => c.Effective.Amount), currency, workspace.Precision)));
    }
    internal bool CanEditRule => CanEdit && !dirty && !partDirty && !adjustmentDirty && !objectDirty && !IsRevaluing;
    internal void DuplicateRule(AdjustmentRuleCard source)
    {
        if (!CanEditRule) return;
        var copy = new AdjustmentRuleCard(this, null, source.Culture) { Name = source.Name, Pattern = source.Pattern,
            Code = source.Code, Value = source.Value, Mode = source.Mode, Reason = source.Reason };
        RuleCards.Add(copy); RuleEdited();
    }
    internal void RuleEdited() { OnPropertyChanged(nameof(HasPendingRuleEdits)); OnPropertyChanged(nameof(IsDirty)); RefreshCommands(); }
    private void RebuildRuleCards()
    {
        var drafts = RuleCards.Where(c => c.IsDirty).ToArray();
        RuleCards.Clear();
        foreach (var draft in drafts) RuleCards.Add(draft);
        var active = workspace?.Ledger.AdjustmentApplications?.Where(a => a.ReversedBy is null).ToArray() ?? [];
        foreach (var rule in savedRules.Where(r => !drafts.Any(c => c.Id == r.Id) && (!r.Archived || active.Any(a => a.Rule.Id == r.Id))))
            RuleCards.Add(new(this, rule, Format));
    }
    internal AdjustmentRuleApplication? ActiveApplication(EntityId id) => workspace?.Ledger.AdjustmentApplications?.SingleOrDefault(a => a.Rule.Id == id && a.ReversedBy is null);
    internal ReusableAdjustmentRule BuildRule(AdjustmentRuleCard card, bool archived = false)
    {
        if (workspace is null || !new DecimalEditSession(card.Culture).TryCommit(card.Value, out var value))
            throw ResourceWorkspaceRules.Error("RES_NUMBER_REQUIRED", "Resource.NumberRequired");
        return new(card.Id, workspace.Study.CompanyId, (card.Saved?.Version ?? 0) + 1,
            card.Name.Trim(), card.Pattern.Trim(), card.Code.Trim(), card.Mode, value,
            workspace.Study.Currency.FunctionalCurrencyCode, card.Reason.Trim(), archived);
    }
    internal async Task SaveRule(AdjustmentRuleCard card, bool archive = false)
    {
        if (!CanEditRule || workspace is null) return;
        if (archive && card.Saved is null) { RuleCards.Remove(card); Refresh(); return; }
        await Run(async token =>
        {
            var rule = archive ? card.Saved! with { Version = card.Saved!.Version + 1, Archived = true } : BuildRule(card);
            AdjustmentRuleEngine.Validate(rule);
            await Commit(RuleCommand(rule, AdjustmentRuleAction.Save), token);
            savedRules = await Task.Run(() => company.ActiveResources!.ReadAdjustmentRules(workspace!.Study.CompanyId, token), token);
            card.Accept(rule); RebuildRuleCards();
        });
    }
    private ResourceMutation RuleCommand(ReusableAdjustmentRule rule, AdjustmentRuleAction action, EntityId? application = null) =>
        new(ResourceMutationKind.AdjustmentRule, workspace!.Study.CompanyId, workspace.Study.Id, workspace.Study.Revision,
            new(Guid.NewGuid()), "local-user", rule.Reason, null, [], [], null, null, [], [])
        { AdjustmentRule = new(action, rule, catalog.GetRequired(workspace.Study.Currency.FunctionalCurrencyCode).Freeze(), application) };
    internal void PrepareRule(AdjustmentRuleCard card, bool undo)
    {
        if (!CanEditRule || workspace is null || card.IsDirty || card.Saved is null) return;
        Try(() =>
        {
            ResourceMutation command;
            string text;
            if (undo)
            {
                var applied = ActiveApplication(card.Id) ?? throw ResourceWorkspaceRules.Error("ADJ_NOTAPPLIED", "Adjustment.NotApplied");
                command = RuleCommand(applied.Rule, AdjustmentRuleAction.Undo, applied.Id);
                text = RuleText("UndoPreview");
            }
            else
            {
                var preview = AdjustmentRuleEngine.Preview(workspace, card.Saved);
                command = RuleCommand(card.Saved, AdjustmentRuleAction.Apply);
                text = preview.Accounts.Count == 0
                    ? string.Format(Format, RuleText("CreatePreview"), MoneyText(preview.Target), card.Saved.AnalyticalCode)
                    : string.Format(Format, RuleText("ApplyPreview"), MoneyText(preview.Before), MoneyText(preview.Target), card.Saved.AnalyticalCode);
                text += Environment.NewLine + string.Join(Environment.NewLine, preview.Accounts.Select(a =>
                    AccountLabel(workspace.Origins.Single(o => o.Id == a.ResourceId)) + ": " + MoneyText(a.Before) + " → 0; " + MoneyText(a.Delta)));
                if (preview.Accounts.Count > 0) text += Environment.NewLine + RuleText("ManualIncluded");
            }
            _ = ResourceWorkspaceRules.Apply(workspace, command);
            card.Prepare(command, text);
        });
    }
    internal async Task ApplyRuleWithConfirmation(AdjustmentRuleCard card)
    {
        PrepareRule(card, false);
        if (!card.HasPreview) return;
        if (dialogs.ConfirmEconomicChange(RuleText("Apply"), card.PreviewText,
            RuleText("Confirm"), RuleText("Cancel")))
            await ConfirmRule(card);
        else { card.ClearPreview(); card.ShowFeedback("PreviewCancelled"); }
    }
    internal async Task ConfirmRule(AdjustmentRuleCard card)
    {
        if (!CanEditRule || workspace is null || card.Prepared is null || card.IsDirty) return;
        await Run(async token =>
        {
            var action = card.Prepared.AdjustmentRule!.Action;
            await Commit(card.Prepared, token);
            card.ClearPreview();
            card.ShowFeedback(action == AdjustmentRuleAction.Undo ? "UndoDone" : "ApplyDone");
            foreach (var item in RuleCards) item.RefreshState();
        });
    }
}

public sealed class AdjustmentRuleCard : ObservableObject
{
    private readonly ResourceWorkspaceViewModel owner;
    public EntityId Id { get; }
    public CultureInfo Culture { get; }
    public ReusableAdjustmentRule? Saved { get; private set; }
    public ResourceMutation? Prepared { get; private set; }
    private string name, pattern, code, value, reason, preview = "";
    private string? feedbackKey;
    private bool suggestCode;
    private AdjustmentValueMode mode;
    private bool dirty;
    public AdjustmentRuleCard(ResourceWorkspaceViewModel owner, ReusableAdjustmentRule? saved, CultureInfo culture)
    {
        this.owner = owner; Saved = saved; Id = saved?.Id ?? new(Guid.NewGuid()); Culture = culture;
        name = saved?.Name ?? ""; pattern = saved?.AccountPattern ?? ""; code = saved?.AnalyticalCode ?? "";
        value = saved?.Value.ToString("0.########", culture) ?? "0"; reason = saved?.Reason ?? ""; mode = saved?.Mode ?? AdjustmentValueMode.Fixed;
        dirty = saved is null;
        suggestCode = saved is null;
        SaveCommand = new AsyncRelayCommand(() => owner.SaveRule(this), () => owner.CanEditRule && IsDirty);
        DeleteCommand = new AsyncRelayCommand(() => owner.SaveRule(this, true), () => owner.CanEditRule);
        ApplyCommand = new AsyncRelayCommand(() => owner.ApplyRuleWithConfirmation(this), () => owner.CanEditRule && !IsDirty && Saved?.Archived == false && owner.ActiveApplication(Id) is null);
        UndoCommand = new RelayCommand(() => owner.PrepareRule(this, true), () => owner.CanEditRule && !IsDirty && owner.ActiveApplication(Id) is not null);
        ConfirmCommand = new AsyncRelayCommand(() => owner.ConfirmRule(this), () => owner.CanEditRule && Prepared is not null && !IsDirty);
        CancelCommand = new RelayCommand(() => { ClearPreview(); ShowFeedback("PreviewCancelled"); });
        DuplicateCommand = new RelayCommand(() => owner.DuplicateRule(this), () => owner.CanEditRule);
    }
    public string this[string key] => owner.RuleText(key);
    public string Name { get => name; set { Edit(ref name, value); UpdateSuggestedCode(); } }
    public string Pattern { get => pattern; set { Edit(ref pattern, value); UpdateSuggestedCode(); } }
    public string SelectionSummary => owner.RuleSelectionSummary(Pattern);
    public bool HasStatusError => owner.HasStatusError;
    public string Feedback => !string.IsNullOrEmpty(owner.RuleFeedback) ? owner.RuleFeedback :
        feedbackKey is not null ? this[feedbackKey] : IsDirty ? this["SaveFirst"] : "";
    public string Code { get => code; set { suggestCode = false; Edit(ref code, value); } }
    private void UpdateSuggestedCode()
    {
        if (!suggestCode) return;
        static string Slug(string input)
        {
            var normalized = input.Normalize(System.Text.NormalizationForm.FormD);
            var plain = new string(normalized.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
            return System.Text.RegularExpressions.Regex.Replace(plain.ToUpperInvariant(), @"[^A-Z0-9]+", "-").Trim('-');
        }
        var account = Slug(Pattern.Trim().TrimEnd('*'));
        var label = Slug(Name);
        var parts = new[] { "AJ", account, label }.Where(p => p.Length > 0);
        var proposed = account.Length == 0 && label.Length == 0 ? "" : string.Join("-", parts);
        if (proposed.Length > 80) proposed = proposed[..80].TrimEnd('-');
        if (SetProperty(ref code, proposed, nameof(Code))) Changed();
    }
    public string Value { get => value; set => Edit(ref this.value, value); }
    public string Reason { get => reason; set => Edit(ref reason, value); }
    public AdjustmentValueMode Mode { get => mode; set { if (SetProperty(ref mode, value)) Changed(); } }
    public bool IsDirty => dirty;
    public bool CanEdit => owner.CanEditRule;
    public string Header => (string.IsNullOrWhiteSpace(Name) ? this["New"] : Name) + " · " + State;
    public string State => this[IsDirty ? "Draft" : owner.ActiveApplication(Id) is not null ? "Applied" : Saved?.Archived == true ? "Archived" : "Saved"];
    public string PreviewText => preview;
    public bool HasPreview => Prepared is not null;
    public IReadOnlyList<ResourceChoice<AdjustmentValueMode>> Modes => Enum.GetValues<AdjustmentValueMode>().Select(m => new ResourceChoice<AdjustmentValueMode>(m, this[m.ToString()])).ToArray();
    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand DeleteCommand { get; }
    public IAsyncRelayCommand ApplyCommand { get; }
    public IRelayCommand UndoCommand { get; }
    public IAsyncRelayCommand ConfirmCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public IRelayCommand DuplicateCommand { get; }
    private void Edit(ref string field, string text, [System.Runtime.CompilerServices.CallerMemberName] string? property = null)
    { if (SetProperty(ref field, text, property)) Changed(); }
    private void Changed() { dirty = true; feedbackKey = null; ClearPreview(); owner.RuleEdited(); }
    internal void Accept(ReusableAdjustmentRule rule) { Saved = rule; suggestCode = false; dirty = false; ClearPreview(); }
    internal void Prepare(ResourceMutation command, string text) { Prepared = command; preview = text; ShowFeedback("ReviewPreview"); }
    internal void ShowFeedback(string key) { feedbackKey = key; RefreshState(); }
    internal void ClearPreview() { Prepared = null; preview = ""; RefreshState(); }
    internal void RefreshState()
    {
        OnPropertyChanged(string.Empty);
        foreach (var c in new IRelayCommand[] { SaveCommand, DeleteCommand, ApplyCommand, UndoCommand, ConfirmCommand, CancelCommand, DuplicateCommand }) c.NotifyCanExecuteChanged();
    }
}
