using Valdrio.Calculation;
using Valdrio.Domain;

namespace Valdrio.Application;

public enum AdjustmentValueMode { Fixed, Percentage, AddAmount }
public enum AdjustmentRuleAction { Save, Apply, Undo }
public sealed record ReusableAdjustmentRule(EntityId Id, EntityId CompanyId, int Version, string Name,
    string AccountPattern, string AnalyticalCode, AdjustmentValueMode Mode, decimal Value,
    CurrencyCode Currency, string Reason, bool Archived = false);
public sealed record AdjustmentRuleOperation(AdjustmentRuleAction Action, ReusableAdjustmentRule Rule,
    CurrencyMetadataSnapshot Metadata, EntityId? ApplicationId = null);
public sealed record AdjustmentAccountEffect(EntityId ResourceId, string Code, Money Before, Money Delta);
public sealed record AdjustmentRulePreview(IReadOnlyList<AdjustmentAccountEffect> Accounts, Money Before, Money Target);
public sealed record AdjustmentAppliedLine(EntityId ResourceId, EntityId AdjustmentId,
    IReadOnlyList<ResourcePartDraft> PreviousParts);
public sealed record AdjustmentRuleApplication(EntityId Id, ReusableAdjustmentRule Rule,
    EntityId GeneratedResourceId, IReadOnlyList<AdjustmentAppliedLine> Lines, EntityId? ReversedBy = null);

