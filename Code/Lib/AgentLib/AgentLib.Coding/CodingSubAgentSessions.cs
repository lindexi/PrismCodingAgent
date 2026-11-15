using System.Diagnostics;
using System.Text.Json;
using AgentLib.Model;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AgentLib.Coding;

internal sealed class CodingSubAgentSessions : ISessionAdditionalInfo
{
    private const string SessionsKey = "AgentLib.Coding.SubAgentSessions";
    private readonly Dictionary<string, CopilotChatSession> _sessions = new(StringComparer.Ordinal);
    private readonly ChatClientAgent _serializationAgent;

    private CodingSubAgentSessions(IChatClient chatClient)
    {
        _serializationAgent = new ChatClientAgent(chatClient);
    }

    internal static async Task<CopilotChatSession> GetOrCreateSessionAsync(CopilotChatSession parent,
        IChatClient chatClient, string? name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return parent.CreateSubSession();
        }

        CodingSubAgentSessions subAgentSessions;

        if (!parent.AdditionalInfo.TryGetValue(SessionsKey, out ISessionAdditionalInfo? value))
        {
            subAgentSessions = new CodingSubAgentSessions(chatClient);
            parent.AdditionalInfo.Add(SessionsKey, subAgentSessions);
        }

        if (value is JsonSessionAdditionalInfo json)
        {
            var restored = new CodingSubAgentSessions(chatClient);
            foreach (JsonProperty item in json.Value.EnumerateObject())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var session = parent.CreateSubSession();
                if (item.Value.ValueKind != JsonValueKind.Null)
                {
                    ChatClientAgent agent = restored._serializationAgent;
                    var deserializeSession = await agent
                        .DeserializeSessionAsync(item.Value, cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
                    session.SetAgentSession(deserializeSession);
                }

                restored._sessions.Add(item.Name, session);
            }

            cancellationToken.ThrowIfCancellationRequested();
            value = restored;
            parent.AdditionalInfo[SessionsKey] = restored;
            subAgentSessions = restored;
        }
        else if(value is CodingSubAgentSessions existing)
        {
            subAgentSessions = existing;
        }
        else
        {
            // 未知的情况
#if DEBUG
            Debugger.Break();
#endif
            // 简单回退逻辑
            return parent.CreateSubSession();
        }

        return subAgentSessions.GetOrCreate(name, parent);
    }

    private CopilotChatSession GetOrCreate(string name, CopilotChatSession parent)
    {
        if (!_sessions.TryGetValue(name, out CopilotChatSession? session))
        {
            session = parent.CreateSubSession();
            _sessions.Add(name, session);
        }

        return session;
    }

    public async ValueTask<JsonElement> SerializeAsync(CancellationToken cancellationToken = default)
    {
        var states = new Dictionary<string, JsonElement?>(StringComparer.Ordinal);
        foreach (var item in _sessions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var agentSession = item.Value.AgentSession;

            if (agentSession is null)
            {
                continue;
            }

            var serializeSession = await _serializationAgent.SerializeSessionAsync(agentSession, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            states.Add(item.Key, serializeSession);
        }

        return JsonSerializer.SerializeToElement(states);
    }
}
