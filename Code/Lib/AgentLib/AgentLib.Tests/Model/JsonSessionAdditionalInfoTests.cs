using System.Text.Json;
using AgentLib.Model;

namespace AgentLib.Tests.Model;

[TestClass]
public sealed class JsonSessionAdditionalInfoTests
{
    [TestMethod]
    public async Task SerializeAsyncPreservesDataAfterSourceDocumentIsDisposed()
    {
        JsonSessionAdditionalInfo info;
        using (JsonDocument document = JsonDocument.Parse("{\"name\":\"worker\"}"))
        {
            info = new JsonSessionAdditionalInfo(document.RootElement);
        }

        JsonElement result = await info.SerializeAsync();

        Assert.AreEqual("worker", result.GetProperty("name").GetString());
    }

    [TestMethod]
    public async Task SerializeAsyncObservesCancellation()
    {
        using JsonDocument document = JsonDocument.Parse("{}");
        ISessionAdditionalInfo info = new JsonSessionAdditionalInfo(document.RootElement);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await info.SerializeAsync(cancellation.Token));
    }
}
