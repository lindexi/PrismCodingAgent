using AgentLib.Coding;
using AgentLib.Core.AgentApiManagers.LanguageModelProviders.Fakes;
using AgentLib.Logging.Sessions;
using AgentLib.Model;

namespace PrismCodingAgent.AvaloniaShell.Tests;

internal static class ToolRegistrationTestContext
{
    internal static CodingRunContext Create(string? workspacePath = null)
    {
        var storage = new FileSessionStorage(Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var toolContext = new CodingToolContext
        {
            Session = new CopilotChatSession(storage),
            WorkspacePath = workspacePath,
            Options = new CodingChatRunOptions(),
        };
        return new CodingRunContext(toolContext, new FakeChatClient(),
            CopilotChatMessage.CreateAssistant(string.Empty, isPresetInfo: false));
    }
}
