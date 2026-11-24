using System;
using System.Text.Json;

namespace PrismCodingAgent.CodingShell.Converters;

internal static class FileToolJson
{
    internal static JsonDocument? Parse(string? input)
    {
        if (string.IsNullOrEmpty(input)) return null;
        try
        {
            return JsonDocument.Parse(input);
        }
        catch (JsonException)
        {
            // 流式参数可能尚未收齐，历史记录也可能只有格式化后的文本。
            return null;
        }
    }

    internal static string? ReadString(JsonElement element, string name, string? alternate = null)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        if (!element.TryGetProperty(name, out var value)
            && (alternate is null || !element.TryGetProperty(alternate, out value))) return null;
        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }
}
