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
        ISessionCopilotChatLogger sessionLogger = logger.CreateSessionLogger(sessionId);

        await sessionLogger.LogMessageAsync(new CopilotChatMessage(ChatRole.User, "Session message"));

        string content = await ReadSessionLogAsync(path, sessionId);
        StringAssert.Contains(content, $"SessionId: {sessionId}");
        StringAssert.Contains(content, "Session message");
    }

    [TestMethod]
    public async Task LogDiagnosticAsync_WhenCalled_PreservesOriginalException()
    {
        string path = CreateLogPath();
        Guid sessionId = Guid.NewGuid();
        ISessionCopilotChatLogger logger = new FileCopilotChatLogger(path).CreateSessionLogger(sessionId);
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
        ISessionCopilotChatLogger sessionLogger = logger.CreateSessionLogger(sessionId);

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
        ISessionCopilotChatLogger first = logger.CreateSessionLogger(firstId);
        ISessionCopilotChatLogger second = logger.CreateSessionLogger(secondId);

        await first.LogMessageAsync(new CopilotChatMessage(ChatRole.User, "First message"));
        await second.LogMessageAsync(new CopilotChatMessage(ChatRole.User, "Second message"));

        string firstContent = await ReadSessionLogAsync(path, firstId);
        string secondContent = await ReadSessionLogAsync(path, secondId);
        Assert.IsFalse(firstContent.Contains("Second message") || secondContent.Contains("First message"));
    }

    [TestMethod]
    public async Task EmptySessionLogger_WhenUsed_CompletesWithoutWriting()
    {
        ISessionCopilotChatLogger logger = new EmptyCopilotChatLogger().CreateSessionLogger(Guid.NewGuid());

        await logger.LogMessageAsync(new CopilotChatMessage(ChatRole.User, "Message"));
        Task diagnostic = logger.LogDiagnosticAsync("Streaming", "Failed", new IOException("Read failed"));
        await diagnostic;

        Assert.IsTrue(diagnostic.IsCompletedSuccessfully);
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
