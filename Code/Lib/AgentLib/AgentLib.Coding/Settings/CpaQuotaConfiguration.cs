namespace PrismCodingAgent.CodingShell.Services.Settings;

/// <summary>配置一个 CPA 额度查询服务。</summary>
/// <param name="Address">服务地址。</param>
/// <param name="ManagementKey">服务管理密钥。</param>
/// <param name="Name">显示名称。</param>
/// <param name="IsPrimary">是否为主要服务。</param>
public sealed record CpaQuotaConfiguration(string Address, string ManagementKey, string Name, bool IsPrimary);
