using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Valdrio.Application;
using Valdrio.Domain;

namespace Valdrio.Desktop;

// The assistant owns proposals, never persisted observations. Adoption populates the grid cell.
public enum PhysicalCapacityMethod { Calendar, Historical, Declared }

public sealed partial class PhysicalHistoricalChoice : ObservableObject
{
    public required PhysicalObservation Observation { get; init; }
    public required string Label { get; init; }
    [ObservableProperty] private bool include;
    [ObservableProperty] private bool exceptional;
    [ObservableProperty] private string reviewReason = "";
}

public sealed partial class PhysicalCapacityAssistantEditor : ObservableObject
{
    private readonly PhysicalActivityEditor host;
    private readonly PhysicalActivityRow row;
    private readonly CultureInfo culture;
    [ObservableProperty] private PhysicalCapacityMethod method = PhysicalCapacityMethod.Calendar;
    [ObservableProperty] private string operatingDays = "";
    [ObservableProperty] private string shiftsPerDay = "";
    [ObservableProperty] private string hoursPerShift = "";
    [ObservableProperty] private string equivalentResources = "";
    [ObservableProperty] private string stoppages = "";
    [ObservableProperty] private string normalFactor = "";
    [ObservableProperty] private string factorReason = "";
    [ObservableProperty] private string declaredQuantity = "";
    [ObservableProperty] private string declaredSource = "";
    [ObservableProperty] private string declaredDescription = "";
    [ObservableProperty] private string proposed = "";
    [ObservableProperty] private string feedback = "";

    public PhysicalCapacityAssistantEditor(PhysicalActivityEditor host)
    {
        this.host = host;
        row = host.SelectedRow ?? throw new InvalidOperationException("Select a physical pool.");
        culture = host.NumericCulture;
        foreach (var observation in host.Activity.ReadReferences(host.Study.CompanyId, host.Study.Id)
            .Where(o => o.PoolId == row.PoolId && o.RealQuantity is not null))
        {
            Historical.Add(new()
            {
                Observation = observation,
                Label = string.Format(culture, "{0} – {1} · {2} {3} · {4}",
                    observation.PeriodStart, observation.PeriodEnd,
                    observation.RealQuantity, row.Unit, observation.RealEvidence?.Source ?? "")
            });
        }
    }

    public ObservableCollection<PhysicalHistoricalChoice> Historical { get; } = [];
    public bool Adopted { get; private set; }
    public bool CalendarVisible => Method == PhysicalCapacityMethod.Calendar;
    public bool HistoryVisible => Method == PhysicalCapacityMethod.Historical;
    public bool DeclaredVisible => Method == PhysicalCapacityMethod.Declared;
    public string Title => L("Activity.Method");
    public string CalendarLabel => L("Activity.Method.Calendar");
    public string HistoryLabel => L("Activity.Method.Historical");
    public string DeclaredLabel => L("Activity.Method.Declared");
    public string DaysLabel => L("Activity.Calendar.Days");
    public string ShiftsLabel => L("Activity.Calendar.Shifts");
    public string HoursLabel => L("Activity.Calendar.Hours");
    public string ResourcesLabel => L("Activity.Calendar.Resources");
    public string StoppagesLabel => L("Activity.Calendar.Stoppages");
    public string FactorLabel => L("Activity.Calendar.Factor");
    public string FactorReasonLabel => L("Activity.Calendar.FactorReason");
    public string HistoryHelp => L("Activity.History.Help");
    public string ReviewLabel => L("Activity.History.Exceptional");
    public string IncludeLabel => L("Activity.History.Include");
    public string SourceLabel => L("Activity.Source");
    public string QualityLabel => L("Activity.Quality");
    public string PreviewLabel => L("Activity.Preview");
    public string AdoptLabel => L("Activity.Adopt");
    public string CancelLabel => L("Activity.Cancel");
    private string L(string key) => host.Label(key);

    partial void OnMethodChanged(PhysicalCapacityMethod value)
    {
        Proposed = "";
        OnPropertyChanged(nameof(CalendarVisible));
        OnPropertyChanged(nameof(HistoryVisible));
        OnPropertyChanged(nameof(DeclaredVisible));
    }

    private decimal Number(string text)
    {
        if (!decimal.TryParse(text, NumberStyles.Number, culture, out var result))
            throw PhysicalActivityRules.Error("ACT_INPUT_INVALID", "Activity.InputInvalid");
        return result;
    }

