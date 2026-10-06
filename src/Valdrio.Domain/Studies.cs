namespace Valdrio.Domain;

public enum StudyScenario { Actual = 0, Budget = 1, WhatIf = 2 }
public enum StudyState { Open = 0, Closed = 1 }

public sealed record StudyPurpose(string Code, string PolicyVersion)
{
    public static StudyPurpose Management => new("management", "R1-management-v1");
    public void EnsureValid()
    {
        if (this != Management) throw StudyRules.Error("STUDY_PURPOSE_UNSUPPORTED", "Study.PurposeUnsupported");
    }
}

// Administrative revision and identity are not proxies for economic equality.
public sealed record StudyDefinition
{
    public StudyDefinition(EntityId id, EntityId companyId, string name, BusinessDate periodStart,
        BusinessDate periodEnd, StudyPurpose purpose, StudyScenario scenarioKind, EntityId? parentStudyId,
        EntityId selectedModelVersionId, StudyCurrencyContext currency, CurrencyValidity currencyValidity, long revision, StudyState state,
        EntityId? currentRunId, string? fxRateSetVersionId, UtcInstant createdAtUtc)
    {
        id.EnsureValid(); companyId.EnsureValid(); selectedModelVersionId.EnsureValid(); parentStudyId?.EnsureValid();
        if (string.IsNullOrWhiteSpace(name)) throw StudyRules.Error("STUDY_NAME_REQUIRED", "Study.NameRequired", id);
        if (periodStart.Value > periodEnd.Value) throw StudyRules.Error("STUDY_PERIOD_INVALID", "Study.PeriodInvalid", id);
        ArgumentNullException.ThrowIfNull(purpose); purpose.EnsureValid();
        ArgumentNullException.ThrowIfNull(currency);
        ArgumentNullException.ThrowIfNull(currencyValidity);
        if (!currencyValidity.Contains(periodStart.Value) || !currencyValidity.Contains(periodEnd.Value))
            throw StudyRules.Error("STUDY_CURRENCY_INVALID", "Study.CurrencyInvalid", id);
        if (!currency.CurrencyMetadataSnapshot.IsMonetary) throw StudyRules.Error("STUDY_CURRENCY_INVALID", "Study.CurrencyInvalid", id);
        if (revision < 0 || !Enum.IsDefined(scenarioKind) || !Enum.IsDefined(state))
            throw StudyRules.Error("STUDY_CONTEXT_INVALID", "Study.ContextInvalid", id);
        // No scenario cloning, applied FX or calculation execution exists in this schema.
        if (parentStudyId is not null || currentRunId is not null || fxRateSetVersionId is not null)
            throw StudyRules.Error("STUDY_MODULE_NOT_AVAILABLE", "Study.ModuleNotAvailable", id);
        Id = id; CompanyId = companyId; Name = name; PeriodStart = periodStart; PeriodEnd = periodEnd;
        Purpose = purpose; ScenarioKind = scenarioKind; ParentStudyId = parentStudyId;
        SelectedModelVersionId = selectedModelVersionId; Currency = currency; CurrencyValidity = currencyValidity; Revision = revision;
        State = state; CurrentRunId = currentRunId; FxRateSetVersionId = fxRateSetVersionId; CreatedAtUtc = createdAtUtc;
    }
    public EntityId Id { get; }
    public EntityId CompanyId { get; }
    public string Name { get; }
    public BusinessDate PeriodStart { get; }
    public BusinessDate PeriodEnd { get; }
    public StudyPurpose Purpose { get; }
    public StudyScenario ScenarioKind { get; }
    public EntityId? ParentStudyId { get; }
    public EntityId SelectedModelVersionId { get; }
    public StudyCurrencyContext Currency { get; }
    public CurrencyValidity CurrencyValidity { get; }
    public long Revision { get; }
    public StudyState State { get; }
    public EntityId? CurrentRunId { get; }
    public string? FxRateSetVersionId { get; }
    public UtcInstant CreatedAtUtc { get; }
}

public static class StudyRules
{
    public static DomainRuleException Error(string code, string key, EntityId? entity = null) =>
        new(new(code, key, entity.HasValue ? [new("entityId", DiagnosticArgumentType.Identifier, entity.Value.ToString())] : []));

    public static void CheckRevision(long expected, long actual, EntityId id)
    {
        if (expected != actual) throw Error("STUDY_REVISION_CHANGED", "Study.RevisionChanged", id);
    }
}
