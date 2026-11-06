using System.Net;
using System.Net.Http;
using PrismCodingAgent.CodingShell.Services.Integrations;
using PrismCodingAgent.CodingShell.ViewModels;

namespace PrismCodingAgent.AvaloniaShell.Tests;

[TestClass]
public sealed class CpaQuotaTests
{
    [TestMethod]
    public void SelectingPrimaryUnselectsOtherServices()
    {
        using var model = new QuotaSettingsViewModel();
        model.AddServiceCommand.Execute(null);
        model.AddServiceCommand.Execute(null);
        model.Services[1].IsPrimary = true;
        Assert.AreSame(model.Services[1], model.Services.Single(service => service.IsPrimary));
    }

    [TestMethod]
    public void NameDefaultsToDomainLabel()
    {
        using var model = new QuotaSettingsViewModel();
        model.AddServiceCommand.Execute(null);
        model.Services[0].Address = "https://aabbcc.xx.com";
        Assert.AreEqual("xx", model.Services[0].Name);
    }

    [TestMethod]
    public void OnlyServiceIsAutomaticallyPrimary()
    {
        using var model = new QuotaSettingsViewModel();
        model.AddServiceCommand.Execute(null);
        Assert.IsTrue(model.Services.Single().IsPrimary);
    }

    [TestMethod]
    public void RemovingPrimarySelectsOnlyRemainingService()
    {
        using var model = new QuotaSettingsViewModel();
        model.AddServiceCommand.Execute(null);
        model.AddServiceCommand.Execute(null);
        model.RemoveServiceCommand.Execute(model.Services[0]);
        Assert.IsTrue(model.Services.Single().IsPrimary);
    }

    [TestMethod]
    public void UnqueriedPrimaryQuotaIsHidden()
    {
        using var model = new QuotaSettingsViewModel();
        model.AddServiceCommand.Execute(null);
        Assert.IsFalse(model.HasPrimaryQuota);
    }

    [TestMethod]
    [DataRow("0")]
    [DataRow("100")]
    public async Task PrimaryQuotaWithResultIsVisibleAsync(string used)
    {
        using var model = new QuotaSettingsViewModel();
        using var client = new HttpClient(new SingleWindowHandler(used));
        using var service = new CpaQuotaServiceViewModel(client)
        {
            Address = "https://example.com", ManagementKey = "offline", IsPrimary = true,
        };
        model.Services.Add(service);
        var notifications = new List<string?>();
        model.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
        await service.RefreshAsync();
        Assert.IsTrue(model.HasPrimaryQuota);
        CollectionAssert.Contains(notifications, nameof(QuotaSettingsViewModel.HasPrimaryQuota));
    }

    [TestMethod]
    public async Task QueryUsesManagementEndpointAsync()
    {
        using var client = new HttpClient(new SingleWindowHandler("30"));
        var result = await CpaQuotaClient.QueryAsync(client, "https://example.com/v1/", "offline", default);
        Assert.AreEqual(70m, result.Single().RemainingPercent);
    }

    [TestMethod]
    public void ChildSelectionDoesNotRequireParentCallbacks()
    {
        using var client = new HttpClient();
        using var service = new CpaQuotaServiceViewModel(client);
        service.IsPrimary = true;
        Assert.IsTrue(service.IsPrimary);
    }

    [TestMethod]
    public void NameChangeUpdatesCollectionSummary()
    {
        using var model = new QuotaSettingsViewModel();
        model.AddServiceCommand.Execute(null);
        var notifications = new List<string?>();
        model.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
        model.Services[0].Name = "renamed";
        CollectionAssert.Contains(notifications, nameof(QuotaSettingsViewModel.AllQuotaDetails));
    }

    [TestMethod]
    public void RemovedServiceNoLongerNotifiesCollection()
    {
        using var model = new QuotaSettingsViewModel();
        model.AddServiceCommand.Execute(null);
        var service = model.Services[0];
        model.RemoveServiceCommand.Execute(service);
        var notifications = new List<string?>();
        model.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
        service.Name = "detached";
        Assert.IsEmpty(notifications);
    }

    [TestMethod]
    public async Task ExistingAliasConfigurationLoadsAsNameAsync()
    {
        var configuration = System.Text.Json.JsonSerializer.Deserialize<PrismCodingAgent.CodingShell.Services.Settings.CpaQuotaConfiguration>(
            """{"Address":"https://example.com","ManagementKey":"offline","Alias":"saved-name","IsPrimary":true}""")!;
        await using var model = new QuotaSettingsViewModel();
        model.Load([configuration]);
        Assert.AreEqual("saved-name", model.Services.Single().Name);
    }

    private sealed class SingleWindowHandler(string used) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.AreEqual("https://example.com/v0/management/auth-files", request.RequestUri!.AbsoluteUri);
            Assert.AreEqual("offline", request.Headers.GetValues("X-Management-Key").Single());
            Assert.IsNull(request.Headers.Authorization);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"files\":[{\"quota\":{\"signals\":{\"X-Codex-Primary-Window-Minutes\":\"10080\",\"X-Codex-Primary-Used-Percent\":\"" + used + "\"}}}]}"),
            });
        }
    }
}