// All amounts and identities are derived from the frozen command and workspace, never UI order or clock.
public static class AdjustmentRuleEngine
{
    private static DomainRuleException Error(string suffix) => ResourceWorkspaceRules.Error("ADJ_" + suffix.ToUpperInvariant(), "Adjustment." + suffix);
    public static bool IsNeutralized(ResourceWorkspaceSnapshot workspace, ResourceDraft resource) =>
        resource.Parts.All(p => p.Amount.Amount == 0m) &&
        workspace.Preparation.Coverage.Single(c => c.ResourceId == resource.Id).Effective.Amount == 0m &&
        (workspace.Ledger.AdjustmentApplications?.Any(a =>
            a.ReversedBy is null && a.Lines.Any(l => l.ResourceId == resource.Id) && a.GeneratedResourceId != resource.Id ||
            a.ReversedBy is not null && a.GeneratedResourceId == resource.Id) == true);
    public static Money RuleTotal(ResourceWorkspaceSnapshot before, EntityId resourceId)
    {
        var ids = (before.Ledger.AdjustmentApplications ?? []).SelectMany(a => a.Lines).Where(l => l.ResourceId == resourceId).Select(l => l.AdjustmentId).ToHashSet();
        var resource = before.Resources.Single(r => r.Id == resourceId);
        return new(resource.Adjustments.Where(a => ids.Contains(a.Id) || a.ReversalOfId is { } id && ids.Contains(id))
            .Sum(a => a.Conversion.ConvertedOriginalMoney.Amount), before.Study.Currency.FunctionalCurrencyCode, before.Precision);
    }
    public static void Validate(ReusableAdjustmentRule rule)
    {
        rule.Id.EnsureValid(); rule.CompanyId.EnsureValid();
        if (rule.Version < 1 || string.IsNullOrWhiteSpace(rule.Name) || string.IsNullOrWhiteSpace(rule.Reason) ||
            string.IsNullOrWhiteSpace(rule.AnalyticalCode) || rule.AnalyticalCode.Contains('*') ||
            string.IsNullOrWhiteSpace(rule.AccountPattern) || rule.AccountPattern == "*" ||
            rule.AccountPattern.Count(c => c == '*') > 1 || rule.AccountPattern.TrimEnd('*').Contains('*') || !Enum.IsDefined(rule.Mode)) throw Error("Invalid");
    }
    public static AdjustmentRulePreview Preview(ResourceWorkspaceSnapshot before, ReusableAdjustmentRule rule)
    {
        Validate(rule);
        if (rule.CompanyId != before.Study.CompanyId || rule.Currency != before.Study.Currency.FunctionalCurrencyCode || rule.Archived)
            throw Error("Invalid");
        var prefix = rule.AccountPattern.EndsWith('*');
        var pattern = prefix ? rule.AccountPattern[..^1] : rule.AccountPattern;
        var ids = before.Origins.Where(o => o.AnalyticalName is null && (prefix ? o.Code.StartsWith(pattern, StringComparison.Ordinal) : o.Code == pattern))
            .OrderBy(o => o.Code, StringComparer.Ordinal).ToArray();
        if (ids.Length == 0) throw Error("NoAccounts");
        var active = before.Ledger.AdjustmentApplications?.Where(a => a.ReversedBy is null).ToArray() ?? [];
        if (active.Any(a => a.Rule.Id == rule.Id || a.Lines.Any(l => ids.Any(o => o.Id == l.ResourceId)))) throw Error("Conflict");
        if (before.Origins.Any(o => o.Code == rule.AnalyticalCode &&
            !before.Resources.Any(r => r.Id == o.Id && r.AnalyticalOrigin is not null &&
                before.Ledger.AdjustmentApplications?.Any(a => a.GeneratedResourceId == r.Id && a.Rule.Id == rule.Id && a.ReversedBy is not null) == true &&
                before.Preparation.Coverage.Single(c => c.ResourceId == r.Id).Effective.Amount == 0m))) throw Error("CodeUsed");
        var lines = ids.Select(o =>
        {
            var amount = before.Preparation.Coverage.Single(c => c.ResourceId == o.Id).Effective;
            return new AdjustmentAccountEffect(o.Id, o.Code, amount, new Money(-amount.Amount, amount.Currency, before.Precision));
        }).ToArray();
        var total = lines.Aggregate(0m, (sum, l) => checked(sum + l.Before.Amount));
        var target = rule.Mode switch
        {
            AdjustmentValueMode.Fixed => rule.Value,
            AdjustmentValueMode.Percentage => checked(total * rule.Value / 100m),
            AdjustmentValueMode.AddAmount => checked(total + rule.Value),
            _ => throw Error("Invalid")
        };
        target = decimal.Round(target, before.Precision.AmountScale, MidpointRounding.ToEven);
        return new(FrozenList.Copy(lines), new(total, rule.Currency, before.Precision), new(target, rule.Currency, before.Precision));
    }
    public static ResourceLedger Apply(ResourceWorkspaceSnapshot before, ResourceMutation command)
    {
        var op = command.AdjustmentRule ?? throw Error("Invalid");
        Validate(op.Rule); op.Metadata.EnsureValid();
        if (!Enum.IsDefined(op.Action) || op.Rule.CompanyId != before.Study.CompanyId ||
            op.Metadata.AlphaCode != before.Study.Currency.FunctionalCurrencyCode || !op.Metadata.IsMonetary ||
            command.Adjustments.Count != 0 || command.Parts.Count != 0 || command.ResourceId is not null ||
            command.CostObject is not null || command.RateSet is not null || command.RateChoices.Count != 0 ||
            command.Replacements.Count != 0 || command.NodeEdits is not null) throw Error("Invalid");
        if (op.Action == AdjustmentRuleAction.Save)
        {
            if (op.ApplicationId is not null) throw Error("Invalid");
            return before.Ledger;
        }
        var entries = before.Resources.ToList();
        var applications = before.Ledger.AdjustmentApplications?.ToList() ?? [];
        EntityId Id(string kind, EntityId resource) => ResourceOrigins.StableId(kind, command.RequestId + ":" + resource);
        if (op.Action == AdjustmentRuleAction.Apply)
        {
            if (op.ApplicationId is not null) throw Error("Invalid");
            var preview = Preview(before, op.Rule);
            var applied = new List<AdjustmentAppliedLine>();
            foreach (var line in preview.Accounts)
            {
                var resource = entries.Single(r => r.Id == line.ResourceId);
                var deltaId = Id("rule-compensation", resource.Id);
                var delta = ResourcePreparationRules.CreateAdjustment(deltaId, line.Delta, op.Rule.Currency, null,
                    "Analytical substitution", op.Rule.Reason, deltaId) with { CurrencyMetadata = op.Metadata };
                // Keep part identities/routing, but neutralize every component, not only the net pending amount.
                var parts = resource.Parts.Select(p => ZeroPart(p, before)).ToArray();
                entries.Remove(resource);
                entries.Add(new ResourceDraft(resource.Id, resource.Sources, resource.Adjustments.Append(delta), parts));
                applied.Add(new(resource.Id, deltaId, resource.Parts));
            }
            var previous = entries.SingleOrDefault(r => r.AnalyticalOrigin?.Code == op.Rule.AnalyticalCode);
            var generatedId = previous?.Id ?? Id("analytical-resource", op.Rule.Id);
            if (previous is not null) entries.Remove(previous);
            var generatedDeltaId = Id("analytical-value", generatedId);
            var generated = ResourcePreparationRules.CreateAdjustment(generatedDeltaId, preview.Target, op.Rule.Currency, null,
                "Analytical cost", op.Rule.Reason, generatedDeltaId) with { CurrencyMetadata = op.Metadata };
            entries.Add(new ResourceDraft(generatedId, [], (previous?.Adjustments ?? []).Append(generated),
                (previous?.Parts.Select(p => ZeroPart(p, before)) ?? []).Append(new ResourcePartDraft(Id("analytical-pending", generatedId), preview.Target, ResourceRoutingKind.Pending)))
                { AnalyticalOrigin = new(command.RequestId, op.Rule.AnalyticalCode, op.Rule.Name) });
            applied.Add(new(generatedId, generatedDeltaId, []));
            applications.Add(new(command.RequestId, op.Rule, generatedId, FrozenList.Copy(applied)));
        }
        else
        {
            var application = applications.SingleOrDefault(a => a.Id == op.ApplicationId && a.ReversedBy is null) ?? throw Error("NotApplied");
            if (application.Rule != op.Rule) throw Error("Invalid");
            foreach (var line in application.Lines)
            {
                var resource = entries.Single(r => r.Id == line.ResourceId);
                var original = resource.Adjustments.Single(a => a.Id == line.AdjustmentId);
                var reversalId = Id("rule-reversal", resource.Id);
                var reversal = ResourcePreparationRules.ReverseAdjustment(original, reversalId, command.Reason, reversalId);
                var effective = before.Preparation.Coverage.Single(c => c.ResourceId == resource.Id).Effective + reversal.Conversion.ConvertedOriginalMoney;
                // Restore the original distribution. Independent later monetary corrections stay pending explicitly.
                var parts = line.PreviousParts.Concat(resource.Parts.Where(p => !line.PreviousParts.Any(old => old.Id == p.Id)).Select(p => ZeroPart(p, before))).ToList();
                var restored = parts.Aggregate(0m, (sum, p) => checked(sum + p.Amount.Amount));
                var remainder = effective.Amount - restored;
                if (parts.Count == 0 || remainder != 0m) parts.Add(new(Id("undo-pending", resource.Id), new(remainder, effective.Currency, before.Precision), ResourceRoutingKind.Pending));
                entries.Remove(resource);
                entries.Add(new ResourceDraft(resource.Id, resource.Sources, resource.Adjustments.Append(reversal), parts) { AnalyticalOrigin = resource.AnalyticalOrigin });
            }
            applications[applications.IndexOf(application)] = application with { ReversedBy = command.RequestId };
        }
        var ledger = new ResourceLedger(entries, before.Ledger.Objects, before.Ledger.NodeEdits) { AdjustmentApplications = FrozenList.Copy(applications) };
        var result = new ResourceWorkspaceSnapshot(before.Study, before.Precision, before.Model, before.Origins, ledger);
        if (!result.Preparation.IsValid) throw new DomainRuleException(result.Preparation.Diagnostics[0]);
        return ledger;
    }
    private static ResourcePartDraft ZeroPart(ResourcePartDraft p, ResourceWorkspaceSnapshot before) =>
        new(p.Id, new(0, p.Amount.Currency, before.Precision), p.Routing,
            p.DirectAssignments.Select(a => a with { Amount = new(0, a.Amount.Currency, before.Precision) }), p.PoolId, p.ExclusionReason, p.Classification);
}
