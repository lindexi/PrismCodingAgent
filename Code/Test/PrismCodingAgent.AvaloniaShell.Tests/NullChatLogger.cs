using AgentLib.Logging;
using AgentLib.Model;

namespace PrismCodingAgent.AvaloniaShell.Tests;

internal sealed class NullChatLogger : ICopilotChatLogger, ISessionCopilotChatLogger
{
    public ISessionCopilotChatLogger CreateSessionLogger(Guid sessionId) => this;

    public Task LogMessageAsync(Guid sessionId, CopilotChatMessage chatMessage) => Task.CompletedTask;

    public Task LogDiagnosticAsync(Guid sessionId, string category, string message, Exception? exception = null) => Task.CompletedTask;

    public Task LogMessageAsync(CopilotChatMessage chatMessage) => Task.CompletedTask;

    public Task LogDiagnosticAsync(string category, string message, Exception? exception = null) => Task.CompletedTask;
}
