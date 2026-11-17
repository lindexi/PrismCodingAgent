using AgentLib.Logging;
using AgentLib.Logging.Sessions;
using AgentLib.Model;
using OpenAI.Responses;
using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;

#pragma warning disable OPENAI001

namespace AgentLib.Tests.Logging;

[TestClass]
public sealed class FileCopilotChatSessionStoreSnapshotTests
{
    [TestMethod]
    public async Task SnapshotZipShouldContainOnlyMainModelStates()
    {
        string root = CreatePath();
        var session = new CopilotChatSession(new FileSessionStorage(root));
        session.AdditionalInfo.Set("business", new JsonSessionAdditionalInfo(JsonSerializer.SerializeToElement(7)));
        session.ResponsesSession.AppendItem(ResponseItem.CreateUserMessageItem("native input"));
        SessionSnapshotId id = SessionSnapshotId.NewId();

        await SessionSnapshots.Get(session).SaveSnapshotAsync(session, id, JsonSerializer.SerializeToElement(new { turn = 2 }));

        using ZipArchive archive = ZipFile.OpenRead(Path.Join(root, "Resources", "SessionSnapshots", id.ToFileName() + ".zip"));
        CollectionAssert.AreEquivalent(new[] { "AgentSession.json", "ResponsesSession.json" },
            archive.Entries.Select(entry => entry.FullName).ToArray());
    }

    [TestMethod]
    public async Task SavingSameSnapshotIdShouldReplacePreviousState()
    {
        var session = new CopilotChatSession(new FileSessionStorage(CreatePath()));
        SessionSnapshotId id = SessionSnapshotId.NewId();
        SessionSnapshots snapshots = SessionSnapshots.Get(session);
        await snapshots.SaveSnapshotAsync(session, id, JsonSerializer.SerializeToElement(new { turn = 1 }));

        await snapshots.SaveSnapshotAsync(session, id, JsonSerializer.SerializeToElement(new { turn = 2 }));
        var state = await snapshots.ReadSnapshotAsync(session, id);

        Assert.AreEqual(2, state.AgentState!.Value.GetProperty("turn").GetInt32());
    }

    [TestMethod]
    public async Task SnapshotShouldPreserveNativeResponsesMultimodalState()
    {
        var session = new CopilotChatSession(new FileSessionStorage(CreatePath()));
        session.ResponsesSession.AppendItem(ResponseItem.CreateUserMessageItem([
            ResponseContentPart.CreateInputImagePart(new Uri("data:image/png;base64,AQID"))
        ]));
        SessionSnapshotId id = SessionSnapshotId.NewId();
        SessionSnapshots snapshots = SessionSnapshots.Get(session);

        await snapshots.SaveSnapshotAsync(session, id, null);
        var state = await snapshots.ReadSnapshotAsync(session, id);

        Assert.AreEqual(System.ClientModel.Primitives.ModelReaderWriter.Write(session.ResponsesSession.Items.Single()).ToString(),
            System.ClientModel.Primitives.ModelReaderWriter.Write(state.Responses!.Items.Single()).ToString());
    }

    [TestMethod]
    public async Task OrdinarySaveShouldPreserveSnapshotState()
    {
        var store = new FileCopilotChatSessionStore(CreatePath());
        Guid sessionId = Guid.NewGuid();
        DateTimeOffset startedTime = DateTimeOffset.Now;
        var session = new CopilotChatSession(store.CreateStorage(sessionId, startedTime), sessionId, startedTime, new EmptyCopilotChatLogger());
        SessionSnapshotId id = SessionSnapshotId.NewId();
        SessionSnapshots snapshots = SessionSnapshots.Get(session);
        await snapshots.SaveSnapshotAsync(session, id, JsonSerializer.SerializeToElement(new { turn = 1 }));

        await store.SaveSessionAsync(session, JsonSerializer.SerializeToElement(new { turn = 2 }));
        var state = await snapshots.ReadSnapshotAsync(session, id);

        Assert.AreEqual(1, state.AgentState!.Value.GetProperty("turn").GetInt32());
    }

    [TestMethod]
    public async Task UnconsumedSnapshotIndexShouldSurviveArchiveLoadAndSave()
    {
        string root = CreatePath();
        var store = new FileCopilotChatSessionStore(root);
        Guid sessionId = Guid.NewGuid();
        DateTimeOffset startedTime = DateTimeOffset.Now;
        var session = new CopilotChatSession(store.CreateStorage(sessionId, startedTime), sessionId, startedTime, new EmptyCopilotChatLogger());
        SessionSnapshotId id = SessionSnapshotId.NewId();
        await SessionSnapshots.Get(session).SaveSnapshotAsync(session, id, JsonSerializer.SerializeToElement(new { turn = 3 }));
        await store.SaveSessionAsync(session, null);
        string entryPath = Directory.GetFiles(root, "Session.xml", SearchOption.AllDirectories).Single();
        XDocument entry = XDocument.Load(entryPath);
        entry.Root!.SetAttributeValue("UpdatedTime", DateTimeOffset.Now.AddDays(-8).ToString("o"));
        entry.Save(entryPath);
        await store.OrganizeAsync();
        var data = await store.LoadSessionAsync(sessionId);
        var restored = new CopilotChatSession(store.CreateStorage(sessionId, startedTime), sessionId, startedTime, new EmptyCopilotChatLogger());
        foreach (var item in data.AdditionalInfo)
            restored.AdditionalInfo.Set(item.Key, new JsonSessionAdditionalInfo(item.Value));

        await store.SaveSessionAsync(restored, null);
        var state = await SessionSnapshots.Get(restored).ReadSnapshotAsync(restored, id);

        Assert.AreEqual(3, state.AgentState!.Value.GetProperty("turn").GetInt32());
    }

    [TestMethod]
    public async Task FileStorageShouldReadWrittenRelativeResource()
    {
        var storage = new FileSessionStorage(CreatePath());
        using (Stream stream = storage.OpenWrite("nested/state.json"))
            await JsonSerializer.SerializeAsync(stream, new { value = 42 });

        using Stream input = storage.OpenRead("nested/state.json");
        using JsonDocument document = await JsonDocument.ParseAsync(input);

        Assert.AreEqual(42, document.RootElement.GetProperty("value").GetInt32());
    }

    private static string CreatePath()
    {
        string path = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Console.WriteLine(path);
        return path;
    }
}
