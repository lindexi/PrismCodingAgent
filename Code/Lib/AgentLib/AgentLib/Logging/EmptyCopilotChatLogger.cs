using AgentLib.Model;

namespace AgentLib.Logging;

class EmptyCopilotChatLogger : ICopilotChatLogger
{
    public ISessionCopilotChatLogger CreateSessionLogger(Guid sessionId) => new SessionLogger();

    private sealed class SessionLogger : ISessionCopilotChatLogger
    {
        public Task LogMessageAsync(CopilotChatMessage chatMessage) => Task.CompletedTask;

        public Task LogDiagnosticAsync(string category, string message, Exception? exception = null) => Task.CompletedTask;
    }

    public Task LogMessageAsync(Guid sessionId, CopilotChatMessage chatMessage)
    {
        return Task.CompletedTask;
    }

    public Task LogDiagnosticAsync(Guid sessionId, string category, string message, Exception? exception = null)
    {
        return Task.CompletedTask;
    }
}