using AgentLib;
using AgentLib.Coding;
using AgentLib.Logging;
using AgentLib.Model;
using PrismCodingAgent.AvaloniaShell.Services.Sessions;
using PrismCodingAgent.AvaloniaShell.ViewModels;
using AgentLib.Core.AgentApiManagers.Contexts;
using AgentLib.Core.AgentApiManagers.LanguageModelProviders.Fakes;
using Microsoft.Extensions.AI;
using PrismCodingAgent.AvaloniaShell.Services.Chat;
using System.Runtime.CompilerServices;

namespace PrismCodingAgent.AvaloniaShell.Tests;

[TestClass]
public sealed class CodingChatContinuationTests
{
    [TestMethod]
    [Timeout(10000)]
    public async Task ContinueAsyncAfterFailedRunShouldReuseSessionAndAssistantWithoutAddingEntries()
    {
        var requests = new List<ChatMessage[]>();
        var client = new FakeChatClient
        {
            OnGetStreamingResponseAsync = (messages, _, cancellationToken) =>
            {
                requests.Add(messages.ToArray());
                return RespondAsync(requests.Count, cancellationToken);
            },
        };
        var manager = new CopilotChatManager();
        var model = new FakeLanguageModel(client)
        {
            ModelDefinition = new ModelDefinition
            {
                Provider = "fake", ModelId = "fake", ModelName = "Fake",
            },
        };
        manager.AgentApiEndpointManager.RegisterLanguageModelProvider(new FakeLanguageModelProvider([model]));
        await using var agent = new CodingAgent();
        var runner = new CodingAgentChatRunner(manager, agent);
        var options = new CodingChatRunOptions(false, false, null);
        var session = manager.SelectedSession;
        int initialEntryCount = session.Entries.Count;

        ICodingAgentRunResult first = await runner.RunAsync([new TextContent("original user")], null,
            options, CancellationToken.None);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => first.CompletionTask);
        Assert.HasCount(1, requests);
        Assert.AreEqual("A", first.AssistantChatMessage.Content);
        Assert.IsFalse(manager.IsChatting);
        var originalSession = session.AgentSession;
        var originalEntries = session.Entries.ToArray();
        Assert.HasCount(initialEntryCount + 2, originalEntries);

        ICodingAgentRunResult second = await runner.ContinueAsync(session, first.AssistantChatMessage, null,
            options, CancellationToken.None);
        Assert.AreEqual("AB", await second.CompletionTask);

        Assert.HasCount(2, requests);
        CollectionAssert.AreEqual(new[] { "original user" }, requests[1]
            .Where(message => message.Role == ChatRole.User).Select(message => message.Text).ToArray());
        CollectionAssert.AreEqual(new[] { "A" }, requests[1]
            .Where(message => message.Role == ChatRole.Assistant).Select(message => message.Text).ToArray());
        Assert.AreSame(originalSession, session.AgentSession);
        Assert.AreSame(first.AssistantChatMessage, second.AssistantChatMessage);
        Assert.AreEqual("AB", first.AssistantChatMessage.Content);
        CollectionAssert.AreEqual(originalEntries, session.Entries.ToArray());
        Assert.IsFalse(manager.IsChatting);
    }

    [TestMethod]
    [Timeout(10000)]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RetryCommandShouldContinueOriginalMessageAndPreserveDraft(bool failContinuation)
    {
        int requests = 0;
        var continueCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new FakeChatClient
        {
            OnGetStreamingResponseAsync = (_, _, cancellationToken) =>
                RespondAsync(++requests, cancellationToken, continueCompletion.Task, failContinuation),
        };
        var manager = new CopilotChatManager();
        var model = new FakeLanguageModel(client)
        {
            ModelDefinition = new ModelDefinition
            {
                Provider = "fake", ModelId = "fake", ModelName = "Fake",
            },
        };
        manager.AgentApiEndpointManager.RegisterLanguageModelProvider(new FakeLanguageModelProvider([model]));
        await using var agent = new CodingAgent();
        var runner = new CodingAgentChatRunner(manager, agent);
        var store = new SessionStore();
        var controller = CodingWorkTaskControllerTestFactory.CreateApplication(manager, store, runner,
            codingAgent: agent);
        using var viewModel = new ChatViewModel(manager, controller, "test");
        var session = manager.SelectedSession;
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => controller.SendMessageAsync(
            [new TextContent("original user")], new CodingChatRunOptions(false, false, null)));
        var assistant = session.ChatMessages.Last();
        var notice = (NoticeItemViewModel)viewModel.Messages.Last();
        var originalSession = session.AgentSession;
        var originalEntries = session.Entries.ToArray();
        viewModel.InputText = "unsent draft";
        Assert.IsTrue(notice.HasContinuation);
        Assert.IsNotNull(notice.RetryCommand);
        Assert.IsTrue(notice.RetryCommand.CanExecute(notice.Notice));

        Task retry = ((SimpleAsyncCommand<CopilotChatNotice>)notice.RetryCommand).ExecuteAsync(notice.Notice);
        Assert.IsFalse(notice.RetryCommand.CanExecute(notice.Notice));
        Assert.IsFalse(controller.CanContinueChat(notice.Notice));
        continueCompletion.SetResult();
        await retry;

        Assert.AreEqual("AB", assistant.Content);
        Assert.AreSame(originalSession, session.AgentSession);
        CollectionAssert.AreEqual(originalEntries, session.Entries.Take(originalEntries.Length).ToArray());
        Assert.AreEqual(originalEntries.Length + (failContinuation ? 1 : 0), session.Entries.Count);
        var latestNotice = (NoticeItemViewModel)viewModel.Messages.Last();
        Assert.AreEqual(failContinuation, latestNotice.HasContinuation);
        Assert.IsNotNull(latestNotice.RetryCommand);
        Assert.AreEqual(failContinuation, latestNotice.RetryCommand.CanExecute(latestNotice.Notice));
        Assert.AreEqual("unsent draft", viewModel.InputText);
        Assert.IsFalse(notice.HasContinuation);
        Assert.IsFalse(notice.RetryCommand.CanExecute(notice.Notice));
        Assert.AreSame(notice.RetryCommand, latestNotice.RetryCommand);
        Assert.AreEqual(2, requests);
        Assert.AreEqual("AB", store.LastSavedAssistantContent);
        Assert.IsFalse(manager.IsChatting);
    }

    private sealed class SessionStore : ICodingChatSessionStore
    {
        public string? LastSavedAssistantContent { get; private set; }
        public Task<IReadOnlyList<CopilotChatSessionSummary>> ListSessionsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CopilotChatSessionSummary>>([]);
        public Task<CopilotChatSession> LoadSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<bool> DeleteSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
        public Task SaveSessionAsync(CopilotChatSession session, CancellationToken cancellationToken = default)
        {
            LastSavedAssistantContent = session.ChatMessages.LastOrDefault()?.Content;
            return Task.CompletedTask;
        }
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> RespondAsync(int request,
        [EnumeratorCancellation] CancellationToken cancellationToken, Task? continueCompletion = null, bool failContinuation = false)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request > 1 && continueCompletion is not null) await continueCompletion.WaitAsync(cancellationToken);
        yield return new ChatResponseUpdate(ChatRole.Assistant, [new TextContent(request == 1 ? "A" : "B")]);
        await Task.Yield();
        if (request == 1 || (request == 2 && failContinuation))
        {
            throw new InvalidOperationException("Non-retryable fake failure.");
        }
    }
}
