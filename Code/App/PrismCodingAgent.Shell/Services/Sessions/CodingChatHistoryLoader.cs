using AgentLib.Logging.Sessions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using AgentLib.Logging;
using PrismCodingAgent.CodingShell.Infrastructure;
using PrismCodingAgent.CodingShell.Services.WorkTasks;

namespace PrismCodingAgent.CodingShell.Services.Sessions;

internal sealed class CodingChatHistoryLoader
{
    private readonly CodingWorkTaskController _workTaskController;

    public CodingChatHistoryLoader(CodingWorkTaskController workTaskController)
    {
        ArgumentNullException.ThrowIfNull(workTaskController);
        _workTaskController = workTaskController;
    }

    public async Task LoadAsync()
    {
        try
        {
            IReadOnlyList<CopilotChatSessionSummary> summaries = await _workTaskController
                .LoadSessionSummariesAsync()
                .ConfigureAwait(false);
            await UiThreadDispatcher.InvokeAtRenderAsync(
                () => _workTaskController.AddSessionSummaries(summaries));
        }
        catch (Exception exception)
        {
            Trace.TraceError($"CodingChatRoom 历史会话加载失败：{exception}");
        }
    }
}