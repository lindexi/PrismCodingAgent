using AgentLib.Core.AgentApiManagers.LanguageModelProviders.Fakes;
using AgentLib.Model;
using AgentLib.Tests.Fakes;
using Microsoft.Extensions.AI;

namespace AgentLib.Tests;

[TestClass]
public class SessionTitleGeneratorTests
{
    [TestMethod]
    public async Task GenerateTitleAsyncShouldSendLastTenValidMessagesInTimelineOrder()
    {
        var capturedRequests = new List<(ChatRole Role, string Content)[]>();
        var chatClient = new FakeChatClient
        {
            OnGetResponseAsync = (messages, _, _) =>
            {
                capturedRequests.Add(messages.Select(message => (message.Role, message.Text)).ToArray());
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Title response")));
            }
        };
        var context = CopilotChatManagerTestContext.Create(chatClient);
        var generator = new SessionTitleGenerator(context.ChatManager.AgentApiEndpointManager);
        var session = new CopilotChatSession();
        var expected = new List<(ChatRole Role, string Content)>
        {
            (ChatRole.System, SessionTitleGenerator.SystemPrompt)
        };
        expected.AddRange(Enumerable.Range(3, 10).Select(i =>
            (i % 2 == 0 ? ChatRole.User : ChatRole.Assistant, $"Message {i}")));
        for (int i = 0; i < 13; i++)
        {
            ChatRole role = i % 2 == 0 ? ChatRole.User : ChatRole.Assistant;
            string content = $"Message {i}";
            await session.AddMessageAsync(new CopilotChatMessage(role, content));
            await session.AddEntryAsync(new CopilotChatNotice($"Notice {i}"));
            await session.AddMessageAsync(CopilotChatMessage.CreateAssistant("Greeting", isPresetInfo: true));
            await session.AddMessageAsync(new CopilotChatMessage(ChatRole.Assistant, string.Empty));
        }

        await generator.GenerateTitleAsync(session);

        CollectionAssert.AreEqual(expected.ToArray(), capturedRequests.Single());
    }

    [TestMethod]
    public async Task GenerateTitleAsyncShouldNotCallModelWhenOnlyNoticesPresetAndEmptyMessagesExist()
    {
        int callCount = 0;
        var chatClient = new FakeChatClient
        {
            OnGetResponseAsync = (_, _, _) =>
            {
                callCount++;
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Unexpected response")));
            }
        };
        var context = CopilotChatManagerTestContext.Create(chatClient);
        var generator = new SessionTitleGenerator(context.ChatManager.AgentApiEndpointManager);
        var session = new CopilotChatSession();
        await session.AddMessageAsync(CopilotChatMessage.CreateAssistant("Greeting", isPresetInfo: true));
        await session.AddEntryAsync(new CopilotChatNotice("Error", CopilotChatNoticeLevel.Error));
        await session.AddMessageAsync(new CopilotChatMessage(ChatRole.Assistant, string.Empty));

        await generator.GenerateTitleAsync(session);

        Assert.AreEqual(0, callCount);
    }

    [TestMethod]
    public async Task GenerateTitleAsyncShouldNotCallModelWhenEntriesAreEmpty()
    {
        int callCount = 0;
        var chatClient = new FakeChatClient
        {
            OnGetResponseAsync = (_, _, _) =>
            {
                callCount++;
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Unexpected response")));
            }
        };
        var context = CopilotChatManagerTestContext.Create(chatClient);
        var generator = new SessionTitleGenerator(context.ChatManager.AgentApiEndpointManager);
        var session = new CopilotChatSession();

        await generator.GenerateTitleAsync(session);

        Assert.AreEqual(0, callCount);
    }
}
