using System;
using System.Threading;
using System.Threading.Tasks;
using AgentLib.Core.AgentApiManagers.LanguageModelProviders;
using AgentLib.Model;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Responses;
using PrismCodingAgent.CodingShell.Localizations;

#pragma warning disable OPENAI001, MAAI001

namespace PrismCodingAgent.CodingShell.Services.Chat;

internal static class SpeakPlainlyChat
{
    internal static async Task RunAsync(ILanguageModel model, CopilotChatSession session,
        CopilotChatMessage user, CopilotChatMessage assistant, bool preferResponses, CancellationToken cancellationToken)
    {
        var responses = session.ResponsesSession;
        if (responses.Items.Count > 0 && (preferResponses || session.AgentSession is null))
        {
            await RunResponsesAsync(model, responses, user, assistant, cancellationToken);
            return;
        }

        using var client = await model.GetChatClientAsync();
        cancellationToken.ThrowIfCancellationRequested();
        var agent = client.AsAIAgent(new ChatClientAgentOptions
        {
            ChatHistoryProvider = new InMemoryChatHistoryProvider(),
            RequirePerServiceCallChatHistoryPersistence = true,
        });
        var agentSession = session.AgentSession ?? await agent.CreateSessionAsync(cancellationToken);
        ChatMessage[] input = [user.ToChatMessage()];
        assistant.ClearPlaceholder();
        await foreach (var update in agent.RunStreamingAsync(input, agentSession, cancellationToken: cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var content in update.Contents)
            {
                switch (content)
                {
                    case TextContent { Text.Length: > 0 } text:
                        assistant.AppendText(text.Text);
                        break;
                    case TextReasoningContent { Text.Length: > 0 } reasoning:
                        assistant.AppendReasoning(reasoning.Text);
                        break;
                    case UsageContent usage:
                        assistant.AppendUsageDetails(usage.Details);
                        break;
                }
            }
        }
    }

    private static async Task RunResponsesAsync(ILanguageModel model, CopilotResponsesSession session,
        CopilotChatMessage user, CopilotChatMessage assistant, CancellationToken cancellationToken)
    {
        if (model is not IResponsesClientProvider provider)
        {
            throw new InvalidOperationException(Lang.Current.SpeakPlainlyResponsesUnavailable);
        }

        var client = await provider.GetResponsesClientAsync();
        session.RepairToolCallHistory();
        var request = new CreateResponseOptions
        {
            Model = client.ModelId,
            StreamingEnabled = true,
            StoredOutputEnabled = false,
        };
        foreach (var item in session.Items)
        {
            request.InputItems.Add(item);
        }
        request.InputItems.Add(ResponseItem.CreateUserMessageItem(user.Content));

        assistant.ClearPlaceholder();
        await foreach (var update in client.Client.CreateResponseStreamingAsync(request, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            assistant.ResponseInfo.AppendResponseUpdate(update);
            if (update is StreamingResponseErrorUpdate error)
            {
                throw new InvalidOperationException(error.Message);
            }
        }

        var response = assistant.ResponseInfo.CurrentResponse;
        if (response?.Status != ResponseStatus.Completed)
        {
            throw new InvalidOperationException(response?.Error?.Message ?? Lang.Current.SpeakPlainlyIncomplete);
        }
    }
}
