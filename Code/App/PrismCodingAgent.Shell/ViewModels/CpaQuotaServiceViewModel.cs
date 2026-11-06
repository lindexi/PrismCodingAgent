using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PrismCodingAgent.CodingShell.Services.Integrations;
using PrismCodingAgent.CodingShell.Services.Settings;

namespace PrismCodingAgent.CodingShell.ViewModels;

public sealed class CpaQuotaServiceViewModel : ViewModelBase, IDisposable
{
    private static readonly HttpClient Client = new() { Timeout = Timeout.InfiniteTimeSpan };
    private readonly Action<CpaQuotaServiceViewModel> _selectPrimary;
    private readonly Action _changed;
    private readonly HttpClient _client;
    private CancellationTokenSource? _request;
    private string _address = "";
    private string _managementKey = "";
    private string _alias = "";
    private bool _isPrimary;
    private string _status = "尚未查询";
    private IReadOnlyList<CpaQuotaWindow> _windows = [];

    internal CpaQuotaServiceViewModel(Action<CpaQuotaServiceViewModel> selectPrimary, Action changed, HttpClient? client = null)
    {
        _selectPrimary = selectPrimary;
        _changed = changed;
        _client = client ?? Client;
        RefreshCommand = new SimpleAsyncCommand(RefreshAsync);
    }

    public string Address
    {
        get => _address;
        set
        {
            if (!SetField(ref _address, value)) return;
            InvalidateResult();
            if (string.IsNullOrWhiteSpace(Alias) && Uri.TryCreate(value, UriKind.Absolute, out var uri))
            {
                string[] parts = uri.Host.Split('.');
                Alias = parts.Length >= 2 ? parts[^2] : uri.Host;
            }
        }
    }
    public string ManagementKey
    {
        get => _managementKey;
        set { if (SetField(ref _managementKey, value)) InvalidateResult(); }
    }
    public string Alias
    {
        get => _alias;
        set { if (SetField(ref _alias, value)) _changed(); }
    }
    public bool IsPrimary
    {
        get => _isPrimary;
        set
        {
            if (!SetField(ref _isPrimary, value)) return;
            if (value) _selectPrimary(this);
            _changed();
        }
    }
    public string Status
    {
        get => _status;
        private set
        {
            if (SetField(ref _status, value)) OnPropertyChanged(nameof(HasStatus));
        }
    }
    public bool HasStatus => !string.IsNullOrEmpty(Status);
    public SimpleAsyncCommand RefreshCommand { get; }
    public bool HasResults => _windows.Count > 0;
    public string QuotaText => _windows.Count == 0 ? "—" : string.Join(" / ", _windows.Select(window => $"{window.RemainingPercent:0.##}%"));
    public string DetailText => string.Join(Environment.NewLine, _windows.Select(window =>
        $"{window.Account} · {window.Window}：{window.RemainingPercent:0.##}%　重置：{Format(window.ResetAt)}"));
    public string ObservedText => string.Join(" / ", _windows.Select(window => window.ObservedAt).Distinct().Select(Format));

    private static string Format(DateTimeOffset? time) => time?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "未知";

    internal CpaQuotaConfiguration ToConfiguration() => new(Address, ManagementKey, Alias, IsPrimary);
    internal void Load(CpaQuotaConfiguration configuration)
    {
        _address = configuration.Address;
        _managementKey = configuration.ManagementKey;
        _alias = configuration.Alias;
        IsPrimary = configuration.IsPrimary;
    }

    private void InvalidateResult()
    {
        _request?.Cancel();
        _windows = [];
        Status = "尚未查询";
        Publish();
    }

    internal async Task RefreshAsync()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        _request = cancellation;
        Status = "查询中…";
        _changed();
        try
        {
            var result = await CpaQuotaClient.QueryAsync(_client, Address, ManagementKey, cancellation.Token);
            if (cancellation.IsCancellationRequested) return;
            _windows = result;
            Status = result.Count == 0 ? "暂无额度数据" : "";
            Publish();
        }
        catch (OperationCanceledException)
        {
            Status = cancellation.IsCancellationRequested ? "查询已取消或超时" : "读取失败，请重试";
        }
        catch (HttpRequestException exception)
        {
            Status = exception.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                ? "请检查 CPA 管理密钥或访问权限" : "读取失败，请检查 CPA 地址和网络后重试";
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
        {
            Status = "CPA 返回的数据格式无法识别";
        }
        catch (ArgumentException exception)
        {
            Status = exception.Message;
        }
        finally
        {
            _request = null;
            _changed();
        }
    }

    private void Publish()
    {
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(QuotaText));
        OnPropertyChanged(nameof(DetailText));
        OnPropertyChanged(nameof(ObservedText));
        _changed();
    }

    public void Dispose() => _request?.Cancel();
}
