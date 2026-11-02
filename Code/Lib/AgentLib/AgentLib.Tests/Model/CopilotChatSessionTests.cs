using AgentLib.Model;

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AgentLib.Tests.Model;

[TestClass]
public class CopilotChatSessionTests
{
    [TestMethod]
    [Description("添加首条用户消息后应根据内容自动生成会话标题")]
    public async Task AddMessage_WhenFirstUserMessageAdded_UpdatesTitleFromContent()
    {
        var session = new CopilotChatSession(Guid.NewGuid(), new DateTimeOffset(2025, 1, 2, 3, 4, 0, TimeSpan.Zero));
        var message = new CopilotChatMessage(ChatRole.User, "   这是   一个  标题   ");

        await session.AddMessageAsync(message);

        Assert.AreEqual("这是 一个 标题", session.Title);
        StringAssert.Contains(session.DisplayText, "01-02 03:04");
    }

    [TestMethod]
    [Description("长标题应保留完整内容，由界面按宽度显示")]
    public async Task AddMessage_WhenUserMessageIsTooLong_PreservesTitle()
    {
        var session = new CopilotChatSession(Guid.NewGuid(), DateTimeOffset.Now);
        var message = new CopilotChatMessage(ChatRole.User, "1234567890123456789012345");

        await session.AddMessageAsync(message);

        Assert.AreEqual("1234567890123456789012345", session.Title);
    }

    [TestMethod]
    [Description("添加系统消息或预设用户消息时不应覆盖默认标题")]
    public async Task AddMessage_WhenMessageIsNotEligibleForTitle_KeepsDefaultTitle()
    {
        var session = new CopilotChatSession(Guid.NewGuid(), DateTimeOffset.Now);
        var systemMessage = new CopilotChatMessage(ChatRole.System, "系统提示");
        var presetUserMessage = new CopilotChatMessage(ChatRole.User, "用户预设")
        {
            IsPresetInfo = true
        };

        await session.AddMessageAsync(systemMessage);
        await session.AddMessageAsync(presetUserMessage);

        Assert.AreEqual("新会话", session.Title);
    }

    [TestMethod]
    [Description("标题确定后再次添加新的用户消息不应重复覆盖现有标题")]
    public async Task AddMessage_WhenTitleAlreadyGenerated_DoesNotOverwriteTitle()
    {
        var session = new CopilotChatSession(Guid.NewGuid(), DateTimeOffset.Now);

        await session.AddMessageAsync(new CopilotChatMessage(ChatRole.User, "第一个标题"));
        await session.AddMessageAsync(new CopilotChatMessage(ChatRole.User, "第二个标题"));

        Assert.AreEqual("第一个标题", session.Title);
    }

    [TestMethod]
    [Description("后台线程添加消息时应通过主线程调度器修改消息集合")]
    public async Task AddMessage_WhenDispatcherRequiresDispatch_InvokesDispatcher()
    {
        var dispatcher = new RecordingMainThreadDispatcher();
        var session = new CopilotChatSession(Guid.NewGuid(), DateTimeOffset.Now)
        {
            MainThreadDispatcher = dispatcher,
        };

        await session.AddMessageAsync(new CopilotChatMessage(ChatRole.User, "测试消息"));

        Assert.AreEqual(1, dispatcher.InvocationCount);
    }

    [TestMethod]
    public void CreateSubSession_WhenCreated_HasIndependentIdentity()
    {
        var parent = new CopilotChatSession();

        CopilotChatSession child = parent.CreateSubSession();

        Assert.AreNotEqual(parent.SessionId, child.SessionId);
    }

    [TestMethod]
    public void CreateSubSession_WhenCreated_ReferencesImmediateParent()
    {
        var parent = new CopilotChatSession().CreateSubSession();

        CopilotChatSession child = parent.CreateSubSession();

        Assert.AreSame(parent, child.ParentSession);
    }

    [TestMethod]
    public void CreateSubSession_WhenCreated_InheritsExecutionEnvironment()
    {
        var parent = new CopilotChatSession
        {
            WorkTaskId = Guid.NewGuid(),
            WorkTaskName = "Task",
            WorkspacePath = "workspace",
            MainThreadDispatcher = new RecordingMainThreadDispatcher(),
        };

        CopilotChatSession child = parent.CreateSubSession();

        Assert.AreEqual(
            (parent.WorkTaskId, parent.WorkTaskName, parent.WorkspacePath, parent.MainThreadDispatcher),
            (child.WorkTaskId, child.WorkTaskName, child.WorkspacePath, child.MainThreadDispatcher));
    }

    [TestMethod]
    public async Task CreateSubSession_WhenParentHasMessages_DoesNotCopyMessages()
    {
        var parent = new CopilotChatSession();
        await parent.AddMessageAsync(new CopilotChatMessage(ChatRole.User, "Parent message"));

        CopilotChatSession child = parent.CreateSubSession();

        Assert.IsEmpty(child.Entries);
    }

    [TestMethod]
    public async Task CreateSubSession_WhenChildReceivesMessage_DoesNotChangeParentMessages()
    {
        var parent = new CopilotChatSession();
        CopilotChatSession child = parent.CreateSubSession();

        await child.AddMessageAsync(new CopilotChatMessage(ChatRole.User, "Child message"));

        Assert.IsEmpty(parent.Entries);
    }

    [TestMethod]
    public void CreateSubSession_WhenParentHasTitle_KeepsDefaultTitle()
    {
        var parent = new CopilotChatSession();
        parent.SetTitle("Parent title");

        CopilotChatSession child = parent.CreateSubSession();

        Assert.AreEqual(new CopilotChatSession().Title, child.Title);
    }

    [TestMethod]
    public async Task CreateSubSession_WhenParentHasAgentSession_DoesNotShareModelHistory()
    {
        var parent = new CopilotChatSession();
        var client = new AgentLib.Core.AgentApiManagers.LanguageModelProviders.Fakes.FakeChatClient();
        var agent = client.AsAIAgent();
        parent.SetAgentSession(await agent.CreateSessionAsync());

        CopilotChatSession child = parent.CreateSubSession();

        Assert.IsNull(child.AgentSession);
    }

    [TestMethod]
    public void CreateSubSession_WhenCreated_DoesNotShareResponsesSession()
    {
        var parent = new CopilotChatSession();

        CopilotChatSession child = parent.CreateSubSession();

        Assert.AreNotSame(parent.ResponsesSession, child.ResponsesSession);
    }

    private sealed class RecordingMainThreadDispatcher : IMainThreadDispatcher
    {
        public int InvocationCount { get; private set; }

        public bool CheckAccess() => false;

        public Task InvokeAsync(Func<Task> action)
        {
            InvocationCount++;
            return action();
        }

        public Task<T> InvokeAsync<T>(Func<Task<T>> action)
        {
            InvocationCount++;
            return action();
        }
    }
}
