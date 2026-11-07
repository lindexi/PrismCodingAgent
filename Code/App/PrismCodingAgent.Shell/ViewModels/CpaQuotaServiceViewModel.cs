using System;
using System.Collections.Generic;
using System.Linq;
using PrismCodingAgent.CodingShell.Localizations;
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
    private readonly HttpClient _client;
    private CancellationTokenSource? _request;
    private string _address = "";
    private string _managementKey = "";
    private string _name = "";
    private bool _isPrimary;
    private string _status = Lang.Current.QuotaPending;
    private IReadOnlyList<CpaQuotaWindow> _windows = [];

    internal CpaQuotaServiceViewModel(HttpClient client)
    {
        _client = client;
        RefreshCommand = new SimpleAsyncCommand(() => RefreshAsync());
    }

    public string Address
    {
        get => _address;
        set
        {
            if (!SetField(ref _address, value)) return;
            InvalidateResult();
            if (string.IsNullOrWhiteSpace(Name) && Uri.TryCreate(value, UriKind.Absolute, out var uri))
            {
                string[] parts = uri.Host.Split('.');
                Name = parts.Length >= 2 ? parts[^2] : uri.Host;
            }
        }
    }
    public string ManagementKey
    {
        get => _managementKey;
        set { if (SetField(ref _managementKey, value)) InvalidateResult(); }
    }
    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }
    public bool IsPrimary
    {
        get => _isPrimary;
        set => SetField(ref _isPrimary, value);
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
        Lang.Format(Lang.Current.QuotaDetail, window.Account,
            $"{(window.Window == "Primary" ? Lang.Current.QuotaPrimaryWindow : Lang.Current.QuotaSecondaryWindow)}：{window.RemainingPercent:0.##}%", Format(window.ResetAt))));
    public string TooltipText => string.Join(Environment.NewLine + Environment.NewLine, _windows.Select(window =>
        Lang.Format(Lang.Current.QuotaTooltip, Name, $"{window.RemainingPercent:0.##}%", Format(window.ResetAt))));
    public string ObservedText => string.Join(" / ", _windows.Select(window => window.ObservedAt).Distinct().Select(Format));

    private static string Format(DateTimeOffset? time) => time?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? Lang.Current.QuotaUnknown;

    internal CpaQuotaConfiguration ToConfiguration() => new(Address, ManagementKey, Name, IsPrimary);
    internal void Load(CpaQuotaConfiguration configuration)
    {
        _address = configuration.Address;
        _managementKey = configuration.ManagementKey;
        _name = configuration.Name;
        IsPrimary = configuration.IsPrimary;
    }

    private void InvalidateResult()
    {
        _request?.Cancel();
        _windows = [];
        Status = Lang.Current.QuotaPending;
        Publish();
    }

    internal async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (_request is not null) return;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(30));
        _request = cancellation;
        Status = Lang.Current.QuotaLoading;
        try
        {
            var result = await CpaQuotaClient.QueryAsync(_client, Address, ManagementKey, cancellation.Token);
            if (cancellation.IsCancellationRequested) return;
            _windows = result;
            Status = result.Count == 0 ? Lang.Current.QuotaEmpty : "";
            Publish();
        }
        catch (OperationCanceledException)
        {
            Status = cancellation.IsCancellationRequested ? Lang.Current.QuotaCancelled : Lang.Current.QuotaNetworkFailed;
        }
        catch (HttpRequestException exception)
        {
            Status = exception.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                ? Lang.Current.QuotaAuthFailed : Lang.Current.QuotaNetworkFailed;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
        {
            Status = Lang.Current.QuotaInvalidResponse;
        }
        catch (ArgumentException exception)
        {
            Status = exception.Message;
        }
        finally
        {
            _request = null;
        }
    }

    internal void UpdateLocalizedResults()
    {
        Publish();
    }

    private void Publish()
    {
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(QuotaText));
        OnPropertyChanged(nameof(DetailText));
        OnPropertyChanged(nameof(TooltipText));
        OnPropertyChanged(nameof(ObservedText));
    }

    public void Dispose() => _request?.Cancel();
}
