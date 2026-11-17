using AgentLib.Logging.Sessions;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AgentLib;
using AgentLib.Logging;
using AgentLib.Model;
using Microsoft.Agents.AI;

namespace PrismCodingAgent.CodingShell.Services.Sessions;

internal sealed class FileCodingChatSessionStore : ICodingChatSessionStore
{
    private readonly CopilotChatManager _chatManager;
    private readonly IMainThreadDispatcher _mainThreadDispatcher;
    private readonly FileCopilotChatSessionStore _store;

    public FileCodingChatSessionStore
    (
        string sessionDirectory,
        string logDirectory,
        CopilotChatManager chatManager,
        IMainThreadDispatcher mainThreadDispatcher
    )
    {
        ArgumentNullException.ThrowIfNull(chatManager);
        ArgumentNullException.ThrowIfNull(mainThreadDispatcher);
        _chatManager = chatManager;
        _mainThreadDispatcher = mainThreadDispatcher;
        _store = new FileCopilotChatSessionStore(sessionDirectory, logDirectory);
    }

    public Task<IReadOnlyList<CopilotChatSessionSummary>> ListSessionsAsync
        (CancellationToken cancellationToken = default)
        => _store.ListSessionsAsync(cancellationToken);

    public IAsyncEnumerable<CopilotChatSessionSummary> EnumerateSessionsAsync
        (CancellationToken cancellationToken = default)
        => _store.EnumerateSessionsAsync(cancellationToken);

    public async Task<CopilotChatSession> LoadSessionAsync
    (
        Guid sessionId,
        CancellationToken cancellationToken = default
    )
    {
        CopilotChatSessionPersistenceData persistenceData = await _store
            .LoadSessionAsync(sessionId, cancellationToken)
            .ConfigureAwait(false);
        return await RestoreSessionAsync(persistenceData, persistenceData.SessionId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>从源会话的指定快照恢复具有独立身份的新会话。</summary>
    public async Task<CopilotChatSession> CreateSessionFromSnapshotAsync(CopilotChatSession sourceSession, SessionSnapshotId snapshotId,
        CancellationToken cancellationToken = default)
    {
        CopilotChatSessionPersistenceData persistenceData = await _store
            .LoadSnapshotAsync(sourceSession, snapshotId, cancellationToken)
            .ConfigureAwait(false);
        return await RestoreSessionAsync(persistenceData, Guid.NewGuid(), cancellationToken).ConfigureAwait(false);
    }

    private async Task<CopilotChatSession> RestoreSessionAsync(CopilotChatSessionPersistenceData persistenceData,
        Guid sessionId, CancellationToken cancellationToken)
    {
        var session = new CopilotChatSession(sessionId, persistenceData.StartedTime, _chatManager.ChatLogger)
        {
            MainThreadDispatcher = _mainThreadDispatcher,
            WorkspacePath = persistenceData.WorkspacePath,
            WorkTaskId = persistenceData.WorkTaskId,
            WorkTaskName = persistenceData.WorkTaskName,
            ResponsesSession = persistenceData.ResponsesSession,
        };
        if (!string.IsNullOrWhiteSpace(persistenceData.Title))
        {
            session.SetTitle(persistenceData.Title);
        }

        foreach (CopilotChatEntry entry in persistenceData.Entries)
        {
            await session.AddEntryAsync(entry).ConfigureAwait(false);
        }

        foreach (var item in persistenceData.AdditionalInfo)
        {
            session.AdditionalInfo.Add(item.Key, new JsonSessionAdditionalInfo(item.Value));
        }

        if (persistenceData.AgentSessionState is JsonElement agentSessionState)
        {
            IManualSendMessageContext context = await _chatManager
                .CreateManualSendMessageContextAsync(cancellationToken)
                .ConfigureAwait(false);
            ChatClientAgent agent = await context
                .GetChatClientAgentAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            AgentSession agentSession = await agent
                .DeserializeSessionAsync(agentSessionState, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            session.SetAgentSession(agentSession);
        }

        return session;
    }

    public Task<bool> DeleteSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
        => _store.DeleteSessionAsync(sessionId, cancellationToken);

    public async Task SaveSessionAsync(CopilotChatSession session, CancellationToken cancellationToken = default)
    {
        JsonElement? agentSessionState = await SerializeAgentSessionAsync(session, cancellationToken).ConfigureAwait(false);
        await _store.SaveSessionAsync(session, agentSessionState, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>保存包含代理状态的会话快照。</summary>
    public async Task SaveSnapshotAsync(CopilotChatSession session, SessionSnapshotId snapshotId,
        CancellationToken cancellationToken = default)
    {
        JsonElement? agentSessionState = await SerializeAgentSessionAsync(session, cancellationToken).ConfigureAwait(false);
        await _store.SaveSnapshotAsync(session, snapshotId, agentSessionState, cancellationToken).ConfigureAwait(false);
    }

    private async Task<JsonElement?> SerializeAgentSessionAsync(CopilotChatSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        JsonElement? agentSessionState = null;
        if (session.AgentSession is AgentSession agentSession)
        {
            IManualSendMessageContext context = await _chatManager
                .CreateManualSendMessageContextAsync(cancellationToken)
                .ConfigureAwait(false);
            ChatClientAgent agent = await context
                .GetChatClientAgentAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            JsonElement state = await agent
                .SerializeSessionAsync(agentSession, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            agentSessionState = state.Clone();
        }

        return agentSessionState;
    }
}