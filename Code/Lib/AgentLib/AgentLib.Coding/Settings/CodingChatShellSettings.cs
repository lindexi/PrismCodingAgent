namespace AgentLib.Coding.Settings;

/// <summary>可由应用扩展的通用 Shell 设置。</summary>
public record CodingChatShellSettings
{
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
