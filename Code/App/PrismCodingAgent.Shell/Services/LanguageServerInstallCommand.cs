namespace PrismCodingAgent.CodingShell.Services;

internal static class LanguageServerInstallCommand
{
    internal const string ToolName = "roslyn-language-server";
    internal const string Text = "dotnet tool update -g " + ToolName;
}
