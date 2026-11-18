using AgentLib.Logging;
using AgentLib.Model;
using Microsoft.Extensions.AI;

namespace AgentLib.Tests.Logging;

[TestClass]
public sealed class SessionCopilotChatLoggerTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task LogMessageAsync_WhenBoundToSession_WritesToThatSession()
    {
        string path = CreateLogPath();
        var logger = new FileCopilotChatLogger(path);
        Guid sessionId = Guid.NewGuid();
        ISessionCopilotChatLogger sessionLogger = logger.CreateSessionLogger(sessionId.ToString("N"));

        await sessionLogger.LogMessageAsync(new CopilotChatMessage(ChatRole.User, "Session message"));

        string content = await ReadSessionLogAsync(path, sessionId);
        StringAssert.Contains(content, "Session message");
    }

    [TestMethod]
    [DataRow("Main_12345")]
    [DataRow("My log (startup)")]
    public async Task NamedLogger_WritesToFileWithThatName(string logName)
    {
        string path = CreateLogPath();
        var logger = new FileCopilotChatLogger(path).CreateSessionLogger(logName);

        await logger.LogDiagnosticAsync("Startup", "Application starting");

        string file = Directory.EnumerateFiles(path, "*.log", SearchOption.AllDirectories).Single();
        StringAssert.EndsWith(Path.GetFileName(file), $"_{logName}.log");
        StringAssert.StartsWith(await File.ReadAllTextAsync(file), "[");
    }

    [TestMethod]
    public async Task LogDiagnosticAsync_WhenCalled_PreservesOriginalException()
    {
        string path = CreateLogPath();
        Guid sessionId = Guid.NewGuid();
        ISessionCopilotChatLogger logger = new FileCopilotChatLogger(path).CreateSessionLogger(sessionId.ToString("N"));
        var exception = new InvalidOperationException("Operation failed", new IOException("Read failed"));

        await logger.LogDiagnosticAsync("Streaming", "[Streaming][MoveNext] Failed.", exception);

        string content = await ReadSessionLogAsync(path, sessionId);
        StringAssert.Contains(content, exception.ToString());
        StringAssert.Contains(content, "[Streaming][MoveNext] Failed.");
    }

    [TestMethod]
    public async Task SessionAndDirectLogging_WhenUsedConcurrently_AppendToSameFile()
    {
        string path = CreateLogPath();
        var logger = new FileCopilotChatLogger(path);
        Guid sessionId = Guid.NewGuid();
        ISessionCopilotChatLogger sessionLogger = logger.CreateSessionLogger(sessionId.ToString("N"));

        await Task.WhenAll(
            logger.LogMessageAsync(sessionId, new CopilotChatMessage(ChatRole.User, "Direct message")),
            sessionLogger.LogDiagnosticAsync("Streaming", "Bound diagnostic"));

        string content = await ReadSessionLogAsync(path, sessionId);
        StringAssert.Contains(content, "Direct message");
        StringAssert.Contains(content, "Bound diagnostic");
    }

    [TestMethod]
    public async Task SessionLoggers_WhenBoundToDifferentSessions_KeepMessagesSeparate()
    {
        string path = CreateLogPath();
        var logger = new FileCopilotChatLogger(path);
        Guid firstId = Guid.NewGuid();
        Guid secondId = Guid.NewGuid();
        ISessionCopilotChatLogger first = logger.CreateSessionLogger(firstId.ToString("N"));
        ISessionCopilotChatLogger second = logger.CreateSessionLogger(secondId.ToString("N"));

        await first.LogMessageAsync(new CopilotChatMessage(ChatRole.User, "First message"));
        await second.LogMessageAsync(new CopilotChatMessage(ChatRole.User, "Second message"));

        string firstContent = await ReadSessionLogAsync(path, firstId);
        string secondContent = await ReadSessionLogAsync(path, secondId);
        Assert.IsFalse(firstContent.Contains("Second message") || secondContent.Contains("First message"));
    }

    [TestMethod]
    public async Task EmptySessionLogger_WhenUsed_CompletesWithoutWriting()
    {
        ISessionCopilotChatLogger logger = new EmptyCopilotChatLogger().CreateSessionLogger("Test");

        await logger.LogMessageAsync(new CopilotChatMessage(ChatRole.User, "Message"));
        Task diagnostic = logger.LogDiagnosticAsync("Streaming", "Failed", new IOException("Read failed"));
        await diagnostic;

        Assert.IsTrue(diagnostic.IsCompletedSuccessfully);
    }

    [TestMethod]
    public async Task SessionLogger_WhenCreatedWithFileLogger_WritesUsingOwnSessionId()
    {
        string path = CreateLogPath();
        Guid sessionId = Guid.NewGuid();
        var session = new CopilotChatSession(new AgentLib.Logging.Sessions.FileSessionStorage(path), sessionId, DateTimeOffset.Now, new FileCopilotChatLogger(path));

        await session.Logger.LogDiagnosticAsync("Run", "Root diagnostic");

        string content = await ReadSessionLogAsync(path, sessionId);
        StringAssert.Contains(content, "Root diagnostic");
    }

    [TestMethod]
    public async Task ChildSessionLogger_WhenUsed_WritesToParentLog()
    {
        string path = CreateLogPath();
        var parent = new CopilotChatSession(new AgentLib.Logging.Sessions.FileSessionStorage(path), Guid.NewGuid(), DateTimeOffset.Now, new FileCopilotChatLogger(path));
        CopilotChatSession child = parent.CreateSubSession();

        await child.Logger.LogDiagnosticAsync("Run", "Child diagnostic");

        string content = await ReadSessionLogAsync(path, parent.SessionId);
        StringAssert.Contains(content, "Child diagnostic");
    }

    [TestMethod]
    public async Task ManagerCreatedSessionLogger_WhenUsed_WritesToConfiguredLog()
    {
        string path = CreateLogPath();
        var store = new AgentLib.Logging.Sessions.FileCopilotChatSessionStore(path);
        var manager = new CopilotChatManager(new FileCopilotChatLogger(path), store);

        await manager.SelectedSession.Logger.LogDiagnosticAsync("Run", "Manager diagnostic");

        string content = await ReadSessionLogAsync(path, manager.SelectedSession.SessionId);
        StringAssert.Contains(content, "Manager diagnostic");
    }

    [TestMethod]
    public async Task BranchedSessionLogger_WhenUsed_WritesToBranchLog()
    {
        string path = CreateLogPath();
        var store = new AgentLib.Logging.Sessions.FileCopilotChatSessionStore(path);
        var manager = new CopilotChatManager(new FileCopilotChatLogger(path), store);
        var message = new CopilotChatMessage(ChatRole.User, "Branch point");
        await manager.SelectedSession.AddMessageAsync(message);
        manager.CreateSessionFromMessage(message);

        await manager.SelectedSession.Logger.LogDiagnosticAsync("Run", "Branch diagnostic");

        string content = await ReadSessionLogAsync(path, manager.SelectedSession.SessionId);
        StringAssert.Contains(content, "Branch diagnostic");
    }

    private string CreateLogPath()
    {
        string path = Path.Combine(Path.GetTempPath(), "AgentLib.Tests", Guid.NewGuid().ToString("N"));
        TestContext.WriteLine(path);
        return path;
    }

    private static Task<string> ReadSessionLogAsync(string path, Guid sessionId) =>
        File.ReadAllTextAsync(Directory.GetFiles(path, $"*_{sessionId:N}.log", SearchOption.AllDirectories).Single());
}
