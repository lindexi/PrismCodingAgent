using AgentLib.Model;

using System;
using System.Collections.Generic;
using System.Text.Json;

namespace AgentLib.Logging.Sessions;

/// <summary>
/// 表示从持久化存储读取的 Copilot 会话数据。
/// </summary>
public sealed record CopilotChatSessionPersistenceData
{
    /// <summary>捕获会话的完整持久化数据，代理协议状态由运行时提供。</summary>
    public static async System.Threading.Tasks.Task<CopilotChatSessionPersistenceData> CaptureAsync(
        CopilotChatSession session, JsonElement? agentSessionState,
        System.Threading.CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        var additionalInfo = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var item in session.AdditionalInfo)
        {
            additionalInfo.Add(item.Key, (await item.Value.SerializeAsync(cancellationToken).ConfigureAwait(false)).Clone());
        }
        return new CopilotChatSessionPersistenceData
        {
            FormatVersion = FileCopilotChatSessionStore.CurrentFormatVersion,
            SessionId = session.SessionId,
            StartedTime = session.StartedTime,
            Title = session.Title,
            WorkspacePath = session.WorkspacePath,
            WorkTaskId = session.WorkTaskId,
            WorkTaskName = session.WorkTaskName,
            Entries = System.Linq.Enumerable.ToArray(session.Entries),
            AgentSessionState = agentSessionState,
            AdditionalInfo = additionalInfo,
            ResponsesSession = session.ExistingResponsesSession,
        };
    }

    /// <summary>
    /// 获取持久化格式版本。
    /// </summary>
    public required int FormatVersion { get; init; }

    /// <summary>
    /// 获取会话唯一标识符。
    /// </summary>
    public required Guid SessionId { get; init; }

    /// <summary>
    /// 获取会话开始时间。
    /// </summary>
    public required DateTimeOffset StartedTime { get; init; }

    /// <summary>
    /// 获取持久化的会话标题。
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// 获取持久化的可选工作路径。
    /// </summary>
    public string? WorkspacePath { get; init; }

    /// <summary>
    /// 获取持久化的可选工作任务标识。
    /// </summary>
    public Guid? WorkTaskId { get; init; }

    /// <summary>
    /// 获取持久化的可选工作任务名称。
    /// </summary>
    public string? WorkTaskName { get; init; }

    /// <summary>按存储顺序恢复的完整时间线。</summary>
    public IReadOnlyList<CopilotChatEntry> Entries { get; init; } = Array.Empty<CopilotChatEntry>();

    /// <summary>
    /// 获取序列化的代理会话状态；尚未创建代理状态时为 <see langword="null"/>。
    /// </summary>
    public JsonElement? AgentSessionState { get; init; }

    /// <summary>尚未被业务消费的附加 JSON 数据。</summary>
    public IReadOnlyDictionary<string, JsonElement> AdditionalInfo { get; init; }
        = new Dictionary<string, JsonElement>(StringComparer.Ordinal);

    /// <summary>
    /// 获取由存储恢复的原生 Responses 会话；旧文件没有此状态时为 null。
    /// </summary>
    public CopilotResponsesSession? ResponsesSession { get; init; }
}
