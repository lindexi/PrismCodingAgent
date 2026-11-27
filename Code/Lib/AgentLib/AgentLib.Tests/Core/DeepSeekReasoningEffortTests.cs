using System.Net;
using System.Text.Json;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.DeepSeek;

namespace AgentLib.Tests.Core;

[TestClass]
public sealed class DeepSeekReasoningEffortTests
{
    [TestMethod]
    [DataRow(null, null)]
    [DataRow(ReasoningEffort.Low, "low")]
    [DataRow(ReasoningEffort.Medium, "high")]
    [DataRow(ReasoningEffort.High, "high")]
    public async Task RequestShouldUseDeepSeekReasoningEffort(ReasoningEffort? effort, string? expected)
    {
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        using var client = new DeepSeekChatClient("test-key", "deepseek-v4-flash", httpClient: httpClient);
        var options = effort is null ? null : new ChatOptions { Reasoning = new ReasoningOptions { Effort = effort } };

        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "test")], options);

        Assert.AreEqual(expected, handler.ReasoningEffort);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public string? ReasoningEffort { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using var stream = await request.Content!.ReadAsStreamAsync(cancellationToken);
            using var body = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            ReasoningEffort = body.RootElement.TryGetProperty("reasoning_effort", out var effort) ? effort.GetString() : null;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("data: [DONE]\n\n") };
        }
    }
}
