using System.Text.Json.Serialization;
using Valdrio.Domain;

namespace Valdrio.Application;

// Persisted codes are invariant: translated captions belong to the presentation layer.
public enum PhysicalObservationState { Draft, Confirmed, Annulled }
public enum PhysicalEvidenceQuality { Recorded, Declared, Calculated }

public sealed record PhysicalEvidence(PhysicalEvidenceQuality Quality, string Source, string Description)
{
    public void EnsureValid()
    {
        if (!Enum.IsDefined(Quality) || string.IsNullOrWhiteSpace(Source) || Description is null)
            throw PhysicalActivityRules.Error("ACT_EVIDENCE_REQUIRED", "Activity.EvidenceRequired");
        if (Source.Length > 500 || Description.Length > 4000)
            throw PhysicalActivityRules.Error("ACT_EVIDENCE_TOO_LONG", "Activity.EvidenceTooLong");
    }
}

// One full, immutable version of the physical row; real and normal are never inferred from each other.
// A NULL quantity is unknown, whereas zero is a documented measurement or reference.
public sealed record PhysicalObservation(
    EntityId Id, EntityId CompanyId, EntityId StudyId, EntityId ModelVersionId,
    EntityId NodeId, EntityId PoolId, UnitId UnitId,
    BusinessDate PeriodStart, BusinessDate PeriodEnd, long Version,
    PhysicalObservationState State, decimal? TheoreticalQuantity, decimal? PracticalQuantity,
    decimal? NormalQuantity, decimal? RealQuantity,
    PhysicalEvidence? NormalEvidence, PhysicalEvidence? RealEvidence,
    string? NormalZeroReason, string? ExceptionReason, string? CorrectionReason,
    string Actor, UtcInstant RecordedAtUtc)
{
    [JsonIgnore]
    public decimal? UtilizationPercent =>
        State == PhysicalObservationState.Annulled ? null : PhysicalActivityRules.Utilization(NormalQuantity, RealQuantity);
    [JsonIgnore]
    public bool ExceedsNormal => NormalQuantity is > 0 && RealQuantity > NormalQuantity;
    [JsonIgnore]
    public bool ExceptionalActivity => NormalQuantity == 0m && RealQuantity > 0m;
}

public sealed record SavePhysicalObservation(
    EntityId CompanyId, EntityId StudyId, long ExpectedStudyRevision,
    EntityId NodeId, EntityId PoolId, long ExpectedPhysicalVersion,
    PhysicalObservationState State, decimal? TheoreticalQuantity, decimal? PracticalQuantity,
    decimal? NormalQuantity, decimal? RealQuantity,
    PhysicalEvidence? NormalEvidence, PhysicalEvidence? RealEvidence,
    string? NormalZeroReason, string? ExceptionReason, string? CorrectionReason, string Actor);

public sealed record AnnulPhysicalObservation(
    EntityId CompanyId, EntityId StudyId, long ExpectedStudyRevision,
    EntityId NodeId, EntityId PoolId,
    long ExpectedPhysicalVersion, string? Reason, string Actor);

public interface IPhysicalActivityWorkspace
{
    IReadOnlyList<PhysicalObservation> ReadCurrent(EntityId companyId, EntityId studyId, CancellationToken cancellationToken = default);
    IReadOnlyList<PhysicalObservation> ReadHistory(EntityId companyId, EntityId studyId, EntityId poolId, CancellationToken cancellationToken = default);
    // Read-only source catalogue. Only exact pool identity is automatically comparable.
    IReadOnlyList<PhysicalObservation> ReadReferences(EntityId companyId, EntityId studyId, CancellationToken cancellationToken = default);
    PhysicalObservation Save(SavePhysicalObservation command, CancellationToken cancellationToken = default);
    IReadOnlyList<PhysicalObservation> ApplyImport(ApplyPhysicalImport command, CancellationToken cancellationToken = default);
    PhysicalObservation Annul(AnnulPhysicalObservation command, CancellationToken cancellationToken = default);
}

public static class PhysicalActivityRules
{
    public static decimal? Utilization(decimal? normal, decimal? real) =>
        normal is > 0 && real is >= 0 ? checked(real.Value / normal.Value * 100m) : null;
    public static DomainRuleException Error(string code, string key) => new(new(code, key, []));

