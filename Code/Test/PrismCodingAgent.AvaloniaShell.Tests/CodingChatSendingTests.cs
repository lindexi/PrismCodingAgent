using AgentLib.Logging.Sessions;
using AgentLib;
using AgentLib.Coding;
using AgentLib.Coding.Responses;
using AgentLib.Coding.Settings;
using AgentLib.Coding.Tools;
using AgentLib.Core.AgentApiManagers.Contexts;
using AgentLib.Core.AgentApiManagers.LanguageModelProviders;
using AgentLib.Logging;
using AgentLib.Model;
using Microsoft.Extensions.AI;
using PrismCodingAgent.CodingShell.Infrastructure;
using PrismCodingAgent.CodingShell.Services.Chat;
using PrismCodingAgent.CodingShell.Services.Sessions;
using PrismCodingAgent.CodingShell.Services.Settings;
using PrismCodingAgent.CodingShell.Services.Workspace;
using PrismCodingAgent.CodingShell.Services.WorkTasks;
using PrismCodingAgent.CodingShell.ViewModels;
using CodingChatApplicationTestFactory = global::PrismCodingAgent.AvaloniaShell.Tests.CodingWorkTaskControllerTestFactory;

namespace PrismCodingAgent.AvaloniaShell.Tests;

[TestClass]
public sealed class CodingChatSendingTests
{
    private static CopilotChatManager CreateManager(ICopilotChatLogger? logger = null)
    {
        var store = new FileCopilotChatSessionStore(Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        return logger is null ? new CopilotChatManager() : new CopilotChatManager(logger, store);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [Timeout(5000)]
    public async Task SendMessageAsyncShouldSelectRequestedRunner(bool useResponses)
    {
        var manager = CreateManager();
        var codingRunner = new TestCodingChatRunner(manager);
        var responsesRunner = new TestCodingChatRunner(manager);
        var application = CodingChatApplicationTestFactory.CreateApplication(
            manager, new TestSessionStore(), codingRunner);
        await application.InitializeAsync();

        var expected = useResponses ? responsesRunner : codingRunner;
        Task sending = application.SendMessageAsync([new TextContent("check")],
            new CodingChatRunOptions { EnableAutomaticCompression = false, LanguageTag = "en" }, runner: expected);
        await expected.Started.Task;
        Assert.IsFalse((useResponses ? codingRunner : responsesRunner).Started.Task.IsCompleted);
        expected.Complete("done");
        await sending;
    }

    [TestMethod]
    [Timeout(10000)]
    public async Task SendCommandShouldAllowAnotherSendWhileTitleIsGenerating()
    {
        var paths = CodingChatRoomPaths.Create(CreateTestDirectory());
        await using var runtime = await CodingWorkTaskRuntimeFactory.InitializeAsync(paths, new ImmediateMainThreadDispatcher());
        using var client = new PendingTitleChatClient();
        runtime.EndpointManager.RegisterLanguageModelProvider(client);
        runtime.EndpointManager.PrimaryModel = client;
        using var chat = new ChatViewModel(runtime)
        {
            IsResponsesApiEnabled = false,
            RunOptions = { EnableAutomaticCompression = false },
            InputText = "First task",
        };
        var saved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        runtime.Controller.Sessions.CollectionChanged += (_, _) =>
        {
            if (runtime.Controller.Sessions.Any(session => session.Title == PendingTitleChatClient.Title))
            {
                saved.TrySetResult();
            }
        };

        try
        {
            var send = (SimpleAsyncCommand)chat.SendCommand;
            await send.ExecuteAsync();
            await client.Requested.Task.WaitAsync(TimeSpan.FromSeconds(5));

            chat.InputText = "Follow-up";
            await send.ExecuteAsync();

            Assert.AreEqual(2, client.ChatRequests);
        }
        finally
        {
            client.CompleteTitle();
        }
        await saved.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private sealed class PendingTitleChatClient : IChatClient, ILanguageModel, ILanguageModelProvider
    {
        public const string Title = "Generated title";
        public ModelDefinition ModelDefinition { get; } = new() { ModelId = "Fake", ModelName = "Fake" };
        public IReadOnlyList<ILanguageModel> GetSupportedModels() => [this];
        public Task<IChatClient> GetChatClientAsync() => Task.FromResult<IChatClient>(this);
        public TaskCompletionSource Requested { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<ChatResponse> _response = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int ChatRequests { get; private set; }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            Requested.TrySetResult();
            return _response.Task;
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ChatRequests++;
            await Task.Yield();
            yield return new ChatResponseUpdate(ChatRole.Assistant, "Done");
        }

        public void CompleteTitle() => _response.TrySetResult(new ChatResponse(new ChatMessage(ChatRole.Assistant,
            [new FunctionCallContent("title", SessionTitleGenerator.TitleSubmissionToolName,
                new Dictionary<string, object?> { ["title"] = Title })])));

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    [TestMethod(DisplayName = "发送应使用启动时已提交的工作路径")]
    [Timeout(5000)]
    public async Task SendMessageAsyncShouldUseCommittedWorkspacePath()
    {
        string workspacePath = CreateTestDirectory();
        var manager = CreateManager();
        var runner = new TestCodingChatRunner(manager);
        var workspaceController = new CodingWorkspaceController(new ImmediateMainThreadDispatcher());
        await workspaceController.ChangeWorkspaceAsync(workspacePath, CancellationToken.None);
        var application = CodingChatApplicationTestFactory.CreateApplication(
            manager,
            new TestSessionStore(),
            runner,
            workspaceController);
        await application.InitializeAsync();

        Task sendTask = application.SendMessageAsync("检查工作区");
        await runner.Started.Task;

        Assert.AreEqual(Path.GetFullPath(workspacePath), runner.ObservedWorkspacePath);
        runner.Complete("完成");
        await sendTask;
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task SendMessageAsyncShouldUseAppliedShellSettings()
    {
        var manager = CreateManager();
        var runner = new TestCodingChatRunner(manager);
        var settingsService = new CodingChatSettingsService(CodingChatRoomPaths.Create(CreateTestDirectory()));
        var settings = new PrismCodingAgentShellSettings { LanguageTag = "en", IsWindowsSandboxEnabled = false };
        settingsService.ApplyShellSettings(new AgentApiManagerConfiguration(), settings);
        var application = CodingChatApplicationTestFactory.CreateApplication(
            manager, new TestSessionStore(), runner, settingsService: settingsService);
        await application.InitializeAsync();

        Task sendTask = application.SendMessageAsync("Check settings");
        await runner.Started.Task;
        runner.Complete("Done");
        await sendTask;

        Assert.AreSame(settings, runner.ObservedShellSettings);
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task SendMessageAsyncShouldPassSharedHttpClientToTools()
    {
        var manager = CreateManager();
        var runner = new TestCodingChatRunner(manager);
        var application = CodingChatApplicationTestFactory.CreateApplication(manager, new TestSessionStore(), runner);
        await application.InitializeAsync();

        Task sendTask = application.SendMessageAsync("Check HTTP client");
        await runner.Started.Task;
        runner.Complete("Done");
        await sendTask;

        Assert.AreSame(manager.AgentApiEndpointManager.HttpClient, runner.ObservedHttpClient);
    }

    [TestMethod(DisplayName = "发送默认应启用自动压缩")]
    [Timeout(5000)]
    public async Task SendMessageAsyncShouldEnableAutomaticCompressionByDefault()
    {
        var manager = CreateManager();
        var runner = new TestCodingChatRunner(manager);
        var application = CodingChatApplicationTestFactory.CreateApplication(manager, new TestSessionStore(), runner);
        await application.InitializeAsync();

        Task sendTask = application.SendMessageAsync("检查代码");
        await runner.Started.Task;

        Assert.IsTrue(runner.ObservedAutomaticCompressionEnabled);
        runner.Complete("完成");
        await sendTask;
    }

    [TestMethod(DisplayName = "发送应允许关闭自动压缩")]
    [Timeout(5000)]
    public async Task SendMessageAsyncShouldAllowDisablingAutomaticCompression()
    {
        var manager = CreateManager();
        var runner = new TestCodingChatRunner(manager);
        var application = CodingChatApplicationTestFactory.CreateApplication(manager, new TestSessionStore(), runner);
        await application.InitializeAsync();

        Task sendTask = application.SendMessageAsync(
            "检查代码",
            new CodingChatRunOptions { EnableAutomaticCompression = false, LanguageTag = "en" });
        await runner.Started.Task;

        Assert.IsFalse(runner.ObservedAutomaticCompressionEnabled);
        runner.Complete("完成");
        await sendTask;
    }

    [TestMethod(DisplayName = "发送应允许为本次运行启用 dotnet run")]
    [Timeout(5000)]
    public async Task SendMessageAsyncShouldAllowEnablingDotNetRun()
    {
        var manager = CreateManager();
        var runner = new TestCodingChatRunner(manager);
        var application = CodingChatApplicationTestFactory.CreateApplication(manager, new TestSessionStore(), runner);
        await application.InitializeAsync();

        Task sendTask = application.SendMessageAsync(
            "运行应用",
            new CodingChatRunOptions { EnableDotNetRun = true, LanguageTag = "en" });
        await runner.Started.Task;

        Assert.IsTrue(runner.ObservedDotNetRunEnabled);
        runner.Complete("完成");
        await sendTask;
    }

    [TestMethod(DisplayName = "发送应把思考强度传递给运行器")]
    [Timeout(5000)]
    public async Task SendMessageAsyncShouldPassReasoningEffortToRunner()
    {
        var manager = CreateManager();
        var runner = new TestCodingChatRunner(manager);
        var application = CodingChatApplicationTestFactory.CreateApplication(manager, new TestSessionStore(), runner);
        await application.InitializeAsync();

        Task sendTask = application.SendMessageAsync(
            "深入检查代码",
            new CodingChatRunOptions { ReasoningEffort = ReasoningEffort.High, LanguageTag = "en" });
        await runner.Started.Task;

        Assert.AreEqual(ReasoningEffort.High, runner.ObservedReasoningEffort);
        runner.Complete("完成");
        await sendTask;
    }

    [TestMethod(DisplayName = "发送多模态内容时应保留文本图片顺序和类型")]
    [Timeout(5000)]
    public async Task SendMessageAsyncShouldPreserveMultimodalContentOrder()
    {
        var manager = CreateManager();
        var runner = new TestCodingChatRunner(manager);
        var application = CodingChatApplicationTestFactory.CreateApplication(manager, new TestSessionStore(), runner);
        await application.InitializeAsync();
        IReadOnlyList<AIContent> contents =
        [
            new TextContent("分析图片"),
            new DataContent(new byte[] { 1, 2, 3 }, "image/jpeg"),
        ];

        Task sendTask = application.SendMessageAsync(contents);
        await runner.Started.Task;

        IReadOnlyList<AIContent> observedContents = runner.ObservedContents!;
        Assert.HasCount(2, observedContents);
        Assert.IsInstanceOfType<TextContent>(observedContents[0]);
        DataContent imageContent = Assert.IsInstanceOfType<DataContent>(observedContents[1]);
        Assert.AreEqual("image/jpeg", imageContent.MediaType);
        runner.Complete("完成");
        await sendTask;
    }

    [TestMethod(DisplayName = "单次发送应只启动一次运行并在完成后保存会话")]
    [Timeout(5000)]
    public async Task SendMessageAsyncShouldRunOnceAndSaveCompletedSession()
    {
        var manager = CreateManager();
        var store = new TestSessionStore();
        var runner = new TestCodingChatRunner(manager);
        var application = CodingChatApplicationTestFactory.CreateApplication(manager, store, runner);
        await application.InitializeAsync();

        Task sendTask = application.SendMessageAsync("检查代码");
        await runner.Started.Task;

        Assert.AreEqual(1, runner.RunCount);
        Assert.IsTrue(application.IsRunActive);
        Assert.HasCount(3, manager.SelectedSession.ChatMessages);
        Assert.AreSame(runner.AssistantMessage, manager.SelectedSession.ChatMessages[2]);

        runner.Complete("已完成");
        await sendTask;

        Assert.IsFalse(application.IsRunActive);
        Assert.AreEqual("已完成", runner.AssistantMessage.Content);
        Assert.AreEqual(1, store.SaveCount);
        Assert.AreEqual(manager.SelectedSession.SessionId, application.Sessions[0].SessionId);
        Assert.AreEqual(2, application.Sessions[0].MessageCount);
    }

    [TestMethod(DisplayName = "运行期间插话后应通过当前助手消息收到更新")]
    [Timeout(5000)]
    public async Task SendMessageAsyncWhileActiveShouldUpdateCurrentAssistantMessage()
    {
        var manager = CreateManager();
        var runner = new TestCodingChatRunner(manager);
        var application = CodingChatApplicationTestFactory.CreateApplication(manager, new TestSessionStore(), runner);
        await application.InitializeAsync();
        Task firstSend = application.SendMessageAsync("第一条");
        await runner.Started.Task;
        CopilotChatMessage assistantMessage = runner.AssistantMessage;

        await application.SendMessageAsync("第二条");

        Assert.AreEqual("插话回复：第二条", assistantMessage.Content);
        runner.Complete("完成");
        await firstSend;
    }

    [TestMethod(DisplayName = "最终保存期间不应启动第二条执行链")]
    [Timeout(5000)]
    public async Task SendMessageAsyncWhileFinalizingShouldRejectSecondRun()
    {
        var manager = CreateManager();
        var store = new TestSessionStore { BlockSave = true };
        var runner = new TestCodingChatRunner(manager);
        var application = CodingChatApplicationTestFactory.CreateApplication(manager, store, runner);
        await application.InitializeAsync();
        Task firstSend = application.SendMessageAsync("第一条");
        await runner.Started.Task;
        runner.Complete("首轮完成");
        await store.SaveStarted.Task;

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => application.SendMessageAsync("结束边界发送"));

        store.ReleaseSave();
        await firstSend;
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [Timeout(15000)]
    public async Task ResponsesStopBeforeCompletionShouldAllowSendingAgainAndInNewSession(bool receivePartialResponse)
    {
        using var handler = new InterruptedResponseHandler(receivePartialResponse);
        using var http = new HttpClient(handler);
        var manager = CreateManager();
        manager.AgentApiEndpointManager.HttpClient?.Dispose();
        manager.AgentApiEndpointManager.HttpClient = http;
        manager.AgentApiEndpointManager.LoadConfiguration(
            AgentLib.Core.AgentApiManagers.LanguageModelProviders.AgentApiManagerConfiguration.FromJsonString("""
                {"PrimaryModel":"demo","OpenAIConfigurationList":[{"EndPoint":"https://example.com/v1","Key":"test-key",
                "ModelDefinitions":[{"ModelName":"demo","ModelId":"model-id","Provider":"test"}]}]}
                """));
        await using var codingAgent = new CodingAgent();
        var runner = new ResponsesCodingChatRunner(manager, new ResponsesCodingAgent(codingAgent));
        var application = CodingChatApplicationTestFactory.CreateApplication(
            manager, new TestSessionStore(), codingAgent: codingAgent);
        await application.InitializeAsync();
        var options = new CodingChatRunOptions { EnableAutomaticCompression = false, LanguageTag = "en" };
        Task first = application.SendMessageAsync([new TextContent("first")], options, runner);
        await handler.WaitingForCancellation.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await application.StopActiveRunByUserAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await first.WaitAsync(TimeSpan.FromSeconds(5)));
        await application.SendMessageAsync([new TextContent("second")], options, runner)
            .WaitAsync(TimeSpan.FromSeconds(5));
        await application.CreateNewSessionAsync();
        await application.SendMessageAsync([new TextContent("third")], options, runner)
            .WaitAsync(TimeSpan.FromSeconds(5));

        Assert.IsTrue(!application.IsRunActive && !manager.IsChatting
            && manager.SelectedSession.ChatMessages.Any(message => message.Role == ChatRole.User && message.Content == "third"),
            $"Running={application.IsRunActive}, chatting={manager.IsChatting}, messages={manager.SelectedSession.ChatMessages.Count}");
    }

    [TestMethod]
    [Timeout(10000)]
    public async Task ResponsesInterruptionShouldAppearOnlyAsACardAndSendOneUserItem()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new InterruptedResponseHandler(receivePartialResponse: false, release.Task);
        using var http = new HttpClient(handler);
        var manager = CreateManager();
        manager.AgentApiEndpointManager.HttpClient?.Dispose();
        manager.AgentApiEndpointManager.HttpClient = http;
        manager.AgentApiEndpointManager.LoadConfiguration(
            AgentLib.Core.AgentApiManagers.LanguageModelProviders.AgentApiManagerConfiguration.FromJsonString("""
                {"PrimaryModel":"demo","OpenAIConfigurationList":[{"EndPoint":"https://example.com/v1","Key":"test-key",
                "ModelDefinitions":[{"ModelName":"demo","ModelId":"model-id","Provider":"test"}]}]}
                """));
        await using var agent = new CodingAgent();
        var runner = new ResponsesCodingChatRunner(manager, new ResponsesCodingAgent(agent));
        var options = new CodingChatRunOptions { EnableAutomaticCompression = false, LanguageTag = "en" };
        var toolContext = new CodingToolContext { Session = manager.SelectedSession, Options = options };
        var run = await runner.RunAsync([new TextContent("start")], toolContext, CancellationToken.None);
        await handler.WaitingForCancellation.Task.WaitAsync(TimeSpan.FromSeconds(5));

        try
        {
            await runner.InjectMessageAsync([new TextContent("follow-up")], CancellationToken.None);
        }
        finally
        {
            release.TrySetResult();
        }
        await run.CompletionTask;

        Assert.AreEqual("follow-up", run.AssistantChatMessage.MessageItems.OfType<CopilotChatToolItem>().Single().Details?.Content);
        CollectionAssert.AreEqual(new[] { "start" }, manager.SelectedSession.ChatMessages
            .Where(message => message.Role == ChatRole.User).Select(message => message.Content).ToArray());
        using var request = System.Text.Json.JsonDocument.Parse(handler.SecondRequestBody!);
        var input = request.RootElement.GetProperty("input").EnumerateArray().ToArray();
        Assert.HasCount(1, input.Where(item => item.GetRawText().Contains("follow-up", StringComparison.Ordinal)));
        Assert.AreEqual("user", input.Single(item => item.GetRawText().Contains("follow-up", StringComparison.Ordinal))
            .GetProperty("role").GetString());
        Assert.IsFalse(input.Any(item => item.GetProperty("type").GetString() == "function_call"));
    }

    private sealed class InterruptedResponseHandler(bool receivePartialResponse, Task? finishFirstRequest = null) : HttpMessageHandler
    {
        private int _requestCount;
        public string? SecondRequestBody { get; private set; }
        public TaskCompletionSource WaitingForCancellation { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            int number = Interlocked.Increment(ref _requestCount);
            if (number == 2 && finishFirstRequest is not null)
            {
                SecondRequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            }
            if (number == 1 && !receivePartialResponse)
            {
                WaitingForCancellation.TrySetResult();
                await (finishFirstRequest ?? Task.Delay(Timeout.Infinite, cancellationToken)).WaitAsync(cancellationToken);
            }
            HttpContent content;
            if (number == 1 && receivePartialResponse)
            {
                content = new StreamContent(new InterruptedResponseStream(WaitingForCancellation));
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/event-stream");
            }
            else
            {
                string response = $$$"""
                    data: {"type":"response.completed","sequence_number":0,"response":{"id":"resp_{{{number}}}","status":"completed","output":[{"type":"message","id":"msg_{{{number}}}","role":"assistant","status":"completed","content":[{"type":"output_text","text":"done","annotations":[]}]}]}}


                    """;
                content = new StringContent(response, System.Text.Encoding.UTF8, "text/event-stream");
            }
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = content };
        }
    }

    private sealed class InterruptedResponseStream(TaskCompletionSource waiting) : Stream
    {
        private readonly MemoryStream _prefix = new(System.Text.Encoding.UTF8.GetBytes("""
            data: {"type":"response.created","sequence_number":0,"response":{"id":"resp_1","status":"in_progress","output":[]}}

            data: {"type":"response.output_text.delta","sequence_number":1,"item_id":"msg_1","output_index":0,"content_index":0,"delta":"partial"}


            """));
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int count = await _prefix.ReadAsync(buffer, cancellationToken);
            if (count > 0) return count;
            waiting.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            if (disposing) _prefix.Dispose();
            base.Dispose(disposing);
        }
    }

    [TestMethod(DisplayName = "停止活动发送应取消完整运行生命周期")]
    [Timeout(5000)]
    public async Task StopActiveRunShouldCancelCompletionTask()
    {
        var manager = CreateManager();
        var runner = new TestCodingChatRunner(manager);
        var application = CodingChatApplicationTestFactory.CreateApplication(manager, new TestSessionStore(), runner);
        await application.InitializeAsync();
        Task sendTask = application.SendMessageAsync("等待取消");
        await runner.Started.Task;

        application.StopActiveRun();

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await sendTask);
        Assert.IsTrue(runner.ObservedCancellationToken.IsCancellationRequested);
        Assert.IsFalse(application.IsRunActive);
    }

    [TestMethod(DisplayName = "运行异常后应恢复可发送状态")]
    [Timeout(5000)]
    public async Task SendMessageAsyncFailureShouldRestoreAvailableState()
    {
        var manager = CreateManager();
        var runner = new TestCodingChatRunner(manager);
        var application = CodingChatApplicationTestFactory.CreateApplication(manager, new TestSessionStore(), runner);
        await application.InitializeAsync();
        Task sendTask = application.SendMessageAsync("触发异常");
        await runner.Started.Task;

        runner.Fail(new InvalidOperationException("模型失败"));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await sendTask);
        Assert.IsFalse(application.IsRunActive);
        Assert.IsTrue(application.CanSend);
    }

