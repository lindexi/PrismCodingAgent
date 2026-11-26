using AgentLib.Model;
using PrismCodingAgent.CodingShell.Services.Settings;

namespace AgentLib.Coding;

/// <summary>提供本轮编程工具使用的会话、工作区与生效设置。</summary>
public sealed record CodingToolContext
{
    /// <summary>本轮运行实际所属的会话。</summary>
    public required CopilotChatSession Session { get; init; }

    /// <summary>本轮使用的工作区；未选择工作区时为空。</summary>
    public string? WorkspacePath { get; init; }

    /// <summary>本轮使用的聊天运行选项。</summary>
    public CodingChatRunOptions Options { get; init; } = CodingChatRunOptions.Default;

    /// <summary>宿主在本轮开始时提供的生效全局设置。</summary>
    public CodingChatShellSettings? Settings { get; init; }
}
