using AgentLib.Core;
using AgentLib.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace AgentLib.Logging;

/// <summary>
/// 在显式目录中保存、列出、加载和删除 Copilot 聊天会话。
/// </summary>
public sealed class FileCopilotChatSessionStore
{
    /// <summary>
    /// 当前会话历史格式版本。
    /// </summary>
    public const int CurrentFormatVersion = 5;

    private static readonly Encoding Utf8EncodingWithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _sessionDirectory;
    private readonly string? _logDirectory;

    /// <summary>
    /// 使用显式会话目录和可选日志目录创建文件会话存储。
    /// </summary>
    /// <param name="sessionDirectory">会话历史目录。</param>
    /// <param name="logDirectory">对应日志根目录；删除会话时用于清理关联日志。</param>
    public FileCopilotChatSessionStore(string sessionDirectory, string? logDirectory = null)
    {
        ArgumentHelper.ThrowIfNullOrWhiteSpace(sessionDirectory);
        _sessionDirectory = Path.GetFullPath(sessionDirectory);
        _logDirectory = string.IsNullOrWhiteSpace(logDirectory) ? null : Path.GetFullPath(logDirectory);
    }

    /// <summary>
    /// 保存完整会话及其代理状态，同一会话始终更新同一个历史文件。
    /// </summary>
    public async Task SaveSessionAsync
    (
        CopilotChatSession session,
        JsonElement? agentSessionState,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(session);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string dayDirectory = Path.Join(_sessionDirectory, session.StartedTime.ToString("yyyyMMdd"));
            Directory.CreateDirectory(dayDirectory);
            string filePath = Path.Join
                (dayDirectory, session.SessionId.ToString("N"), SessionDirectoryStorage.EntryFileName);
            if (File.Exists(filePath))
            {
                await SessionDirectoryStorage.ExpandAsync(filePath, cancellationToken).ConfigureAwait(false);
            }

            var data = new CopilotChatSessionPersistenceData
            {
                FormatVersion = CurrentFormatVersion,
                SessionId = session.SessionId,
                StartedTime = session.StartedTime,
                Title = session.Title,
                WorkspacePath = session.WorkspacePath,
                WorkTaskId = session.WorkTaskId,
                WorkTaskName = session.WorkTaskName,
                Entries = session.Entries.ToArray(),
                AgentSessionState = agentSessionState,
                ResponsesSession = session.ExistingResponsesSession,
            };
            await SessionDirectoryStorage.WriteAsync
                (filePath, data, DateTimeOffset.Now, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// 列出所有可读取的会话摘要；损坏文件会被隔离跳过。
    /// </summary>
    public async Task<IReadOnlyList<CopilotChatSessionSummary>> ListSessionsAsync
        (CancellationToken cancellationToken = default)
    {
        var summaries = new List<CopilotChatSessionSummary>();
        await foreach (var summary in EnumerateSessionsAsync(cancellationToken).ConfigureAwait(false))
        {
            summaries.Add(summary);
        }

        return summaries;
    }

    /// <summary>
    /// 逐个读取文件头并返回会话摘要，不解析正文或统计消息；正文在打开会话时验证。
    /// </summary>
    public async IAsyncEnumerable<CopilotChatSessionSummary> EnumerateSessionsAsync
    (
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default
    )
    {
        string[] files = await Task.Run
        (
            () => Directory.Exists(_sessionDirectory)
                ? EnumerateEntryFiles()
                    .Select(file => new { File = file, SortInfo = GetSessionFileSortInfo(file) })
                    .OrderByDescending(item => item.SortInfo.IsRecognized)
                    .ThenByDescending(item => item.SortInfo.SortKey, StringComparer.Ordinal)
                    .Select(item => item.File)
                    .ToArray()
                : Array.Empty<string>(), cancellationToken
        ).ConfigureAwait(false);
        foreach (string file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CopilotChatSessionSummary summary;
            try
            {
                summary = await LoadSummaryAsync(file, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is InvalidDataException or XmlException or JsonException
                                                  or FormatException or FileNotFoundException
                                                  or DirectoryNotFoundException)
            {
                System.Diagnostics.Trace.TraceWarning
                    ("[FileCopilotChatSessionStore][EnumerateSessions] FilePath={0} Exception={1}", file, exception);
                continue;
            }

            yield return summary;
        }
    }

    /// <summary>
    /// 按会话 ID 加载持久化的会话数据。
    /// </summary>
    public async Task<CopilotChatSessionPersistenceData> LoadSessionAsync
    (
        Guid sessionId,
        CancellationToken cancellationToken = default
    )
    {
        string filePath = FindSessionFile(sessionId)
                          ?? throw new FileNotFoundException($"未找到会话 {sessionId} 的历史文件。", sessionId.ToString());
        filePath = await SessionStorageMigrations.UpgradeAsync
            (filePath, _sessionDirectory, cancellationToken).ConfigureAwait(false);
        return await SessionDirectoryStorage.ReadAsync(filePath, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 删除会话历史及对应日志文件。
    /// </summary>
    /// <returns>存在并删除了历史或日志文件时返回 <see langword="true"/>。</returns>
    public async Task<bool> DeleteSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            bool deleted = false;
            string? historyFile = FindSessionFile(sessionId);
            if (historyFile is not null)
            {
                historyFile = await SessionStorageMigrations.UpgradeAsync
                    (historyFile, _sessionDirectory, cancellationToken).ConfigureAwait(false);
                Directory.Delete(Path.GetDirectoryName(historyFile)!, recursive: true);
                deleted = true;
            }

            if (_logDirectory is not null && Directory.Exists(_logDirectory))
            {
                foreach (string logFile in Directory.GetFiles
                             (_logDirectory, $"*{sessionId:N}*.log", SearchOption.AllDirectories))
                {
                    File.Delete(logFile);
                    deleted = true;
                }
            }

            return deleted;
        }
        finally
        {
            _gate.Release();
        }
    }

    private IEnumerable<string> EnumerateEntryFiles() => Directory.EnumerateFiles
            (_sessionDirectory, "*.xml", SearchOption.AllDirectories)
        .Where
        (file => Path.GetFileName(file) == SessionDirectoryStorage.EntryFileName
                 || Path.GetFileName(file) != "History.xml" && Path.GetFileName(file).Contains('_')
        );

    /// <summary>升级旧会话，并以 ZIP 压缩七天未更新的会话正文与资源。</summary>
    public async Task<(int Upgraded, int Compressed)> OrganizeAsync(CancellationToken cancellationToken = default)
    {
        int upgraded = 0;
        int compressed = 0;
        if (!Directory.Exists(_sessionDirectory)) return (0, 0);
        DateTimeOffset cutoff = DateTimeOffset.Now.AddDays(-7);
        foreach (string path in EnumerateEntryFiles().ToArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            string entryPath = await SessionStorageMigrations.UpgradeAsync
                (path, _sessionDirectory, cancellationToken).ConfigureAwait(false);
            if (entryPath != path) upgraded++;
            XDocument entry = await SessionDirectoryStorage.ReadXmlAsync(entryPath, cancellationToken).ConfigureAwait
                (false);
            DateTimeOffset updated = DateTimeOffset.Parse
                (entry.Root!.Attribute("UpdatedTime")!.Value, CultureInfo.InvariantCulture);
            if (updated < cutoff && await SessionDirectoryStorage.CompressAsync
                    (entryPath, cancellationToken).ConfigureAwait(false)) compressed++;
        }

        return (upgraded, compressed);
    }

    private string? FindSessionFile(Guid sessionId)
    {
        if (!Directory.Exists(_sessionDirectory))
        {
            return null;
        }

        return EnumerateEntryFiles().SingleOrDefault
        (path =>
            Path.GetFileName(Path.GetDirectoryName(path)) == sessionId.ToString("N")
            || Path.GetFileNameWithoutExtension(path).EndsWith("_" + sessionId.ToString("N"), StringComparison.Ordinal)
        );
    }

    private static (bool IsRecognized, string SortKey) GetSessionFileSortInfo(string filePath)
    {
        if (Path.GetFileName(filePath) == SessionDirectoryStorage.EntryFileName)
        {
            string directory = Path.GetDirectoryName(filePath)!;
            return (true, Path.GetFileName(Path.GetDirectoryName(directory)) + "_" + Path.GetFileName(directory));
        }

        string fileName = Path.GetFileNameWithoutExtension(filePath);
        string? directoryName = Path.GetFileName(Path.GetDirectoryName(filePath));
        bool isRecognized = DateOnly.TryParseExact
                            (
                                directoryName, "yyyyMMdd", CultureInfo.InvariantCulture,
                                DateTimeStyles.None, out _
                            )
                            && fileName.StartsWith($"{directoryName}_", StringComparison.Ordinal);
        return isRecognized ? (true, fileName) : (false, fileName);
    }

    private static async Task<CopilotChatSessionSummary> LoadSummaryAsync
    (
        string filePath,
        CancellationToken cancellationToken
    )
    {
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using XmlReader reader = XmlReader.Create
        (
            stream, new XmlReaderSettings
            {
                Async = true,
                IgnoreComments = true,
                IgnoreWhitespace = true,
            }
        );
        cancellationToken.ThrowIfCancellationRequested();
        await reader.MoveToContentAsync().ConfigureAwait(false);
        if (reader.NodeType != XmlNodeType.Element || reader.Name != "CopilotChatSessionHistory")
        {
            throw new InvalidDataException("聊天历史文件缺少根节点。");
        }

        int formatVersion = int.TryParse(reader.GetAttribute("FormatVersion"), out int parsedFormatVersion)
            ? parsedFormatVersion
            : 1;
        if (formatVersion is < 1 or > CurrentFormatVersion)
        {
            throw new InvalidDataException($"不支持的聊天历史格式版本：{formatVersion}。");
        }

        Guid sessionId = Guid.Parse
        (
            reader.GetAttribute("SessionId")
            ?? throw new InvalidDataException("聊天历史文件缺少 SessionId。")
        );
        string? startedTimeText = reader.GetAttribute("StartedTime") ?? reader.GetAttribute("CreatedTime");
        if (!DateTimeOffset.TryParse
            (
                startedTimeText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind,
                out DateTimeOffset startedTime
            ))
        {
            throw new InvalidDataException("聊天历史文件缺少有效的 StartedTime。");
        }

        string title = reader.GetAttribute("Title") ?? string.Empty;
        string? workspacePath = reader.GetAttribute("WorkspacePath");
        Guid? workTaskId = Guid.TryParse(reader.GetAttribute("WorkTaskId"), out Guid parsedWorkTaskId)
            ? parsedWorkTaskId
            : null;
        string? workTaskName = reader.GetAttribute("WorkTaskName");
        int? messageCount = int.TryParse(reader.GetAttribute("MessageCount"), out int count) ? count : null;
        DateTimeOffset? updatedTime = DateTimeOffset.TryParse
        (
            reader.GetAttribute("UpdatedTime"), CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out DateTimeOffset updated
        )
            ? updated
            : null;
        string? preview = null;
        if (formatVersion == CurrentFormatVersion && !reader.IsEmptyElement)
        {
            await reader.ReadAsync().ConfigureAwait(false);
            if (await reader.MoveToContentAsync().ConfigureAwait(false) == XmlNodeType.Element &&
                reader.Name == "Summary")
                preview = await reader.ReadElementContentAsStringAsync().ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();

        return new CopilotChatSessionSummary
        {
            SessionId = sessionId,
            Title = title,
            WorkspacePath = workspacePath,
            WorkTaskId = workTaskId,
            WorkTaskName = workTaskName,
            StartedTime = startedTime,
            MessageCount = messageCount,
            UpdatedTime = updatedTime,
            Summary = preview,
        };
    }

    internal static async Task SaveDocumentAsync
        (string filePath, XDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        string directory = Path.GetDirectoryName(filePath)
                           ?? throw new InvalidOperationException($"无法确定会话文件 '{filePath}' 的目录。");
        string temporaryFilePath = Path.Join(directory, $".{Path.GetFileName(filePath)}.{Guid.NewGuid():N}.tmp");
        XDocument sanitizedDocument = CreateSanitizedDocument(document);
        try
        {
            await using (var fileStream = new FileStream
                             (temporaryFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            await using (XmlWriter xmlWriter = XmlWriter.Create
                         (
                             fileStream, new XmlWriterSettings
                             {
                                 Async = true,
                                 Encoding = Utf8EncodingWithoutBom,
                                 Indent = true,
                                 IndentChars = "  ",
                                 NewLineChars = Environment.NewLine,
                                 NewLineHandling = NewLineHandling.Replace,
                                 OmitXmlDeclaration = true,
                             }
                         ))
            {
                await sanitizedDocument.SaveAsync(xmlWriter, cancellationToken).ConfigureAwait(false);
                await xmlWriter.FlushAsync().ConfigureAwait(false);
                await fileStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryFilePath, filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryFilePath))
            {
                File.Delete(temporaryFilePath);
            }
        }
    }

    private static XDocument CreateSanitizedDocument(XDocument document)
    {
        var sanitizedDocument = new XDocument(document);
        if (sanitizedDocument.Root is not { } rootElement)
        {
            return sanitizedDocument;
        }

        foreach (XAttribute attribute in rootElement.DescendantsAndSelf().Attributes())
        {
            attribute.Value = ReplaceInvalidXmlCharacters(attribute.Value);
        }

        foreach (XText textNode in rootElement.DescendantNodesAndSelf().OfType<XText>())
        {
            textNode.Value = ReplaceInvalidXmlCharacters(textNode.Value);
        }

        return sanitizedDocument;
    }

    private static string ReplaceInvalidXmlCharacters(string value)
    {
        StringBuilder? builder = null;
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (char.IsHighSurrogate(character)
                && index + 1 < value.Length
                && char.IsLowSurrogate(value[index + 1]))
            {
                if (builder is not null)
                {
                    builder.Append(character);
                    builder.Append(value[++index]);
                }
                else
                {
                    index++;
                }

                continue;
            }

            if (IsValidXmlCharacter(character))
            {
                builder?.Append(character);
                continue;
            }

            builder ??= new StringBuilder(value.Length).Append(value, 0, index);
            builder.Append('\uFFFD');
        }

        return builder?.ToString() ?? value;
    }

    private static bool IsValidXmlCharacter(char character) =>
        character is '\u0009' or '\u000A' or '\u000D'
        || character is >= '\u0020' and <= '\uD7FF'
        || character is >= '\uE000' and <= '\uFFFD';
}