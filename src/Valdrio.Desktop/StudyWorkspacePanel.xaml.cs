using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Valdrio.Desktop;

public partial class StudyWorkspacePanel : UserControl
{
    private async void RefreshStudiesOnDropDownOpened(object sender, System.EventArgs e)
    {
        // Refresh in-place without loading the selected study or losing pending
        // context edits. Reformat preserves the selected study identity.
        if (DataContext is StudyWorkspaceViewModel vm && vm.RefreshCommand.CanExecute(null))
            await vm.RefreshCommand.ExecuteAsync(null);
    }

    private async void OpenResults(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow main && DataContext is StudyWorkspaceViewModel vm && await vm.CanLeaveAsync())
            await main.NavigateWorkflow(8);
    }
    public StudyWorkspacePanel()
    {
        InitializeComponent();
        CreateStudyButton.Click += async (_, _) =>
        {
            // Wait until the command has finished before revealing its result.
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Background);
            if (DataContext is StudyWorkspaceViewModel vm && vm.CreateCommand.ExecutionTask is { } task) await task;
            ActionStatus.BringIntoView();
        };
    }
    private void OpenPhysicalActivity(object sender, RoutedEventArgs e)
    {
        if (DataContext is StudyWorkspaceViewModel { CanOpenPhysical: true } vm)
        {
            var window = new PhysicalActivityWindow(vm) { Owner = Window.GetWindow(this) };
            window.ShowIntegrated();
        }
    }

    private async void GoToModels(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow main)
        {
            await main.NavigateWorkflow(1);
            main.ModelPanel.Focus();
        }
    }
    private async void OpenImportColumns(object sender, RoutedEventArgs e)
    {
        if (DataContext is StudyWorkspaceViewModel { ImportColumns: { } vm } studies && await studies.AttachImportTarget(previewOnly: true))
            new ImportColumnMappingWindow { Owner = Window.GetWindow(this), DataContext = vm }.ShowIntegrated();
    }
}

// Calendar selection is typed. A region switch cannot reinterpret pending date text.
public sealed class CalendarDatePicker : DatePicker
{
    private DatePickerTextBox? calendarText;
    static CalendarDatePicker() => LanguageProperty.OverrideMetadata(typeof(CalendarDatePicker),
        new FrameworkPropertyMetadata((d, _) => ((CalendarDatePicker)d).RefreshDateText()));

    private void RefreshDateText()
    {
        // WPF's language callback changes the first weekday, but does not redraw
        // an existing selected date. Format the typed value rather than old text.
        if (SelectedDate is { } date)
            SetCurrentValue(TextProperty, date.ToString(SelectedDateFormat == DatePickerFormat.Short ? "d" : "D",
                CultureInfo.GetCultureInfo(Language.IetfLanguageTag)));
    }

    public override void OnApplyTemplate()
    {
        if (calendarText is not null) calendarText.IsEnabledChanged -= KeepCalendarTextReadOnly;
        base.OnApplyTemplate();
        calendarText = GetTemplateChild("PART_TextBox") as DatePickerTextBox;
        if (calendarText is not null)
        {
            // DatePickerTextBox resets IsReadOnly whenever IsEnabled changes.
            calendarText.IsEnabledChanged += KeepCalendarTextReadOnly;
            calendarText.SetCurrentValue(TextBox.IsReadOnlyProperty, true);
        }
        RefreshDateText();
    }
    private static void KeepCalendarTextReadOnly(object sender, System.Windows.DependencyPropertyChangedEventArgs e) =>
        ((DatePickerTextBox)sender).SetCurrentValue(TextBox.IsReadOnlyProperty, true);
}
