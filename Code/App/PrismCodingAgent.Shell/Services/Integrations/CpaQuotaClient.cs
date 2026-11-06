using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PrismCodingAgent.CodingShell.Services.Integrations;

internal sealed record CpaQuotaWindow(string Account, string Window, decimal RemainingPercent,
    DateTimeOffset? ResetAt, DateTimeOffset? ObservedAt);

internal static class CpaQuotaClient
{
    internal static async Task<IReadOnlyList<CpaQuotaWindow>> QueryAsync(HttpClient client,
        string address, string managementKey, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("https" or "http"))
            throw new ArgumentException("请输入有效的 CPA HTTP 或 HTTPS 地址。");
        if (string.IsNullOrWhiteSpace(managementKey)) throw new ArgumentException("请输入 CPA 管理密钥。");
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(uri, "/v0/management/auth-files"));
        request.Headers.Add("X-Management-Key", managementKey);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var result = new List<CpaQuotaWindow>();
        foreach (var file in json.RootElement.GetProperty("files").EnumerateArray())
        {
            if (!file.TryGetProperty("quota", out var quota) || quota.ValueKind != JsonValueKind.Object
                || !quota.TryGetProperty("signals", out var signals) || signals.ValueKind != JsonValueKind.Object) continue;
            string account = file.TryGetProperty("account", out var name) ? name.ToString() : "账户";
            DateTimeOffset? observed = quota.TryGetProperty("observed_at", out var time)
                && DateTimeOffset.TryParse(time.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) ? parsed : null;
            AddWindow("Primary", "主窗口");
            AddWindow("Secondary", "次窗口");

            void AddWindow(string key, string label)
            {
                string prefix = "X-Codex-" + key;
                if (!Number(prefix + "-Window-Minutes", out var minutes) || minutes <= 0
                    || !Number(prefix + "-Used-Percent", out var used) || used < 0 || used > 100) return;
                DateTimeOffset? reset = Number(prefix + "-Reset-At", out var seconds)
                    && seconds >= -62135596800m && seconds <= 253402300799m
                    ? DateTimeOffset.FromUnixTimeSeconds((long)seconds) : null;
                result.Add(new(account, label, 100 - used, reset, observed));
            }
            bool Number(string key, out decimal number)
            {
                number = 0;
                return signals.TryGetProperty(key, out var value)
                    && decimal.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number);
            }
        }
        return result;
    }
}
