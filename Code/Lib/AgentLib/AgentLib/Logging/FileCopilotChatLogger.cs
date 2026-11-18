using AgentLib.Core;
using AgentLib.Model;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AgentLib.Logging;

/// <summary>
/// 将聊天消息追加到文件系统中的人类可读文本日志。
/// </summary>
public sealed class FileCopilotChatLogger : ICopilotChatLogger
{
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly Dictionary<string, string> _logFilePaths = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 使用默认日志目录创建日志记录器。
    /// </summary>
    public FileCopilotChatLogger()
        : this(Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AgentLib", "CopilotChatLogs"))
    {
    }

    /// <summary>
    /// 使用指定的日志目录创建日志记录器。
    /// </summary>
    /// <param name="chatLogFolder">聊天日志文件夹路径。</param>
    public FileCopilotChatLogger(string chatLogFolder)
    {
        ArgumentHelper.ThrowIfNullOrWhiteSpace(chatLogFolder);
        ChatLogFolder = chatLogFolder;
    }

    /// <summary>
    /// 聊天日志文件夹路径。
    /// </summary>
    public string ChatLogFolder { get; }

    /// <inheritdoc/>
    public ISessionCopilotChatLogger CreateSessionLogger(string logName)
    {
        ArgumentHelper.ThrowIfNullOrWhiteSpace(logName);
        return new SessionLogger(this, logName);
    }

    private sealed class SessionLogger(FileCopilotChatLogger logger, string logName) : ISessionCopilotChatLogger
    {
        public Task LogMessageAsync(CopilotChatMessage chatMessage) => logger.WriteMessageAsync(logName, chatMessage);

        public Task LogDiagnosticAsync(string category, string message, Exception? exception = null) =>
            logger.WriteDiagnosticAsync(logName, category, message, exception);
    }

    /// <inheritdoc/>
    public Task LogMessageAsync(Guid sessionId, CopilotChatMessage chatMessage)
        => WriteMessageAsync(sessionId.ToString("N"), chatMessage);

    private Task WriteMessageAsync(string logName, CopilotChatMessage chatMessage)
    {
        ArgumentNullException.ThrowIfNull(chatMessage);
        return AppendAsync(logName, chatMessage.CreatedTime, builder =>
        {
            builder.Append(chatMessage.Author).AppendLine(":");
            builder.AppendLine(chatMessage.FullContent);

            if (chatMessage.TotalUsageDetails is { } usageDetails)
            {
                builder.AppendLine("用量:");
                AppendUsageLine(builder, "总计", usageDetails.TotalTokenCount);
                AppendUsageLine(builder, "输入", usageDetails.InputTokenCount);
                AppendUsageLine(builder, "输出", usageDetails.OutputTokenCount);
                AppendUsageLine(builder, "思考", usageDetails.ReasoningTokenCount);
                AppendUsageLine(builder, "缓存", usageDetails.CachedInputTokenCount);
            }
        });
    }

    /// <inheritdoc/>
    public Task LogDiagnosticAsync(Guid sessionId, string category, string message, Exception? exception = null)
        => WriteDiagnosticAsync(sessionId.ToString("N"), category, message, exception);

    private Task WriteDiagnosticAsync(string logName, string category, string message, Exception? exception)
    {
        ArgumentHelper.ThrowIfNullOrWhiteSpace(category);
        ArgumentHelper.ThrowIfNullOrWhiteSpace(message);
        return AppendAsync(logName, DateTimeOffset.Now, builder =>
        {
            builder.Append("诊断/").Append(category).AppendLine(":");
            builder.AppendLine(message);
            if (exception is not null)
            {
                builder.AppendLine("异常:");
                builder.AppendLine(exception.ToString());
            }
        });
    }

    private async Task AppendAsync(string logName, DateTimeOffset createdTime, Action<StringBuilder> appendContent)
    {
        await _writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            string logFilePath = GetLogFilePath(logName, createdTime);
            var builder = new StringBuilder();
            builder.Append('[')
                .Append(createdTime.ToString("yyyy-MM-dd HH:mm:ss.fff zzz"))
                .AppendLine("]");
            appendContent(builder);
            builder.AppendLine();
            await File.AppendAllTextAsync(logFilePath, builder.ToString(), Encoding.UTF8).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private string GetLogFilePath(string logName, DateTimeOffset createdTime)
    {
        if (_logFilePaths.TryGetValue(logName, out string? logFilePath))
        {
            return logFilePath;
        }

        string dayFolderPath = Path.Join(ChatLogFolder, createdTime.ToString("yyyyMMdd"));
        Directory.CreateDirectory(dayFolderPath);

        logFilePath = Path.Join(dayFolderPath, $"{createdTime:yyyyMMdd_HHmmss}_{logName}.log");
        _logFilePaths[logName] = logFilePath;
        return logFilePath;
    }

    private static void AppendUsageLine(StringBuilder builder, string label, long? value)
    {
        if (value is null)
        {
            return;
        }

        builder.Append("- ")
            .Append(label)
            .Append(": ")
            .AppendLine(value.Value.ToString("N0"));
    }
}