    public static void Validate(SavePhysicalObservation input, PrecisionPolicy precision)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!Enum.IsDefined(input.State) || input.State == PhysicalObservationState.Annulled
            || input.ExpectedPhysicalVersion < 0 || input.ExpectedStudyRevision < 0 || string.IsNullOrWhiteSpace(input.Actor) || input.Actor.Length > 300)
            throw Error("ACT_INPUT_INVALID", "Activity.InputInvalid");

        foreach (var quantity in new[] { input.TheoreticalQuantity, input.PracticalQuantity, input.NormalQuantity, input.RealQuantity })
        {
            if (!quantity.HasValue) continue;
            if (quantity.Value < 0) throw Error("ACT_NEGATIVE_QUANTITY", "Activity.NegativeQuantity");
            precision.ValidateQuantity(quantity.Value, nameof(input.NormalQuantity));
        }
        if (input.TheoreticalQuantity is decimal theoretical && input.PracticalQuantity is decimal practical && practical > theoretical)
            throw Error("ACT_CAPACITY_ORDER", "Activity.CapacityOrder");

        // Drafts may retain incomplete source descriptions. Confirmation is the trust boundary:
        // both physical quantities must then have identifiable, nonempty provenance.
        foreach (var evidence in new[] { input.NormalEvidence, input.RealEvidence })
        {
            if (evidence is null) continue;
            if (!Enum.IsDefined(evidence.Quality) || evidence.Source is null ||
                evidence.Description is null || evidence.Source.Length > 500 ||
                evidence.Description.Length > 4000)
                throw Error("ACT_EVIDENCE_TOO_LONG", "Activity.EvidenceTooLong");
        }
        if (input.NormalZeroReason?.Length > 4000 || input.ExceptionReason?.Length > 4000 ||
            input.CorrectionReason?.Length > 4000)
            throw Error("ACT_INPUT_INVALID", "Activity.InputInvalid");

        if (input.State != PhysicalObservationState.Confirmed) return;
        if (input.NormalQuantity is null || input.RealQuantity is null)
            throw Error("ACT_INCOMPLETE", "Activity.Incomplete");
        if (input.NormalEvidence is null || input.RealEvidence is null)
            throw Error("ACT_EVIDENCE_REQUIRED", "Activity.EvidenceRequired");
        input.NormalEvidence.EnsureValid();
        input.RealEvidence.EnsureValid();

    }

    public static void EnsureCorrectionReason(PhysicalObservation? latest, string? reason)
    {
        // Corrections are versioned with actor, timestamp and before/after values.
        // A narrative is optional; retain the storage length contract.
        if (reason?.Length > 4000)
            throw Error("ACT_INPUT_INVALID", "Activity.InputInvalid");
    }
}

// Pure physical planning: no costs, T12 solver, locale-sensitive parsing or hidden defaults.
public sealed record PhysicalCalendarInput(
    int OperatingDays, decimal ShiftsPerDay, decimal HoursPerShift, decimal EquivalentResources,
    decimal PlannedStoppageHours, decimal NormalFactor, string? FactorJustification);
public sealed record PhysicalCalendarProposal(decimal Theoretical, decimal Practical, decimal Normal);
public sealed record PhysicalHistoryPeriod(BusinessDate Start, BusinessDate End, decimal RealQuantity,
    bool Include, string? ExclusionReason);

public static class PhysicalNormalCalculator
{
    public static PhysicalCalendarProposal Calendar(PhysicalCalendarInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.OperatingDays < 0 || input.ShiftsPerDay <= 0 || input.HoursPerShift <= 0 ||
            input.EquivalentResources <= 0 || input.PlannedStoppageHours < 0 ||
            input.HoursPerShift > 24 || input.ShiftsPerDay > 24 / input.HoursPerShift ||
            input.NormalFactor is < 0 or > 1 || input.FactorJustification?.Length > 4000)
            throw PhysicalActivityRules.Error("ACT_CALENDAR_INVALID", "Activity.CalendarInvalid");
        var theoretical = checked(input.OperatingDays * input.ShiftsPerDay * input.HoursPerShift * input.EquivalentResources);
        if (input.PlannedStoppageHours > theoretical)
            throw PhysicalActivityRules.Error("ACT_CALENDAR_INVALID", "Activity.CalendarInvalid");
        var practical = theoretical - input.PlannedStoppageHours;
        return new(theoretical, practical, checked(practical * input.NormalFactor));
    }

    // The analyst, not an algorithm, selects representative comparable periods.
    // No extrapolation, outlier removal or default weights are permitted.
    public static decimal History(IReadOnlyList<PhysicalHistoryPeriod> periods)
    {
        ArgumentNullException.ThrowIfNull(periods);
        if (periods.Count == 0 || periods.Any(p => p.RealQuantity < 0 ||
            p.Start.Value > p.End.Value))
            throw PhysicalActivityRules.Error("ACT_HISTORY_INVALID", "Activity.HistoryInvalid");
        var included = periods.Where(p => p.Include).ToArray();
        if (included.Length == 0 || included.Select(p => p.End.Value.DayNumber - p.Start.Value.DayNumber + 1).Distinct().Count() != 1)
            throw PhysicalActivityRules.Error("ACT_HISTORY_NOT_COMPARABLE", "Activity.HistoryNotComparable");
        return checked(included.Sum(p => p.RealQuantity) / included.Length);
    }
}
