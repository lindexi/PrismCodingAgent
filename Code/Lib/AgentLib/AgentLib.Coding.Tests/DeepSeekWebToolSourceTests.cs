using System.Net;
using System.Text.Json;
using DeepSeekSDK;
using Microsoft.Extensions.AI;

namespace AgentLib.Coding.Tests;

[TestClass]
public sealed class DeepSeekWebToolSourceTests
{
    [TestMethod]
    public void WorkspaceToolSourceShouldExposeOnlyRunScopedRegistrationEntry()
    {
        var method = typeof(ICodingWorkspaceToolSource).GetMethods().Single();

        Assert.AreEqual("CreateToolRegistrations", method.Name);
        CollectionAssert.AreEqual(new[] { typeof(CodingRunContext) },
            method.GetParameters().Select(parameter => parameter.ParameterType).ToArray());
        Assert.IsTrue(method.IsAbstract);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" \t\r\n")]
    public void CreateToolRegistrationsWhenApiKeyIsMissingShouldRegisterBothFunctions(string? apiKey)
    {
        using var httpClient = new HttpClient();
        IReadOnlyList<AIFunction> functions = CreateFunctions(httpClient, apiKey);

        CollectionAssert.AreEquivalent(new[] { "Search", "Fetch" },
            functions.Select(function => function.Name).ToArray());
    }

    [TestMethod]
    [DataRow(null, "Search", "query")]
    [DataRow("", "Search", "query")]
    [DataRow(" \t\r\n", "Search", "query")]
    [DataRow(null, "Fetch", "url")]
    [DataRow("", "Fetch", "url")]
    [DataRow(" \t\r\n", "Fetch", "url")]
    public async Task InvokeAsyncWhenApiKeyIsMissingShouldReturnSettingsHintAsync(
        string? apiKey, string toolName, string parameterName)
    {
        using var httpClient = new HttpClient();
        AIFunction function = CreateFunctions(httpClient, apiKey).Single(tool => tool.Name == toolName);
        var arguments = new AIFunctionArguments { [parameterName] = "https://example.invalid/" };

        object? result = await function.InvokeAsync(arguments);

        Assert.AreEqual("尚未配置 DeepSeek API Key，网页搜索和网页拉取工具不可用。请提醒用户前往设置填写 DeepSeek API Key，或配置 DeepSeek 官方模型服务的 API Key，或者是不调用 Search 和 Fetch 工具。", Assert.IsInstanceOfType<JsonElement>(result).GetString());
    }

    [TestMethod]
    [DataRow(null, "Search", "query")]
    [DataRow(null, "Fetch", "url")]
    [DataRow("offline-test-key", "Search", "query")]
    [DataRow("offline-test-key", "Fetch", "url")]
    public async Task InvokeAsyncWhenCanceledShouldPropagateCancellationAsync(
        string? apiKey, string toolName, string parameterName)
    {
        using var httpClient = new HttpClient();
        AIFunction function = CreateFunctions(httpClient, apiKey).Single(tool => tool.Name == toolName);
        var arguments = new AIFunctionArguments { [parameterName] = "https://example.invalid/" };
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        OperationCanceledException exception = await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await function.InvokeAsync(arguments, cancellation.Token));

        Assert.AreEqual(cancellation.Token, exception.CancellationToken);
    }

    [TestMethod]
    [DataRow("Search", "query", "query,count,needContent,originalQuestion,localization")]
    [DataRow("Fetch", "url", "url")]
    public void CreateToolRegistrationsShouldGenerateSerializableParameterSchema(
        string toolName, string requiredParameter, string parameters)
    {
        using var httpClient = new HttpClient();
        AIFunction function = CreateFunctions(httpClient, null).Single(tool => tool.Name == toolName);

        using JsonDocument schema = JsonDocument.Parse(JsonSerializer.Serialize(function.JsonSchema));

        CollectionAssert.AreEquivalent(parameters.Split(','), schema.RootElement.GetProperty("properties")
            .EnumerateObject().Select(property => property.Name).ToArray());
        CollectionAssert.AreEqual(new[] { requiredParameter }, schema.RootElement.GetProperty("required")
            .EnumerateArray().Select(parameter => parameter.GetString()).ToArray());
        Assert.AreEqual("object", schema.RootElement.GetProperty("type").GetString());
        Assert.AreEqual("string", schema.RootElement.GetProperty("properties")
            .GetProperty(requiredParameter).GetProperty("type").GetString());
    }

    [TestMethod]
    public void RunWithoutHttpClientShouldNotRegisterTools()
    {
        Assert.IsEmpty(DeepSeekWebToolSource.CreateToolRegistrations(ToolRegistrationTestContext.Create()));
    }

    [TestMethod]
    public void CreateToolRegistrationsShouldNotChangeHttpClientConfiguration()
    {
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(17) };
        CreateFunctions(httpClient, "offline-test-key");

        Assert.AreEqual(TimeSpan.FromSeconds(17), httpClient.Timeout);
    }

    [TestMethod]
    [DataRow("Search", "query", "{\"results\":[]}")]
    [DataRow("Fetch", "url", "{\"title\":\"Offline\",\"content\":\"Body\"}")]
    public async Task InvokeAsyncShouldUseInjectedHttpClientAsync(string toolName, string parameterName, string response)
    {
        using var httpClient = new HttpClient(new OfflineHandler(response));
        AIFunction function = CreateFunctions(httpClient, "offline-test-key").Single(tool => tool.Name == toolName);

        object? result = await function.InvokeAsync(new AIFunctionArguments { [parameterName] = "https://example.invalid/" });

        Assert.AreEqual(response, Assert.IsInstanceOfType<JsonElement>(result).GetString());
    }

    [TestMethod]
    public async Task InvokeAsyncShouldLeaveInjectedHttpClientUsableAsync()
    {
        const string response = "{\"results\":[]}";
        using var httpClient = new HttpClient(new OfflineHandler(response));
        AIFunction function = CreateFunctions(httpClient, "offline-test-key").Single(tool => tool.Name == "Search");
        await function.InvokeAsync(new AIFunctionArguments { ["query"] = "offline" });

        string content = await httpClient.GetStringAsync("https://example.invalid/");

        Assert.AreEqual(response, content);
    }

    [TestMethod]
    [DataRow(typeof(DeepSeekSearchRequest))]
    [DataRow(typeof(DeepSeekFetchRequest))]
    [DataRow(typeof(DeepSeekSearchResponse))]
    [DataRow(typeof(DeepSeekSearchResult))]
    [DataRow(typeof(DeepSeekFetchResponse))]
    public void DeserializeWhenRequiredFieldsAreMissingShouldThrow(Type type)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize("{}", type));
    }

    [TestMethod]
    [DataRow("site_name")]
    [DataRow("site_icon")]
    [DataRow("date")]
    public void DeserializeWhenNullableRequiredFieldIsMissingShouldThrow(string field)
    {
        var fields = new Dictionary<string, object?>
        {
            ["rerank_score"] = 1.5, ["title"] = "Title", ["url"] = "https://example.invalid/",
            ["snippet"] = "", ["summary"] = "", ["content"] = "", ["site_name"] = null,
            ["site_icon"] = null, ["date"] = null, ["authority"] = -1,
        };
        fields.Remove(field);

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<DeepSeekSearchResult>(JsonSerializer.Serialize(fields)));
    }

    [TestMethod]
    [DataRow("Search", "query", "{\"results\":[]}")]
    [DataRow("Fetch", "url", "{\"title\":\"Offline\",\"content\":\"Body\"}")]
    public async Task EachRunShouldKeepItsOwnApiKeyAsync(string name, string parameter, string response)
    {
        var handler = new RecordingHandler(response);
        using var httpClient = new HttpClient(handler);
        var first = CreateFunctions(httpClient, " first-test-key ").Single(tool => tool.Name == name);
        var second = CreateFunctions(httpClient, "second-test-key").Single(tool => tool.Name == name);
        var arguments = new AIFunctionArguments { [parameter] = "https://example.invalid/" };

        await first.InvokeAsync(arguments);
        await second.InvokeAsync(arguments);
        await first.InvokeAsync(arguments);

        CollectionAssert.AreEqual(new[] { "first-test-key", "second-test-key", "first-test-key" }, handler.Keys.ToArray());
    }

    [TestMethod]
    public async Task RegisteringNewRunDuringRequestShouldKeepInFlightSnapshotAsync()
    {
        var started = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var httpClient = new HttpClient(new BlockingHandler(started, release));
        var function = CreateFunctions(httpClient, "old-test-key").Single(tool => tool.Name == "Search");
        var arguments = new AIFunctionArguments { ["query"] = "offline" };
        var pending = function.InvokeAsync(arguments).AsTask();
        try
        {
            Assert.AreEqual("old-test-key", await started.Task.WaitAsync(TimeSpan.FromSeconds(10)));
            var next = CreateFunctions(httpClient, null).Single(tool => tool.Name == "Search");
            StringAssert.Contains(Assert.IsInstanceOfType<JsonElement>(await next.InvokeAsync(arguments)).GetString(), "尚未配置");
        }
        finally
        {
            release.TrySetResult();
        }
        Assert.AreEqual("{\"results\":[]}", Assert.IsInstanceOfType<JsonElement>(await pending).GetString());
    }

    [TestMethod]
    [DataRow("Search", "query")]
    [DataRow("Fetch", "url")]
    public async Task HttpFailureShouldReturnContextWithoutThrowingAsync(string operation, string parameter)
    {
        using var httpClient = new HttpClient(new FailureHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"error\":\"offline failure\",\"request_id\":\"req-offline\"}")
            })));
        var function = CreateFunctions(httpClient, "offline-secret-key").Single(tool => tool.Name == operation);

        var result = Assert.IsInstanceOfType<JsonElement>(await function.InvokeAsync(
            new AIFunctionArguments { [parameter] = "https://example.invalid/target" })).GetString();

        StringAssert.Contains(result, operation);
        StringAssert.Contains(result, "https://example.invalid/target");
        StringAssert.Contains(result, "offline failure");
        StringAssert.Contains(result, "request_id");
        StringAssert.Contains(result, "req-offline");
        StringAssert.Contains(result, "400");
        Assert.IsFalse(result!.Contains("offline-secret-key", StringComparison.Ordinal));
        Assert.IsFalse(result.Contains("Authorization", StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow("Search", "Search Query=offline-target; Count=10; NeedContent=False; Localization=; OriginalQuestion=")]
    [DataRow("Fetch", "Fetch Url=https://example.invalid/offline-target")]
    public async Task SdkHttpFailureShouldPreserveStatusAndContextAsync(string operation, string context)
    {
        using var httpClient = new HttpClient(new FailureHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent(" \r\nrequest_id=req-sdk; offline-secret-key\t ")
            })));
        var client = new DeepSeekClient(httpClient, "offline-secret-key");

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => CallSdkAsync(client, operation));

        Assert.AreEqual(HttpStatusCode.Forbidden, exception.StatusCode);
        Assert.IsNull(exception.InnerException);
        Assert.AreEqual($"DeepSeek {context} 失败；endpoint=https://api.deepseek.com/v1/tools/{operation.ToLowerInvariant()}；HTTP 403:  \r\nrequest_id=req-sdk; offline-secret-key\t ",
            exception.Message);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" \t\r\n")]
    public async Task SdkEmptyErrorBodyShouldUseReasonPhraseAsync(string? body)
    {
        using var httpClient = new HttpClient(new FailureHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                ReasonPhrase = "offline reason",
                Content = new StringContent(body ?? string.Empty)
            })));
        var client = new DeepSeekClient(httpClient, "offline-secret-key");

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => CallSdkAsync(client, "Fetch"));

        Assert.AreEqual("DeepSeek Fetch Url=https://example.invalid/offline-target 失败；endpoint=https://api.deepseek.com/v1/tools/fetch；HTTP 403: offline reason",
            exception.Message);
    }

    [TestMethod]
    public async Task SearchFailureShouldIncludeExplicitBusinessParametersAsync()
    {
        using var httpClient = new HttpClient(new FailureHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.BadRequest))));
        var client = new DeepSeekClient(httpClient, "offline-secret-key");

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => client.SearchAsync(
            new DeepSeekSearchRequest
            {
                Query = "offline query", Count = 3, NeedContent = true,
                Localization = "127.0.0.1", OriginalQuestion = "original question"
            }));

        StringAssert.Contains(exception.Message, "Query=offline query");
        StringAssert.Contains(exception.Message, "Count=3");
        StringAssert.Contains(exception.Message, "NeedContent=True");
        StringAssert.Contains(exception.Message, "Localization=127.0.0.1");
        StringAssert.Contains(exception.Message, "OriginalQuestion=original question");
    }

    [TestMethod]
    [DataRow("Search")]
    [DataRow("Fetch")]
    public async Task SdkTransportFailureShouldPreserveExceptionChainAsync(string operation)
    {
        var cause = new IOException("offline connection failure");
        var transport = new HttpRequestException("offline transport", cause, HttpStatusCode.BadGateway);
        using var httpClient = new HttpClient(new FailureHandler((_, _) => throw transport));
        var client = new DeepSeekClient(httpClient, "offline-secret-key");

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => CallSdkAsync(client, operation));

        Assert.AreEqual(HttpStatusCode.BadGateway, exception.StatusCode);
        Assert.AreSame(transport, exception.InnerException);
        Assert.AreSame(cause, Assert.IsInstanceOfType<HttpRequestException>(exception.InnerException).InnerException);
        StringAssert.Contains(exception.Message, operation);
        StringAssert.Contains(exception.Message, "offline-target");
        StringAssert.Contains(exception.Message, "HTTP 502: offline transport");
        StringAssert.Contains(exception.Message, "endpoint=https://api.deepseek.com/v1/tools/" + operation.ToLowerInvariant());
        Assert.IsFalse(exception.Message.Contains("offline-secret-key", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task SdkErrorBodyReadFailureShouldPreserveInnerExceptionAsync()
    {
        var readException = new HttpRequestException("offline read failure", null, HttpStatusCode.RequestTimeout);
        using var httpClient = new HttpClient(new FailureHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.BadGateway) { Content = new BrokenContent(readException) })));
        var client = new DeepSeekClient(httpClient, "offline-secret-key");

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => CallSdkAsync(client, "Fetch"));

        Assert.AreEqual(HttpStatusCode.BadGateway, exception.StatusCode);
        Assert.AreSame(readException, exception.InnerException);
        Assert.AreEqual("DeepSeek Fetch Url=https://example.invalid/offline-target 失败；endpoint=https://api.deepseek.com/v1/tools/fetch；HTTP 502: Bad Gateway",
            exception.Message);
    }

    [TestMethod]
    [DataRow("Search", "query", "transport")]
    [DataRow("Fetch", "url", "transport")]
    [DataRow("Search", "query", "json")]
    [DataRow("Fetch", "url", "json")]
    [DataRow("Search", "query", "timeout")]
    [DataRow("Fetch", "url", "timeout")]
    public async Task ExpectedFailuresShouldReturnReadableToolResultAsync(string operation, string parameter, string failure)
    {
        using var httpClient = new HttpClient(new FailureHandler((_, _) => failure switch
        {
            "transport" => throw new HttpRequestException("offline transport offline-secret-key"),
            "timeout" => throw new TaskCanceledException("offline timeout"),
            _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") })
        }));
        var function = CreateFunctions(httpClient, "offline-secret-key").Single(tool => tool.Name == operation);

        var result = Assert.IsInstanceOfType<JsonElement>(await function.InvokeAsync(
            new AIFunctionArguments { [parameter] = "offline-target" })).GetString();

        StringAssert.Contains(result, operation);
        StringAssert.Contains(result, "offline-target");
        StringAssert.Contains(result, "失败");
        StringAssert.Contains(result, "endpoint=https://api.deepseek.com/v1/tools/" + operation.ToLowerInvariant());
        StringAssert.Contains(result, failure switch
        {
            "transport" => "offline transport offline-secret-key",
            "json" => "服务端返回的 JSON 无效或缺少必需字段",
            _ => "请求超时或传输被取消"
        });
    }

    [TestMethod]
    [DataRow("Search", "query")]
    [DataRow("Fetch", "url")]
    public async Task CancellationDuringRequestShouldPropagateAsync(string operation, string parameter)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var httpClient = new HttpClient(new FailureHandler(async (_, token) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));
        using var cancellation = new CancellationTokenSource();
        var function = CreateFunctions(httpClient, "offline-secret-key").Single(tool => tool.Name == operation);
        var pending = function.InvokeAsync(new AIFunctionArguments { [parameter] = "offline-target" }, cancellation.Token).AsTask();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));

        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await pending);
    }

    [TestMethod]
    [DataRow("Search", "query")]
    [DataRow("Fetch", "url")]
    public async Task ProgrammingErrorsShouldNotBecomeToolResultsAsync(string operation, string parameter)
    {
        using var httpClient = new HttpClient(new FailureHandler((_, _) => throw new InvalidOperationException("offline bug")));
        var function = CreateFunctions(httpClient, "offline-secret-key").Single(tool => tool.Name == operation);

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await function.InvokeAsync(
            new AIFunctionArguments { [parameter] = "offline-target" }));
    }

    [TestMethod]
    [DataRow("Search")]
    [DataRow("Fetch")]
    public async Task SdkSuccessBodyHttpFailureShouldPreserveContextAndExceptionAsync(string operation)
    {
        var cause = new IOException("offline body failure");
        var readException = new HttpRequestException("offline body transport", cause, HttpStatusCode.BadGateway);
        using var httpClient = new HttpClient(new FailureHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new BrokenContent(readException) })));
        var client = new DeepSeekClient(httpClient, "offline-secret-key");

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => CallSdkAsync(client, operation));

        Assert.AreEqual(HttpStatusCode.BadGateway, exception.StatusCode);
        Assert.AreSame(readException, exception.InnerException);
        Assert.AreSame(cause, readException.InnerException);
        StringAssert.Contains(exception.Message, "DeepSeek " + operation);
        StringAssert.Contains(exception.Message, "offline-target");
        StringAssert.Contains(exception.Message, "endpoint=https://api.deepseek.com/v1/tools/" + operation.ToLowerInvariant());
        StringAssert.Contains(exception.Message, "HTTP 502: offline body transport");
    }

    private static async Task CallSdkAsync(DeepSeekClient client, string operation)
    {
        if (operation == "Search")
        {
            await client.SearchAsync(new DeepSeekSearchRequest { Query = "offline-target" });
        }
        else
        {
            await client.FetchAsync(new DeepSeekFetchRequest { Url = "https://example.invalid/offline-target" });
        }
    }

    [TestMethod]
    [DataRow(null, "Search", "query")]
    [DataRow(null, "Fetch", "url")]
    [DataRow("offline-key", "Search", "query")]
    [DataRow("offline-key", "Fetch", "url")]
    public async Task DisabledToolsShouldRemainRegisteredAndMakeNoHttpRequestAsync(string? key, string name, string parameter)
    {
        using var handler = new RecordingHandler("{}");
        using var http = new HttpClient(handler);
        var context = ToolRegistrationTestContext.Create(httpClient: http, settings: new() { DeepSeekApiKey = key }, options:
            new CodingChatRunOptions { EnableWebSearch = false, EnableWebFetch = false });
        var tools = DeepSeekWebToolSource.CreateToolRegistrations(context);

        CollectionAssert.AreEquivalent(new[] { "Search", "Fetch" },
            tools.Select(tool => ((AIFunction)tool.Tool).Name).ToArray());
        var function = tools.Select(tool => (AIFunction)tool.Tool).Single(tool => tool.Name == name);
        object? result = await function.InvokeAsync(new AIFunctionArguments { [parameter] = "https://example.com/" });

        StringAssert.Contains(Assert.IsInstanceOfType<JsonElement>(result).GetString(), name + " 已在当前对话运行设置中禁用");
        Assert.IsEmpty(handler.Keys);
    }

    [TestMethod]
    [DataRow("Search", "query", false, true)]
    [DataRow("Fetch", "url", true, false)]
    public async Task WebToolFunctionsShouldCaptureIndependentSwitchesAsync(string name, string parameter, bool search, bool fetch)
    {
        using var handler = new RecordingHandler("{}");
        using var http = new HttpClient(handler);
        var options = new CodingChatRunOptions { EnableWebSearch = search, EnableWebFetch = fetch };
        var first = DeepSeekWebToolSource.CreateToolRegistrations(ToolRegistrationTestContext.Create(options: options, httpClient: http))
            .Select(tool => (AIFunction)tool.Tool).ToArray();
        options.EnableWebSearch = !search;
        options.EnableWebFetch = !fetch;
        var second = DeepSeekWebToolSource.CreateToolRegistrations(ToolRegistrationTestContext.Create(options: options, httpClient: http))
            .Select(tool => (AIFunction)tool.Tool).ToArray();
        var arguments = new AIFunctionArguments { [parameter] = "https://example.com/" };

        object? disabled = await first.Single(tool => tool.Name == name).InvokeAsync(arguments);
        object? enabled = await second.Single(tool => tool.Name == name).InvokeAsync(arguments);
        object? other = await first.Single(tool => tool.Name != name).InvokeAsync(new AIFunctionArguments
            { [name == "Search" ? "url" : "query"] = "https://example.com/" });

        StringAssert.Contains(Assert.IsInstanceOfType<JsonElement>(disabled).GetString(), "禁用");
        StringAssert.Contains(Assert.IsInstanceOfType<JsonElement>(enabled).GetString(), "尚未配置 DeepSeek API Key");
        StringAssert.Contains(Assert.IsInstanceOfType<JsonElement>(other).GetString(), "尚未配置 DeepSeek API Key");
    }

    [TestMethod]
    [DataRow("Search", "query")]
    [DataRow("Fetch", "url")]
    public async Task DisabledToolsShouldPreserveCancellationAsync(string name, string parameter)
    {
        using var http = new HttpClient();
        var context = ToolRegistrationTestContext.Create(httpClient: http, options:
            new CodingChatRunOptions { EnableWebSearch = false, EnableWebFetch = false });
        var function = DeepSeekWebToolSource.CreateToolRegistrations(context)
            .Select(tool => (AIFunction)tool.Tool).Single(tool => tool.Name == name);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await function.InvokeAsync(new AIFunctionArguments { [parameter] = "https://example.com/" }, cancellation.Token));
    }

    private sealed class FailureHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => send(request, cancellationToken);
    }

    private sealed class BrokenContent(Exception cause) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => Task.FromException(cause);

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    private sealed class RecordingHandler(string response) : HttpMessageHandler
    {
        internal List<string?> Keys { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Keys.Add(request.Headers.Authorization?.Parameter);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response) });
        }
    }

    private sealed class BlockingHandler(TaskCompletionSource<string?> started, TaskCompletionSource release) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            started.TrySetResult(request.Headers.Authorization?.Parameter);
            await release.Task.WaitAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"results\":[]}") };
        }
    }

    private static IReadOnlyList<AIFunction> CreateFunctions(HttpClient httpClient, string? apiKey)
    {
        var context = ToolRegistrationTestContext.Create(httpClient: httpClient, settings: new() { DeepSeekApiKey = apiKey });
        return DeepSeekWebToolSource.CreateToolRegistrations(context)
            .Select(registration => (AIFunction)registration.Tool).ToArray();
    }

    private sealed class OfflineHandler(string response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response) });
    }
}
