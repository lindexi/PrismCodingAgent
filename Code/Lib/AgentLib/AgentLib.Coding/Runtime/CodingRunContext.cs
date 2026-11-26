using AgentLib.Core.AgentApiManagers.LanguageModelProviders;
using AgentLib.Coding.Settings;
using AgentLib.Coding.Tools;
using AgentLib.Model;
using Microsoft.Extensions.AI;

namespace AgentLib.Coding;

/// <summary>
/// 保存一次编程执行的会话、模型客户端与输出目标。
/// </summary>
public sealed class CodingRunContext
{
    /// <summary>使用本轮工具设置快照创建编程执行上下文。</summary>
    public CodingRunContext(CodingToolContext toolContext, IChatClient chatClient,
        CopilotChatMessage assistantChatMessage, ICopilotChatOutput? output = null)
    {
        ArgumentNullException.ThrowIfNull(toolContext);
        ArgumentNullException.ThrowIfNull(toolContext.Session);
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentNullException.ThrowIfNull(assistantChatMessage);
        ToolContext = toolContext;
        ChatClient = chatClient;
        AssistantChatMessage = assistantChatMessage;
        Output = output ?? assistantChatMessage;
    }

    /// <summary>本次执行使用的工具设置快照。</summary>
    public CodingToolContext ToolContext { get; }

    /// <summary>本次执行使用的工作区。</summary>
    public string? WorkspacePath => ToolContext.WorkspacePath;

    /// <summary>本次执行使用的运行选项。</summary>
    public CodingChatRunOptions Options => ToolContext.Options;

    /// <summary>本次执行使用的全局设置。</summary>
    public CodingChatShellSettings? Settings => ToolContext.Settings;

    /// <summary>本次执行的工具可复用的 HTTP 客户端。</summary>
    public HttpClient? HttpClient => ToolContext.HttpClient;

    internal CodingRunContext WithToolContext(CodingToolContext toolContext)
    {
        ArgumentNullException.ThrowIfNull(toolContext);
        if (!ReferenceEquals(Session, toolContext.Session))
        {
            throw new ArgumentException("编程运行与工具上下文必须使用同一会话。", nameof(toolContext));
        }

        return new CodingRunContext(toolContext, ChatClient, AssistantChatMessage, Output)
        {
            ModelResolver = ModelResolver,
        };
    }

    internal CodingRunContext NormalizeWorkspacePath()
    {
        string? path = string.IsNullOrWhiteSpace(WorkspacePath) ? null : Path.GetFullPath(WorkspacePath);
        return string.Equals(path, WorkspacePath, StringComparison.Ordinal)
            ? this
            : WithToolContext(ToolContext with { WorkspacePath = path });
    }

    /// <summary>本次执行使用的模型客户端。</summary>
    public IChatClient ChatClient { get; }

    /// <summary>供子任务按需查询当前用途模型；未指定时沿用本次执行的聊天客户端。</summary>
    public ILanguageModelResolver? ModelResolver { get; init; }

    /// <summary>本次执行所属的会话及其模型历史。</summary>
    public CopilotChatSession Session => ToolContext.Session;

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
