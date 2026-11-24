using System;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Threading.Tasks;

namespace PrismCodingAgent.CodingShell.Services.Settings;

internal static class FavoritePromptStore
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };

    internal static async Task<string[]> LoadAsync(string filePath, ISessionCopilotChatLogger? logger = null)
    {
        try
        {
            await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete,
                4096, FileOptions.Asynchronous);
            var prompts = await JsonSerializer.DeserializeAsync<string[]>(stream).ConfigureAwait(false);
            if (prompts is null || prompts.Any(string.IsNullOrWhiteSpace)) throw new JsonException();
            return prompts;
        }
        catch (FileNotFoundException)
        {
            return [];
        }
        catch (DirectoryNotFoundException)
        {
            return [];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or System.Security.SecurityException)
        {
            await logger.TryLogDiagnosticAsync("[FavoritePromptStore][Load]", "读取收藏提示词失败。", exception).ConfigureAwait(false);
            return [];
        }
    }

    internal static async Task SaveAsync(string filePath, string[] prompts, ISessionCopilotChatLogger? logger = null)
    {
        string directory = Path.GetDirectoryName(Path.GetFullPath(filePath))!;
        Directory.CreateDirectory(directory);
        string tempPath = Path.Combine(directory, $".{Path.GetFileName(filePath)}.{Guid.NewGuid():N}.tmp");
        var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            4096, FileOptions.Asynchronous);
        try
        {
            await using (stream.ConfigureAwait(false))
            {
                await JsonSerializer.SerializeAsync(stream, prompts, s_jsonOptions).ConfigureAwait(false);
            }
            File.Move(tempPath, filePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            try
            {
                File.Delete(tempPath);
            }
            catch (Exception cleanupError) when (cleanupError is IOException or UnauthorizedAccessException)
            {
                await logger.TryLogDiagnosticAsync("[FavoritePromptStore][Save]", "清理收藏提示词临时文件失败。", cleanupError).ConfigureAwait(false);
            }
            throw;
        }
    }
}
