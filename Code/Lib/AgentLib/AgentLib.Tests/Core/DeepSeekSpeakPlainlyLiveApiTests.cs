using AgentLib.Logging;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.DeepSeek;

namespace AgentLib.Tests.Core;

[TestClass]
[TestCategory("LiveApi")]
[DoNotParallelize]
public sealed class DeepSeekSpeakPlainlyLiveApiTests
{
    [TestMethod]
    public async Task SpeakPlainlyWithTrailingSystemMessageCanFailWhereUserInputSucceedsAsync()
    {
        string? key = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY");
        if (string.IsNullOrWhiteSpace(key))
        {
            Assert.Inconclusive("Set DEEPSEEK_API_KEY to run the live test.");
        }

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(50));
        using var client = new DeepSeekChatClient(key.Trim(), "deepseek-v4-pro");
        var agent = client.AsAIAgent(new ChatClientAgentOptions
        {
            ChatOptions = new ChatOptions { MaxOutputTokens = 512 },
            ChatHistoryProvider = new InMemoryChatHistoryProvider(),
#pragma warning disable MAAI001
            RequirePerServiceCallChatHistoryPersistence = true,
#pragma warning restore MAAI001
        });
        const string closingPrompt = "Restate the preceding answer in plain language. Reply in one short sentence.";
        const string reasoningError = "The `reasoning_content` in the thinking mode must be passed back to the API.";
        var logger = new EmptyCopilotChatLogger().CreateSessionLogger("DeepSeekSpeakPlainlyLive");

        string answer = await RunAsync(new ChatMessage(ChatRole.User, closingPrompt));
        Assert.IsFalse(string.IsNullOrWhiteSpace(answer), "The user-input path did not return any text.");

        try
        {
            await RunAsync(new ChatMessage(ChatRole.System, closingPrompt));
        }
        catch (InvalidOperationException exception) when (exception.Message.Contains("HTTP 400", StringComparison.Ordinal)
                                                          && exception.Message.Contains(reasoningError, StringComparison.Ordinal))
        {
            return;
        }

        Assert.Inconclusive("The live endpoint accepted the legacy request; the reported HTTP 400 was not reproduced.");

        async Task<string> RunAsync(ChatMessage prompt)
        {
            var session = await agent.CreateSessionAsync(cancellation.Token);
            const string callId = "call_test_lookup_weather";
            List<ChatMessage> history =
            [
                new(ChatRole.System, "Explain the prior conversation in plain language."),
                new(ChatRole.User, "What is the weather in the fictional city of Sampleton?"),
                new(ChatRole.Assistant, [new FunctionCallContent(callId, "lookup_weather", new Dictionary<string, object?> { ["city"] = "Sampleton" })]),
                new(ChatRole.Tool, [new FunctionResultContent(callId, "Clear skies, 20 C")]),
                new(ChatRole.Assistant, "Sampleton has clear skies and a temperature of 20 C."),
            ];
            IReadOnlyList<ChatMessage> input = [prompt];
            if (prompt.Role == ChatRole.System)
            {
                history.Add(prompt);
                input = [];
            }
            session.SetInMemoryChatHistory(history);

            var answer = new System.Text.StringBuilder();
            await foreach (var update in agent.RunWithHistoryCompletionAsync(input, session, logger, cancellation.Token))
            {
                answer.Append(update.Text);
            }
            return answer.ToString();
        }
    }
}
