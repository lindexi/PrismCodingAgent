using AgentLib.Core.AgentApiManagers.LanguageModelProviders.Fakes;
using AgentLib.Model;
using AgentLib.Tests.Fakes;
using Microsoft.Extensions.AI;

namespace AgentLib.Tests;

[TestClass]
public class SessionTitleGeneratorTests
{
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
        var session = new CopilotChatSession(context.ChatManager.SelectedSession.AdditionalInfo.Storage);
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
        var session = new CopilotChatSession(context.ChatManager.SelectedSession.AdditionalInfo.Storage);
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
        var session = new CopilotChatSession(context.ChatManager.SelectedSession.AdditionalInfo.Storage);
        session.SetTitle("Original", TitleSource.UserSet);
        await session.AddMessageAsync(new CopilotChatMessage(ChatRole.User, "task"));
        var generator = new SessionTitleGenerator(context.ChatManager.AgentApiEndpointManager);

        string? title = await generator.GenerateTitlePreviewAsync(session);

        Assert.AreEqual(("New title", "Original", TitleSource.UserSet), (title, session.Title, session.TitleSourceValue));
    }

    [TestMethod]
    public async Task PreviewShouldUseCurrentPurposeMappingOnEveryCall()
    {
        var calls = new List<(string Model, ReasoningEffort? Effort)>();
        FakeChatClient Client(string name) => new()
        {
            OnGetResponseAsync = (_, options, _) =>
            {
                calls.Add((name, options?.Reasoning?.Effort));
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "unused")));
            }
        };
        var context = CopilotChatManagerTestContext.Create(Client("primary"), Client("flash"));
        var manager = context.ChatManager.AgentApiEndpointManager;
        var generator = new SessionTitleGenerator(manager);
        var session = new CopilotChatSession(context.ChatManager.SelectedSession.AdditionalInfo.Storage);
        await session.AddMessageAsync(new CopilotChatMessage(ChatRole.User, "task"));

        await generator.GenerateTitlePreviewAsync(session);
        manager.SetModelForPurpose(AgentLib.Core.ModelPurposes.SessionTitle, "flash-model", ReasoningEffort.Low);
        await generator.GenerateTitlePreviewAsync(session);
        manager.SetModelForPurpose(AgentLib.Core.ModelPurposes.SessionTitle, "primary-model");
        await generator.GenerateTitlePreviewAsync(session);

        CollectionAssert.AreEqual(new (string, ReasoningEffort?)[] { ("primary", null), ("flash", ReasoningEffort.Low), ("primary", null) }, calls);
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
        var session = new CopilotChatSession(context.ChatManager.SelectedSession.AdditionalInfo.Storage);
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
        var session = new CopilotChatSession(context.ChatManager.SelectedSession.AdditionalInfo.Storage);
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
        var session = new CopilotChatSession(context.ChatManager.SelectedSession.AdditionalInfo.Storage);

        await generator.GenerateTitleAsync(session);

        Assert.AreEqual(0, callCount);
    }
}
