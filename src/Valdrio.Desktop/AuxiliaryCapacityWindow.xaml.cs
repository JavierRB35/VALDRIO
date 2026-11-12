using System.IO;
using System.Windows;
using Microsoft.Win32;
namespace Valdrio.Desktop;
public partial class AuxiliaryCapacityWindow : IntegratedWorkspaceWindow
{
    private readonly StudyWorkspaceViewModel host;
    public AuxiliaryCapacityEditor Editor { get; }
    public AuxiliaryCapacityWindow(StudyWorkspaceViewModel host)
    {
        this.host = host; Editor = new(host, initialize: false); InitializeComponent(); DataContext = Editor;
        ContentRendered += async (_, _) => await InitializeAsync();
        CenterColumn.Header = Editor["Center"]; UnitColumn.Header = Editor["Unit"]; NormalColumn.Header = Editor["Normal"];
        RealColumn.Header = Editor["Real"]; ModeColumn.Header = Editor["Mode"]; GroupColumn.Header = Editor["Group"];
        DestinationColumn.Header = Editor["Destination"]; QuantityColumn.Header = Editor["Quantity"];
        PreparationColumn.Header = Editor["Preparation"];
        QuestionColumn.Header = Editor["Question"]; AnswerColumn.Header = Editor["Answer"];
        Closing += (_, e) => { if (!CanDiscard()) e.Cancel = true; };
    }
    private Task? initialization;
    internal override Task InitializeAsync() => initialization ??= RunActivityAsync(Editor.ReloadAsync);
    private bool CanDiscard() => !Editor.Dirty || new EconomicConfirmationWindow(Editor["Title"], Editor["Discard"],
        new Valdrio.Localization.UiTextCatalog().Get("UX.Discard", host.PhysicalUiCulture),
        new Valdrio.Localization.UiTextCatalog().Get("UX.KeepEditing", host.PhysicalUiCulture)) { Owner = WorkspaceHost ?? (Window)this }.ShowDialog() == true;
    private async void Save(object sender, RoutedEventArgs e) => await RunActivityAsync(Editor.SaveAsync);
    private async void Validate(object sender, RoutedEventArgs e) => await RunActivityAsync(Editor.ValidateAsync);
    private async void Calculate(object sender, RoutedEventArgs e) => await RunActivityAsync(Editor.CalculateAsync);
    private async void Reopen(object sender, RoutedEventArgs e) => await RunActivityAsync(Editor.ReopenAsync);
    private async void Refresh(object sender, RoutedEventArgs e) { if (CanDiscard()) await RunActivityAsync(Editor.ReloadAsync); }
    private void Activity(object sender, RoutedEventArgs e) { if (!CanDiscard()) return; new PhysicalActivityWindow(host) { Owner = WorkspaceHost ?? (Window)this }.ShowIntegrated(async () => await RunActivityAsync(Editor.ReloadAsync)); }
    private void Rates(object sender, RoutedEventArgs e) { if (Editor.Dirty) { Editor.Feedback = Editor["SaveFirst"]; return; } new PrincipalRatesWindow(host) { Owner = WorkspaceHost ?? (Window)this }.ShowIntegrated(); }
    private void CloseWindow(object sender, RoutedEventArgs e) => Close();
    private async void Import(object sender, RoutedEventArgs e)
    {
        var d = new OpenFileDialog { Filter = "CSV|*.csv" }; if (d.ShowDialog(WorkspaceHost ?? (Window)this) != true) return;
        try { await RunActivityAsync(async () => Editor.Import(await File.ReadAllTextAsync(d.FileName))); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Editor.Feedback = Editor["Failed"]; }
    }
    private async void Export(object sender, RoutedEventArgs e)
    {
        if (Editor.Dirty) { Editor.Feedback = Editor["SaveFirst"]; return; }
        await RunActivityAsync(Editor.ReloadAsync); var json = Editor.Export(); if (json is null) return;
        var d = new SaveFileDialog { Filter = "JSON|*.json", FileName = "VALDRIO-servicios-auxiliares.json" }; if (d.ShowDialog(WorkspaceHost ?? (Window)this) != true) return;
        try { await RunActivityAsync(() => File.WriteAllTextAsync(d.FileName, json)); Editor.Feedback = Editor["Exported"]; } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Editor.Feedback = Editor["Failed"]; }
    }
}
