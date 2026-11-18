namespace AgentLib.Logging;

/// <summary>Records diagnostics without replacing the outcome of the operation being reported.</summary>
public static class DiagnosticLoggingExtensions
{
    /// <summary>Attempts to write a diagnostic, returning false if logging is unavailable or the write fails.</summary>
    public static async Task<bool> TryLogDiagnosticAsync(this ISessionCopilotChatLogger? logger,
        string category, string message, Exception? exception = null)
    {
        if (logger is null)
        {
            return false;
        }

        try
        {
            await logger.LogDiagnosticAsync(category, message, exception).ConfigureAwait(false);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
