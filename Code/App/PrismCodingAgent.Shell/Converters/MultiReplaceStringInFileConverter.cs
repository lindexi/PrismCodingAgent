using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
#if USE_WPF
using System.Windows.Data;
#else
using Avalonia.Data.Converters;
#endif

namespace PrismCodingAgent.CodingShell.Converters;

/// <summary>按请求顺序展示批量替换及其说明。</summary>
public sealed class MultiReplaceStringInFileConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var input = value as string;
        using var document = FileToolJson.Parse(input);
        return document is not null && ReadBatch(document.RootElement) is { } batch
            ? batch : new MultiReplaceStringInFilePreview([], null, input);
    }

    private static MultiReplaceStringInFilePreview? ReadBatch(JsonElement args)
    {
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty("replacements", out var operations)
            || operations.ValueKind != JsonValueKind.Array || operations.GetArrayLength() == 0)
        {
            return null;
        }

        var diffs = new List<ReplacementDiff>(operations.GetArrayLength());
        foreach (var operation in operations.EnumerateArray())
        {
            if (ReplacementDiff.Read(operation) is not { } diff)
            {
                return null;
            }

            diffs.Add(diff with { Explanation = FileToolJson.ReadString(operation, "explanation", "Explanation") });
        }

        return new(diffs, FileToolJson.ReadString(args, "explanation"), null);
    }

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

internal sealed record MultiReplaceStringInFilePreview(IReadOnlyList<ReplacementDiff> Diffs, string? Explanation, string? RawText);
