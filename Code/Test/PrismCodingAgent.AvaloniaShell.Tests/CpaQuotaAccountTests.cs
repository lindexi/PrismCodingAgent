using System.Net;
using System.Net.Http;
using PrismCodingAgent.CodingShell.Localizations;
using PrismCodingAgent.CodingShell.Services.Integrations;
using PrismCodingAgent.CodingShell.ViewModels;

namespace PrismCodingAgent.AvaloniaShell.Tests;

[TestClass]
public sealed class CpaQuotaAccountTests
{
    private const string Response = """
        {"files":[
          {"account":"account@example.com","quota":{"signals":{
            "X-Codex-Primary-Window-Minutes":10080,"X-Codex-Primary-Used-Percent":6,
            "X-Codex-Secondary-Window-Minutes":300,"X-Codex-Secondary-Used-Percent":2}}},
          {"account":"single@example.com","quota":{"signals":{
            "X-Codex-Primary-Window-Minutes":45,"X-Codex-Primary-Used-Percent":12}}},
          {"account":"account@example.com","quota":{"signals":{
            "X-Codex-Primary-Window-Minutes":30,"X-Codex-Primary-Used-Percent":1}}}
        ]}
        """;

    [TestMethod]
    public async Task QueryPreservesActualWindowMinutesAsync()
    {
        using var client = new HttpClient(new Handler());
        var windows = await CpaQuotaClient.QueryAsync(client, "https://example.com", "offline", default);
        CollectionAssert.AreEqual(new decimal[] { 10080, 300, 45, 30 }, windows.Select(window => window.WindowMinutes).ToArray());
    }

    [TestMethod]
    public async Task MixedWindowsGroupByActualAccountAndSortByDurationAsync()
    {
        Lang.SetCurrent("zh-Hans");
        using var client = new HttpClient(new Handler());
        using var service = CreateService(client);
        await service.RefreshAsync();
        string expected = string.Join(Environment.NewLine,
            "account@example.com 剩余额度：99% / 98% / 94%",
            "30 分钟：99%　重置：未知",
            "5 小时：98%　重置：未知",
            "7 天：94%　重置：未知",
            "",
            "single@example.com 剩余额度：88%",
            "重置：未知");
        Assert.AreEqual(expected, service.TooltipText);
    }

    [TestMethod]
    public async Task SettingsDetailsUseSameAccountFormattingAsync()
    {
        using var client = new HttpClient(new Handler());
        using var service = CreateService(client);
        await service.RefreshAsync();
        Assert.AreEqual(service.TooltipText, service.DetailText);
    }

    [TestMethod]
    public async Task SameAccountsInDifferentServicesRemainSeparateAndPrimaryFirstAsync()
    {
        using var client = new HttpClient(new Handler());
        using var model = new QuotaSettingsViewModel();
        using var first = CreateService(client);
        using var second = CreateService(client);
        model.Services.Add(first);
        model.Services.Add(second);
        second.IsPrimary = true;
        await first.RefreshAsync();
        await second.RefreshAsync();
        Assert.AreEqual(second.TooltipText + Environment.NewLine + Environment.NewLine + first.TooltipText, model.AllQuotaDetails);
    }

    [TestMethod]
    public async Task FailedQueryKeepsServiceStatusSeparateFromCachedAccountsAsync()
    {
        Lang.SetCurrent("zh-Hans");
        var handler = new Handler();
        using var client = new HttpClient(handler);
        using var model = new QuotaSettingsViewModel();
        using var service = CreateService(client);
        model.Services.Add(service);
        await service.RefreshAsync();
        handler.StatusCode = HttpStatusCode.Unauthorized;
        await service.RefreshAsync();
        Assert.AreEqual(service.TooltipText + Environment.NewLine + Environment.NewLine
            + "service-name：" + Lang.Current.QuotaAuthFailed, model.AllQuotaDetails);
    }

    [TestMethod]
    public async Task RepeatedRefreshNotifiesTooltipEvenWithUnchangedQuotaAsync()
    {
        using var client = new HttpClient(new Handler());
        using var model = new QuotaSettingsViewModel();
        using var service = CreateService(client);
        model.Services.Add(service);
        await service.RefreshAsync();
        var notifications = new List<string?>();
        model.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
        await service.RefreshAsync();
        CollectionAssert.Contains(notifications, nameof(QuotaSettingsViewModel.AllQuotaDetails));
    }

    [TestMethod]
    public async Task FooterKeepsOriginalFlatWindowOrderAsync()
    {
        using var client = new HttpClient(new Handler());
        using var service = CreateService(client);
        await service.RefreshAsync();
        Assert.AreEqual("94% / 98% / 88% / 99%", service.QuotaText);
    }

    private static CpaQuotaServiceViewModel CreateService(HttpClient client)
        => new(client) { Address = "https://example.com", ManagementKey = "offline", Name = "service-name" };

    private sealed class Handler : HttpMessageHandler
    {
        internal HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(StatusCode) { Content = new StringContent(Response) });
    }
}
