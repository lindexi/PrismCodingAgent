using AgentLib.Model;

namespace AgentLib.Logging;

/// <summary>
/// 记录归属于固定会话的聊天消息与诊断信息。
/// </summary>
public interface ISessionCopilotChatLogger
{
    /// <summary>记录一条聊天消息。</summary>
    Task LogMessageAsync(CopilotChatMessage chatMessage);

    /// <summary>记录诊断信息及相关原始异常。</summary>
    Task LogDiagnosticAsync(string category, string message, Exception? exception = null);
}
