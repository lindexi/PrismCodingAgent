using AgentLib;
using AgentLib.Coding;
using AgentLib.Core.AgentApiManagers.Contexts;
using AgentLib.Core.AgentApiManagers.LanguageModelProviders.Fakes;
using AgentLib.Logging.Sessions;
using AgentLib.Model;
using Microsoft.Extensions.AI;
using OpenAI.Responses;
using PrismCodingAgent.CodingShell.Services.Chat;
using PrismCodingAgent.CodingShell.Services.Sessions;
using PrismCodingAgent.CodingShell.ViewModels;
using System.Runtime.CompilerServices;

#pragma warning disable OPENAI001

namespace PrismCodingAgent.AvaloniaShell.Tests;

[TestClass]
public sealed class SessionSnapshotTests
{
    [TestMethod]
    public async Task RestoredSessionShouldHaveIndependentIdentityAndPreserveSnapshotIds()
    {
        var manager = new CopilotChatManager();
        var store = CreateStore(manager, out string directory);
        var source = manager.SelectedSession;
        Guid snapshotId = Guid.NewGuid();
        var assistant = new CopilotChatMessage(ChatRole.Assistant, "answer");
        await source.AddMessageAsync(assistant);
        await store.SaveSnapshotAsync(source, assistant);
        snapshotId = assistant.SessionSnapshotId ?? throw new AssertFailedException("Snapshot was not associated.");

        var restored = await store.CreateSessionFromSnapshotAsync(source, snapshotId);
        await store.SaveSessionAsync(restored);
        var loaded = await store.LoadSessionAsync(restored.SessionId);

        Assert.AreNotEqual(source.SessionId, loaded.SessionId);
        Assert.IsTrue(loaded.StartedTime > source.StartedTime);
        Assert.AreEqual(snapshotId, loaded.ChatMessages.Last().SessionSnapshotId);
        Assert.IsFalse(Directory.Exists(Path.Join(directory, loaded.StartedTime.ToString("yyyyMMdd"), loaded.SessionId.ToString("N"), "Snapshots")));
        Assert.AreSame(source, manager.SelectedSession);
    }

    [TestMethod]
    public async Task RestoredHistorySnapshotIdShouldNotResolveInAnotherSession()
    {
        var manager = new CopilotChatManager();
        var store = CreateStore(manager, out _);
        var source = manager.SelectedSession;
        Guid snapshotId = Guid.NewGuid();
        var assistant = new CopilotChatMessage(ChatRole.Assistant, "answer");
        await source.AddMessageAsync(assistant);
        await store.SaveSnapshotAsync(source, assistant);
        snapshotId = assistant.SessionSnapshotId ?? throw new AssertFailedException("Snapshot was not associated.");
        var restored = await store.CreateSessionFromSnapshotAsync(source, snapshotId);
        await store.SaveSessionAsync(restored);

        await Assert.ThrowsExactlyAsync<DirectoryNotFoundException>(() => store.CreateSessionFromSnapshotAsync(restored, snapshotId));
    }

    [TestMethod]
    public async Task SnapshotShouldPreserveResponsesAndResourcesAfterCompression()
    {
        var manager = new CopilotChatManager();
        var store = CreateStore(manager, out string directory);
        var source = manager.SelectedSession;
        Guid snapshotId = Guid.NewGuid();
        byte[] bytes = [1, 2, 3, 4];
        var assistant = new CopilotChatMessage(ChatRole.Assistant, [new DataContent(bytes, "image/png")]);
        await source.AddMessageAsync(assistant);
        source.ResponsesSession.AppendItem(ResponseItem.CreateUserMessageItem("native input"));
        await store.SaveSnapshotAsync(source, assistant);
        snapshotId = assistant.SessionSnapshotId ?? throw new AssertFailedException("Snapshot was not associated.");
        string entry = Path.Join(directory, source.StartedTime.ToString("yyyyMMdd"), source.SessionId.ToString("N"), "Session.xml");
        var document = System.Xml.Linq.XDocument.Load(entry);
        document.Root!.SetAttributeValue("UpdatedTime", DateTimeOffset.Now.AddDays(-8).ToString("o"));
        document.Save(entry);
        var fileStore = new FileCopilotChatSessionStore(directory);
        await fileStore.OrganizeAsync();

        var restored = await store.CreateSessionFromSnapshotAsync(source, snapshotId);

        Assert.HasCount(1, restored.ResponsesSession.Items);
        Assert.AreEqual("native input", ((MessageResponseItem)restored.ResponsesSession.Items[0]).Content[0].Text);
        CollectionAssert.AreEqual(bytes, ((CopilotChatImageItem)restored.ChatMessages.Last().MessageItems.Single()).Data.ToArray());
        Assert.HasCount(1, await store.ListSessionsAsync());
    }

