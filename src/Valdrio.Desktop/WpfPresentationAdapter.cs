using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using Valdrio.Localization;

namespace Valdrio.Desktop;

public static class WpfPresentationAdapter
{
    public static void Apply(
        FrameworkElement root,
        UserPresentationPreferences preferences,
        bool forceRtlForTest)
    {
        var uiCulture = CultureInfo.GetCultureInfo(preferences.UiLanguageTag);
        root.Language = XmlLanguage.GetLanguage(uiCulture.IetfLanguageTag);
        root.FlowDirection = forceRtlForTest || uiCulture.TextInfo.IsRightToLeft
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;
    }
}
