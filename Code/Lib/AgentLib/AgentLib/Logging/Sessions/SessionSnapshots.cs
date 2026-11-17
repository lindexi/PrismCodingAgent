using System.IO.Compression;
using System.Text.Json;
using AgentLib.Logging.Sessions.Codecs;
using AgentLib.Model;

namespace AgentLib.Logging.Sessions;

/// <summary>以独立 ZIP 资源保存主模型状态，并维护快照索引。</summary>
public sealed class SessionSnapshots : ISessionAdditionalInfo
{
    private const string Key = "SessionSnapshots";
    private readonly Dictionary<string, string> _paths = new(StringComparer.Ordinal);

    /// <summary>获取会话的快照索引。</summary>
    public static SessionSnapshots Get(CopilotChatSession session)
    {
        session.AdditionalInfo.TryGetValue(Key, out ISessionAdditionalInfo? value);
        if (value is SessionSnapshots snapshots) return snapshots;
        snapshots = new SessionSnapshots();
        if (value is JsonSessionAdditionalInfo json)
        {
            foreach (JsonProperty item in json.Value.EnumerateObject())
                snapshots._paths.Add(item.Name, item.Value.GetString()!);
        }
        session.AdditionalInfo.Set(Key, snapshots);
        return snapshots;
    }

    /// <summary>保存主模型状态，同一快照 ID 覆盖写入。</summary>
    public async Task SaveSnapshotAsync(CopilotChatSession session, SessionSnapshotId snapshotId,
        JsonElement? agentSessionState, CancellationToken cancellationToken = default)
    {
        string path = $"SessionSnapshots/{snapshotId.ToFileName()}.zip";
        using (Stream stream = session.AdditionalInfo.Storage.OpenWrite(path))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            if (agentSessionState is { } state)
            {
                using Stream entry = archive.CreateEntry("AgentSession.json").Open();
                await JsonSerializer.SerializeAsync(entry, state, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            if (session.ExistingResponsesSession is { } responses)
            {
                using Stream entry = archive.CreateEntry("ResponsesSession.json").Open();
                using var writer = new Utf8JsonWriter(entry);
                writer.WriteStartArray();
                foreach (var item in responses.Items)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    using JsonDocument document = JsonDocument.Parse(System.ClientModel.Primitives.ModelReaderWriter.Write(item).ToString());
                    document.RootElement.WriteTo(writer);
                }
                writer.WriteEndArray();
                await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        _paths[snapshotId.ToString()] = path;
    }

    /// <summary>读取快照中的 Chat 与 Responses 原生状态。</summary>
    public async Task<(JsonElement? AgentState, CopilotResponsesSession? Responses)> ReadSnapshotAsync(CopilotChatSession sourceSession, SessionSnapshotId snapshotId,
        CancellationToken cancellationToken = default)
    {
        using Stream stream = sourceSession.AdditionalInfo.Storage.OpenRead(_paths[snapshotId.ToString()]);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        JsonElement? agentState = null;
        CopilotResponsesSession? responses = null;
        if (archive.GetEntry("AgentSession.json") is { } agentEntry)
        {
            using Stream entry = agentEntry.Open();
            using JsonDocument document = await JsonDocument.ParseAsync(entry, cancellationToken: cancellationToken).ConfigureAwait(false);
            agentState = document.RootElement.Clone();
        }
        if (archive.GetEntry("ResponsesSession.json") is { } responsesEntry)
        {
            using Stream entry = responsesEntry.Open();
            using JsonDocument document = await JsonDocument.ParseAsync(entry, cancellationToken: cancellationToken).ConfigureAwait(false);
            responses = new CopilotResponsesSession();
            foreach (JsonElement item in document.RootElement.EnumerateArray())
                responses.AppendItem(ResponsesSessionJsonCodec.ReadItem(item.GetRawText()));
        }
        return (agentState, responses);
    }

    /// <inheritdoc />
    public ValueTask<JsonElement> SerializeAsync(ISessionStorage storage, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new(JsonSerializer.SerializeToElement(_paths));
    }
}
