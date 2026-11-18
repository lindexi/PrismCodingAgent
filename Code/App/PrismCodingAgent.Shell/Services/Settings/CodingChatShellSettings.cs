using PrismCodingAgent.CodingShell.Localizations;

namespace PrismCodingAgent.CodingShell.Services.Settings;

/// <summary>Shell 的全局设置。</summary>
public sealed record CodingChatShellSettings
{
    [System.Text.Json.Serialization.JsonInclude]
    internal CpaQuotaConfiguration[] CpaQuotaServices { get; init; } = [];

    public string LanguageTag { get; init; } = GetDefaultLanguageTag();

    private static string GetDefaultLanguageTag() => ShellLanguage.GetDefault();

    public bool IsWindowsSandboxEnabled { get; init; } = true;

    public string WindowsSandboxToolPath { get; init; } = "WinRemoteShell.exe";

    public string WindowsSandboxServerAddress { get; init; } = "127.0.0.1:12399";

    public string? DeepSeekApiKey { get; init; }

    public bool IsNetworkProxyEnabled { get; init; }

    public string? NetworkProxyAddress { get; init; }

    public bool BypassProxyOnLocal { get; init; } = true;

    public bool IsAutomaticSessionTitleEnabled { get; set; } = true;

    public bool IsCopilotInstructionsEnabled { get; init; }

    public string? CopilotInstructionsPath { get; init; }
}