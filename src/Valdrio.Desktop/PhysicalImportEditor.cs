using System.Collections.ObjectModel;
using System.IO;
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
    public PhysicalObservation? Prior { get; init; }
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
    private string? previewPath, previewSheet, previewSeparator;
    public bool HasPendingImportPreview => !string.IsNullOrWhiteSpace(Path) &&
        (Path != previewPath || SelectedSheet != previewSheet || SelectedSeparator != previewSeparator);
    partial void OnPathChanged(string value) => OnPropertyChanged(nameof(HasPendingImportPreview));
    partial void OnSelectedSheetChanged(string value) => OnPropertyChanged(nameof(HasPendingImportPreview));
    partial void OnSelectedSeparatorChanged(string value) => OnPropertyChanged(nameof(HasPendingImportPreview));

    public PhysicalImportEditor(PhysicalActivityEditor host)
    {
        this.host = host; numeric = host.NumericCulture;
        ui = host.LanguageCulture;
    }

    public string CreateTemplate()
    {
        static string Csv(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
        var rows = host.Structure.Nodes.Where(n => n.IsActive).Select(n =>
        {
            var pool = host.Structure.Pools.Single(p => p.NodeId == n.Id);
            var unit = host.Units.Single(u => u.Id == pool.DriverUnitId);
            return string.Join(";", new[] { n.Code, unit.Code, host.Study.PeriodStart.ToString(),
                host.Study.PeriodEnd.ToString(), "", "", "", "" }.Select(Csv));
        });
        return "node;unit;period_start;period_end;normal;real;normal_source;real_source\n" + string.Join("\n", rows) + "\n";
    }

    public ObservableCollection<PhysicalImportCandidate> Lines { get; } = [];
    public ObservableCollection<string> Sheets { get; } = [];
    public bool CanWrite => !host.IsReadOnly;
    public bool CanApply => CanWrite && !Busy && Lines.Any(l => l.Replace);
    public string Title => Text("Activity.Import.Title");
    public string Help => Text("Activity.Import.Help");
    public string TemplateLabel => Text("Activity.Import.Template");
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
        using var activityScope = host.BeginActivity?.Invoke();
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
        using var activityScope = host.BeginActivity?.Invoke();
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
            var result = await Task.Run(() => PhysicalImportMapper.PreviewResult(document, model, host.Units,
                host.Study, numeric));
            if (result.Preview is not { } preview) { Feedback = Text(result.Diagnostic!.MessageKey); return; }
            var current = (await Task.Run(() => host.Activity.ReadCurrent(host.Study.CompanyId, host.Study.Id)))
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
                    Warning = problem, Existing = existing,
                    Prior = line.PoolId is { } id ? current.GetValueOrDefault(id) : null
                };
                item.PropertyChanged += (_, _) => OnPropertyChanged(nameof(CanApply));
                Lines.Add(item);
            }
            Feedback = string.Format(ui, Text("Activity.Import.Summary"),
                preview.Lines.Count, preview.Lines.Count(r => r.CanChoose));
            previewPath = Path; previewSheet = SelectedSheet; previewSeparator = SelectedSeparator;
            OnPropertyChanged(nameof(HasPendingImportPreview));
        }
        catch (DomainRuleException e) { Feedback = Text(e.Diagnostic.MessageKey); }
        catch (CompanyFileException e) { Feedback = Text(e.Diagnostic.MessageKey); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException)
        { Feedback = Text("Activity.Import.Invalid"); }
        finally { Busy = false; OnPropertyChanged(nameof(CanApply)); }
    }

    public bool Apply() => ApplyCore(false).GetAwaiter().GetResult();
    public Task<bool> ApplyAsync() => ApplyCore(true);
    private async Task<bool> ApplyCore(bool background)
    {
        if (!CanApply) return false;
        using var activityScope = host.BeginActivity?.Invoke();
        Busy = true;
        try
        {
            var chosen = Lines.Where(l => l.Replace).Select(l => l.Source).ToArray();
            if (chosen.Any(l => !l.CanChoose) ||
                chosen.Select(l => l.PoolId).Distinct().Count() != chosen.Length)
                { Feedback = Text("Activity.ImportDuplicate"); return false; }
            var commands = new List<PhysicalImportOperation>();
            foreach (var line in chosen)
            {
                // Compare against the version reviewed, never silently refresh it at adoption.
                var reviewed = Lines.Single(l => ReferenceEquals(l.Source, line)).Prior;
                var prior = reviewed?.State == PhysicalObservationState.Annulled ? null : reviewed;
                if (prior?.State == PhysicalObservationState.Confirmed &&
                    string.IsNullOrWhiteSpace(CorrectionReason))
                    { Feedback = Text("Activity.CorrectionReason"); return false; }
                var normal = line.NormalQuantity ?? prior?.NormalQuantity;
                var real = line.RealQuantity ?? prior?.RealQuantity;
                var normalEvidence = line.NormalQuantity.HasValue ? line.NormalEvidence : prior?.NormalEvidence;
                var realEvidence = line.RealQuantity.HasValue ? line.RealEvidence : prior?.RealEvidence;
                var command = new SavePhysicalObservation(host.Study.CompanyId, host.Study.Id,
                    host.Study.Revision, line.NodeId!.Value, line.PoolId!.Value,
                    reviewed?.Version ?? 0L, PhysicalObservationState.Draft,
                    prior?.TheoreticalQuantity, prior?.PracticalQuantity,
                    normal, real, normalEvidence, realEvidence,
                    string.IsNullOrWhiteSpace(NormalZeroReason) ? prior?.NormalZeroReason : NormalZeroReason.Trim(),
                    string.IsNullOrWhiteSpace(ExceptionReason) ? prior?.ExceptionReason : ExceptionReason.Trim(),
                    string.IsNullOrWhiteSpace(CorrectionReason) ? null : CorrectionReason.Trim(),
                    "local-user");
                commands.Add(new(command, PhysicalImportChoice.Replace));
            }
            if (background)
            {
                await Task.Run(() => host.Activity.ApplyImport(new(commands)));
                await host.ReloadAsync();
            }
            else { host.Activity.ApplyImport(new(commands)); host.Reload(); }
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
        finally { Busy = false; }
    }
}
