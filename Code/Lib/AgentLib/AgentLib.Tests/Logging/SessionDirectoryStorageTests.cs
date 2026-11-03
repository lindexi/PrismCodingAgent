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
        var document = new XDocument(CopilotChatHistoryXmlCodec.CreateRootElement(id, started, "Legacy", 4, session.Entries, null));
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

    private string CreatePath()
    {
        string path = Path.Join(Path.GetTempPath(), "PrismCodingAgent.StorageTests", Guid.NewGuid().ToString("N"));
        TestContext.WriteLine(path);
        return path;
    }
}
