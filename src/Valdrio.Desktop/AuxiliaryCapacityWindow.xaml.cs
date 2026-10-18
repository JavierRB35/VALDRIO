using System.IO;
using System.Windows;
using Microsoft.Win32;
namespace Valdrio.Desktop;
public partial class AuxiliaryCapacityWindow : Window
{
    private readonly StudyWorkspaceViewModel host;
    public AuxiliaryCapacityEditor Editor { get; }
    public AuxiliaryCapacityWindow(StudyWorkspaceViewModel host)
    {
        this.host = host; Editor = new(host); InitializeComponent(); DataContext = Editor;
        CenterColumn.Header = Editor["Center"]; UnitColumn.Header = Editor["Unit"]; NormalColumn.Header = Editor["Normal"];
        RealColumn.Header = Editor["Real"]; ModeColumn.Header = Editor["Mode"]; GroupColumn.Header = Editor["Group"];
        DestinationColumn.Header = Editor["Destination"]; QuantityColumn.Header = Editor["Quantity"];
        QuestionColumn.Header = Editor["Question"]; AnswerColumn.Header = Editor["Answer"];
        Closing += (_, e) => { if (!CanDiscard()) e.Cancel = true; };
    }
    private bool CanDiscard() => !Editor.Dirty || MessageBox.Show(this, Editor["Discard"], Editor["Title"], MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    private void Save(object sender, RoutedEventArgs e) => Editor.Save();
    private void Validate(object sender, RoutedEventArgs e) => Editor.Validate();
    private void Calculate(object sender, RoutedEventArgs e) => Editor.Calculate();
    private void Reopen(object sender, RoutedEventArgs e) => Editor.Reopen();
    private void Refresh(object sender, RoutedEventArgs e) { if (CanDiscard()) Editor.Reload(); }
    private void Activity(object sender, RoutedEventArgs e) { if (!CanDiscard()) return; new PhysicalActivityWindow(host) { Owner = this }.ShowDialog(); Editor.Reload(); }
    private void Rates(object sender, RoutedEventArgs e) { if (Editor.Dirty) { Editor.Feedback = Editor["SaveFirst"]; return; } new PrincipalRatesWindow(host) { Owner = this }.ShowDialog(); }
    private void CloseWindow(object sender, RoutedEventArgs e) => Close();
    private void Import(object sender, RoutedEventArgs e)
    {
        var d = new OpenFileDialog { Filter = "CSV|*.csv" }; if (d.ShowDialog(this) != true) return;
        try { Editor.Import(File.ReadAllText(d.FileName)); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Editor.Feedback = Editor["Failed"]; }
    }
    private void Export(object sender, RoutedEventArgs e)
    {
        if (Editor.Dirty) { Editor.Feedback = Editor["SaveFirst"]; return; }
        Editor.Reload(); var json = Editor.Export(); if (json is null) return;
        var d = new SaveFileDialog { Filter = "JSON|*.json", FileName = "VALDRIO-T16.json" }; if (d.ShowDialog(this) != true) return;
        try { File.WriteAllText(d.FileName, json); Editor.Feedback = Editor["Exported"]; } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Editor.Feedback = Editor["Failed"]; }
    }
}
