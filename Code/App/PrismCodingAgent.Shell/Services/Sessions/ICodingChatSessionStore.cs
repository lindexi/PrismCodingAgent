using AgentLib.Logging.Sessions;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AgentLib.Logging;
using AgentLib.Model;

namespace PrismCodingAgent.CodingShell.Services.Sessions;

internal interface ICodingChatSessionStore
{
    Task<IReadOnlyList<CopilotChatSessionSummary>> ListSessionsAsync(CancellationToken cancellationToken = default);

    async IAsyncEnumerable<CopilotChatSessionSummary> EnumerateSessionsAsync
    (
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default
    )
    {
        foreach (var summary in await ListSessionsAsync(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return summary;
        }
    }

    Task<CopilotChatSession> LoadSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>从源会话的指定快照创建独立会话。</summary>
    Task<CopilotChatSession> CreateSessionFromSnapshotAsync(CopilotChatSession sourceSession, SessionSnapshotId snapshotId,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task SaveSessionAsync(CopilotChatSession session, CancellationToken cancellationToken = default);

    Task SaveSnapshotAsync(CopilotChatSession session, SessionSnapshotId snapshotId, CancellationToken cancellationToken = default);
}