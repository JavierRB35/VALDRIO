using System.Windows.Markup;

namespace Valdrio.Desktop;

public sealed partial class StudyWorkspaceViewModel
{
    public XmlLanguage DateLanguage => XmlLanguage.GetLanguage(preferences.Current.FormatCultureTag);
}
