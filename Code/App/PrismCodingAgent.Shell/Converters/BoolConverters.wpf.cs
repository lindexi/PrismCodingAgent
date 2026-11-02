using System.Globalization;
using System.Windows.Data;
using PrismCodingAgent.AvaloniaShell.Localizations;

namespace PrismCodingAgent.AvaloniaShell.Converters;

internal sealed class ModelStatusLabelConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        string status = values.ElementAtOrDefault(0) as string ?? string.Empty;
        string? modelName = values.ElementAtOrDefault(1) as string;
        return modelName is not null && status == Lang.Format(Lang.Current.CurrentModel, modelName)
            ? (string)Lang.Current.ModelText : status;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>将布尔值取反。</summary>
public sealed class InverseBoolConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
}
