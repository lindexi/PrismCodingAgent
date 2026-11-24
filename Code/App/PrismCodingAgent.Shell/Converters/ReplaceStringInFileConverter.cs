using System;
using System.Globalization;
#if USE_WPF
using System.Windows.Data;
#else
using Avalonia.Data.Converters;
#endif

namespace PrismCodingAgent.CodingShell.Converters;

/// <summary>展示一次替换，不将单条差异包装成列表。</summary>
public sealed class ReplaceStringInFileConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var input = value as string;
        using var document = FileToolJson.Parse(input);
        return document is not null && ReplacementDiff.Read(document.RootElement) is { } diff
            ? new ReplaceStringInFilePreview(diff, null)
            : new ReplaceStringInFilePreview(null, input);
    }

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

internal sealed record ReplaceStringInFilePreview(ReplacementDiff? Diff, string? RawText);
