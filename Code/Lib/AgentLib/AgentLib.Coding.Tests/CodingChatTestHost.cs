using Microsoft.Extensions.AI;

namespace AgentLib.Coding.Tests;

internal static class CodingChatTestHost
{
    internal static Task<ICodingAgentRunResult> RunAsync(this CodingAgent agent,
        CodingRunContext context, string prompt, string? workspacePath,
        bool enableAutomaticCompression = true, bool enableDotNetRun = false,
        CancellationToken cancellationToken = default) =>
        agent.RunAsync(context, [new TextContent(prompt)], workspacePath,
            new CodingChatRunOptions(enableAutomaticCompression, enableDotNetRun, null) { LanguageTag = "en" }, cancellationToken);
}
