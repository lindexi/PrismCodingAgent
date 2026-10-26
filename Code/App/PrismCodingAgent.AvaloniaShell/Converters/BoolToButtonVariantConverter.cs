using System;
using System.Globalization;
using Avalonia.Data.Converters;
using TKCode.Arc;

namespace PrismCodingAgent.AvaloniaShell.Converters;

/// <summary>
/// 把布尔状态映射为 <see cref="ButtonVariant"/>，用于导航项之类的"选中即高亮"场景。
/// </summary>
public sealed class BoolToButtonVariantConverter : IValueConverter
{
    /// <summary>
    /// 条件为真时使用的外观。
    /// </summary>
    public ButtonVariant WhenTrue { get; set; } = ButtonVariant.Secondary;

    /// <summary>
    /// 条件为假时使用的外观。
    /// </summary>
    public ButtonVariant WhenFalse { get; set; } = ButtonVariant.Ghost;

    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? WhenTrue : WhenFalse;

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
