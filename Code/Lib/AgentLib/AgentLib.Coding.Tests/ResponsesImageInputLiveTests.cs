#pragma warning disable OPENAI001

using System.ClientModel;
using System.ClientModel.Primitives;
using OpenAI.Responses;

namespace AgentLib.Coding.Tests;

[TestClass]
[TestCategory("LiveApi")]
[DoNotParallelize]
public sealed class ResponsesImageInputLiveTests
{
    [TestMethod]
    [Ignore("One-shot paid API experiment; requires explicit authorization before enabling.")]
    public async Task MiniMaxShouldAcceptInlineImageAsync()
    {
        const string keyPath = @"C:\lindexi\Work\Key\MiniMax.txt";
        if (!File.Exists(keyPath))
        {
            Assert.Inconclusive("MiniMax credential file is unavailable; no request sent.");
        }
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        string key = (await File.ReadAllTextAsync(keyPath, cancellation.Token)).Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            Assert.Inconclusive("MiniMax credential is empty; no request sent.");
        }
        using var transport = new SingleRequestHandler();
        using var http = new HttpClient(transport);
        var client = new ResponsesClient(new ApiKeyCredential(key), new ResponsesClientOptions
        {
            Endpoint = new Uri("https://api.minimaxi.com/v1"),
            RetryPolicy = new ClientRetryPolicy(0),
            Transport = new HttpClientPipelineTransport(http),
        });
        byte[] png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a9XkAAAAASUVORK5CYII=");
        var options = new CreateResponseOptions
        {
            Model = "MiniMax-M3",
            StoredOutputEnabled = false,
            MaxOutputTokenCount = 128,
        };
        options.InputItems.Add(ResponseItem.CreateUserMessageItem([
            ResponseContentPart.CreateInputTextPart("Briefly describe this tiny image."),
            ResponsesCodingAgent.CreateInputPart(new Microsoft.Extensions.AI.DataContent(png, "image/png")),
        ]));
        try
        {
            var result = await client.CreateResponseAsync(options, cancellation.Token);
            Console.WriteLine($"MiniMax Responses: HTTP {result.GetRawResponse().Status}; requests={transport.Count}.");
            Assert.AreEqual(200, result.GetRawResponse().Status);
        }
        catch (ClientResultException error)
        {
            Assert.Fail($"MiniMax Responses: HTTP {error.Status}; requests={transport.Count}. Response body omitted.");
        }
        catch (HttpRequestException)
        {
            Assert.Fail($"MiniMax Responses transport failure; requests={transport.Count}. Details omitted.");
        }
        catch (OperationCanceledException)
        {
            Assert.Fail($"MiniMax Responses timed out; requests={transport.Count}.");
        }
    }

    private sealed class SingleRequestHandler : DelegatingHandler
    {
        private int _count;
        internal int Count => _count;

        internal SingleRequestHandler() : base(new HttpClientHandler { AllowAutoRedirect = false }) { }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _count) != 1 || request.Method != HttpMethod.Post ||
                request.RequestUri != new Uri("https://api.minimaxi.com/v1/responses"))
            {
                throw new InvalidOperationException("Only one Responses request is permitted.");
            }
            return base.SendAsync(request, cancellationToken);
        }
    }
}
