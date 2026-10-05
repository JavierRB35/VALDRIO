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
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (GetTemplateChild("PART_TextBox") is DatePickerTextBox text) text.IsReadOnly = true;
    }
}
