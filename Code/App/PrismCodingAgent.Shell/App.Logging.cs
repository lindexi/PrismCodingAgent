#if USE_AVALONIA
global using DesktopApplication = Avalonia.Application;
#elif USE_WPF
global using DesktopApplication = System.Windows.Application;
#endif

global using AgentLib.Logging;

using System;
using PrismCodingAgent.CodingShell.Infrastructure;

namespace PrismCodingAgent.CodingShell;

public partial class App
{
    internal ISessionCopilotChatLogger? ProcessLogger { get; private set; }

    private FileCopilotChatLogger InitializeLogging(CodingChatRoomPaths paths)
    {
        var logger = new FileCopilotChatLogger(paths.LogDirectory);
        ProcessLogger = logger.CreateSessionLogger($"Main_{Environment.ProcessId}");
        return logger;
    }
}
