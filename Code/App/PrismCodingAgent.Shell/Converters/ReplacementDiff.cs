using System;
using System.Text.Json;

namespace PrismCodingAgent.CodingShell.Converters;

internal sealed record ReplacementDiff(string FilePath, string RemovedText, string AddedText, string? Explanation = null)
{
    internal static ReplacementDiff? Read(JsonElement args)
    {
        var path = FileToolJson.ReadString(args, "filePath", "FilePath");
        if (string.IsNullOrWhiteSpace(path)
            || FileToolJson.ReadString(args, "oldString", "OldString") is not { Length: > 0 } oldText
            || FileToolJson.ReadString(args, "newString", "NewString") is not { } newText)
        {
            return null;
        }

        // 只去掉首尾相同的完整行，不推测文件中的行号或位置。
        int start = 0;
        int oldEnd = oldText.Length;
        int newEnd = newText.Length;
        while (start < oldEnd && start < newEnd)
        {
            int oldLength = FirstLineLength(oldText.AsSpan(start));
            int newLength = FirstLineLength(newText.AsSpan(start));
            if (oldLength != newLength || !oldText.AsSpan(start, oldLength).SequenceEqual(newText.AsSpan(start, newLength)))
            {
                break;
            }

            start += oldLength;
        }

        while (oldEnd > start && newEnd > start)
        {
            int oldStart = start + LastLineStart(oldText.AsSpan(start, oldEnd - start));
            int newStart = start + LastLineStart(newText.AsSpan(start, newEnd - start));
            if (!oldText.AsSpan(oldStart, oldEnd - oldStart).SequenceEqual(newText.AsSpan(newStart, newEnd - newStart)))
            {
                break;
            }

            oldEnd = oldStart;
            newEnd = newStart;
        }

        return new(path, oldText[start..oldEnd], newText[start..newEnd]);
    }

    private static int FirstLineLength(ReadOnlySpan<char> text)
    {
        int end = text.IndexOfAny('\r', '\n');
        if (end < 0) return text.Length;
        return text[end] == '\r' && end + 1 < text.Length && text[end + 1] == '\n' ? end + 2 : end + 1;
    }

    private static int LastLineStart(ReadOnlySpan<char> text)
    {
        int end = text.Length;
        if (text[end - 1] == '\n') end--;
        if (end > 0 && text[end - 1] == '\r') end--;
        return text[..end].LastIndexOfAny('\r', '\n') + 1;
    }
}
