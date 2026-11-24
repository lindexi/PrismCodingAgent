using System;
using System.Globalization;
#if USE_WPF
using System.Windows.Data;
#else
using Avalonia.Data.Converters;
#endif

namespace PrismCodingAgent.CodingShell.Converters;

/// <summary>展示待写入的原文，不将全量写入视为差异。</summary>
public sealed class WriteFileContentConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var input = value as string;
        using var document = FileToolJson.Parse(input);
        if (document is not null
            && FileToolJson.ReadString(document.RootElement, "filePath") is { } path
            && FileToolJson.ReadString(document.RootElement, "content") is { } content)
        {
            return new WriteFileContentPreview(path, content);
        }

        return new WriteFileContentPreview(null, input);
    }

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

internal sealed record WriteFileContentPreview(string? FilePath, string? Text);
