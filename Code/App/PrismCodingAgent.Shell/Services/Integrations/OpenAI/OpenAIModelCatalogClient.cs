using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OpenAI;
using OpenAI.Models;
using PrismCodingAgent.CodingShell.Localizations;

namespace PrismCodingAgent.CodingShell.Services.Integrations.OpenAI;

internal sealed class OpenAIModelCatalogClient : IOpenAIModelCatalogClient
{
    public async Task<OpenAIModelCatalogResult> GetModelIdsAsync
    (
        string endPoint,
        string apiKey,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(endPoint))
        {
            return OpenAIModelCatalogResult.Failure(Lang.Current.CatalogEndpointRequired);
        }

        if (!Uri.TryCreate(endPoint, UriKind.Absolute, out Uri? endpointUri))
        {
            return OpenAIModelCatalogResult.Failure(Lang.Current.CatalogEndpointInvalid);
        }

        var options = new OpenAIClientOptions
        {
            Endpoint = endpointUri,
        };
        var client = new OpenAIClient(new ApiKeyCredential(string.IsNullOrEmpty(apiKey) ? "unused" : apiKey), options);
        OpenAIModelClient modelClient = client.GetOpenAIModelClient();
        ClientResult<OpenAIModelCollection> result;
        try
        {
            result = await modelClient.GetModelsAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ClientResultException exception)
        {
            return await FailureAsync(Lang.Current.CatalogResponseError, exception).ConfigureAwait(false);
        }
        catch (JsonException exception)
        {
            return await FailureAsync(Lang.Current.CatalogJsonInvalid, exception).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            return await FailureAsync(Lang.Current.CatalogConnectionFailed, exception).ConfigureAwait(false);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return OpenAIModelCatalogResult.Failure(Lang.Current.CatalogTimeout);
        }
        catch (IOException exception)
        {
            return await FailureAsync(Lang.Current.CatalogReadFailed, exception).ConfigureAwait(false);
        }
        catch (InvalidOperationException exception)
        {
            return await FailureAsync(Lang.Current.CatalogProtocolInvalid, exception).ConfigureAwait(false);
        }

        var modelIds = new List<string>(result.Value.Count);
        var uniqueModelIds = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < result.Value.Count; index++)
        {
            string modelId = result.Value[index].Id;
            if (string.IsNullOrWhiteSpace(modelId))
            {
                return OpenAIModelCatalogResult.Failure(Localizations.Lang.Format(Lang.Current.CatalogBlankId, index + 1));
            }

            if (!uniqueModelIds.Add(modelId))
            {
                return OpenAIModelCatalogResult.Failure(Localizations.Lang.Format(Lang.Current.CatalogDuplicateId, modelId));
            }

            modelIds.Add(modelId);
        }

        return OpenAIModelCatalogResult.Success(modelIds);
    }

    private static async Task<OpenAIModelCatalogResult> FailureAsync(string message, Exception exception)
    {
        await ((DesktopApplication.Current as App)?.ProcessLogger).TryLogDiagnosticAsync("[OpenAIModelCatalogClient]", "获取 OpenAI 模型列表失败。", exception).ConfigureAwait(false);
        return OpenAIModelCatalogResult.Failure(message);
    }
}