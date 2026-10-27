using AgentLib.Logging;
using AgentLib.Model;
using Microsoft.Extensions.AI;
using System.Xml.Linq;

namespace AgentLib.Tests;

[TestClass]
public class ChatNoticeTests
{
    [TestMethod]
    public async Task NoticesDoNotAffectMessageProjectionOrTitle()
    {
        var session = new CopilotChatSession();
        await session.AddEntryAsync(new CopilotChatNotice("notice"));
        await session.AddMessageAsync(new CopilotChatMessage(ChatRole.User, "title"));

        Assert.AreEqual(("title", 1), (session.Title, session.ChatMessages.Count));
    }

    [TestMethod]
    public void RoundTripPreservesTimelineOrderAndMessageCount()
    {
        CopilotChatEntry[] entries = [new CopilotChatMessage(ChatRole.User, "question"),
            new CopilotChatNotice("warning", CopilotChatNoticeLevel.Warning),
            new CopilotChatMessage(ChatRole.System, "instruction")];
        var restored = RoundTrip(entries, 4);

        CollectionAssert.AreEqual(entries.Select(e => e.GetType()).ToArray(),
            restored.Entries.Select(e => e.GetType()).ToArray());
    }

    [DataTestMethod]
    [DataRow(CopilotChatNoticeLevel.Information)]
    [DataRow(CopilotChatNoticeLevel.Warning)]
    [DataRow(CopilotChatNoticeLevel.Error)]
    public void RoundTripPreservesNoticeTimeContentAndLevel(CopilotChatNoticeLevel level)
    {
        var time = new DateTimeOffset(2025, 6, 1, 12, 30, 45, TimeSpan.FromHours(8));
        var notice = new CopilotChatNotice("<notice>\n& detail", level, time);
        var restored = (CopilotChatNotice) RoundTrip([notice], 4).Entries.Single();

        Assert.AreEqual((notice.Content, time, level), (restored.Content, restored.CreatedTime, restored.Level));
    }

    [DataTestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    public void LegacyPresetSystemTextIsNotice(int version)
    {
        var message = new CopilotChatMessage(ChatRole.System, "legacy") { IsPresetInfo = true };

        Assert.IsInstanceOfType<CopilotChatNotice>(RoundTrip([message], version).Entries.Single());
    }

    [DataTestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    public void PresetAssistantRemainsMessage(int version)
    {
        var message = CopilotChatMessage.CreateAssistant("assistant", isPresetInfo: true);

        Assert.IsInstanceOfType<CopilotChatMessage>(RoundTrip([message], version).Entries.Single());
    }

    [DataTestMethod]
    [DataRow(1)]
    [DataRow(3)]
    [DataRow(4)]
    public void RealSystemRemainsProtocolMessage(int version)
    {
        var message = new CopilotChatMessage(ChatRole.System, "instruction");

        Assert.AreEqual(ChatRole.System, RoundTrip([message], version).Entries.OfType<CopilotChatMessage>().Single().Role);
    }

    [TestMethod]
    public void VersionFourDoesNotReinterpretPresetSystem()
    {
        var message = new CopilotChatMessage(ChatRole.System, "instruction") { IsPresetInfo = true };

        Assert.IsInstanceOfType<CopilotChatMessage>(RoundTrip([message], 4).Entries.Single());
    }

    [TestMethod]
    public async Task NoticeDispatchDoesNotRequireDispatcherToReportAccess()
    {
        var dispatcher = new InlineDispatcher();
        var session = new CopilotChatSession { MainThreadDispatcher = dispatcher };
        await session.AddEntryAsync(new CopilotChatNotice("notice"));

        Assert.AreEqual(1, dispatcher.InvocationCount);
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task NoticesAreNotSentToModelContext()
    {
        var observed = new List<ChatMessage>();
        var client = new AgentLib.Core.AgentApiManagers.LanguageModelProviders.Fakes.FakeChatClient
        {
            OnGetStreamingResponseAsync = (messages, _, _) => CaptureAsync(messages),
        };
        var manager = AgentLib.Tests.Fakes.CopilotChatManagerTestContext.Create(client).ChatManager;
        await manager.SelectedSession.AddEntryAsync(new CopilotChatNotice("private notice"));
        await manager.SendMessageAsync(contents: [new TextContent("question")], withHistory: true);

        Assert.IsFalse(observed.Any(message => message.Text.Contains("private notice", StringComparison.Ordinal)));

        async IAsyncEnumerable<ChatResponseUpdate> CaptureAsync(IEnumerable<ChatMessage> messages)
        {
            observed.AddRange(messages);
            await Task.Yield();
            yield return new ChatResponseUpdate(ChatRole.Assistant, "answer");
        }
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task StreamingFailurePreservesPartialOutputAndNotifiesOriginalSession()
    {
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new AgentLib.Core.AgentApiManagers.LanguageModelProviders.Fakes.FakeChatClient
        {
            OnGetStreamingResponseAsync = (_, _, _) => FailAsync(),
        };
        var manager = AgentLib.Tests.Fakes.CopilotChatManagerTestContext.Create(client).ChatManager;
        CopilotChatSession original = manager.SelectedSession;
        Task sending = manager.SendMessageAsync(contents: [new TextContent("question")]);
        await started.Task;
        manager.CreateNewSession();
        release.SetResult(true);
        await sending;

        Assert.AreEqual(("partial", 1, 0),
            (original.ChatMessages.Last().Content, original.Entries.OfType<CopilotChatNotice>().Count(),
                manager.SelectedSession.Entries.OfType<CopilotChatNotice>().Count()));

        async IAsyncEnumerable<ChatResponseUpdate> FailAsync()
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, "partial");
            started.SetResult(true);
            await release.Task;
            throw new InvalidOperationException("stream failed");
        }
    }

    [TestMethod]
    [DataRow("Level", null)]
    [DataRow("Level", "Unexpected")]
    [DataRow("Level", "99")]
    [DataRow("CreatedTime", null)]
    [DataRow("CreatedTime", "invalid")]
    public void InvalidNoticeAttributesAreReportedAsInvalidData(string attribute, string? value)
    {
        XElement root = CopilotChatHistoryXmlCodec.CreateRootElement(Guid.NewGuid(), DateTimeOffset.Now,
            "title", 4, [new CopilotChatNotice("notice")], null);
        root.Element("Messages")!.Element("Notice")!.SetAttributeValue(attribute, value);

        Assert.ThrowsExactly<InvalidDataException>(() =>
            CopilotChatHistoryXmlCodec.ReadPersistenceData(new XDocument(root)));
    }

    private static CopilotChatSessionPersistenceData RoundTrip(CopilotChatEntry[] entries, int version)
        => CopilotChatHistoryXmlCodec.ReadPersistenceData(new XDocument(
            CopilotChatHistoryXmlCodec.CreateRootElement(Guid.NewGuid(), DateTimeOffset.Now,
                "title", version, entries, null)));

    private sealed class InlineDispatcher : IMainThreadDispatcher
    {
        public int InvocationCount { get; private set; }
        public bool CheckAccess() => false;
        public Task InvokeAsync(Func<Task> action)
        {
            if (++InvocationCount > 1) throw new InvalidOperationException("Recursive dispatch");
            return action();
        }
        public Task<T> InvokeAsync<T>(Func<Task<T>> action) => action();
    }
}
