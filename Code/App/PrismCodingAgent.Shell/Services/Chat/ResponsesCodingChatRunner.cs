using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AgentLib;
using AgentLib.Coding;
using AgentLib.Model;
using Microsoft.Extensions.AI;

namespace PrismCodingAgent.CodingShell.Services.Chat;

internal sealed class ResponsesCodingChatRunner(CopilotChatManager chatManager, ResponsesCodingAgent agent) : ICodingChatRunner
{
    public async Task<bool> TryCompactConversationAsync(CopilotChatSession session,
        string? instructions, CancellationToken cancellationToken)
    {
        var context = await chatManager.CreateManualSendMessageContextAsync(session, cancellationToken).ConfigureAwait(false);
        return await agent.TryCompactConversationAsync(context.LanguageModel, session.ResponsesSession,
            instructions, cancellationToken).ConfigureAwait(false);
    }

    private ICodingAgentRunResult? _activeRun;

    public Task InjectMessageAsync(IReadOnlyList<AIContent> contents, CancellationToken cancellationToken)
    {
        var run = _activeRun ?? throw new InvalidOperationException("当前没有正在运行的 Responses 代理。");
        return CodingChatInterruption.InjectAndDisplayAsync(run, contents,
            chatManager.MainThreadDispatcher, cancellationToken);
    }

    public async Task<ICodingAgentRunResult> RunAsync(IReadOnlyList<AIContent> contents,
        CodingToolContext toolContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(toolContext);
        var session = toolContext.Session;
        var context = await chatManager.CreateManualSendMessageContextAsync(session, cancellationToken).ConfigureAwait(false);
        var run = await agent.RunAsync(context, toolContext, contents, cancellationToken).ConfigureAwait(false);
        _activeRun = run;
        return new CompletedCodingAgentRunResult(run.AssistantChatMessage, CompleteAsync(run, session.SessionId));
    }

    private async Task<string?> CompleteAsync(ICodingAgentRunResult run, Guid sessionId)
    {
        try
        {
            return await run.CompletionTask.ConfigureAwait(false);
        }
        finally
        {
            try
            {
                await chatManager.ChatLogger.LogMessageAsync(sessionId, run.AssistantChatMessage).ConfigureAwait(false);
            }
            finally
            {
                _activeRun = null;
            }
        }
    }
}
