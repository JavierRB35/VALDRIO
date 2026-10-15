using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Valdrio.Application;
using Valdrio.Domain;
using Valdrio.Infrastructure;
using Valdrio.Localization;

namespace Valdrio.Desktop;

public sealed partial class PhysicalImportCandidate : ObservableObject
{
    public required PhysicalImportLine Source { get; init; }
    public required string Node { get; init; }
    public required string Unit { get; init; }
    public required string Normal { get; init; }
    public required string Real { get; init; }
    public required string Warning { get; init; }
    public required string Existing { get; init; }
    [ObservableProperty] private bool replace;
}

public sealed partial class PhysicalImportEditor : ObservableObject
{
    private readonly PhysicalActivityEditor host;
    private readonly CultureInfo ui, numeric;
    private readonly UiTextCatalog texts = new();
    private readonly IImportSourceSessionFactory factory = new ImportSourceSessionFactory();
    [ObservableProperty] private string path = "";
    [ObservableProperty] private string selectedSheet = "";
    [ObservableProperty] private string selectedSeparator = ";";
    [ObservableProperty] private string correctionReason = "";
    [ObservableProperty] private string normalZeroReason = "";
    [ObservableProperty] private string exceptionReason = "";
    [ObservableProperty] private string feedback = "";
    [ObservableProperty] private bool busy;

    public PhysicalImportEditor(PhysicalActivityEditor host)
    {
        this.host = host; numeric = host.NumericCulture;
        ui = CultureInfo.GetCultureInfo(host.Label("Activity.UiCulture"));
    }

    public ObservableCollection<PhysicalImportCandidate> Lines { get; } = [];
    public ObservableCollection<string> Sheets { get; } = [];
    public bool CanWrite => !host.IsReadOnly;
    public bool CanApply => CanWrite && !Busy && Lines.Any(l => l.Replace);
    public string Title => Text("Activity.Import.Title");
    public string Help => Text("Activity.Import.Help");
    public string BrowseLabel => Text("Activity.Import.Browse");
    public string PreviewLabel => Text("Activity.Preview");
    public string ApplyLabel => Text("Activity.Import.Apply");
    public string CloseLabel => Text("Activity.Cancel");
    public string SheetLabel => Text("Activity.Import.Sheet");
    public string DelimiterLabel => Text("Activity.Import.Delimiter");
    public string CorrectionLabel => Text("Activity.Correction");
    public string ZeroLabel => Text("Activity.ZeroReason");
    public string ExceptionLabel => Text("Activity.Exception");
    public string ChooseLabel => Text("Activity.Import.Choose");
    public string NodeLabel => Text("Activity.Node");
    public string UnitLabel => Text("Activity.Unit");
    public string NormalLabel => Text("Activity.Normal");
    public string RealLabel => Text("Activity.Real");
    public string WarningLabel => Text("Activity.Import.Warning");
    public string ExistingLabel => Text("Activity.Import.Existing");
    public string[] SeparatorOptions { get; } = [";", ",", "\t"];
    private string Text(string key) => texts.Get(key, ui);

    partial void OnBusyChanged(bool value) => OnPropertyChanged(nameof(CanApply));

    public async Task LoadFile(string file)
    {
        if (Busy) return;
        Busy = true;
        try
        {
            using var session = await factory.OpenAsync(file);
            Path = file;
            Sheets.Clear();
            foreach (var sheet in session.WorksheetNames) Sheets.Add(sheet);
            SelectedSheet = Sheets.FirstOrDefault() ?? "";
            Lines.Clear();
            Feedback = "";
        }
        catch (DomainRuleException e) { Feedback = Text(e.Diagnostic.MessageKey); }
        catch (IOException) { Feedback = Text("Activity.Import.Invalid"); }
        finally { Busy = false; OnPropertyChanged(nameof(CanApply)); }
    }

