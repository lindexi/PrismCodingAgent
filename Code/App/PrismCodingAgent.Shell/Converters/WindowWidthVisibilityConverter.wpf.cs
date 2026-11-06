using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PrismCodingAgent.CodingShell.Converters;

internal sealed class WindowWidthVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is double width && width >= double.Parse((string)parameter, CultureInfo.InvariantCulture)
            ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
