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
    // The integrated editor has one source and description for the normal value.
    public string DeclaredSource { get => row.NormalSource; set { row.NormalSource = value; OnPropertyChanged(); } }
    public string DeclaredDescription { get => row.NormalDescription; set { row.NormalDescription = value; OnPropertyChanged(); } }
    [ObservableProperty] private string proposed = "";
    [ObservableProperty] private string feedback = "";
    [ObservableProperty] private string calculationSteps = "";
    [ObservableProperty] private bool canAdopt;
    private bool initialized;
    [ObservableProperty] private bool reviewExpanded = true;
    [ObservableProperty] private bool detailsExpanded;
    [ObservableProperty] private bool useProposal;
    private decimal? proposedNumber;
    public string NormalValue
    {
        get => UseProposal && proposedNumber.HasValue ? proposedNumber.Value.ToString(culture) : row.NormalText;
        set { UseProposal = false; row.NormalText = value; DeclaredQuantity = value; NotifyGuided(); }
    }
    public string RealValue { get => row.RealText; set { row.RealText = value; NotifyGuided(); } }
    public string QuantityUnit => host.Units.Single(u => u.Id == host.Structure.Pools.Single(p => p.Id == row.PoolId).DriverUnitId).Symbol;
    public string NormalFieldLabel => L("Activity.Guided.Normal") + " (" + QuantityUnit + ")";
    public string RealFieldLabel => L("Activity.Guided.Real") + " (" + QuantityUnit + ")";
    public string NormalHelp => L("Activity.Guided.NormalHelp");
    public string RealHelp => L("Activity.Guided.RealHelp");
    public string ReviewLabelText => L("Activity.Guided.Review");
    public string DetailsLabel => L("Activity.Guided.Details");
    public string MethodLabel => L("Activity.Guided.Method");
    public string MethodHelp => L("Activity.Guided.MethodHelp");
    public string UseProposalLabel => L("Activity.Guided.UseProposal");
    public string NormalSourceLabel => L("Activity.Guided.NormalSource");
    public string RealSourceLabel => L("Activity.Guided.RealSource");
    public string SourceHelp => L("Activity.Guided.SourceHelp");
    public string NormalNotesLabel => L("Activity.Guided.NormalNotes");
    public string RealNotesLabel => L("Activity.Guided.RealNotes");
    public string NotesLabel => L("Activity.Guided.Notes");
    public string NotesHelp => L("Activity.Guided.NotesHelp");
    public string ProvenanceSummary => UseProposal ? L("Activity.Guided.Proposed") :
        string.IsNullOrWhiteSpace(row.NormalSource) ? L("Activity.Guided.Manual") : row.NormalSource;
    public string RealProvenance => string.IsNullOrWhiteSpace(row.RealText) ? L("Activity.Guided.NoRecords") :
        string.IsNullOrWhiteSpace(row.RealSource) ? L("Activity.Guided.Manual") : row.RealSource;
    public string InferenceSummary { get; private set; } = "";
    public bool ShowZeroNote => ParseOptional(NormalValue) == 0;
    public bool ShowExceptionNote => ParseOptional(NormalValue) is { } normal && ParseOptional(RealValue) > normal;
    public bool ShowCorrectionNote => row.Version > 0 && row.HasChanges;
    public string ReviewWarning => ShowExceptionNote ? L("Activity.Guided.AboveNormal") : ShowZeroNote ? L("Activity.UtilizationUndefined") : "";
    public string UtilizationSummary
    {
        get
        {
            try { return PhysicalActivityRules.Utilization(ParseOptional(NormalValue), ParseOptional(RealValue)) is { } percent
                ? host.UtilizationLabel + ": " + percent.ToString("0.##", culture) + " %" : ""; }
            catch (OverflowException) { return L("Activity.InputInvalid"); }
        }
    }
    public string Readiness => row.SavedState == PhysicalObservationState.Confirmed && !row.HasChanges && !UseProposal
        ? L("Activity.Guided.Confirmed") : NormalError.Length > 0 ? NormalError : RealError.Length > 0 ? RealError :
        L(string.IsNullOrWhiteSpace(NormalValue) ? "Activity.Guided.MissingNormal" :
          string.IsNullOrWhiteSpace(RealValue) ? "Activity.Guided.MissingReal" : "Activity.Guided.Ready");
    private decimal? ParseOptional(string text) => decimal.TryParse(text, NumberStyles.AllowDecimalPoint |
        NumberStyles.AllowLeadingSign, culture, out var n) ? n : null;
    private string InputError(string text) => string.IsNullOrWhiteSpace(text) ? "" :
        ParseOptional(text) is not { } n ? L("Activity.InputInvalid") : n < 0 ? L("Activity.NegativeQuantity") : "";
    private string CalendarError(string value, string label, decimal minimum, decimal maximum)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        if (ParseOptional(value) is not { } number)
            return string.Format(culture, L("Activity.Assistant.NumberRequired"), label, 1.5m.ToString(culture));
        return number < minimum || number > maximum
            ? string.Format(culture, L("Activity.Assistant.Range"), label, minimum, maximum) : "";
    }
    public string DaysError => CalendarError(OperatingDays, DaysLabel, 0, host.Study.PeriodEnd.Value.DayNumber - host.Study.PeriodStart.Value.DayNumber + 1);
    public string ShiftsError => CalendarError(ShiftsPerDay, ShiftsLabel, 0, 24);
    public string HoursError => CalendarError(HoursPerShift, HoursLabel, 0, 24);
    public string ResourcesError => InputError(EquivalentResources);
    public string StoppagesError => InputError(Stoppages);
    public string FactorError => CalendarError(NormalFactor, FactorLabel, 0, 100);
    public string NormalError => InputError(NormalValue);
    public string RealError => InputError(RealValue);
    public bool CanSave => host.IsWritable && (row.HasChanges || HasPendingProposal || UseProposal);
    public bool CanConfirm => (row.SavedState != PhysicalObservationState.Confirmed || row.HasChanges || UseProposal) && host.IsWritable && !string.IsNullOrWhiteSpace(NormalValue) &&
        !string.IsNullOrWhiteSpace(RealValue) && NormalError.Length == 0 && RealError.Length == 0 &&
        (!UseProposal || CanAdopt);
    public string SaveLabel => L("Activity.Guided.Save");
    public string ConfirmLabel => L("Activity.Guided.Confirm");
    public string HistoryText => host.History();
    public string NormalNotes
    {
        get => row.NormalQuality != PhysicalEvidenceQuality.Calculated && !row.NormalDescription.StartsWith("calendar:", StringComparison.Ordinal) ? row.NormalDescription :
            row.NormalDescription.Split("\nNote: ", 2).ElementAtOrDefault(1) ?? "";
        set => row.NormalDescription = row.NormalQuality != PhysicalEvidenceQuality.Calculated && !row.NormalDescription.StartsWith("calendar:", StringComparison.Ordinal) ? value :
            row.NormalDescription.Split("\nNote: ", 2)[0] + (string.IsNullOrEmpty(value) ? "" : "\nNote: " + value);
    }
    public string NormalKind => string.IsNullOrWhiteSpace(NormalValue) ? L("Activity.Guided.MissingNormal") : host.Label("Activity.Quality." + (UseProposal ? PhysicalEvidenceQuality.Calculated : row.NormalQuality));
    public string RealKind => string.IsNullOrWhiteSpace(RealValue) ? L("Activity.Guided.NoRecords") : host.Label("Activity.Quality." + row.RealQuality);

    private void NotifyGuided()
    {
        foreach (var property in new[] { nameof(NormalValue), nameof(RealValue), nameof(NormalError), nameof(RealError),
            nameof(DaysError), nameof(ShiftsError), nameof(HoursError), nameof(ResourcesError), nameof(StoppagesError), nameof(FactorError), nameof(Readiness), nameof(ShowZeroNote), nameof(ShowExceptionNote), nameof(ShowCorrectionNote),
            nameof(CanSave), nameof(CanConfirm), nameof(ReviewWarning), nameof(UtilizationSummary), nameof(NormalKind), nameof(RealKind), nameof(ProvenanceSummary), nameof(RealProvenance) }) OnPropertyChanged(property);
    }
    partial void OnUseProposalChanged(bool value) { if (initialized) NotifyGuided(); }
    public void Save(bool confirm) => SaveCore(confirm, background: false).GetAwaiter().GetResult();
    public Task<bool> SaveAsync(bool confirm) => SaveCore(confirm, background: true);
    private async Task<bool> SaveCore(bool confirm, bool background)
    {
        if (!host.IsWritable) return false;
        if (UseProposal)
        {
            if (!CanAdopt && confirm) { ReviewExpanded = true; return false; }
            if (CanAdopt) { Adopted = false; Adopt(); if (!Adopted) return false; }
        }
        var version = row.Version;
        if (background) await host.SaveAsync(confirm); else host.Save(confirm);
        var saved = row.Version > version;
        if (saved) { HasPendingProposal = false; UseProposal = false; OnPropertyChanged(nameof(HistoryText)); }
        NotifyGuided();
        return saved;
    }

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
        var pool = host.Structure.Pools.Single(p => p.Id == row.PoolId);
        suggestions = PhysicalActivityInference.Suggest(new(host.Study.CompanyId, host.Study.SelectedModelVersionId,
            row.NodeId, row.PoolId, pool.DriverUnitId, host.Study.PeriodStart, host.Study.PeriodEnd), host.References);
        if (string.IsNullOrWhiteSpace(OperatingDays) && CalendarSupported && suggestions.CalendarReference is { } previous)
        {
            var defaults = PhysicalActivityInference.ReadCalendar(previous.NormalEvidence?.Description)!;
            ShiftsPerDay = defaults.Shifts?.ToString(culture) ?? "";
            HoursPerShift = defaults.Hours?.ToString(culture) ?? "";
            EquivalentResources = defaults.Resources?.ToString(culture) ?? "";
            NormalFactor = (defaults.Factor * 100)?.ToString(culture) ?? "";
            // Period totals/days cannot be copied across calendars. Rates may be proposed.
            if (Enum.TryParse<PhysicalStoppageBasis>(defaults.StopsBasis, out var basis) &&
                basis is PhysicalStoppageBasis.PerResourceShift or PhysicalStoppageBasis.PerResourceDay)
            { StoppageBasis = basis; Stoppages = defaults.StopsEntered?.ToString(culture) ?? ""; }
            InferenceSummary = L("Activity.Guided.Recovered") + " " + previous.PeriodStart + " – " + previous.PeriodEnd;
        }
        if (row.NormalQuality == PhysicalEvidenceQuality.Calculated && row.NormalDescription.Contains(":real=", StringComparison.Ordinal))
            Method = PhysicalCapacityMethod.Historical;
        if (string.IsNullOrWhiteSpace(row.NormalText) && suggestions.ComparableHistory.Count > 0 && string.IsNullOrWhiteSpace(OperatingDays))
            Method = PhysicalCapacityMethod.Historical;
        UseProposal = string.IsNullOrWhiteSpace(row.NormalText) && Method != PhysicalCapacityMethod.Declared;
        initialized = true;
        Preview();
        row.PropertyChanged += RowChanged;
        host.PropertyChanged += HostChanged;
    }

    private void RowChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args) => NotifyGuided();
    private void HostChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    { if (args.PropertyName is nameof(PhysicalActivityEditor.IsBusy)) NotifyGuided(); }
    public void Detach()
    {
        row.PropertyChanged -= RowChanged;
        host.PropertyChanged -= HostChanged;
    }

    private PhysicalActivitySuggestions suggestions = new(null, []);
    private bool historyLoaded;
    private void LoadHistory()
    {
        if (historyLoaded) return;
        foreach (var observation in suggestions.ComparableHistory)
        {
            Historical.Add(new()
            {
                Observation = observation, Include = true, Reviewed = true,
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
    public string Context => row.Node + " · " + QuantityUnit + " · " + host.StudyPeriod;
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
    public bool ManualAuxiliaryVisible => !CalendarVisible;
    public bool DeclaredVisible => Method == PhysicalCapacityMethod.Declared;
    public PhysicalActivityEditor Host => host;
    public string Title => L("Activity.EditActivityCapacity");
    public string DeclaredQuantityLabel => L("Activity.Assistant.ProposedQuantity");
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
    public string DescriptionLabel => L("Activity.Assistant.OptionalDescription");
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
        var description = row.NormalDescription.StartsWith("calendar: days=", StringComparison.Ordinal)
            ? row.NormalDescription : row.CalendarReferenceDescription;
        if (!CalendarSupported || !description.StartsWith("calendar: days=", StringComparison.Ordinal)) return;
        Method = PhysicalCapacityMethod.Calendar;
        var parts = description.Split("; ");
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
        var start = description.IndexOf("; reason=", StringComparison.Ordinal);
        var end = description.LastIndexOf("; stopsBasis=", StringComparison.Ordinal);
        if (start >= 0) FactorReason = description[(start + 9)..(end > start ? end : description.Length)];
    }

    partial void OnMethodChanged(PhysicalCapacityMethod value)
    {
        if (value == PhysicalCapacityMethod.Historical) LoadHistory();
        if (value == PhysicalCapacityMethod.Declared) UseProposal = false;
        Proposed = "";
        OnPropertyChanged(nameof(CalendarVisible));
        OnPropertyChanged(nameof(HistoryVisible));
        OnPropertyChanged(nameof(DeclaredVisible));
        OnPropertyChanged(nameof(ManualAuxiliaryVisible));
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

    private bool Input(string text, string label, decimal minimum, decimal maximum, out decimal value)
    {
        if (!decimal.TryParse(text.Trim(), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, culture, out value))
        { Feedback = string.Format(culture, L("Activity.Assistant.NumberRequired"), label, 1.5m.ToString(culture)); return false; }
        if (value < minimum || value > maximum)
        { Feedback = string.Format(culture, L("Activity.Assistant.Range"), label, minimum, maximum); return false; }
        return true;
    }
    private (decimal Normal, decimal? Theoretical, decimal? Practical, PhysicalEvidence Evidence)? Invalid(string message)
    { Feedback = message; return null; }

    private (decimal Normal, decimal? Theoretical, decimal? Practical, PhysicalEvidence Evidence)? Calculate()
    {
        switch (Method)
        {
            case PhysicalCapacityMethod.Calendar:
            {
                if (!CalendarSupported)
                    return Invalid(L("Activity.CalendarUnit"));
                if (!Input(OperatingDays, DaysLabel, 0,
                    host.Study.PeriodEnd.Value.DayNumber - host.Study.PeriodStart.Value.DayNumber + 1, out var days)) return null;
                if (days != decimal.Truncate(days))
                    return Invalid(L("Activity.Assistant.WholeDays"));
                if (!Input(ShiftsPerDay, ShiftsLabel, 0, 24, out var shifts)) return null;
                if (!Input(HoursPerShift, HoursLabel, 0, 24, out var hours)) return null;
                if (!Input(EquivalentResources, ResourcesLabel, 0, decimal.MaxValue, out var resources)) return null;
                if (shifts == 0 || hours == 0 || resources == 0)
                    return Invalid(L("Activity.Assistant.PositiveCalendar"));
                if (shifts * hours > 24)
                    return Invalid(L("Activity.Assistant.DayLimit"));
                var theoretical = checked(days * shifts * hours * resources);
                if (!Input(Stoppages, StoppagesLabel, 0, decimal.MaxValue, out var enteredStops)) return null;
                var multiplier = StoppageBasis switch
                {
                    PhysicalStoppageBasis.PerResourceShift => checked(days * shifts * resources),
                    PhysicalStoppageBasis.PerResourceDay => checked(days * resources),
                    PhysicalStoppageBasis.PerResourcePeriod => resources,
                    _ => 1m
                };
                var stops = checked(enteredStops * multiplier);
                if (stops > theoretical)
                    return Invalid(string.Format(culture, L("Activity.Stoppages.Exceeds"), stops, theoretical));
                if (!Input(NormalFactor, FactorLabel, 0, 100, out var enteredFactor)) return null;
                var factor = enteredFactor / 100m;
                if (FactorReason.Length > 4000)
                    return Invalid(L("Activity.CalendarInvalid"));
                var parameters = new PhysicalCalendarInput((int)days,
                    shifts, hours, resources, stops, factor, FactorReason);
                var result = PhysicalActivityInference.CalendarSegments([new(host.Study.PeriodStart, host.Study.PeriodEnd, parameters)]);
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
                if (chosen.Length == 0 || chosen.Any(p => p.ReviewReason.Length > 4000))
                    return Invalid(L("Activity.HistoryInvalid"));
                var periods = chosen.Select(p => new PhysicalHistoryPeriod(
                    p.Observation.PeriodStart, p.Observation.PeriodEnd, p.Observation.RealQuantity!.Value,
                    p.Include, p.ReviewReason)).ToArray();
                var duration = host.Study.PeriodEnd.Value.DayNumber - host.Study.PeriodStart.Value.DayNumber + 1;
                if (!periods.Any(p => p.Include) || periods.Any(p => p.Include && p.End.Value.DayNumber - p.Start.Value.DayNumber + 1 != duration))
                    return Invalid(L("Activity.HistoryNotComparable"));
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
                if (!Input(DeclaredQuantity, L("Activity.Normal"), 0, decimal.MaxValue, out var declared)) return null;
                return (declared, null, null,
                    new(PhysicalEvidenceQuality.Declared, string.IsNullOrWhiteSpace(DeclaredSource) ? L("Activity.Guided.Manual") : DeclaredSource.Trim(), DeclaredDescription.Trim()));
            }
            default:
                return Invalid(L("Activity.InputInvalid"));
        }
    }

    public void Preview()
    {
        CanAdopt = false;
        proposedNumber = null;
        Proposed = "";
        CalculationSteps = "";
        try
        {
            if (Calculate() is not { } item) return;
            if (item.Normal < 0) { Feedback = L("Activity.NegativeQuantity"); return; }
            proposedNumber = item.Normal;
            Proposed = item.Normal.ToString(culture) + " " + QuantityUnit;
            Feedback = "";
            CanAdopt = host.IsWritable;
        }
        catch (FormatException e) { Proposed = ""; Feedback = e.Message; }
        catch (DomainRuleException e) { Proposed = ""; Feedback = L(e.Diagnostic.MessageKey); }
        catch (OverflowException) { Proposed = ""; Feedback = L("Activity.CalendarInvalid"); }
        finally { NotifyGuided(); }
    }

    public void Adopt()
    {
        if (!host.IsWritable) return;
        try
        {
            if (Calculate() is not { } item) return;
            if (item.Normal < 0) { Feedback = L("Activity.NegativeQuantity"); return; }
            if (item.Evidence.Source.Length > 500 || item.Evidence.Description.Length > 4000)
                { Feedback = L("Activity.EvidenceTooLong"); return; }
            var note = row.NormalDescription.Split("\nNote: ", 2).ElementAtOrDefault(1);
            row.NormalText = item.Normal.ToString(culture);
            row.NormalSource = item.Evidence.Source;
            row.NormalDescription = item.Evidence.Description + (string.IsNullOrEmpty(note) ? "" : "\nNote: " + note);
            row.NormalQuality = item.Evidence.Quality;
            if (item.Theoretical.HasValue) row.TheoreticalText = item.Theoretical.Value.ToString(culture);
            if (item.Practical.HasValue) row.PracticalText = item.Practical.Value.ToString(culture);
            Adopted = true;
            HasPendingProposal = false;
            host.Feedback = host.Label("Activity.ProposalAdopted") + " · " + row.Node;
            Feedback = "";
        }
        catch (FormatException e) { Feedback = e.Message; }
        catch (DomainRuleException e) { Feedback = L(e.Diagnostic.MessageKey); }
        catch (OverflowException) { Feedback = L("Activity.CalendarInvalid"); }
    }
}
