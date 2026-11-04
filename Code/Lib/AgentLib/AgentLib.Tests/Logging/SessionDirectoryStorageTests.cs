using AgentLib.Logging.Sessions;
using AgentLib.Logging;
using AgentLib.Model;
using Microsoft.Extensions.AI;
using System.Text.Json;
using System.Xml.Linq;

namespace AgentLib.Tests.Logging;

[TestClass]
public sealed class SessionDirectoryStorageTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task SaveShouldSeparateEntryHistoryAndDeduplicatedResources()
    {
        string root = CreatePath();
        var store = new FileCopilotChatSessionStore(root);
        var session = new CopilotChatSession();
        byte[] image = [1, 2, 3];
        await session.AddMessageAsync(new CopilotChatMessage(ChatRole.User,
            [new TextContent("preview"), new DataContent(image, "image/png"), new DataContent(image, "image/png")]));

        await store.SaveSessionAsync(session, null);

        string entry = Directory.GetFiles(root, "Session.xml", SearchOption.AllDirectories).Single();
        string directory = Path.GetDirectoryName(entry)!;
        Assert.IsNull(XDocument.Load(entry).Root!.Element("Messages"));
        Assert.HasCount(1, Directory.GetFiles(Path.Join(directory, "Resources")));
        Assert.IsFalse(File.ReadAllText(Path.Join(directory, "History.xml")).Contains("AQID"));
        var loaded = await store.LoadSessionAsync(session.SessionId);
        CollectionAssert.AreEqual(image, loaded.Entries.OfType<CopilotChatMessage>().Single().MessageItems.OfType<CopilotChatImageItem>().First().Data.ToArray());
    }

    [TestMethod]
    public async Task OrganizeShouldUpgradeLegacyCompressAndExpandOnLoad()
    {
        string root = CreatePath();
        Directory.CreateDirectory(root);
        Guid id = Guid.NewGuid();
        DateTimeOffset started = DateTimeOffset.Now.AddDays(-10);
        string legacy = Path.Join(root, $"20260101_120000_{id:N}.xml");
        var session = new CopilotChatSession(id, started, new EmptyCopilotChatLogger());
        await session.AddMessageAsync(new CopilotChatMessage(ChatRole.User, "legacy text"));
        var document = new XDocument(LegacySessionXmlFixture.CreateRootElement(id, started, "Legacy", 4, session.Entries, null));
        await FileCopilotChatSessionStore.SaveDocumentAsync(legacy, document);
        File.SetLastWriteTimeUtc(legacy, started.UtcDateTime);
        var store = new FileCopilotChatSessionStore(root);

        var result = await store.OrganizeAsync();

        Assert.AreEqual((1, 1), result);
        Assert.IsFalse(File.Exists(legacy));
        string entry = Directory.GetFiles(root, "Session.xml", SearchOption.AllDirectories).Single();
        string directory = Path.GetDirectoryName(entry)!;
        Assert.AreEqual("Zip", (string?)XDocument.Load(entry).Root!.Attribute("ContentStorage"));
        Assert.IsTrue(File.Exists(Path.Join(directory, "Content.zip")));
        Assert.IsFalse(File.Exists(Path.Join(directory, "History.xml")));
        Assert.HasCount(1, await store.ListSessionsAsync());
        var loaded = await store.LoadSessionAsync(id);
        Assert.AreEqual("legacy text", loaded.Entries.OfType<CopilotChatMessage>().Single().Content);
        Assert.AreEqual("Expanded", (string?)XDocument.Load(entry).Root!.Attribute("ContentStorage"));
        Assert.IsFalse(File.Exists(Path.Join(directory, "Content.zip")));
    }

    [TestMethod]
    public async Task OrganizeShouldLeaveRecentSessionExpandedAndBeRepeatable()
    {
        var store = new FileCopilotChatSessionStore(CreatePath());
        await store.SaveSessionAsync(new CopilotChatSession(), null);

        Assert.AreEqual((0, 0), await store.OrganizeAsync());
        Assert.AreEqual((0, 0), await store.OrganizeAsync());
    }

    [TestMethod]
    public async Task ModelStateDataUrlsShouldRoundTripThroughResources()
    {
        string root = CreatePath();
        var store = new FileCopilotChatSessionStore(root);
        var session = new CopilotChatSession();
        using JsonDocument json = JsonDocument.Parse("""{"messages":[{"type":"input_image","image_url":"data:image/png;base64,AQID"}]}""");

        await store.SaveSessionAsync(session, json.RootElement);

        string statePath = Directory.GetFiles(root, "AgentSession.json", SearchOption.AllDirectories).Single();
        Assert.IsFalse(File.ReadAllText(statePath).Contains("AQID"));
        var loaded = await store.LoadSessionAsync(session.SessionId);
        Assert.AreEqual(json.RootElement.ToString(), loaded.AgentSessionState!.Value.ToString());
    }

    [TestMethod]
    public async Task DeleteShouldRemoveEntireCompressedSessionDirectory()
    {
        string root = CreatePath();
        var store = new FileCopilotChatSessionStore(root);
        var session = new CopilotChatSession();
        await store.SaveSessionAsync(session, null);
        string entry = Directory.GetFiles(root, "Session.xml", SearchOption.AllDirectories).Single();
        XDocument document = XDocument.Load(entry);
        document.Root!.SetAttributeValue("UpdatedTime", DateTimeOffset.Now.AddDays(-8).ToString("o"));
        document.Save(entry);
        await store.OrganizeAsync();

        await store.DeleteSessionAsync(session.SessionId);

        Assert.IsFalse(Directory.Exists(Path.GetDirectoryName(entry)));
    }

    [TestMethod]
    public async Task TypedBinaryModelStateShouldBeExternalizedWithoutChangingText()
    {
        string root = CreatePath();
        var store = new FileCopilotChatSessionStore(root);
        var session = new CopilotChatSession();
        using JsonDocument state = JsonDocument.Parse("""{"contents":[{"$type":"data","data":"AQID","mediaType":"image/png"},{"$type":"text","text":"data:image/png;base64,AQID"}]}""");

        await store.SaveSessionAsync(session, state.RootElement);

        var loaded = await store.LoadSessionAsync(session.SessionId);
        Assert.AreEqual(state.RootElement.ToString(), loaded.AgentSessionState!.Value.ToString());
        Assert.HasCount(1, Directory.GetFiles(root, "*.png", SearchOption.AllDirectories));
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    public async Task LoadLegacyShouldMigrateBeforeLoadingCurrentFormat(int version)
    {
        string root = CreatePath();
        Directory.CreateDirectory(root);
        var session = new CopilotChatSession();
        await session.AddMessageAsync(new CopilotChatMessage(ChatRole.User, "Preserved message"));
        string legacy = Path.Join(root, $"20260101_120000_{session.SessionId:N}.xml");
        var document = new XDocument(LegacySessionXmlFixture.CreateRootElement(session.SessionId,
            session.StartedTime, "Legacy", version, session.Entries, null));
        await FileCopilotChatSessionStore.SaveDocumentAsync(legacy, document);
        var store = new FileCopilotChatSessionStore(root);

        CopilotChatSessionPersistenceData loaded = await store.LoadSessionAsync(session.SessionId);

        Assert.AreEqual(5, loaded.FormatVersion);
        Assert.AreEqual("Preserved message", loaded.Entries.OfType<CopilotChatMessage>().Single().Content);
        Assert.IsFalse(File.Exists(legacy));
        Assert.HasCount(1, Directory.GetFiles(root, "Session.xml", SearchOption.AllDirectories));
        Assert.AreEqual((0, 0), await store.OrganizeAsync());
    }

    [TestMethod]
    public async Task LoadShouldRejectFutureVersionBeforeReadingContent()
    {
        string root = CreatePath();
        var session = new CopilotChatSession();
        var store = new FileCopilotChatSessionStore(root);
        await store.SaveSessionAsync(session, null);
        string entry = Directory.GetFiles(root, "Session.xml", SearchOption.AllDirectories).Single();
        XDocument document = XDocument.Load(entry);
        document.Root!.SetAttributeValue("FormatVersion", 6);
        document.Save(entry);
        File.Delete(Path.Join(Path.GetDirectoryName(entry)!, "History.xml"));

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => store.LoadSessionAsync(session.SessionId));
    }

    [TestMethod]
    public async Task NestedMessagesShouldStoreDirectResourceReferencesAndRestoreBinaryContents()
    {
        string root = CreatePath();
        var store = new FileCopilotChatSessionStore(root);
        var session = new CopilotChatSession();
        var message = CopilotChatMessage.CreateAssistant(string.Empty, false);
        var detail = CopilotChatMessage.CreateAssistant(string.Empty, false);
        detail.MessageItems.Add(new CopilotChatImageItem(BinaryData.FromBytes(new byte[] { 1, 2, 3 }), "image/png"));
        message.CreateToolItem(new FunctionCallContent("tool", "read", new Dictionary<string, object?>())).Details = detail;
        var child = message.CreateSubAgentItem("child", "task");
        child.MessageItems.Add(new CopilotChatAudioItem(BinaryData.FromBytes(new byte[] { 4, 5, 6 }), "audio/wav"));
        await session.AddMessageAsync(message);

        await store.SaveSessionAsync(session, null);

        string historyPath = Directory.GetFiles(root, "History.xml", SearchOption.AllDirectories).Single();
        XDocument history = XDocument.Load(historyPath);
        Assert.HasCount(2, history.Descendants().Attributes("Resource").ToArray());
        Assert.IsTrue(history.Descendants().Where(element => element.Name == "ImageItem" || element.Name == "AudioItem")
            .All(element => element.Value.Length == 0));
        var loaded = await store.LoadSessionAsync(session.SessionId);
        var restored = loaded.Entries.OfType<CopilotChatMessage>().Single();
        var image = restored.MessageItems.OfType<CopilotChatToolItem>().Single().Details!.MessageItems.OfType<CopilotChatImageItem>().Single();
        var audio = restored.MessageItems.OfType<CopilotChatSubAgentItem>().Single().MessageItems.OfType<CopilotChatAudioItem>().Single();
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, image.Data.ToArray());
        CollectionAssert.AreEqual(new byte[] { 4, 5, 6 }, audio.Data.ToArray());
    }

    private string CreatePath()
    {
        string path = Path.Join(Path.GetTempPath(), "PrismCodingAgent.StorageTests", Guid.NewGuid().ToString("N"));
        TestContext.WriteLine(path);
        return path;
    }
}
