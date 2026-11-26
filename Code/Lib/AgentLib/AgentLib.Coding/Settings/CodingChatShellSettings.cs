namespace PrismCodingAgent.CodingShell.Services.Settings;

/// <summary>Shell 的全局设置。</summary>
public sealed record CodingChatShellSettings
{
    /// <summary>获取配置的 CPA 额度查询服务。</summary>
    public CpaQuotaConfiguration[] CpaQuotaServices { get; init; } = [];

    /// <summary>获取界面语言标签；未配置时由 Shell 决定默认语言。</summary>
    public string? LanguageTag { get; init; }

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
