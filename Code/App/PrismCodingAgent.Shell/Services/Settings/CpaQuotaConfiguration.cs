namespace PrismCodingAgent.CodingShell.Services.Settings;

internal sealed record CpaQuotaConfiguration(string Address, string ManagementKey, [property: System.Text.Json.Serialization.JsonPropertyName("Alias")] string Name, bool IsPrimary);