    public async Task Preview()
    {
        if (Busy || string.IsNullOrWhiteSpace(Path)) return;
        Busy = true; Lines.Clear();
        try
        {
            using var session = await factory.OpenAsync(Path);
            var csv = session.SourceKind == ImportSourceKind.Csv ?
                new CsvSourceDialect("utf-8", SelectedSeparator == "\t" ? '\t' :
                    SelectedSeparator == "," ? ',' : ';', '"', CsvBomPolicy.Optional) : null;
            var settings = new ImportSourceReadSettings(session.SourceKind,
                session.SourceKind == ImportSourceKind.Xlsx ? SelectedSheet : null,
                1, 2, csv);
            var document = await session.ReadAsync(settings);
            var model = host.Structure;
            var preview = PhysicalImportMapper.Preview(document, model, host.Units,
                host.Study, numeric);
            var current = host.Activity.ReadCurrent(host.Study.CompanyId, host.Study.Id)
                .ToDictionary(p => p.PoolId);
            foreach (var line in preview.Lines)
            {
                var problem = line.ProblemKey is not null ? Text(line.ProblemKey) :
                    line.PartialPeriod ? Text("Activity.Import.Partial") :
                    line.Duplicate ? Text("Activity.Import.Duplicate") : "";
                var existing = line.PoolId is { } pool && current.TryGetValue(pool, out var value)
                    ? Text("Activity.Import.Version") + " " + value.Version.ToString(numeric) :
                    Text("Activity.Import.NoExisting");
                var item = new PhysicalImportCandidate
                {
                    Source = line, Node = line.NodeCode, Unit = line.UnitCode,
                    Normal = line.NormalQuantity?.ToString(numeric) ?? "—",
                    Real = line.RealQuantity?.ToString(numeric) ?? "—",
                    Warning = problem, Existing = existing
                };
                item.PropertyChanged += (_, _) => OnPropertyChanged(nameof(CanApply));
                Lines.Add(item);
            }
            Feedback = string.Format(ui, Text("Activity.Import.Summary"),
                preview.Lines.Count, preview.Lines.Count(r => r.CanChoose));
        }
        catch (DomainRuleException e) { Feedback = Text(e.Diagnostic.MessageKey); }
        catch (CompanyFileException e) { Feedback = Text(e.Diagnostic.MessageKey); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException)
        { Feedback = Text("Activity.Import.Invalid"); }
        finally { Busy = false; OnPropertyChanged(nameof(CanApply)); }
    }

    public bool Apply()
    {
        if (!CanApply) return false;
        try
        {
            var chosen = Lines.Where(l => l.Replace).Select(l => l.Source).ToArray();
            if (chosen.Any(l => !l.CanChoose) ||
                chosen.Select(l => l.PoolId).Distinct().Count() != chosen.Length)
                throw PhysicalActivityRules.Error("ACT_IMPORT_DUPLICATE", "Activity.ImportDuplicate");
            var current = host.Activity.ReadCurrent(host.Study.CompanyId, host.Study.Id)
                .ToDictionary(r => r.PoolId);
            var commands = new List<PhysicalImportOperation>();
            foreach (var line in chosen)
            {
                var prior = current.GetValueOrDefault(line.PoolId!.Value);
                if (prior?.State == PhysicalObservationState.Confirmed &&
                    string.IsNullOrWhiteSpace(CorrectionReason))
                    throw PhysicalActivityRules.Error("ACT_CORRECTION_REASON", "Activity.CorrectionReason");
                var normal = line.NormalQuantity ?? prior?.NormalQuantity;
                var real = line.RealQuantity ?? prior?.RealQuantity;
                var normalEvidence = line.NormalQuantity.HasValue ? line.NormalEvidence : prior?.NormalEvidence;
                var realEvidence = line.RealQuantity.HasValue ? line.RealEvidence : prior?.RealEvidence;
                var command = new SavePhysicalObservation(host.Study.CompanyId, host.Study.Id,
                    host.Study.Revision, line.NodeId!.Value, line.PoolId!.Value,
                    prior?.Version ?? 0L, PhysicalObservationState.Draft,
                    prior?.TheoreticalQuantity, prior?.PracticalQuantity,
                    normal, real, normalEvidence, realEvidence,
                    string.IsNullOrWhiteSpace(NormalZeroReason) ? prior?.NormalZeroReason : NormalZeroReason.Trim(),
                    string.IsNullOrWhiteSpace(ExceptionReason) ? prior?.ExceptionReason : ExceptionReason.Trim(),
                    string.IsNullOrWhiteSpace(CorrectionReason) ? null : CorrectionReason.Trim(),
                    "local-user");
                commands.Add(new(command, PhysicalImportChoice.Replace));
            }
            host.Activity.ApplyImport(new(commands));
            host.Reload();
            Feedback = string.Format(ui, Text("Activity.Import.Applied"), chosen.Length);
            Lines.Clear();
            OnPropertyChanged(nameof(CanApply));
            return true;
        }
        catch (DomainRuleException e) { Feedback = Text(e.Diagnostic.MessageKey); return false; }
        catch (CompanyFileException e) { Feedback = Text(e.Diagnostic.MessageKey); return false; }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError("Physical import: {0}", ex);
            Feedback = Text("Activity.SaveFailed"); return false;
        }
    }
}
