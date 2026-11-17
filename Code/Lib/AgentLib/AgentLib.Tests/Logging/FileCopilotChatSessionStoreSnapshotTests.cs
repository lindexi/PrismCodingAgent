using AgentLib.Logging;
using AgentLib.Logging.Sessions;
using AgentLib.Model;
using Microsoft.Extensions.AI;
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
    public async Task SnapshotShouldPreserveCompleteSessionState()
    {
        var store = new FileCopilotChatSessionStore(CreatePath());
        var session = new CopilotChatSession
        {
            WorkspacePath = "/projects/sample",
            WorkTaskId = Guid.NewGuid(),
            WorkTaskName = "Review",
        };
        session.SetTitle("Snapshot title", TitleSource.UserSet);
        var message = new CopilotChatMessage(ChatRole.Assistant, "answer");
        message.AppendReasoning("reasoning");
        message.AppendUsageDetails(new UsageDetails { TotalTokenCount = 42 });
        var details = new CopilotChatMessage(ChatRole.Assistant, "tool details");
        message.CreateToolItem(new FunctionCallContent("call-1", "ReadFile")).Details = details;
        await session.AddMessageAsync(message);
        await session.AddMessageAsync(new CopilotChatMessage(ChatRole.User,
            [new DataContent(BinaryData.FromBytes([1, 2, 3]), "image/png")]));
        session.AdditionalInfo.Add("test", new JsonSessionAdditionalInfo(JsonSerializer.SerializeToElement(new { value = 7 })));
        session.ResponsesSession.AppendItem(ResponseItem.CreateUserMessageItem("native input"));
        JsonElement state = JsonSerializer.SerializeToElement(new { turn = 2, image = "data:image/png;base64,AQID" });

        await store.SaveSnapshotAsync(session, message.SessionSnapshotId, state);
        CopilotChatSessionPersistenceData loaded = await store.LoadSnapshotAsync(session, message.SessionSnapshotId);

        Assert.AreEqual((FileCopilotChatSessionStore.CurrentFormatVersion, session.SessionId, session.StartedTime,
            session.Title, session.WorkspacePath, session.WorkTaskId, session.WorkTaskName),
            (loaded.FormatVersion, loaded.SessionId, loaded.StartedTime, loaded.Title, loaded.WorkspacePath,
                loaded.WorkTaskId, loaded.WorkTaskName));
        Assert.AreEqual(state.GetRawText(), loaded.AgentSessionState!.Value.GetRawText());
        Assert.AreEqual(7, loaded.AdditionalInfo["test"].GetProperty("value").GetInt32());
        Assert.AreEqual(System.ClientModel.Primitives.ModelReaderWriter.Write(session.ResponsesSession.Items.Single()).ToString(),
            System.ClientModel.Primitives.ModelReaderWriter.Write(loaded.ResponsesSession!.Items.Single()).ToString());
        CopilotChatMessage restored = loaded.Entries.OfType<CopilotChatMessage>().First();
        Assert.AreEqual((message.SessionSnapshotId, "answer", 42L),
            (restored.SessionSnapshotId, restored.Content, restored.TotalUsageDetails!.TotalTokenCount));
        Assert.HasCount(message.MessageItems.Count, restored.MessageItems);
        Assert.AreEqual(details.SessionSnapshotId, restored.MessageItems.OfType<CopilotChatToolItem>().Single().Details!.SessionSnapshotId);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, loaded.Entries.OfType<CopilotChatMessage>().Last()
            .MessageItems.OfType<CopilotChatImageItem>().Single().Data.ToArray());
    }

    [TestMethod]
    public async Task SavingSameSnapshotIdShouldReplacePreviousState()
    {
        string root = CreatePath();
        var store = new FileCopilotChatSessionStore(root);
        var session = new CopilotChatSession();
        var snapshotId = SessionSnapshotId.NewId();
        session.AdditionalInfo.Add("test", new JsonSessionAdditionalInfo(JsonSerializer.SerializeToElement(7)));
        session.ResponsesSession.AppendItem(ResponseItem.CreateUserMessageItem("old input"));
        await store.SaveSnapshotAsync(session, snapshotId, JsonSerializer.SerializeToElement(new { turn = 1 }));
        session.AdditionalInfo.Clear();
        var updated = new CopilotChatSession(session.SessionId, session.StartedTime, new EmptyCopilotChatLogger());
        await updated.AddMessageAsync(new CopilotChatMessage(ChatRole.User, "updated"));

        await store.SaveSnapshotAsync(updated, snapshotId, null);
        CopilotChatSessionPersistenceData loaded = await store.LoadSnapshotAsync(session, snapshotId);

        Assert.AreEqual("updated", loaded.Entries.OfType<CopilotChatMessage>().Single().Content);
        Assert.IsNull(loaded.AgentSessionState);
        Assert.IsNull(loaded.ResponsesSession);
        Assert.IsEmpty(loaded.AdditionalInfo);
        Assert.HasCount(1, Directory.GetFiles(root, "Session.xml", SearchOption.AllDirectories));
    }

    [TestMethod]
    public async Task OrdinarySaveShouldNotChangeSnapshot()
    {
        var store = new FileCopilotChatSessionStore(CreatePath());
        var session = new CopilotChatSession();
        var message = new CopilotChatMessage(ChatRole.User, "snapshot");
        await session.AddMessageAsync(message);
        await store.SaveSnapshotAsync(session, message.SessionSnapshotId, null);
        await session.AddMessageAsync(new CopilotChatMessage(ChatRole.Assistant, "later"));

        await store.SaveSessionAsync(session, JsonSerializer.SerializeToElement(new { turn = 2 }));
        CopilotChatSessionPersistenceData loaded = await store.LoadSnapshotAsync(session, message.SessionSnapshotId);

        Assert.AreEqual("snapshot", loaded.Entries.OfType<CopilotChatMessage>().Single().Content);
        Assert.IsNull(loaded.AgentSessionState);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task HistoryListsShouldExcludeSnapshots(bool saveOrdinaryHistory)
    {
        var store = new FileCopilotChatSessionStore(CreatePath());
        var session = new CopilotChatSession();
        await store.SaveSnapshotAsync(session, SessionSnapshotId.NewId(), null);
        if (saveOrdinaryHistory)
        {
            await store.SaveSessionAsync(session, null);
        }

        var enumerated = new List<Guid>();
        await foreach (CopilotChatSessionSummary summary in store.EnumerateSessionsAsync())
        {
            enumerated.Add(summary.SessionId);
        }
        IReadOnlyList<CopilotChatSessionSummary> listed = await store.ListSessionsAsync();

        CollectionAssert.AreEqual(saveOrdinaryHistory ? new[] { session.SessionId } : Array.Empty<Guid>(), enumerated);
        CollectionAssert.AreEqual(enumerated, listed.Select(summary => summary.SessionId).ToArray());
    }

    [TestMethod]
    public async Task DeleteSessionShouldRemoveSnapshotsWithSessionDirectory()
    {
        string root = CreatePath();
        var store = new FileCopilotChatSessionStore(root);
        var session = new CopilotChatSession();
        await store.SaveSessionAsync(session, null);
        await store.SaveSnapshotAsync(session, SessionSnapshotId.NewId(), null);

        await store.DeleteSessionAsync(session.SessionId);

        Assert.IsFalse(Directory.Exists(Path.Join(root, session.StartedTime.ToString("yyyyMMdd"), session.SessionId.ToString("N"))));
    }

    [TestMethod]
    public async Task LoadingMissingSnapshotShouldThrowDirectoryNotFound()
    {
        var store = new FileCopilotChatSessionStore(CreatePath());
        var session = new CopilotChatSession();
        await store.SaveSessionAsync(session, null);
        var snapshotId = SessionSnapshotId.NewId();

        await Assert.ThrowsExactlyAsync<DirectoryNotFoundException>(() => store.LoadSnapshotAsync(session, snapshotId));
    }

    [TestMethod]
    public async Task OrganizeShouldCompressEntireDirectoryAndSnapshotShouldRemainReadableAfterSessionLoad()
    {
        string root = CreatePath();
        var store = new FileCopilotChatSessionStore(root);
        var session = new CopilotChatSession();
        var message = new CopilotChatMessage(ChatRole.User, "snapshot");
        await session.AddMessageAsync(message);
        await store.SaveSessionAsync(session, null);
        await store.SaveSnapshotAsync(session, message.SessionSnapshotId, null);
        string entryPath = Path.Join(root, session.StartedTime.ToString("yyyyMMdd"), session.SessionId.ToString("N"), "Session.xml");
        XDocument entry = XDocument.Load(entryPath);
        entry.Root!.SetAttributeValue("UpdatedTime", DateTimeOffset.Now.AddDays(-8).ToString("o"));
        entry.Save(entryPath);

        string directory = Path.GetDirectoryName(entryPath)!;
        string[] contentFiles = Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
            .Where(path => path != entryPath)
            .Select(path => Path.GetRelativePath(directory, path).Replace('\\', '/')).ToArray();

        Assert.AreEqual((0, 1), await store.OrganizeAsync());
        CollectionAssert.AreEquivalent(new[] { "Session.xml", "Content.zip" },
            Directory.GetFiles(directory).Select(Path.GetFileName).ToArray());
        Assert.IsEmpty(Directory.GetDirectories(directory));
        using (ZipArchive archive = ZipFile.OpenRead(Path.Join(directory, "Content.zip")))
        {
            CollectionAssert.AreEquivalent(contentFiles, archive.Entries.Select(item => item.FullName).ToArray());
        }

        await store.LoadSessionAsync(session.SessionId);
        Assert.IsFalse(File.Exists(Path.Join(directory, "Content.zip")));
        CopilotChatSessionPersistenceData beforeSave = await store.LoadSnapshotAsync(session, message.SessionSnapshotId);
        await session.AddMessageAsync(new CopilotChatMessage(ChatRole.Assistant, "later"));
        await store.SaveSessionAsync(session, null);
        CopilotChatSessionPersistenceData afterSave = await store.LoadSnapshotAsync(session, message.SessionSnapshotId);

        Assert.AreEqual("snapshot", beforeSave.Entries.OfType<CopilotChatMessage>().Single().Content);
        Assert.AreEqual("snapshot", afterSave.Entries.OfType<CopilotChatMessage>().Single().Content);
    }

    private static string CreatePath()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Console.WriteLine(path);
        return path;
    }
}