    [TestMethod(DisplayName = "运行失败且保存也失败时应保留运行异常")]
    [Timeout(5000)]
    public async Task RunFailureShouldTakePrecedenceOverPersistenceFailure()
    {
        var manager = CreateManager();
        var store = new TestSessionStore { SaveException = new IOException("保存失败") };
        var runner = new TestCodingChatRunner(manager);
        var application = CodingChatApplicationTestFactory.CreateApplication(manager, store, runner);
        await application.InitializeAsync();
        Task sendTask = application.SendMessageAsync("触发异常");
        await runner.Started.Task;
        var operationException = new InvalidOperationException("模型失败");
        runner.AssistantMessage.ClearMessageItems();
        runner.AssistantMessage.AppendText("partial");
        runner.Fail(operationException);

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await sendTask);

        Assert.AreSame(operationException, exception);
        Assert.AreSame(store.SaveException, exception.Data["SessionSaveException"]);
        Assert.AreEqual("partial", manager.SelectedSession.ChatMessages.Last().Content);
        Assert.IsFalse(application.IsRunActive);
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task RunFailureShouldTakePrecedenceOverDiagnosticFailure()
    {
        var manager = CreateManager(new FailureDiagnosticLogger());
        var runner = new TestCodingChatRunner(manager);
        var application = CodingChatApplicationTestFactory.CreateApplication(manager, new TestSessionStore(), runner);
        await application.InitializeAsync();
        Task sending = application.SendMessageAsync("question");
        await runner.Started.Task;
        var failure = new InvalidOperationException("model failed");
        runner.Fail(failure);

        InvalidOperationException actual = await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await sending);

