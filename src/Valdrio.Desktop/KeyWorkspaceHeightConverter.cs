using System.Globalization;
using System.Windows.Data;

namespace Valdrio.Desktop;

public sealed class KeyWorkspaceHeightConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is double height && double.IsFinite(height) ? Math.Max(620d, height - 80d) : 620d;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
