using System.IO;
using System.Windows;
using Microsoft.Win32;
using Valdrio.Application;

namespace Valdrio.Desktop;
public partial class PrincipalRatesWindow : IntegratedWorkspaceWindow
{
    private readonly StudyWorkspaceViewModel host;
    public PrincipalRatesEditor Editor { get; }
    public bool OpenCostsRequested { get; private set; }
    public PrincipalRatesWindow(StudyWorkspaceViewModel host)
    {
        this.host = host; Editor = new(host); InitializeComponent(); DataContext = Editor;
        CenterColumn.Header = Editor["Center"]; UnitColumn.Header = Editor["Unit"]; CostColumn.Header = Editor["Cost"];
        NumeratorColumn.Header = Editor["Numerator"]; DenominatorColumn.Header = Editor["Denominator"];
        RateColumn.Header = Editor["Rate"]; StatusColumn.Header = Editor["Status"];
        BreakdownComponentColumn.Header = Editor["Component"]; BreakdownCostColumn.Header = Editor["Cost"];
        BreakdownBaseColumn.Header = Editor["Base"]; BreakdownRateColumn.Header = Editor["Rate"];
        QuestionColumn.Header = Editor["Question"]; AnswerColumn.Header = Editor["Answer"];
        ComponentColumn.Header = Editor["Component"]; BehaviorColumn.Header = Editor["Behavior"];
        SourceColumn.Header = Editor["Source"];
        AmountColumn.Header = Editor["Cost"]; BaseColumn.Header = Editor["Base"]; ComponentRateColumn.Header = Editor["Rate"];
    }
    private void Refresh(object sender, RoutedEventArgs e) => Editor.Reload();
    private void EditActivity(object sender, RoutedEventArgs e)
    { new PhysicalActivityWindow(host) { Owner = WorkspaceHost ?? (Window)this }.ShowIntegrated(Editor.Reload); }
    private void ViewCosts(object sender, RoutedEventArgs e) { OpenCostsRequested = true; Close(); }
    private void CloseWindow(object sender, RoutedEventArgs e) => Close();
    private void Export(object sender, RoutedEventArgs e)
    {
        Editor.Reload();
        if (!Editor.CanExport) return;
        var dialog = new SaveFileDialog { Filter = "JSON|*.json", FileName = "VALDRIO-T15-" + Editor.Report!.Version[..12] + ".json" };
        if (dialog.ShowDialog(WorkspaceHost ?? (Window)this) != true) return;
        try { File.WriteAllText(dialog.FileName, CanonicalSnapshotJson.Serialize(Editor.Report)); Editor.Feedback = Editor["Exported"]; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Editor.Feedback = Editor["ExportFailed"]; }
    }
}
