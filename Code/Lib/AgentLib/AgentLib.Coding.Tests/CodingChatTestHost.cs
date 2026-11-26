using Microsoft.Extensions.AI;

namespace AgentLib.Coding.Tests;

internal static class CodingChatTestHost
{
    internal static Task<ICodingAgentRunResult> RunAsync(this CodingAgent agent,
        CodingRunContext context, string prompt, string? workspacePath,
        bool enableAutomaticCompression = true, bool enableDotNetRun = false,
        CancellationToken cancellationToken = default) =>
        agent.RunAsync(context.WithToolContext(context.ToolContext with
        {
            WorkspacePath = workspacePath,
            Options = new CodingChatRunOptions
            {
                EnableAutomaticCompression = enableAutomaticCompression,
                EnableDotNetRun = enableDotNetRun,
                LanguageTag = "en",
            },
        }), [new TextContent(prompt)], cancellationToken);
}
