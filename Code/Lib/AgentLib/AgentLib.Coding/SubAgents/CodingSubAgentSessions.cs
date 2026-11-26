using System.Text.Json;
using AgentLib.Logging;
using AgentLib.Model;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AgentLib.Coding.SubAgents;

internal sealed class CodingSubAgentSessions : ISessionAdditionalInfo
{
    private const string SessionsKey = "AgentLib.Coding.SubAgentSessions";
    private readonly Dictionary<string, CopilotChatSession> _sessions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _paths = new(StringComparer.Ordinal);
    private readonly ChatClientAgent _serializationAgent;

    private CodingSubAgentSessions(IChatClient chatClient)
    {
        _serializationAgent = new ChatClientAgent(chatClient);
    }

    internal static async Task<CopilotChatSession> GetOrCreateSessionAsync(CopilotChatSession parent,
        IChatClient chatClient, string? name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name)) return parent.CreateSubSession();

        List<(string Name, JsonValueKind Kind)>? invalidEntries = null;
        parent.AdditionalInfo.TryGetValue(SessionsKey, out ISessionAdditionalInfo? value);
        if (value is not CodingSubAgentSessions)
        {
            var restored = new CodingSubAgentSessions(chatClient);
            if (value is JsonSessionAdditionalInfo json)
            {
                foreach (JsonProperty item in json.Value.EnumerateObject())
                {
                    if (item.Value.ValueKind == JsonValueKind.String && item.Value.GetString() is { } pathValue)
                        restored._paths.Add(item.Name, pathValue);
                    else
                        (invalidEntries ??= []).Add((item.Name, item.Value.ValueKind));
                }
            }
            parent.AdditionalInfo.Set(SessionsKey, restored);
            value = restored;
        }

        var sessions = (CodingSubAgentSessions)value;
        if (sessions._sessions.TryGetValue(name, out CopilotChatSession? existing)) return existing;

        var session = parent.CreateSubSession();
        if (sessions._paths.TryGetValue(name, out string? path))
        {
            using Stream stream = parent.AdditionalInfo.Storage.OpenRead(path);
            using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            session.SetAgentSession(await sessions._serializationAgent.DeserializeSessionAsync(document.RootElement,
                cancellationToken: cancellationToken).ConfigureAwait(false));
        }
        else
        {
            sessions._paths.Add(name, $"SubAgentSessions/{session.SessionId:N}.json");
        }
        sessions._sessions.Add(name, session);

        if (invalidEntries is not null)
        {
            foreach (var (entryName, kind) in invalidEntries)
            {
                await parent.Logger.TryLogDiagnosticAsync("[CodingSubAgentSessions][Restore]",
                    $"忽略非字符串的子代理会话路径。SessionId={parent.SessionId:N}, IndexKey={SessionsKey}, EntryName={entryName}, JsonValueKind={kind}")
                    .ConfigureAwait(false);
            }
        }
        return session;
    }

    public async ValueTask<JsonElement> SerializeAsync(ISessionStorage storage, CancellationToken cancellationToken = default)
    {
        foreach (var item in _sessions)
        {
            if (item.Value.AgentSession is not { } agentSession) continue;
            JsonElement state = await _serializationAgent.SerializeSessionAsync(agentSession,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            using Stream stream = storage.OpenWrite(_paths[item.Key]);
            await JsonSerializer.SerializeAsync(stream, state, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        return JsonSerializer.SerializeToElement(_paths);
    }
}
