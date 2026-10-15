using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Valdrio.Desktop;

public sealed class DistinctStatusVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        => values.FirstOrDefault() is string status && !string.IsNullOrWhiteSpace(status)
           && !values.Skip(1).OfType<string>().Contains(status)
            ? Visibility.Visible : Visibility.Collapsed;

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
