using System.Net;
using System.Net.Http;
using PrismCodingAgent.CodingShell.Services.Integrations;
using PrismCodingAgent.CodingShell.ViewModels;

namespace PrismCodingAgent.AvaloniaShell.Tests;

[TestClass]
public sealed class CpaQuotaTests
{
    [TestMethod]
    public async Task QueryUsesManagementEndpointAndPreservesIndependentWindowsAsync()
    {
        using var client = new HttpClient(new ResponseHandler());
        var result = await CpaQuotaClient.QueryAsync(client, "https://example.com/v1/", "test-management", default);
        CollectionAssert.AreEqual(new decimal[] { 70, 40 }, result.Select(item => item.RemainingPercent).ToArray());
    }

    [TestMethod]
    public void SelectingPrimaryUnselectsOtherServices()
    {
        using var model = new SettingsViewModel();
        model.AddCpaServiceCommand.Execute(null);
        model.AddCpaServiceCommand.Execute(null);
        model.CpaServices[0].IsPrimary = true;
        model.CpaServices[1].IsPrimary = true;
        Assert.AreSame(model.CpaServices[1], model.CpaServices.Single(service => service.IsPrimary));
    }

    [TestMethod]
    public void AliasDefaultsToDomainLabel()
    {
        using var model = new SettingsViewModel();
        model.AddCpaServiceCommand.Execute(null);
        model.CpaServices[0].Address = "https://aabbcc.xx.com";
        Assert.AreEqual("xx", model.CpaServices[0].Alias);
    }

    [TestMethod]
    public void OnlyServiceIsAutomaticallyPrimary()
    {
        using var model = new SettingsViewModel();
        model.AddCpaServiceCommand.Execute(null);
        Assert.IsTrue(model.CpaServices.Single().IsPrimary);
    }

    [TestMethod]
    public void RemovingPrimarySelectsOnlyRemainingService()
    {
        using var model = new SettingsViewModel();
        model.AddCpaServiceCommand.Execute(null);
        model.AddCpaServiceCommand.Execute(null);
        model.RemoveCpaServiceCommand.Execute(model.CpaServices[0]);
        Assert.IsTrue(model.CpaServices.Single().IsPrimary);
    }

    [TestMethod]
    public void UnqueriedPrimaryQuotaIsHidden()
    {
        using var model = new SettingsViewModel();
        model.AddCpaServiceCommand.Execute(null);
        Assert.IsFalse(model.HasPrimaryQuota);
    }

    [TestMethod]
    [DataRow("0")]
    [DataRow("100")]
    public async Task PrimaryQuotaWithResultIsVisibleAsync(string used)
    {
        using var model = new SettingsViewModel();
        using var client = new HttpClient(new SingleWindowHandler(used));
        using var service = new CpaQuotaServiceViewModel(_ => { }, () => { }, client)
        {
            Address = "https://example.com", ManagementKey = "offline", IsPrimary = true,
        };
        model.CpaServices.Add(service);
        await service.RefreshAsync();
        Assert.IsTrue(model.HasPrimaryQuota);
    }

    private sealed class SingleWindowHandler(string used) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"files\":[{\"quota\":{\"signals\":{\"X-Codex-Primary-Window-Minutes\":\"10080\",\"X-Codex-Primary-Used-Percent\":\"" + used + "\"}}}]}"),
            });
    }

    private sealed class ResponseHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.AreEqual("https://example.com/v0/management/auth-files", request.RequestUri!.AbsoluteUri);
            Assert.AreEqual("test-management", request.Headers.GetValues("X-Management-Key").Single());
            Assert.IsNull(request.Headers.Authorization);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"files":[{"account":"one","quota":{"signals":{"X-Codex-Primary-Used-Percent":"30","X-Codex-Primary-Window-Minutes":"10080"}}},{"account":"two","quota":{"signals":{"X-Codex-Primary-Used-Percent":"60","X-Codex-Primary-Window-Minutes":"10080","X-Codex-Secondary-Used-Percent":"0","X-Codex-Secondary-Window-Minutes":"0"}}}]}""")
            });
        }
    }
}
