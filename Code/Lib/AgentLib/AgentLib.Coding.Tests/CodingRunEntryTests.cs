using AgentLib.Core.AgentApiManagers.LanguageModelProviders.Fakes;
using AgentLib.Model;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;

namespace AgentLib.Coding.Tests;

[TestClass]
public sealed class CodingRunEntryTests
{
    [TestMethod]
    public void CodingRunContextWhenCreatedShouldKeepProvidedSession()
    {
        var session = new CopilotChatSession();
        var message = CopilotChatMessage.CreateAssistant(string.Empty, isPresetInfo: false);

        var context = new CodingRunContext(new FakeChatClient(), message, session);

        Assert.AreSame(session, context.Session);
    }

    [TestMethod]
    public void CodingRunContextWhenSessionIsNullShouldRejectIt()
    {
        var message = CopilotChatMessage.CreateAssistant(string.Empty, isPresetInfo: false);

        Assert.ThrowsExactly<ArgumentNullException>(() => new CodingRunContext(new FakeChatClient(), message, null!));
    }

    [TestMethod]
    public async Task RunAsyncWhenSessionHasNoModelHistoryShouldInitializeProvidedSession()
    {
        var client = new FakeChatClient
        {
            OnGetStreamingResponseAsync = (_, _, cancellationToken) => RespondAsync(cancellationToken),
        };
        var session = new CopilotChatSession();
        var context = new CodingRunContext(client,
            CopilotChatMessage.CreateAssistant(string.Empty, isPresetInfo: false), session);
        await using var agent = new CodingAgent();

        ICodingAgentRunResult run = await agent.RunAsync(context, [new TextContent("task")], null);
        await run.CompletionTask;

        Assert.IsNotNull(session.AgentSession);
    }

    [TestMethod]
    public async Task RunAsyncWhenContentsAreEmptyShouldNotAddUserMessage()
    {
        var requests = new List<ChatMessage[]>();
        var client = new FakeChatClient
        {
            OnGetStreamingResponseAsync = (messages, _, cancellationToken) =>
            {
                requests.Add(messages.ToArray());
                return RespondAsync(cancellationToken);
            },
        };
        await using var agent = new CodingAgent();
        var context = new CodingRunContext(client,
            CopilotChatMessage.CreateAssistant(string.Empty, isPresetInfo: false), new CopilotChatSession());

        ICodingAgentRunResult run = await agent.RunAsync(context, [], null);
        await run.CompletionTask;

        Assert.IsFalse(requests.SelectMany(messages => messages).Any(message => message.Role == ChatRole.User));
    }

    [TestMethod]
    public async Task RunAsyncWhenOptionsAreOmittedShouldCompleteConversation()
    {
        var client = new FakeChatClient
        {
            OnGetStreamingResponseAsync = (_, _, cancellationToken) => RespondAsync(cancellationToken),
        };
        await using var agent = new CodingAgent();
        var context = new CodingRunContext(client,
            CopilotChatMessage.CreateAssistant(string.Empty, isPresetInfo: false), new CopilotChatSession());

        ICodingAgentRunResult run = await agent.RunAsync(context, [new TextContent("task")], null);

        Assert.AreEqual("done", await run.CompletionTask);
    }

    [TestMethod]
    public void CodingRunContextWhenCreatedShouldUseAssistantMessageAsOutput()
    {
        var message = CopilotChatMessage.CreateAssistant(string.Empty, isPresetInfo: false);
        var context = new CodingRunContext(new FakeChatClient(), message, new CopilotChatSession());

        Assert.AreSame(message, context.Output);
    }

    [TestMethod]
    public async Task RunAsyncWhenMessageHasPlaceholderShouldReplaceItWithOutput()
    {
        var client = new FakeChatClient
        {
            OnGetStreamingResponseAsync = (_, _, cancellationToken) => RespondAsync(cancellationToken),
        };
        await using var agent = new CodingAgent();
        var message = CopilotChatMessage.CreateAssistant(CopilotChatMessage.PlaceholderContent, isPresetInfo: false);
        var context = new CodingRunContext(client, message, new CopilotChatSession());

        ICodingAgentRunResult run = await agent.RunAsync(context, [new TextContent("task")], null);
        await run.CompletionTask;

        Assert.AreEqual("done", message.Content);
    }

    [TestMethod]
    public async Task RunAsyncWithEmptyContentsAfterFailureShouldAppendToOriginalAssistantUsingExistingHistory()
    {
        var requests = new List<ChatMessage[]>();
        var client = new FakeChatClient
        {
            OnGetStreamingResponseAsync = (messages, _, cancellationToken) =>
            {
                requests.Add(messages.ToArray());
                return RespondThenFailAsync(requests.Count, cancellationToken);
            },
        };
        await using var agent = new CodingAgent();
        var message = CopilotChatMessage.CreateAssistant(string.Empty, isPresetInfo: false);
        var context = new CodingRunContext(client, message, new CopilotChatSession());
        var options = new CodingChatRunOptions(false, false, null);
        ICodingAgentRunResult first = await agent.RunAsync(context, [new TextContent("task")], null, options);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => first.CompletionTask);
        Assert.HasCount(1, requests);
        Assert.AreEqual("A", message.Content);
        var session = context.Session;
        var agentSession = session.AgentSession;
        var continuationContext = new CodingRunContext(client, message, session);

        ICodingAgentRunResult second = await agent.RunAsync(continuationContext, [], null, options);
        Assert.AreEqual("AB", await second.CompletionTask);
        Assert.AreSame(agentSession, continuationContext.Session.AgentSession);
        Assert.AreSame(message, second.AssistantChatMessage);
        Assert.HasCount(2, requests);
        CollectionAssert.AreEqual(new[] { "task" }, requests[1]
            .Where(item => item.Role == ChatRole.User).Select(item => item.Text).ToArray());
        CollectionAssert.AreEqual(new[] { "A" }, requests[1]
            .Where(item => item.Role == ChatRole.Assistant).Select(item => item.Text).ToArray());
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> RespondThenFailAsync(int request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        yield return new ChatResponseUpdate(ChatRole.Assistant, [new TextContent(request == 1 ? "A" : "B")]);
        await Task.Yield();
        if (request == 1)
        {
            throw new InvalidOperationException("Non-retryable fake failure.");
        }
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> RespondAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        yield return new ChatResponseUpdate(ChatRole.Assistant, [new TextContent("done")]);
        await Task.CompletedTask;
    }
}
