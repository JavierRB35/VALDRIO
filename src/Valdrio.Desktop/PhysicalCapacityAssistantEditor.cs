using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Valdrio.Application;
using Valdrio.Domain;

namespace Valdrio.Desktop;

// The assistant owns proposals, never persisted observations. Adoption populates the grid cell.
public enum PhysicalCapacityMethod { Calendar, Historical, Declared }
public sealed record PhysicalCapacityMethodOption(PhysicalCapacityMethod Value, string Label);
public enum PhysicalStoppageBasis { PerResourceShift, PerResourceDay, PerResourcePeriod, TotalPeriod }
public sealed record PhysicalStoppageOption(PhysicalStoppageBasis Value, string Label);

public sealed partial class PhysicalHistoricalChoice : ObservableObject
{
    public required PhysicalObservation Observation { get; init; }
    public required string Label { get; init; }
    [ObservableProperty] private bool reviewed;
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
    [ObservableProperty] private PhysicalStoppageBasis stoppageBasis = PhysicalStoppageBasis.PerResourceShift;
    [ObservableProperty] private string normalFactor = "";
    [ObservableProperty] private string factorReason = "";
    [ObservableProperty] private string declaredQuantity = "";
    [ObservableProperty] private string declaredSource = "";
    [ObservableProperty] private string declaredDescription = "";
    [ObservableProperty] private string proposed = "";
    [ObservableProperty] private string feedback = "";
    [ObservableProperty] private string calculationSteps = "";
    [ObservableProperty] private bool canAdopt;
    private bool initialized;

    public PhysicalCapacityAssistantEditor(PhysicalActivityEditor host)
    {
        this.host = host;
        row = host.SelectedRow ?? throw new InvalidOperationException("Select a physical pool.");
        culture = host.NumericCulture;
        DeclaredQuantity = row.NormalText;
        if (row.NormalQuality == PhysicalEvidenceQuality.Declared)
        {
            DeclaredSource = row.NormalSource;
            DeclaredDescription = row.NormalDescription;
        }
        if (!CalendarSupported || row.NormalQuality == PhysicalEvidenceQuality.Declared)
            Method = PhysicalCapacityMethod.Declared;
        RestoreCalendar();
        initialized = true;
        Preview();
    }

    private bool historyLoaded;
    private void LoadHistory()
    {
        if (historyLoaded) return;
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
        foreach (var choice in Historical) choice.PropertyChanged += (_, _) =>
        { HasPendingProposal = true; Preview(); };
        historyLoaded = true;
    }

    public ObservableCollection<PhysicalHistoricalChoice> Historical { get; } = [];
    public IReadOnlyList<PhysicalCapacityMethodOption> Methods =>
    [new(PhysicalCapacityMethod.Calendar, CalendarLabel),
     new(PhysicalCapacityMethod.Historical, HistoryLabel),
     new(PhysicalCapacityMethod.Declared, DeclaredLabel)];
    public bool Adopted { get; private set; }
    public bool HasPendingProposal { get; private set; }
    public IReadOnlyList<PhysicalStoppageOption> StoppageOptions => Enum.GetValues<PhysicalStoppageBasis>()
        .Select(value => new PhysicalStoppageOption(value, L("Activity.Stoppages." + value))).ToArray();
    public string StoppageBasisLabel => L("Activity.Stoppages.Basis");
    private bool CalendarSupported
    {
        get
        {
            var pool = host.Structure.Pools.Single(p => p.Id == row.PoolId);
            var unit = host.Units.Single(u => u.Id == pool.DriverUnitId);
            return unit.Dimension == "time" && unit.Symbol == "h";
        }
    }
    public string Context => row.Node + " · " + row.Unit + " · " + host.StudyPeriod;
    public string ExistingCost => host.CostLabel + ": " + row.Cost;
    public string ProposalHelp => L("Activity.Assistant.Help");
    public string CalendarHelp => L("Activity.Assistant.CalendarHelp");
    public string DaysHelp => L("Activity.Assistant.DaysHelp");
    public string ShiftsHelp => L("Activity.Assistant.ShiftsHelp");
    public string HoursHelp => L("Activity.Assistant.HoursHelp");
    public string ResourcesHelp => L("Activity.Assistant.ResourcesHelp");
    public string StoppagesHelp => L("Activity.Assistant.StoppagesHelp");
    public string FactorHelp => L("Activity.Assistant.FactorHelp");
    public string ResultLabel => L("Activity.Assistant.Result");
    public bool CalendarVisible => Method == PhysicalCapacityMethod.Calendar;
    public bool HistoryVisible => Method == PhysicalCapacityMethod.Historical;
    public bool DeclaredVisible => Method == PhysicalCapacityMethod.Declared;
    public string Title => L("Activity.Method");
    public string NormalLabel => L("Activity.Normal");
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
    public string ReviewedLabel => L("Activity.History.Reviewed");
    public string IncludeLabel => L("Activity.History.Include");
    public string SourceLabel => L("Activity.Source");
    public string QualityLabel => L("Activity.Quality");
    public string PreviewLabel => L("Activity.Preview");
    public string AdoptLabel => L("Activity.Adopt");
    public string CancelLabel => L("Activity.Cancel");
    private string L(string key) => host.Label(key);

