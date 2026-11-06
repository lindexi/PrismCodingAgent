using PrismCodingAgent.CodingShell.Infrastructure;

namespace PrismCodingAgent.AvaloniaShell.Tests;

[TestClass]
public sealed class CodingChatRoomPathsTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void DefaultPathsShouldUsePrismCodingAgentDirectory()
    {
        string root = CreateRoot();

        CodingChatRoomPaths paths = CodingChatRoomPaths.CreateForLocalApplicationData(root);

        Assert.AreEqual(Path.Join(root, "PrismCodingAgent"), paths.RootDirectory);
    }

    [TestMethod]
    public void LegacyOnlyShouldCopyNestedDataAndKeepSource()
    {
        string root = CreateRoot();
        string legacy = Path.Join(root, "CodingChatRoom");
        Directory.CreateDirectory(Path.Join(legacy, "Sessions", "20260101"));
        File.WriteAllText(Path.Join(legacy, "AgentConfiguration.json"), "configuration");
        File.WriteAllText(Path.Join(legacy, "Sessions", "20260101", "session.xml"), "history");
        Directory.CreateDirectory(Path.Join(legacy, "EmptyFolder"));
        CodingChatRoomPaths paths = CodingChatRoomPaths.CreateForLocalApplicationData(root);

        paths.EnsureDirectories();

        Assert.AreEqual("configuration", File.ReadAllText(paths.ConfigurationFile.FullName));
        Assert.AreEqual("history", File.ReadAllText(Path.Join(paths.SessionDirectory.FullName, "20260101", "session.xml")));
        Assert.IsTrue(File.Exists(Path.Join(legacy, "AgentConfiguration.json")));
        Assert.IsTrue(Directory.Exists(Path.Join(paths.RootDirectory, "EmptyFolder")));
    }

    [TestMethod]
    public void ExistingNewDirectoryShouldNotMergeLegacyData()
    {
        string root = CreateRoot();
        string legacy = Path.Join(root, "CodingChatRoom");
        Directory.CreateDirectory(legacy);
        File.WriteAllText(Path.Join(legacy, "AgentConfiguration.json"), "legacy");
        Directory.CreateDirectory(Path.Join(root, "PrismCodingAgent"));
        CodingChatRoomPaths paths = CodingChatRoomPaths.CreateForLocalApplicationData(root);

        paths.EnsureDirectories();

        Assert.IsFalse(File.Exists(paths.ConfigurationFile.FullName));
    }

    [TestMethod]
    public void RepeatedInitializationShouldNotOverwriteNewData()
    {
        string root = CreateRoot();
        string legacy = Path.Join(root, "CodingChatRoom");
        Directory.CreateDirectory(legacy);
        File.WriteAllText(Path.Join(legacy, "AgentConfiguration.json"), "legacy");
        CodingChatRoomPaths paths = CodingChatRoomPaths.CreateForLocalApplicationData(root);
        paths.EnsureDirectories();
        File.WriteAllText(paths.ConfigurationFile.FullName, "updated");

        paths.EnsureDirectories();

        Assert.AreEqual("updated", File.ReadAllText(paths.ConfigurationFile.FullName));
    }

    [TestMethod]
    public void FreshInstallationShouldCreateNewDirectories()
    {
        CodingChatRoomPaths paths = CodingChatRoomPaths.CreateForLocalApplicationData(CreateRoot());

        paths.EnsureDirectories();

        Assert.IsTrue(paths.SessionDirectory.Exists);
        Assert.IsTrue(Directory.Exists(paths.LogDirectory));
        Assert.IsFalse(paths.ConfigurationFile.Exists);
    }

    [TestMethod]
    public void ExplicitDirectoryShouldNotMigrateSiblingLegacyDirectory()
    {
        string root = CreateRoot();
        string legacy = Path.Join(root, "CodingChatRoom");
        Directory.CreateDirectory(legacy);
        File.WriteAllText(Path.Join(legacy, "AgentConfiguration.json"), "legacy");
        CodingChatRoomPaths paths = CodingChatRoomPaths.Create(Path.Join(root, "custom"));

        paths.EnsureDirectories();

        Assert.IsFalse(File.Exists(paths.ConfigurationFile.FullName));
    }

    [TestMethod]
    public void MissingLogDirectoryShouldReturnNoFile()
    {
        var paths = CodingChatRoomPaths.Create(CreateRoot());

        Assert.IsNull(paths.FindSessionLogFile(Guid.NewGuid()));
    }

    [TestMethod]
    public void LogsForOtherSessionsShouldNotMatch()
    {
        var paths = CodingChatRoomPaths.Create(CreateRoot());
        Directory.CreateDirectory(paths.LogDirectory);
        File.WriteAllText(Path.Join(paths.LogDirectory, $"preview_{Guid.NewGuid():N}.log"), "other");

        Assert.IsNull(paths.FindSessionLogFile(Guid.NewGuid()));
    }

    [TestMethod]
    public void SessionLogLookupShouldReturnLatestModifiedFileAcrossDays()
    {
        var paths = CodingChatRoomPaths.Create(CreateRoot());
        Guid id = Guid.NewGuid();
        string olderDirectory = Directory.CreateDirectory(Path.Join(paths.LogDirectory, "20260101")).FullName;
        string newerDirectory = Directory.CreateDirectory(Path.Join(paths.LogDirectory, "20260102")).FullName;
        string older = Path.Join(olderDirectory, $"preview_{id:N}.log");
        string newer = Path.Join(newerDirectory, $"preview_{id:N}.log");
        File.WriteAllText(older, "older");
        File.WriteAllText(newer, "newer");
        File.SetLastWriteTimeUtc(older, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(newer, new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));

        Assert.AreEqual(newer, paths.FindSessionLogFile(id));
    }

    [TestMethod]
    public void ArchiveDirectoryShouldBelongToSpecifiedSession()
    {
        var paths = CodingChatRoomPaths.Create(CreateRoot());
        var session = new AgentLib.Model.CopilotChatSession();

        Assert.AreEqual(Path.Join(paths.SessionDirectory.FullName,
            session.StartedTime.ToString("yyyyMMdd"), session.SessionId.ToString("N")),
            paths.GetSessionArchiveDirectory(session));
    }

    private string CreateRoot()
    {
        string root = Path.Join(Path.GetTempPath(), "PrismCodingAgent.PathTests", Guid.NewGuid().ToString("N"));
        TestContext.WriteLine(root);
        return root;
    }
}