        Assert.AreSame(failure, actual);
    }

    private sealed class FailureDiagnosticLogger : ICopilotChatLogger
    {
        public ISessionCopilotChatLogger CreateSessionLogger(string logName) => new SessionLogger(this, Guid.ParseExact(logName, "N"));

        private sealed class SessionLogger(FailureDiagnosticLogger logger, Guid sessionId) : ISessionCopilotChatLogger
        {
            public Task LogMessageAsync(CopilotChatMessage message) => logger.LogMessageAsync(sessionId, message);

            public Task LogDiagnosticAsync(string category, string message, Exception? exception = null) =>
                logger.LogDiagnosticAsync(sessionId, category, message, exception);
        }

        public Task LogMessageAsync(Guid sessionId, CopilotChatMessage message) => Task.CompletedTask;
        public Task LogDiagnosticAsync(Guid sessionId, string category, string message, Exception? exception = null)
            => exception is null ? Task.CompletedTask : Task.FromException(new IOException("diagnostic failed"));
    }

    [TestMethod(DisplayName = "运行成功但保存失败时应抛出保存异常")]
    [Timeout(5000)]
    public async Task PersistenceFailureShouldBeThrownAfterSuccessfulRun()
    {
        var manager = CreateManager();
        var store = new TestSessionStore { SaveException = new IOException("保存失败") };
        var runner = new TestCodingChatRunner(manager);
        var application = CodingChatApplicationTestFactory.CreateApplication(manager, store, runner);
        await application.InitializeAsync();
        Task sendTask = application.SendMessageAsync("正常运行");
        await runner.Started.Task;
        runner.Complete("完成");

        IOException exception = await Assert.ThrowsExactlyAsync<IOException>(async () => await sendTask);

        Assert.AreEqual("保存失败", exception.Message);
        Assert.IsFalse(application.IsRunActive);
    }

    [TestMethod(DisplayName = "空回复完成后应保存没有占位符的助手消息")]
    [Timeout(5000)]
    public async Task EmptyResponseShouldSaveAssistantMessageWithoutPlaceholder()
    {
        var manager = CreateManager();
        var store = new TestSessionStore();
        var runner = new TestCodingChatRunner(manager);
        var application = CodingChatApplicationTestFactory.CreateApplication(manager, store, runner);
        await application.InitializeAsync();
        Task sendTask = application.SendMessageAsync("无内容回复");
        await runner.Started.Task;

        runner.Complete(null);
        await sendTask;

        Assert.AreEqual(string.Empty, runner.AssistantMessage.Content);
        Assert.AreNotEqual(CopilotChatMessage.PlaceholderContent, store.SavedSession!.ChatMessages[2].Content);
    }

    [TestMethod(DisplayName = "发送期间切换选择后保存仍应捕获开始运行的会话实例")]
    [Timeout(5000)]
    public async Task SendMessageAsyncShouldSaveCapturedSessionInstance()
    {
        var manager = CreateManager();
        var store = new TestSessionStore();
        var runner = new TestCodingChatRunner(manager);
        var application = CodingChatApplicationTestFactory.CreateApplication(manager, store, runner);
        await application.InitializeAsync();
        CopilotChatSession runningSession = manager.SelectedSession;
        Task sendTask = application.SendMessageAsync("检查代码");
        await runner.Started.Task;
        var otherSession = new CopilotChatSession(CreateManager().SelectedSession.AdditionalInfo.Storage);
        manager.AddSession(otherSession, select: true);

        runner.Complete("完成");
        await sendTask;

        Assert.AreSame(runningSession, store.SavedSession);
    }

    private sealed class TestCodingChatRunner(CopilotChatManager manager) : ICodingChatRunner
    {
        private TaskCompletionSource<string?> _completion = CreateCompletionSource();

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource SecondRunStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int RunCount { get; private set; }

        public CancellationToken ObservedCancellationToken { get; private set; }

        public string? ObservedWorkspacePath { get; private set; }

        public CodingChatShellSettings? ObservedShellSettings { get; private set; }
        public System.Net.Http.HttpClient? ObservedHttpClient { get; private set; }

        public bool ObservedAutomaticCompressionEnabled { get; private set; }

        public bool ObservedDotNetRunEnabled { get; private set; }

        public ReasoningEffort? ObservedReasoningEffort { get; private set; }

        public IReadOnlyList<AIContent>? ObservedContents { get; private set; }

        public IReadOnlyList<AIContent>? InjectedContents { get; private set; }

        public CopilotChatMessage AssistantMessage { get; private set; } = CreateAssistantMessage();

        public async Task<ICodingAgentRunResult> RunAsync(
            IReadOnlyList<AIContent> contents,
            CodingToolContext toolContext,
            CancellationToken cancellationToken)
        {
            RunCount++;
            if (RunCount > 1)
            {
                _completion = CreateCompletionSource();
                AssistantMessage = CreateAssistantMessage();
            }

            ObservedContents = contents;
            ObservedWorkspacePath = toolContext.WorkspacePath;
            ObservedShellSettings = toolContext.Settings;
            ObservedHttpClient = toolContext.HttpClient;
            ObservedAutomaticCompressionEnabled = toolContext.Options.EnableAutomaticCompression;
            ObservedDotNetRunEnabled = toolContext.Options.EnableDotNetRun;
            ObservedReasoningEffort = toolContext.Options.ReasoningEffort;
            ObservedCancellationToken = cancellationToken;
            cancellationToken.ThrowIfCancellationRequested();
            var session = toolContext.Session;
            var userMessage = CopilotChatMessage.CreateUser(contents);
            await session.AddMessageAsync(userMessage);
            await manager.ChatLogger.LogMessageAsync(session.SessionId, userMessage);
            await session.AddMessageAsync(AssistantMessage);
            Started.TrySetResult();
            if (RunCount == 2)
            {
                SecondRunStarted.TrySetResult();
            }

            return new CompletedCodingAgentRunResult(AssistantMessage, CompleteAsync(cancellationToken));
        }

        public Task InjectMessageAsync(
            IReadOnlyList<AIContent> contents,
            CancellationToken cancellationToken)
        {
            InjectedContents = contents;
            string text = Assert.IsInstanceOfType<TextContent>(contents[0]).Text;
            AssistantMessage.ClearMessageItems();
            AssistantMessage.AppendText($"插话回复：{text}");
            return Task.CompletedTask;
        }

        public void Complete(string? content)
        {
            AssistantMessage.ClearMessageItems();
            if (!string.IsNullOrEmpty(content))
            {
                AssistantMessage.AppendText(content);
            }

            _completion.TrySetResult(content);
        }

        public void Fail(Exception exception) => _completion.TrySetException(exception);

        private async Task<string?> CompleteAsync(CancellationToken cancellationToken)
        {
            return await _completion.Task.WaitAsync(cancellationToken);
        }

        private static TaskCompletionSource<string?> CreateCompletionSource()
            => new(TaskCreationOptions.RunContinuationsAsynchronously);

        private static CopilotChatMessage CreateAssistantMessage()
            => CopilotChatMessage.CreateAssistant(CopilotChatMessage.PlaceholderContent, isPresetInfo: false);
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task CompressionSaveShouldRemainActiveUntilSaveCompletes()
    {
        var manager = CreateManager();
        var store = new TestSessionStore { BlockSave = true };
        var controller = CodingChatApplicationTestFactory.CreateApplication(manager, store);
        bool notifiedIdle = false;
        controller.StateChanged += (_, _) => notifiedIdle = controller.CanChangeSession;
        Task<bool> compression = controller.CompressConversationAsync(null, new SuccessfulCompressionRunner());
        try
        {
            await store.SaveStarted.Task;
            Assert.IsTrue(controller.IsFinalizing && !controller.CanChangeSession && !notifiedIdle);
        }
        finally { store.ReleaseSave(); }
        await compression;
        Assert.IsTrue(controller.CanChangeSession && notifiedIdle);
    }

    private sealed class SuccessfulCompressionRunner : ICodingChatRunner
    {
        public Task<ICodingAgentRunResult> RunAsync(IReadOnlyList<AIContent> contents, CodingToolContext toolContext,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> TryCompactConversationAsync(CopilotChatSession session, string? instructions,
            CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private static string CreateTestDirectory()
    {
        string path = Path.Join(Path.GetTempPath(), $"CodingChatRoom.SendWorkspace.{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class ImmediateMainThreadDispatcher : IMainThreadDispatcher
    {
        public Task InvokeAsync(Func<Task> action) => action();

        public Task<T> InvokeAsync<T>(Func<Task<T>> action) => action();

        public bool CheckAccess() => true;
    }

    private sealed class TestSessionStore : ICodingChatSessionStore
    {
        private readonly TaskCompletionSource _continueSave = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Exception? SaveException { get; init; }

        public bool BlockSave { get; init; }

        public TaskCompletionSource SaveStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int SaveCount { get; private set; }

        public CopilotChatSession? SavedSession { get; private set; }

        public Task<IReadOnlyList<CopilotChatSessionSummary>> ListSessionsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CopilotChatSessionSummary>>([]);

        public Task<CopilotChatSession> LoadSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<CopilotChatSession> CreateSessionFromSnapshotAsync(CopilotChatSession sourceSession, CopilotChatMessage message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> DeleteSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task SaveSnapshotAsync(CopilotChatSession session, SessionSnapshotId snapshotId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public async Task SaveSessionAsync(CopilotChatSession session, CancellationToken cancellationToken = default)
        {
            SaveStarted.TrySetResult();
            if (BlockSave)
            {
                await _continueSave.Task.WaitAsync(cancellationToken);
            }

            if (SaveException is not null)
            {
                throw SaveException;
            }

            SaveCount++;
            SavedSession = session;
        }

        public void ReleaseSave() => _continueSave.TrySetResult();
    }
}
