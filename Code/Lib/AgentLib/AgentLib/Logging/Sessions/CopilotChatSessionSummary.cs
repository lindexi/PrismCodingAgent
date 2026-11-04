using System;

namespace AgentLib.Logging.Sessions;

/// <summary>
/// 表示持久化 Copilot 会话的列表摘要。
/// </summary>
public sealed record CopilotChatSessionSummary
{
    /// <summary>
    /// 获取会话唯一标识符。
    /// </summary>
    public required Guid SessionId { get; init; }

    /// <summary>
    /// 获取会话标题。
    /// </summary>
    public required string Title { get; init; }

    /// <summary>入口保存的简短内容预览。</summary>
    public string? Summary { get; init; }

    /// <summary>会话内容最近保存时间；旧格式未记录时为空。</summary>
    public DateTimeOffset? UpdatedTime { get; init; }

    /// <summary>
    /// 获取会话工作路径；未设置时为空。
    /// </summary>
    public string? WorkspacePath { get; init; }

    /// <summary>
    /// 获取会话所属工作任务的唯一标识；旧会话未记录时为空。
    /// </summary>
    public Guid? WorkTaskId { get; init; }

    /// <summary>
    /// 获取会话所属工作任务的名称；旧会话未记录时为空。
    /// </summary>
    public string? WorkTaskName { get; init; }

    /// <summary>
    /// 获取会话开始时间。
    /// </summary>
    public required DateTimeOffset StartedTime { get; init; }

    /// <summary>
    /// 获取入口记录的会话消息数；旧格式未记录时为空。
    /// </summary>
    public int? MessageCount { get; init; }
}