    private void RestoreCalendar()
    {
        if (!CalendarSupported || row.NormalQuality != PhysicalEvidenceQuality.Calculated ||
            !row.NormalDescription.StartsWith("calendar: days=", StringComparison.Ordinal)) return;
        var parts = row.NormalDescription.Split("; ");
        var values = parts.Select(p => p.Split('=', 2)).Where(p => p.Length == 2)
            .GroupBy(p => p[0]).ToDictionary(g => g.Key, g => g.First()[1]);
        string Read(string key) => values.TryGetValue(key, out var text) &&
            decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
                ? value.ToString(culture) : "";
        OperatingDays = Read("calendar: days"); ShiftsPerDay = Read("shifts");
        HoursPerShift = Read("hours"); EquivalentResources = Read("resources");
        if (values.TryGetValue("factor", out var factor) && decimal.TryParse(factor,
            NumberStyles.Number, CultureInfo.InvariantCulture, out var fraction))
            NormalFactor = (fraction * 100m).ToString("0.############################", culture);
        StoppageBasis = PhysicalStoppageBasis.TotalPeriod;
        Stoppages = Read("stops");
        if (values.TryGetValue("stopsBasis", out var basis) &&
            Enum.TryParse<PhysicalStoppageBasis>(basis, out var parsed) && Enum.IsDefined(parsed) &&
            Read("stopsEntered") is { Length: > 0 } entered)
        { StoppageBasis = parsed; Stoppages = entered; }
        var start = row.NormalDescription.IndexOf("; reason=", StringComparison.Ordinal);
        var end = row.NormalDescription.LastIndexOf("; stopsBasis=", StringComparison.Ordinal);
        if (start >= 0) FactorReason = row.NormalDescription[(start + 9)..(end > start ? end : row.NormalDescription.Length)];
    }

