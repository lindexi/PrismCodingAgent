namespace AgentLib.Model;

/// <summary>
/// 会话时间线中的条目，不隐含协议角色。
/// </summary>
public abstract class CopilotChatEntry : NotifyBase
{
    /// <summary>条目的创建时间。</summary>
    public System.DateTimeOffset CreatedTime { get; protected set; } = System.DateTimeOffset.Now;

    /// <summary>显示用的创建时间。</summary>
    public string TimeText => CreatedTime.ToString("HH:mm");
}

/// <summary>应用通知的严重程度。</summary>
public enum CopilotChatNoticeLevel
{
    Information,
    Warning,
    Error,
}

/// <summary>不参与模型对话的应用通知。</summary>
public sealed class CopilotChatNotice : CopilotChatEntry
{
    /// <summary>创建应用通知，可指定恢复时使用的原始时间。</summary>
    public CopilotChatNotice(string content, CopilotChatNoticeLevel level = CopilotChatNoticeLevel.Information,
        System.DateTimeOffset? createdTime = null)
    {
        System.ArgumentNullException.ThrowIfNull(content);
        Content = content;
        Level = level;
        CreatedTime = createdTime ?? System.DateTimeOffset.Now;
    }

    /// <summary>通知正文。</summary>
    public string Content { get; }

    /// <summary>通知级别。</summary>
    public CopilotChatNoticeLevel Level { get; }
}
