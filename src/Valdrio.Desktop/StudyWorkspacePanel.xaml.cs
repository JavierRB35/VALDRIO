using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Valdrio.Desktop;

public partial class StudyWorkspacePanel : UserControl
{
    public StudyWorkspacePanel() => InitializeComponent();
}

// Calendar selection is typed. A region switch cannot reinterpret pending date text.
public sealed class CalendarDatePicker : DatePicker
{
    private DatePickerTextBox? calendarText;
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
    }
    private static void KeepCalendarTextReadOnly(object sender, System.Windows.DependencyPropertyChangedEventArgs e) =>
        ((DatePickerTextBox)sender).SetCurrentValue(TextBox.IsReadOnlyProperty, true);
}