    partial void OnMethodChanged(PhysicalCapacityMethod value)
    {
        if (value == PhysicalCapacityMethod.Historical) LoadHistory();
        Proposed = "";
        OnPropertyChanged(nameof(CalendarVisible));
        OnPropertyChanged(nameof(HistoryVisible));
        OnPropertyChanged(nameof(DeclaredVisible));
    }

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (initialized && e.PropertyName is nameof(Method) or nameof(OperatingDays) or
            nameof(ShiftsPerDay) or nameof(HoursPerShift) or nameof(EquivalentResources) or
            nameof(Stoppages) or nameof(StoppageBasis) or nameof(NormalFactor) or nameof(FactorReason) or
            nameof(DeclaredQuantity) or nameof(DeclaredSource) or nameof(DeclaredDescription))
        { HasPendingProposal = true; Preview(); }
    }

    private decimal Input(string text, string label, decimal minimum, decimal maximum)
    {
        if (!decimal.TryParse(text.Trim(), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, culture, out var value))
            throw new FormatException(string.Format(culture, L("Activity.Assistant.NumberRequired"), label, 1.5m.ToString(culture)));
        if (value < minimum || value > maximum)
            throw new FormatException(string.Format(culture, L("Activity.Assistant.Range"), label, minimum, maximum));
        return value;
    }

    private decimal Number(string text)
    {
        if (!decimal.TryParse(text.Trim(), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, culture, out var result))
            throw PhysicalActivityRules.Error("ACT_INPUT_INVALID", "Activity.InputInvalid");
        return result;
    }

    private (decimal Normal, decimal? Theoretical, decimal? Practical, PhysicalEvidence Evidence) Calculate()
    {
        switch (Method)
        {
            case PhysicalCapacityMethod.Calendar:
            {
                if (!CalendarSupported)
                    throw PhysicalActivityRules.Error("ACT_CALENDAR_UNIT", "Activity.CalendarUnit");
                var days = Input(OperatingDays, DaysLabel, 0,
                    host.Study.PeriodEnd.Value.DayNumber - host.Study.PeriodStart.Value.DayNumber + 1);
                if (days != decimal.Truncate(days))
                    throw new FormatException(L("Activity.Assistant.WholeDays"));
                var shifts = Input(ShiftsPerDay, ShiftsLabel, 0, 24);
                var hours = Input(HoursPerShift, HoursLabel, 0, 24);
                var resources = Input(EquivalentResources, ResourcesLabel, 0, decimal.MaxValue);
                if (shifts == 0 || hours == 0 || resources == 0)
                    throw new FormatException(L("Activity.Assistant.PositiveCalendar"));
                if (shifts * hours > 24)
                    throw new FormatException(L("Activity.Assistant.DayLimit"));
                var theoretical = checked(days * shifts * hours * resources);
                var enteredStops = Input(Stoppages, StoppagesLabel, 0, decimal.MaxValue);
                var multiplier = StoppageBasis switch
                {
                    PhysicalStoppageBasis.PerResourceShift => checked(days * shifts * resources),
                    PhysicalStoppageBasis.PerResourceDay => checked(days * resources),
                    PhysicalStoppageBasis.PerResourcePeriod => resources,
                    _ => 1m
                };
                var stops = checked(enteredStops * multiplier);
                if (stops > theoretical)
                    throw new FormatException(string.Format(culture, L("Activity.Stoppages.Exceeds"), stops, theoretical));
                var factor = Input(NormalFactor, FactorLabel, 0, 100) / 100m;
                if (string.IsNullOrWhiteSpace(FactorReason))
                    throw new FormatException(L("Activity.Assistant.ReasonRequired"));
                var parameters = new PhysicalCalendarInput((int)days,
                    shifts, hours, resources, stops, factor, FactorReason);
                var result = PhysicalNormalCalculator.Calendar(parameters);
                CalculationSteps = string.Format(culture, L("Activity.Stoppages.Calculation"), enteredStops,
                    StoppageOptions.Single(o => o.Value == StoppageBasis).Label, multiplier, stops) + Environment.NewLine +
                    string.Format(culture, L("Activity.Assistant.Calculation"),
                    days, shifts, hours, resources, result.Theoretical, stops, result.Practical,
                    factor * 100m, result.Normal);
                // Persist the formula inputs in evidence; data remain a proposal until adoption.
                var description = string.Format(CultureInfo.InvariantCulture,
                    "calendar: days={0}; shifts={1}; hours={2}; resources={3}; stops={4}; factor={5}; reason={6}",
                    parameters.OperatingDays, parameters.ShiftsPerDay, parameters.HoursPerShift,
                    parameters.EquivalentResources, parameters.PlannedStoppageHours,
                    parameters.NormalFactor, parameters.FactorJustification) +
                    string.Format(CultureInfo.InvariantCulture, "; stopsBasis={0}; stopsEntered={1}; stopsMultiplier={2}",
                        StoppageBasis, enteredStops, multiplier);
                return (result.Normal, result.Theoretical, result.Practical,
                    new(PhysicalEvidenceQuality.Calculated, L("Activity.Method.Calendar"), description));
            }
            case PhysicalCapacityMethod.Historical:
            {
                var chosen = Historical.ToArray();
                if (chosen.Length == 0 || chosen.Any(p => !p.Reviewed ||
                    ((!p.Include || p.Exceptional) && string.IsNullOrWhiteSpace(p.ReviewReason))))
                    throw PhysicalActivityRules.Error("ACT_HISTORY_INVALID", "Activity.HistoryInvalid");
                var periods = chosen.Select(p => new PhysicalHistoryPeriod(
                    p.Observation.PeriodStart, p.Observation.PeriodEnd, p.Observation.RealQuantity!.Value,
                    p.Include, p.ReviewReason)).ToArray();
                var duration = host.Study.PeriodEnd.Value.DayNumber - host.Study.PeriodStart.Value.DayNumber + 1;
                if (periods.Any(p => p.Include && p.End.Value.DayNumber - p.Start.Value.DayNumber + 1 != duration))
                    throw PhysicalActivityRules.Error("ACT_HISTORY_NOT_COMPARABLE", "Activity.HistoryNotComparable");
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
                if (string.IsNullOrWhiteSpace(DeclaredSource) || string.IsNullOrWhiteSpace(DeclaredDescription))
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
        CanAdopt = false;
        CalculationSteps = "";
        try
        {
            var item = Calculate();
            if (item.Normal < 0) throw PhysicalActivityRules.Error("ACT_NEGATIVE_QUANTITY", "Activity.NegativeQuantity");
            Proposed = item.Normal.ToString(culture) + " " + row.Unit;
            Feedback = "";
            CanAdopt = host.IsWritable;
        }
        catch (FormatException e) { Proposed = ""; Feedback = e.Message; }
        catch (DomainRuleException e) { Proposed = ""; Feedback = L(e.Diagnostic.MessageKey); }
        catch (OverflowException) { Proposed = ""; Feedback = L("Activity.CalendarInvalid"); }
    }

    public void Adopt()
    {
        if (!host.IsWritable) return;
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
            host.Feedback = host.Label("Activity.ProposalAdopted") + " · " + row.Node;
            Feedback = "";
        }
        catch (FormatException e) { Feedback = e.Message; }
        catch (DomainRuleException e) { Feedback = L(e.Diagnostic.MessageKey); }
        catch (OverflowException) { Feedback = L("Activity.CalendarInvalid"); }
    }
}
