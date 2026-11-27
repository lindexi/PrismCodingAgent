using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AgentLib.Coding;
using AgentLib.Coding.Tools;
using Microsoft.Extensions.AI;

namespace PrismCodingAgent.CodingShell.Services.Chat;

internal interface ICodingChatRunner
{
    Task<ICodingAgentRunResult> RunAsync
    (
        IReadOnlyList<AIContent> contents,
        CodingToolContext toolContext,
        CancellationToken cancellationToken
    );

    Task<bool> TryCompactConversationAsync(AgentLib.Model.CopilotChatSession session,
        string? instructions, CancellationToken cancellationToken) => Task.FromResult(false);

    Task InjectMessageAsync
    (
        IReadOnlyList<AIContent> contents,
        CancellationToken cancellationToken
    )
        => throw new NotSupportedException();
}