    private (decimal Normal, decimal? Theoretical, decimal? Practical, PhysicalEvidence Evidence) Calculate()
    {
        switch (Method)
        {
            case PhysicalCapacityMethod.Calendar:
            {
                var days = Number(OperatingDays);
                if (days != decimal.Truncate(days) || days < 0 || days > int.MaxValue)
                    throw PhysicalActivityRules.Error("ACT_CALENDAR_INVALID", "Activity.CalendarInvalid");
                var parameters = new PhysicalCalendarInput((int)days,
                    Number(ShiftsPerDay), Number(HoursPerShift), Number(EquivalentResources),
                    Number(Stoppages), Number(NormalFactor), FactorReason);
                var result = PhysicalNormalCalculator.Calendar(parameters);
                // Persist the formula inputs in evidence; data remain a proposal until adoption.
                var description = string.Format(CultureInfo.InvariantCulture,
                    "calendar: days={0}; shifts={1}; hours={2}; resources={3}; stops={4}; factor={5}; reason={6}",
                    parameters.OperatingDays, parameters.ShiftsPerDay, parameters.HoursPerShift,
                    parameters.EquivalentResources, parameters.PlannedStoppageHours,
                    parameters.NormalFactor, parameters.FactorJustification);
                return (result.Normal, result.Theoretical, result.Practical,
                    new(PhysicalEvidenceQuality.Calculated, L("Activity.Method.Calendar"), description));
            }
            case PhysicalCapacityMethod.Historical:
            {
                var chosen = Historical.Where(p => p.Include || p.Exceptional).ToArray();
                if (chosen.Length == 0 || chosen.Any(p => p.Exceptional && string.IsNullOrWhiteSpace(p.ReviewReason)))
                    throw PhysicalActivityRules.Error("ACT_HISTORY_INVALID", "Activity.HistoryInvalid");
                var periods = chosen.Select(p => new PhysicalHistoryPeriod(
                    p.Observation.PeriodStart, p.Observation.PeriodEnd, p.Observation.RealQuantity!.Value,
                    p.Include, p.Exceptional ? p.ReviewReason : null)).ToArray();
                var result = PhysicalNormalCalculator.History(periods);
                var refs = string.Join("; ", chosen.Select(p =>
                    string.Format(CultureInfo.InvariantCulture, "{0}/{1}:real={2}:include={3}:reason={4}:version={5}",
                        p.Observation.StudyId, p.Observation.PeriodStart, p.Observation.RealQuantity,
                        p.Include, p.ReviewReason, p.Observation.Version)));
                return (result, null, null,
                    new(PhysicalEvidenceQuality.Calculated, L("Activity.Method.Historical"), refs));
            }
            case PhysicalCapacityMethod.Declared:
            {
                if (string.IsNullOrWhiteSpace(DeclaredSource))
                    throw PhysicalActivityRules.Error("ACT_EVIDENCE_REQUIRED", "Activity.EvidenceRequired");
                return (Number(DeclaredQuantity), null, null,
                    new(PhysicalEvidenceQuality.Declared, DeclaredSource.Trim(), DeclaredDescription.Trim()));
            }
            default:
                throw PhysicalActivityRules.Error("ACT_INPUT_INVALID", "Activity.InputInvalid");
        }
    }

    public void Preview()
    {
        try
        {
            var item = Calculate();
            if (item.Normal < 0) throw PhysicalActivityRules.Error("ACT_NEGATIVE_QUANTITY", "Activity.NegativeQuantity");
            Proposed = item.Normal.ToString(culture) + " " + row.Unit;
            Feedback = "";
        }
        catch (DomainRuleException e) { Proposed = ""; Feedback = L(e.Diagnostic.MessageKey); }
        catch (OverflowException) { Proposed = ""; Feedback = L("Activity.CalendarInvalid"); }
    }

    public void Adopt()
    {
        try
        {
            var item = Calculate();
            if (item.Normal < 0) throw PhysicalActivityRules.Error("ACT_NEGATIVE_QUANTITY", "Activity.NegativeQuantity");
            if (item.Evidence.Source.Length > 500 || item.Evidence.Description.Length > 4000)
                throw PhysicalActivityRules.Error("ACT_EVIDENCE_TOO_LONG", "Activity.EvidenceTooLong");
            row.NormalText = item.Normal.ToString(culture);
            row.NormalSource = item.Evidence.Source;
            row.NormalDescription = item.Evidence.Description;
            row.NormalQuality = item.Evidence.Quality;
            if (item.Theoretical.HasValue) row.TheoreticalText = item.Theoretical.Value.ToString(culture);
            if (item.Practical.HasValue) row.PracticalText = item.Practical.Value.ToString(culture);
            Adopted = true;
            Feedback = "";
        }
        catch (DomainRuleException e) { Feedback = L(e.Diagnostic.MessageKey); }
        catch (OverflowException) { Feedback = L("Activity.CalendarInvalid"); }
    }
}
