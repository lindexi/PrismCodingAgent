using AgentLib.Model;
using Microsoft.Extensions.AI;

namespace AgentLib.Coding.Tests;

internal static class CodingChatTestHost
{
    internal static Task<ICodingAgentRunResult> RunAsync(this CodingAgent agent,
        IManualSendMessageContext host, string prompt, string? workspacePath,
        bool enableAutomaticCompression = true, bool enableDotNetRun = false,
        CancellationToken cancellationToken = default) =>
        RunAsync(agent, host, [new TextContent(prompt)], workspacePath,
            enableAutomaticCompression, enableDotNetRun, cancellationToken);

    internal static async Task<ICodingAgentRunResult> RunAsync(this CodingAgent agent,
        IManualSendMessageContext host, IReadOnlyList<AIContent> contents, string? workspacePath,
        bool enableAutomaticCompression = true, bool enableDotNetRun = false,
        CancellationToken cancellationToken = default)
    {
        AIContent[] snapshot = [.. contents];
        var session = new CopilotChatSession { MainThreadDispatcher = host.MainThreadDispatcher };
        session.SetAgentSession(await host.GetAgentSessionAsync(cancellationToken));
        var context = new CodingRunContext(host.ChatClient, host.AssistantChatMessage, session);
        using IDisposable chatting = host.StartChatting();
        var userMessage = new CopilotChatMessage(ChatRole.User, snapshot);
        foreach (ICopilotChatMessageItem item in userMessage.MessageItems)
        {
            host.UserChatMessage.MessageItems.Add(item);
        }
        await host.AppendMessagesToSessionAsync();
        return await agent.RunAsync(context, snapshot, workspacePath,
            new CodingChatRunOptions(enableAutomaticCompression, enableDotNetRun, null), cancellationToken);
    }
}
