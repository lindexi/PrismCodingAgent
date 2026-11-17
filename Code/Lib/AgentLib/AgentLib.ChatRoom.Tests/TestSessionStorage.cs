using AgentLib.Logging.Sessions;
using AgentLib.Model;

namespace AgentLib.ChatRoom.Tests;

internal static class TestSessionStorage
{
    internal static FileCopilotChatSessionStore Create()
    {
        string path = Path.Combine(Path.GetTempPath(), "AgentLib.ChatRoom.Tests", Guid.NewGuid().ToString("N"));
        Console.WriteLine(path);
        return new FileCopilotChatSessionStore(path);
    }
}
