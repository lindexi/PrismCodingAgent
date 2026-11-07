using System.Net;
using System.Net.Http;
using Moq;
using PrismCodingAgent.CodingShell.ViewModels;

namespace PrismCodingAgent.AvaloniaShell.Tests;

[TestClass]
public sealed class CpaQuotaRefreshTests
{
    [TestMethod]
    public async Task RefreshQueriesServicesSequentiallyThenWaitsTenSecondsAsync()
    {
        await using var model = new QuotaSettingsViewModel();
        using var cancellation = new CancellationTokenSource();
        var timer = new Mock<ITimer>();
        var clock = new Mock<TimeProvider>();
        TimerCallback? tick = null;
        object? tickState = null;
        clock.Setup(provider => provider.CreateTimer(It.IsAny<TimerCallback>(), It.IsAny<object>(),
                TimeSpan.FromSeconds(10), Timeout.InfiniteTimeSpan))
            .Callback<TimerCallback, object, TimeSpan, TimeSpan>((callback, state, _, _) =>
            {
                tick = callback;
                tickState = state;
                cancellation.Cancel();
            }).Returns(timer.Object);
        var order = new List<string>();
        var firstResponse = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var firstClient = new HttpClient(new Handler(_ =>
        {
            order.Add("first");
            firstStarted.SetResult();
            return firstResponse.Task;
        }));
        using var secondClient = new HttpClient(new Handler(_ =>
        {
            order.Add("second");
            return Task.FromResult(Response());
        }));
        using var first = CreateService(firstClient);
        using var second = CreateService(secondClient);
        model.Services.Add(first);
        model.Services.Add(second);
        Task refresh = model.RunRefreshAsync(clock.Object, cancellation.Token);
        await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        CollectionAssert.AreEqual(new[] { "first" }, order);
        firstResponse.SetResult(Response());
        await refresh.WaitAsync(TimeSpan.FromSeconds(5));
        CollectionAssert.AreEqual(new[] { "first", "second" }, order);
        Assert.IsNotNull(tick);
    }

    private static CpaQuotaServiceViewModel CreateService(HttpClient client)
        => new(client) { Address = "https://example.com", ManagementKey = "offline" };

    private static HttpResponseMessage Response()
        => new(HttpStatusCode.OK) { Content = new StringContent("{\"files\":[]}") };

    private sealed class Handler(Func<CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => respond(cancellationToken);
    }
}
