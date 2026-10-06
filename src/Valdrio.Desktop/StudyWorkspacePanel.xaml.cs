using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Valdrio.Desktop;

public partial class StudyWorkspacePanel : UserControl
{
    public StudyWorkspacePanel() => InitializeComponent();
    private async void OpenImportColumns(object sender, RoutedEventArgs e)
    {
        if (DataContext is StudyWorkspaceViewModel { ImportColumns: { } vm } studies && await studies.AttachImportTarget())
            new ImportColumnMappingWindow { Owner = Window.GetWindow(this), DataContext = vm }.ShowDialog();
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
