namespace PrismCodingAgent.CodingShell.Services.Settings;

/// <summary>PrismCodingAgent 应用的 Shell 设置。</summary>
public sealed record PrismCodingAgentShellSettings : CodingChatShellSettings
{
    /// <summary>获取配置的 CPA 额度查询服务。</summary>
    public CpaQuotaConfiguration[] CpaQuotaServices { get; init; } = [];
}