    [TestMethod]
    public async Task CodingSnapshotShouldRestoreProtocolHistoryAndContinueIndependently()
    {
        var inputs = new List<string[]>();
        var client = new FakeChatClient
        {
            OnGetStreamingResponseAsync = (messages, _, token) =>
            {
                inputs.Add(messages.Select(message => message.Text ?? "").ToArray());
                return RespondAsync(token);
            },
        };
        var manager = new CopilotChatManager();
        var model = new FakeLanguageModel(client)
        {
            ModelDefinition = new ModelDefinition { Provider = "fake", ModelId = "fake", ModelName = "Fake" },
        };
        manager.AgentApiEndpointManager.RegisterLanguageModelProvider(new FakeLanguageModelProvider([model]));
        var store = CreateStore(manager, out _);
        await using var agent = new CodingAgent();
        var controller = CodingWorkTaskControllerTestFactory.CreateApplication(manager, store, codingAgent: agent);
        var runner = new CodingAgentChatRunner(manager, agent);
        var options = new CodingChatRunOptions { EnableAutomaticCompression = false };
        var run = await runner.RunAsync([new TextContent("original")], null, options, CancellationToken.None);
        await run.CompletionTask;
        var source = manager.SelectedSession;
        Guid snapshotId = Guid.NewGuid();
        await store.SaveSnapshotAsync(source, run.AssistantChatMessage);
        snapshotId = run.AssistantChatMessage.SessionSnapshotId ?? throw new AssertFailedException("Snapshot was not associated.");
        var originalState = source.AgentSession;

        await controller.CreateSessionFromMessageAsync(run.AssistantChatMessage);
        var restored = manager.SelectedSession;
        var next = await runner.RunAsync([new TextContent("next")], null, options, CancellationToken.None);
        await next.CompletionTask;

        Assert.AreNotSame(originalState, restored.AgentSession);
        Assert.AreSame(originalState, source.AgentSession);
        CollectionAssert.Contains(inputs.Last(), "original");
        CollectionAssert.Contains(inputs.Last(), "answer");
        Assert.AreEqual(snapshotId, source.ChatMessages.Last().SessionSnapshotId);
        Assert.HasCount(2, await store.ListSessionsAsync());
    }

    [TestMethod]
    public async Task MissingSnapshotShouldAppendNoticeAndKeepSelectedSession()
    {
        var manager = new CopilotChatManager();
        var store = CreateStore(manager, out _);
        var source = manager.SelectedSession;
        var message = new CopilotChatMessage(ChatRole.Assistant, "answer");
        message.AssociateSessionSnapshot(Guid.NewGuid());
        await source.AddMessageAsync(message);
        await store.SaveSessionAsync(source);
        var controller = CodingWorkTaskControllerTestFactory.CreateApplication(manager, store);
        using var viewModel = new ChatViewModel(manager, controller, "ready");

        await viewModel.CreateSessionFromMessageAsync(new MessageItemViewModel(message));

        Assert.AreSame(source, manager.SelectedSession);
        Assert.IsInstanceOfType<CopilotChatNotice>(source.Entries.Last());
    }

    [TestMethod]
    public void CloneShouldPreserveSnapshotId()
    {
        var message = new CopilotChatMessage(ChatRole.Assistant, "answer");
        message.AssociateSessionSnapshot(Guid.NewGuid());
        Assert.AreEqual(message.SessionSnapshotId, message.Clone().SessionSnapshotId);
    }

    [TestMethod]
    public async Task ForkShouldUseSnapshotWorkspaceForNextRun()
    {
        var manager = new CopilotChatManager();
        var store = CreateStore(manager, out string directory);
        string snapshotWorkspace = Path.Join(directory, "snapshot-workspace");
        string currentWorkspace = Path.Join(directory, "current-workspace");
        Directory.CreateDirectory(snapshotWorkspace);
        Directory.CreateDirectory(currentWorkspace);
        var source = manager.SelectedSession;
        source.WorkspacePath = snapshotWorkspace;
        var assistant = new CopilotChatMessage(ChatRole.Assistant, "answer");
        await source.AddMessageAsync(assistant);
        await store.SaveSnapshotAsync(source, assistant);
        source.WorkspacePath = currentWorkspace;
        var workspace = new PrismCodingAgent.CodingShell.Services.Workspace.CodingWorkspaceController(new ImmediateDispatcher());
        await workspace.ChangeWorkspaceAsync(currentWorkspace);
        var controller = CodingWorkTaskControllerTestFactory.CreateApplication(manager, store, workspaceController: workspace);

        await controller.CreateSessionFromMessageAsync(assistant);

        Assert.AreEqual((snapshotWorkspace, snapshotWorkspace, currentWorkspace),
            (workspace.NextRunWorkspacePath, manager.SelectedSession.WorkspacePath, source.WorkspacePath));
    }

    [TestMethod]
    public void UserMessageShouldNotAssociateSnapshot()
    {
        var message = new CopilotChatMessage(ChatRole.User, "question");

        Assert.ThrowsExactly<InvalidOperationException>(() => message.AssociateSessionSnapshot(Guid.NewGuid()));
    }

    [TestMethod]
    public async Task OrdinarySaveShouldLeaveRoundSnapshotUnchanged()
    {
        var manager = new CopilotChatManager();
        var store = CreateStore(manager, out _);
        var source = manager.SelectedSession;
        var assistant = new CopilotChatMessage(ChatRole.Assistant, "answer");
        await source.AddMessageAsync(assistant);
        await store.SaveSnapshotAsync(source, assistant);
        Guid snapshotId = assistant.SessionSnapshotId ?? throw new AssertFailedException("Snapshot was not associated.");
        await source.AddMessageAsync(new CopilotChatMessage(ChatRole.User, "later"));

        await store.SaveSessionAsync(source);
        var restored = await store.CreateSessionFromSnapshotAsync(source, snapshotId);

        Assert.AreEqual("answer", restored.ChatMessages.Last().Content);
    }

    private static FileCodingChatSessionStore CreateStore(CopilotChatManager manager, out string directory)
    {
        directory = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Console.WriteLine(directory);
        return new FileCodingChatSessionStore(directory, Path.Join(directory, "Logs"), manager, new ImmediateDispatcher());
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> RespondAsync([EnumeratorCancellation] CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        yield return new ChatResponseUpdate(ChatRole.Assistant, "answer");
        await Task.Yield();
    }

    private sealed class ImmediateDispatcher : IMainThreadDispatcher
    {
        public Task InvokeAsync(Func<Task> action) => action();
        public Task<T> InvokeAsync<T>(Func<Task<T>> action) => action();
        public bool CheckAccess() => true;
    }
}
