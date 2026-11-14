using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using PrismCodingAgent.CodingShell.Localizations;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using PrismCodingAgent.CodingShell.Services.Settings;

namespace PrismCodingAgent.CodingShell.ViewModels;

/// <summary>管理额度服务配置、主额度选择与顺序刷新。</summary>
public sealed class QuotaSettingsViewModel : ViewModelBase, IDisposable, IAsyncDisposable
{
    private HttpClient? _client;
    private readonly CancellationTokenSource _refreshCancellation = new();
    private Task? _refreshTask;
    private bool _disposed;

    /// <summary>初始化额度设置；尚未添加服务时不创建 HTTP 客户端。</summary>
    public QuotaSettingsViewModel()
    {
        AddServiceCommand = new SimpleCommand(() => Services.Add(CreateService()));
        RemoveServiceCommand = new SimpleCommand<CpaQuotaServiceViewModel>
        (service =>
            {
                if (service is not null) Services.Remove(service);
            }
        );
        Services.CollectionChanged += OnServicesChanged;
        Lang.Current.PropertyChanged += OnLanguageChanged;
    }

    public ObservableCollection<CpaQuotaServiceViewModel> Services { get; } = [];
    public SimpleCommand AddServiceCommand { get; }
    public SimpleCommand<CpaQuotaServiceViewModel> RemoveServiceCommand { get; }
    public bool HasServices => Services.Count > 0;
    public bool HasPrimaryQuota => Services.Any(service => service.IsPrimary && service.HasResults);

    public string PrimaryQuotaText => Lang.Current.QuotaPrefix +
                                      (Services.FirstOrDefault(service => service.IsPrimary)?.QuotaText ?? "—");

    public string AllQuotaDetails
    {
        get
        {
            var now = DateTimeOffset.Now;
            return string.Join
            (
                Environment.NewLine + Environment.NewLine,
                Services.OrderByDescending(service => service.IsPrimary).Select
                (service =>
                    {
                        string status = Lang.Format(Lang.Current.QuotaServiceStatus, service.Name, service.Status);
                        if (!service.HasResults) return status;
                        string details = service.FormatAccountDetails(now);
                        return service.HasStatus
                            ? details + Environment.NewLine + Environment.NewLine + status
                            : details;
                    }
                )
            );
        }
    }

    private CpaQuotaServiceViewModel CreateService()
        => new(_client ??= new HttpClient { Timeout = Timeout.InfiniteTimeSpan });

    internal void Load(CpaQuotaConfiguration[] configurations)
    {
        foreach (var service in Services.ToArray()) Services.Remove(service);
        foreach (var configuration in configurations)
        {
            var service = CreateService();
            service.Load(configuration);
            Services.Add(service);
        }

        _refreshTask ??= RunRefreshAsync(TimeProvider.System, _refreshCancellation.Token);
    }

    internal CpaQuotaConfiguration[] ToConfiguration() =>
        Services.Select(service => service.ToConfiguration()).ToArray();

    private void OnServicesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (CpaQuotaServiceViewModel service in e.OldItems)
            {
                service.PropertyChanged -= OnServiceChanged;
                service.Dispose();
            }
        }

        if (e.NewItems is not null)
        {
            foreach (CpaQuotaServiceViewModel service in e.NewItems)
            {
                service.PropertyChanged += OnServiceChanged;
                if (service.IsPrimary) SelectPrimary(service);
            }
        }

        if (Services.Count == 1) Services[0].IsPrimary = true;
        NotifySummaryChanged();
    }

    private void SelectPrimary(CpaQuotaServiceViewModel selected)
    {
        foreach (var service in Services.Where(service => service != selected))
        {
            service.IsPrimary = false;
        }
    }

    private void OnServiceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CpaQuotaServiceViewModel.IsPrimary)
            && sender is CpaQuotaServiceViewModel { IsPrimary: true } service)
        {
            SelectPrimary(service);
        }

        if (e.PropertyName is nameof(CpaQuotaServiceViewModel.IsPrimary) or nameof(CpaQuotaServiceViewModel.Name)
            or nameof(CpaQuotaServiceViewModel.HasResults) or nameof(CpaQuotaServiceViewModel.Status)
            or nameof(CpaQuotaServiceViewModel.TooltipText))
        {
            NotifySummaryChanged();
        }
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        foreach (var service in Services) service.UpdateLocalizedResults();
        NotifySummaryChanged();
    }

    private void NotifySummaryChanged()
    {
        OnPropertyChanged(nameof(HasServices));
        OnPropertyChanged(nameof(HasPrimaryQuota));
        OnPropertyChanged(nameof(PrimaryQuotaText));
        OnPropertyChanged(nameof(AllQuotaDetails));
    }

    internal async Task RunRefreshAsync(TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                foreach (var service in Services.ToArray())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!Services.Contains(service) || string.IsNullOrWhiteSpace(service.Address)
                                                    || string.IsNullOrWhiteSpace(service.ManagementKey)) continue;
                    await service.RefreshAsync(cancellationToken);
                }

                await Task.Delay(TimeSpan.FromSeconds(30), timeProvider, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 所属设置释放时结束刷新等待。
        }
    }

    /// <summary>停止刷新并释放所属 HTTP 客户端。</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Lang.Current.PropertyChanged -= OnLanguageChanged;
        _refreshCancellation.Cancel();
        foreach (var service in Services) service.Dispose();
        _client?.Dispose();
    }

    /// <summary>停止并等待所属自动刷新任务。</summary>
    public async ValueTask DisposeAsync()
    {
        Dispose();
        if (_refreshTask is not null) await _refreshTask;
    }
}