using AgentLib.Model;
using Microsoft.Extensions.AI;

namespace AgentLib.Coding;

/// <summary>
/// 保存一次编程执行的会话、模型客户端与输出目标。
/// </summary>
public sealed class CodingRunContext
{
    /// <summary>创建编程执行上下文。</summary>
    public CodingRunContext(IChatClient chatClient, CopilotChatMessage assistantChatMessage,
        CopilotChatSession session, ICopilotChatOutput? output = null)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentNullException.ThrowIfNull(assistantChatMessage);
        ArgumentNullException.ThrowIfNull(session);
        ChatClient = chatClient;
        AssistantChatMessage = assistantChatMessage;
        Session = session;
        Output = output ?? assistantChatMessage;
    }

    /// <summary>本次执行使用的模型客户端。</summary>
    public IChatClient ChatClient { get; }

    /// <summary>本次执行所属的会话及其模型历史。</summary>
    public CopilotChatSession Session { get; }

    /// <summary>供宿主读取和保存的助手消息。</summary>
    public CopilotChatMessage AssistantChatMessage { get; }

    /// <summary>接收本次执行的内容、工具展示和用量信息。</summary>
    public ICopilotChatOutput Output { get; }

    internal Task InvokeOutputAsync(Action action)
    {
        if (MainThreadDispatcher is { } dispatcher)
        {
            return dispatcher.InvokeAsync(() =>
            {
                action();
                return Task.CompletedTask;
            });
        }

        action();
        return Task.CompletedTask;
    }

    /// <summary>输出绑定 UI 时使用的线程调度器。</summary>
    public IMainThreadDispatcher? MainThreadDispatcher => Session.MainThreadDispatcher;
}
