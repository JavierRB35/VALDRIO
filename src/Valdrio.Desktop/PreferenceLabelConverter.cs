using System.Globalization;
using System.Windows.Data;
using Valdrio.Localization;

namespace Valdrio.Desktop;

/// <summary>Displays friendly names while keeping persisted preference codes unchanged.</summary>
public sealed class PreferenceLabelConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var code = values.ElementAtOrDefault(0)?.ToString() ?? "";
        var language = values.ElementAtOrDefault(1)?.ToString() ?? "es";
        var text = new UiTextCatalog().Get("Preference." + code.Replace('/', '.'), CultureInfo.GetCultureInfo(language));
        return text.StartsWith("Preference.", StringComparison.Ordinal) ? code : text;
    }
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
