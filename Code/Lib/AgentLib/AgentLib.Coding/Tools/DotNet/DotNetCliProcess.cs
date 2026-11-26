using System.Diagnostics;

namespace AgentLib.Coding;

internal static class DotNetCliProcess
{
    internal const string FileName = "dotnet";

    internal static void ConfigureEnvironment(ProcessStartInfo startInfo)
    {
        startInfo.Environment["DOTNET_NOLOGO"] = "1";
        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
    }
}
