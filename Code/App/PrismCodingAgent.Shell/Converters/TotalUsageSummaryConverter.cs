using System;
using System.Globalization;
using Microsoft.Extensions.AI;
using PrismCodingAgent.CodingShell.ViewModels;
#if USE_WPF
using System.Windows.Data;
#else
using System.Collections.Generic;
using Avalonia.Data.Converters;
#endif

namespace PrismCodingAgent.CodingShell.Converters;

/// <summary>格式化工具详情中的累计用量，并在界面语言切换时重新求值。</summary>
public sealed class TotalUsageSummaryConverter : IMultiValueConverter
{
    /// <inheritdoc />
#if USE_WPF
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
#else
    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
#endif
        => MessageItemViewModel.FormatTotalUsage(values[0] as UsageDetails);

#if USE_WPF
    /// <inheritdoc />
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
#endif
}
