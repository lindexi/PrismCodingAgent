namespace PrismCodingAgent.AvaloniaShell.Services;

internal static class LanguageServerInstallCommand
{
    internal const string ToolName = "roslyn-language-server";
    internal const string Text = "dotnet tool update -g " + ToolName;
}
