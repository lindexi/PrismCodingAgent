using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AgentLib;
using AgentLib.Coding;
using AgentLib.Model;
using Microsoft.Extensions.AI;
using PrismCodingAgent.CodingShell.Localizations;

namespace PrismCodingAgent.CodingShell.Services.Chat;

internal static class CodingChatInterruption
{
    internal static async Task InjectAndDisplayAsync(ICodingAgentRunResult run, IReadOnlyList<AIContent> contents,
        IMainThreadDispatcher? dispatcher, CancellationToken cancellationToken)
    {
        await run.InjectMessageAsync(contents, cancellationToken).ConfigureAwait(false);

        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            await dispatcher.InvokeAsync(AppendCardAsync).ConfigureAwait(false);
        }
        else
        {
            await AppendCardAsync().ConfigureAwait(false);
        }

        Task AppendCardAsync()
        {
            CopilotChatMessage assistant = run.AssistantChatMessage;
            assistant.ClearPlaceholder();
            assistant.MessageItems.Add(new CopilotChatToolItem(Guid.NewGuid().ToString("N"), Lang.Current.InterruptText, null)
            {
                Details = CopilotChatMessage.CreateUser(contents),
            });
            return Task.CompletedTask;
        }
    }
}
