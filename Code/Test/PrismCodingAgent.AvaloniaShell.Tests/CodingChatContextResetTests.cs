using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using AgentLib;
using AgentLib.Coding;
using AgentLib.Core.AgentApiManagers.Contexts;
using AgentLib.Core.AgentApiManagers.LanguageModelProviders;
using AgentLib.Core.AgentApiManagers.LanguageModelProviders.Fakes;
using AgentLib.Logging.Sessions;
using AgentLib.Model;
using Microsoft.Extensions.AI;
using PrismCodingAgent.CodingShell.Services.Chat;
using PrismCodingAgent.CodingShell.Services.Sessions;

namespace PrismCodingAgent.AvaloniaShell.Tests;

[TestClass]
public sealed class CodingChatContextResetTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [Timeout(30000)]
    public async Task RunnerShouldWriteToSessionCapturedInToolContext(bool responses)
    {
        await using var fixture = await Fixture.CreateAsync(responses);
        var session = fixture.Manager.SelectedSession;
        var toolContext = new CodingToolContext
        {
            Session = session,
            Options = new CodingChatRunOptions { EnableAutomaticCompression = false, LanguageTag = "en" },
        };
        var otherSession = fixture.Manager.AddSession(new CopilotChatSession(session.AdditionalInfo.Storage), select: true);

        var run = await fixture.Runner.RunAsync([new TextContent("target-user")], toolContext, CancellationToken.None);
        await run.CompletionTask;

        Assert.AreSame(run.AssistantChatMessage, session.ChatMessages.Last());
        Assert.HasCount(0, otherSession.ChatMessages);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [Timeout(30000)]
    public async Task ResetShouldRemovePreviousContextFromNextRealRequest(bool responses)
    {
        await using var fixture = await Fixture.CreateAsync(responses);
        await fixture.RunAsync("previous-user");
        await fixture.RunAsync("before-reset-user");
        StringAssert.Contains(fixture.Requests[1], "previous-user");
        StringAssert.Contains(fixture.Requests[1], "previous-assistant");

        fixture.Manager.SelectedSession.ResetModelContext();
        await fixture.RunAsync("next-user");

        Assert.IsFalse(fixture.Requests[2].Contains("previous-user", StringComparison.Ordinal)
            || fixture.Requests[2].Contains("previous-assistant", StringComparison.Ordinal)
            || fixture.Requests[2].Contains("before-reset-user", StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [Timeout(30000)]
    public async Task ResetShouldPreserveVisibleHistoryAndSaveOnlyClearedContext(bool responses)
    {
        await using var fixture = await Fixture.CreateAsync(responses);
        await fixture.RunAsync("previous-user");
        var session = fixture.Manager.SelectedSession;
        var entries = session.Entries.ToArray();
        var store = new FileCodingChatSessionStore(fixture.StorageStore,
            fixture.Manager, new ImmediateDispatcher());
        await store.SaveSessionAsync(session);

        session.ResetModelContext();
        CollectionAssert.AreEqual(entries, session.Entries.ToArray());
        await store.SaveSessionAsync(session);
        var data = await new FileCopilotChatSessionStore(fixture.Directory).LoadSessionAsync(session.SessionId);
        Assert.IsNull(data.AgentSessionState);
        Assert.IsTrue(data.ResponsesSession is null || data.ResponsesSession.Items.Count == 0);
        var restored = await store.LoadSessionAsync(session.SessionId);

        CollectionAssert.AreEqual(entries.Cast<CopilotChatMessage>().Select(message => message.Content).ToArray(),
            restored.Entries.Cast<CopilotChatMessage>().Select(message => message.Content).ToArray());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [Timeout(30000)]
    public async Task SystemInstructionsShouldFollowBackendLifetimeAndReloadAfterReset(bool responses)
    {
        await using var fixture = await Fixture.CreateAsync(responses);
        await fixture.RunAsync("previous-user");
        StringAssert.Contains(fixture.Requests[0], "instruction-original-marker");
        await File.WriteAllTextAsync(fixture.InstructionsPath, "instruction-updated-marker");
        await fixture.RunAsync("before-reset-user");
        StringAssert.Contains(fixture.Requests[1], responses
            ? "instruction-updated-marker" : "instruction-original-marker");

        fixture.Manager.SelectedSession.ResetModelContext();
        await fixture.RunAsync("next-user");

        StringAssert.Contains(fixture.Requests[2], "instruction-updated-marker");
        Assert.IsFalse(fixture.Requests[2].Contains("instruction-original-marker", StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [Timeout(30000)]
    public async Task RepeatedResetShouldKeepOldHistoryOutOfRestoredRequest(bool responses)
    {
        await using var fixture = await Fixture.CreateAsync(responses);
        await fixture.RunAsync("previous-user");
        var session = fixture.Manager.SelectedSession;
        var store = new FileCodingChatSessionStore(fixture.StorageStore,
            fixture.Manager, new ImmediateDispatcher());
        await store.SaveSessionAsync(session);

        session.ResetModelContext();
        session.ResetModelContext();
        await store.SaveSessionAsync(session);
        fixture.Manager.SelectedSession = await store.LoadSessionAsync(session.SessionId);
        await fixture.RunAsync("next-user");

        StringAssert.Contains(fixture.Requests[1], "next-user");
        Assert.IsFalse(fixture.Requests[1].Contains("previous-user", StringComparison.Ordinal)
            || fixture.Requests[1].Contains("previous-assistant", StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [Timeout(30000)]
    public async Task RoundEndShouldSaveMainStateSnapshotAndOrdinarySession(bool responses)
    {
        await using var fixture = await Fixture.CreateAsync(responses);
        var session = fixture.Manager.SelectedSession;
        var store = new FileCodingChatSessionStore(fixture.StorageStore,
            fixture.Manager, new ImmediateDispatcher());
        var controller = CodingWorkTaskControllerTestFactory.CreateApplication(fixture.Manager, store, fixture.Runner);

        await controller.SendMessageAsync("previous-user",
            new CodingChatRunOptions { EnableAutomaticCompression = false, LanguageTag = "en" });

        var files = new FileCopilotChatSessionStore(fixture.Directory);
        var snapshot = await SessionSnapshots.Get(session).ReadSnapshotAsync(session, session.ChatMessages.Last().SessionSnapshotId);
        var ordinary = await files.LoadSessionAsync(session.SessionId);
        Assert.AreEqual(ordinary.AgentSessionState?.GetRawText(), snapshot.AgentState?.GetRawText());
        Assert.AreEqual(ordinary.ResponsesSession?.Items.Count, snapshot.Responses?.Items.Count);
        Assert.AreEqual(responses, snapshot.Responses?.Items.Count > 0);
        Assert.AreEqual(!responses, snapshot.AgentState.HasValue);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [Timeout(30000)]
    public async Task SaveSnapshotShouldNotOverwriteOrdinarySession(bool responses)
    {
        await using var fixture = await Fixture.CreateAsync(responses);
        await fixture.RunAsync("previous-user");
        var session = fixture.Manager.SelectedSession;
        var store = new FileCodingChatSessionStore(fixture.StorageStore,
            fixture.Manager, new ImmediateDispatcher());
        session.SetTitle("ordinary-session");
        await store.SaveSessionAsync(session);
        session.SetTitle("snapshot");
        SessionSnapshotId snapshotId = session.ChatMessages.Last().SessionSnapshotId;

        await store.SaveSnapshotAsync(session, snapshotId);

        var files = new FileCopilotChatSessionStore(fixture.Directory);
        var ordinary = await files.LoadSessionAsync(session.SessionId);
        Assert.AreEqual("ordinary-session", ordinary.Title);
        Assert.AreEqual("snapshot", session.Title);
        var snapshot = await SessionSnapshots.Get(session).ReadSnapshotAsync(session, snapshotId);
        Assert.AreEqual(!responses, snapshot.AgentState.HasValue);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [Timeout(30000)]
    public async Task SnapshotSessionShouldRestoreIndependentHistoryAndContinue(bool responses)
    {
        await using var fixture = await Fixture.CreateAsync(responses);
        await fixture.RunAsync("previous-user");
        var source = fixture.Manager.SelectedSession;
        source.SetTitle("snapshot-title");
        source.WorkspacePath = fixture.Directory;
        source.AdditionalInfo.Set("marker", new JsonSessionAdditionalInfo(JsonSerializer.SerializeToElement("snapshot-value")));
        var store = new FileCodingChatSessionStore(fixture.StorageStore,
            fixture.Manager, new ImmediateDispatcher());
        SessionSnapshotId snapshotId = source.ChatMessages.Last().SessionSnapshotId;
        var snapshotMessageIds = source.ChatMessages.Select(message => message.SessionSnapshotId).ToArray();
        var snapshotMessage = source.ChatMessages.Last();
        int snapshotEntryCount = source.Entries.Count;
        await store.SaveSnapshotAsync(source, snapshotId);
        await fixture.RunAsync("after-snapshot-user");
        int sourceMessageCount = source.ChatMessages.Count;
        var restored = await store.CreateSessionFromSnapshotAsync(source, snapshotMessage);

        Assert.AreNotEqual(source.SessionId, restored.SessionId);
        Assert.AreEqual("previous-user", restored.Title);
        Assert.AreEqual(fixture.Directory, restored.WorkspacePath);
        Assert.IsFalse(restored.AdditionalInfo.TryGetValue("marker", out _));
        CollectionAssert.AreEqual(snapshotMessageIds,
            restored.ChatMessages.Select(message => message.SessionSnapshotId).ToArray());
        Assert.AreNotSame(source.ChatMessages[0], restored.ChatMessages[0]);
        Assert.AreEqual(snapshotEntryCount, restored.Entries.Count);
        if (responses)
            Assert.AreNotSame(source.ResponsesSession, restored.ResponsesSession);
        else
            Assert.AreNotSame(source.AgentSession, restored.AgentSession);

        fixture.Manager.AddSession(restored, select: true);
        await fixture.RunAsync("continued-user");
        StringAssert.Contains(fixture.Requests[2], "previous-user");
        StringAssert.Contains(fixture.Requests[2], "previous-assistant");
        Assert.IsFalse(fixture.Requests[2].Contains("after-snapshot-user", StringComparison.Ordinal));
        Assert.AreEqual(sourceMessageCount, source.ChatMessages.Count);
        await store.SaveSessionAsync(restored);
        string restoredDirectory = Path.Combine(fixture.Directory, restored.StartedTime.ToString("yyyyMMdd"), restored.SessionId.ToString("N"));
        Assert.IsFalse(System.IO.Directory.Exists(Path.Combine(restoredDirectory, "Snapshots")));
        var loaded = await store.LoadSessionAsync(restored.SessionId);
        Assert.AreEqual(restored.ChatMessages.Count, loaded.ChatMessages.Count);
        var snapshot = await SessionSnapshots.Get(source).ReadSnapshotAsync(source, snapshotId);
        Assert.AreEqual(!responses, snapshot.AgentState.HasValue);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly CodingAgent _agent;
        private readonly HttpClient _http;
        private readonly FakeChatClient _client;

        private Fixture(string directory, bool responses)
        {
            Directory = directory;
            InstructionsPath = Path.Combine(directory, "instructions.md");
            StorageStore = new FileCopilotChatSessionStore(directory);
            Manager = new CopilotChatManager(new AgentLib.Logging.FileCopilotChatLogger(Path.Join(directory, "Logs")), StorageStore);
            _client = new FakeChatClient
            {
                OnGetStreamingResponseAsync = (messages, _, cancellationToken) =>
                {
                    Requests.Add(string.Join("\n", messages.Select(message => message.Text)));
                    return RespondAsync(cancellationToken);
                },
            };
            _http = new HttpClient(new RecordingHandler(Requests));
            Manager.AgentApiEndpointManager.HttpClient?.Dispose();
            Manager.AgentApiEndpointManager.HttpClient = _http;
            _agent = new CodingAgent(new CodingAgentOptions { CopilotInstructionsPath = InstructionsPath });
            if (responses)
            {
                Manager.AgentApiEndpointManager.LoadConfiguration(AgentApiManagerConfiguration.FromJsonString("""
                    {"PrimaryModel":"demo","OpenAIConfigurationList":[{"EndPoint":"https://example.invalid/v1","Key":"offline-test-key",
                    "ModelDefinitions":[{"ModelName":"demo","ModelId":"model-id","Provider":"test"}]}]}
                    """));
                Runner = new ResponsesCodingChatRunner(Manager, new ResponsesCodingAgent(_agent, InstructionsPath));
            }
            else
            {
                var model = new FakeLanguageModel(_client)
                {
                    ModelDefinition = new ModelDefinition { Provider = "fake", ModelId = "fake", ModelName = "Fake" },
                };
                Manager.AgentApiEndpointManager.RegisterLanguageModelProvider(new FakeLanguageModelProvider([model]));
                Runner = new CodingAgentChatRunner(Manager, _agent);
            }
        }

        internal FileCopilotChatSessionStore StorageStore { get; }
        internal string Directory { get; }
        internal string InstructionsPath { get; }
        internal CopilotChatManager Manager { get; }
        internal ICodingChatRunner Runner { get; }
        internal List<string> Requests { get; } = [];

        internal static async Task<Fixture> CreateAsync(bool responses)
        {
            string directory = Path.Combine(Path.GetTempPath(), $"context-reset-{Guid.NewGuid():N}");
            System.IO.Directory.CreateDirectory(directory);
            Console.WriteLine(directory);
            var fixture = new Fixture(directory, responses);
            await File.WriteAllTextAsync(fixture.InstructionsPath, "instruction-original-marker");
            return fixture;
        }

        internal async Task RunAsync(string text)
        {
            var toolContext = new CodingToolContext
            {
                Session = Manager.SelectedSession,
                Options = new CodingChatRunOptions { EnableAutomaticCompression = false, LanguageTag = "en" },
            };
            var result = await Runner.RunAsync([new TextContent(text)], toolContext, CancellationToken.None);
            await result.CompletionTask;
        }

        public async ValueTask DisposeAsync()
        {
            await _agent.DisposeAsync();
            _client.Dispose();
            _http.Dispose();
        }

        private static async IAsyncEnumerable<ChatResponseUpdate> RespondAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return new ChatResponseUpdate(ChatRole.Assistant, "previous-assistant");
        }
    }

    private sealed class RecordingHandler(List<string> requests) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Assert.IsNotNull(request.Content);
            string body = await request.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(body);
            requests.Add(document.RootElement.ToString());
            string response = JsonSerializer.Serialize(new
            {
                id = $"resp_{requests.Count}", status = "completed",
                output = new[]
                {
                    new
                    {
                        type = "message", id = $"msg_{requests.Count}", role = "assistant", status = "completed",
                        content = new[] { new { type = "output_text", text = "previous-assistant", annotations = Array.Empty<object>() } },
                    },
                },
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"data: {{\"type\":\"response.completed\",\"sequence_number\":0,\"response\":{response}}}\n\n",
                    Encoding.UTF8, "text/event-stream"),
            };
        }
    }

    private sealed class ImmediateDispatcher : IMainThreadDispatcher
    {
        public bool CheckAccess() => true;
        public Task InvokeAsync(Func<Task> action) => action();
        public Task<T> InvokeAsync<T>(Func<Task<T>> action) => action();
    }
}
