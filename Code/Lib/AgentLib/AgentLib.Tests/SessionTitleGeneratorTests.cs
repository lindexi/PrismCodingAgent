using AgentLib.Core.AgentApiManagers.LanguageModelProviders.Fakes;
using AgentLib.Model;
using AgentLib.Tests.Fakes;
using Microsoft.Extensions.AI;

namespace AgentLib.Tests;

[TestClass]
public class SessionTitleGeneratorTests
{
    [TestMethod]
    public async Task PreviewShouldTruncateOlderMessageToRemainingTotalBudget()
    {
        ChatMessage[] captured = [];
        var client = new FakeChatClient
        {
            OnGetResponseAsync = (messages, _, _) =>
            {
                captured = messages.ToArray();
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "unused")));
            }
        };
        var context = CopilotChatManagerTestContext.Create(client);
        var session = new CopilotChatSession();
        await session.AddMessageAsync(new CopilotChatMessage(ChatRole.User, "excluded"));
        await session.AddMessageAsync(new CopilotChatMessage(ChatRole.User, "abcdef"));
        await session.AddMessageAsync(new CopilotChatMessage(ChatRole.Assistant, new string('x', 3997)));

        await new SessionTitleGenerator(context.ChatManager.AgentApiEndpointManager).GenerateTitlePreviewAsync(session);

        CollectionAssert.AreEqual(new[] { SessionTitleGenerator.SystemPrompt, "abc", new string('x', 3997) }, captured.Select(m => m.Text).ToArray());
    }

    [TestMethod]
    public async Task PreviewShouldAllowSingleMessageToUseEntireBudget()
    {
        string? capturedBody = null;
        var client = new FakeChatClient
        {
            OnGetResponseAsync = (messages, _, _) =>
            {
                capturedBody = messages.Last().Text;
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "unused")));
            }
        };
        var context = CopilotChatManagerTestContext.Create(client);
        var session = new CopilotChatSession();
        await session.AddMessageAsync(new CopilotChatMessage(ChatRole.User, new string('a', 4000) + "tail"));

        await new SessionTitleGenerator(context.ChatManager.AgentApiEndpointManager).GenerateTitlePreviewAsync(session);

        Assert.AreEqual(new string('a', 4000), capturedBody);
    }

    [TestMethod]
    [DataRow(TitleSource.UserSet)]
    [DataRow(TitleSource.Generated)]
    public async Task AutomaticGenerationShouldSkipExistingTitle(TitleSource source)
    {
        int calls = 0;
        var client = new FakeChatClient
        {
            OnGetResponseAsync = (_, _, _) =>
            {
                calls++;
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "unused")));
            }
        };
        var context = CopilotChatManagerTestContext.Create(client);
        var session = new CopilotChatSession();
        session.SetTitle("Original", source);
        await session.AddMessageAsync(new CopilotChatMessage(ChatRole.User, "task"));

        await new SessionTitleGenerator(context.ChatManager.AgentApiEndpointManager).GenerateTitleAsync(session);

        Assert.AreEqual(0, calls);
    }

    [TestMethod]
    public async Task PreviewShouldUseOnlyVisibleUserAndAssistantBody()
    {
        ChatMessage[] captured = [];
        var client = new FakeChatClient
        {
            OnGetResponseAsync = (messages, _, _) =>
            {
                captured = messages.ToArray();
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "unused")));
            }
        };
        var context = CopilotChatManagerTestContext.Create(client);
        var session = new CopilotChatSession();
        await session.AddMessageAsync(new CopilotChatMessage(ChatRole.System, "system"));
        await session.AddMessageAsync(new CopilotChatMessage(ChatRole.Tool, "tool"));
        await session.AddMessageAsync(new CopilotChatMessage(ChatRole.Assistant, new AIContent[]
        {
            new TextContent("visible"), new TextReasoningContent("reasoning"),
            new FunctionResultContent("call", "result"), new DataContent(new byte[] { 1 }, "image/png")
        }));

        await new SessionTitleGenerator(context.ChatManager.AgentApiEndpointManager).GenerateTitlePreviewAsync(session);

        CollectionAssert.AreEqual(new[] { SessionTitleGenerator.SystemPrompt, "visible" }, captured.Select(m => m.Text).ToArray());
    }

    [TestMethod]
    public async Task PreviewShouldReturnSubmittedTitleWithoutChangingUserSetTitle()
    {
        var client = new FakeChatClient
        {
            OnGetResponseAsync = (_, _, _) => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant,
                new AIContent[] { new FunctionCallContent("title", SessionTitleGenerator.TitleSubmissionToolName,
                    new Dictionary<string, object?> { ["title"] = "New title" }) })))
        };
        var context = CopilotChatManagerTestContext.Create(client);
        var session = new CopilotChatSession();
        session.SetTitle("Original", TitleSource.UserSet);
        await session.AddMessageAsync(new CopilotChatMessage(ChatRole.User, "task"));
        var generator = new SessionTitleGenerator(context.ChatManager.AgentApiEndpointManager);

        string? title = await generator.GenerateTitlePreviewAsync(session);

        Assert.AreEqual(("New title", "Original", TitleSource.UserSet), (title, session.Title, session.TitleSourceValue));
    }

    [TestMethod]
    public async Task PreviewShouldUseCurrentPurposeMappingOnEveryCall()
    {
        var calls = new List<string>();
        FakeChatClient Client(string name) => new()
        {
            OnGetResponseAsync = (_, _, _) =>
            {
                calls.Add(name);
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "unused")));
            }
        };
        var context = CopilotChatManagerTestContext.Create(Client("primary"), Client("flash"));
        var manager = context.ChatManager.AgentApiEndpointManager;
        var generator = new SessionTitleGenerator(manager);
        var session = new CopilotChatSession();
        await session.AddMessageAsync(new CopilotChatMessage(ChatRole.User, "task"));

        await generator.GenerateTitlePreviewAsync(session);
        manager.SetModelForPurpose(AgentLib.Core.ModelPurposes.SessionTitle, "flash-model");
        await generator.GenerateTitlePreviewAsync(session);
        manager.SetModelForPurpose(AgentLib.Core.ModelPurposes.SessionTitle, "primary-model");
        await generator.GenerateTitlePreviewAsync(session);

        CollectionAssert.AreEqual(new[] { "primary", "flash", "primary" }, calls);
    }

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
