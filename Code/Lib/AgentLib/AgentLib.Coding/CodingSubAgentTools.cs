using System.ComponentModel;
using AgentLib.Model;
using AgentLib.Tools;
using Microsoft.Extensions.AI;

namespace AgentLib.Coding;

internal sealed class CodingSubAgentTools
(
    CodingAgent agent,
    CodingRunContext parentContext,
    CodingRunWorkspaceContext workspaceContext,
    IReadOnlyList<string> prompts,
    CodingChatRunOptions options
)
{
    private const string SessionsKey = "AgentLib.Coding.SubAgentSessions";

    internal ToolRegistration CreateRegistration() => new(
        AIFunctionFactory.Create(InvokeCodingSubAgent), CreatePresentation);

    private static ToolCallPresentation CreatePresentation(IDictionary<string, object?> arguments) =>
        new(ToolCallPresentationFactory.GetString(arguments, "name"), null);

    [Description("执行编程任务并返回提交的结果。")]
    private async Task<string> InvokeCodingSubAgent
    (
        [Description("子智能体名称。使用已有名称继续其上下文，使用新名称开始独立对话。")]
        string? name,
        [Description("本次编程任务提示词。")] string prompt,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        FunctionCallContent callContent = (FunctionInvokingChatClient.CurrentContext
            ?? throw new InvalidOperationException("编程委托必须在模型工具调用中执行。")).CallContent;
        var childMessage = CopilotChatMessage.CreateAssistant(string.Empty, isPresetInfo: false);
        await parentContext.InvokeOutputAsync(() =>
        {
            parentContext.Output.ClearPlaceholder();
            parentContext.Output.CreateToolItem(callContent, new ToolCallPresentation(name, null)).Details = childMessage;
        }).ConfigureAwait(false);
        var submission = new GeneratedTextSubmissionTool();
        CodingRunWorkspaceContext childTools = workspaceContext.WithTool(new ToolRegistration(
            submission.CreateTool("ReturnOutputToParent", "提交最终结果文本。")));

        var subSession = GetOrCreateSession(name);

        var context = new CodingRunContext(parentContext.ChatClient,
            childMessage, subSession);
        IReadOnlyList<string> childPrompts = [.. prompts, "最终结果必须通过 `ReturnOutputToParent` 提交"];
        ICodingAgentRunResult run = await agent.RunAsync(context, [new TextContent(prompt)], childTools,
            childPrompts, options, cancellationToken).ConfigureAwait(false);
        await run.CompletionTask.ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!submission.HasSubmittedText)
        {
            run = await agent.RunAsync(context, [new TextContent("要求调用 ReturnOutputToParent 方法")],
                childTools, childPrompts, options, cancellationToken).ConfigureAwait(false);
            await run.CompletionTask.ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }

        if (!submission.HasSubmittedText)
        {
            submission.SubmitText("子智能体没有返回任何内容");
        }

        return submission.SubmittedText ?? string.Empty;
    }

    private CopilotChatSession GetOrCreateSession(string? name)
    {
        CopilotChatSession? subSession;
        if (string.IsNullOrWhiteSpace(name))
        {
            subSession = parentContext.Session.CreateSubSession();
        }
        else
        {
            if (!parentContext.Session.AdditionalInfo.TryGetValue(SessionsKey, out object? value))
            {
                value = new Dictionary<string, CopilotChatSession>(StringComparer.Ordinal);
                parentContext.Session.AdditionalInfo.Add(SessionsKey, value);
            }

            var sessions = (Dictionary<string, CopilotChatSession>)value;
            if (!sessions.TryGetValue(name, out subSession))
            {
                subSession = parentContext.Session.CreateSubSession();
                sessions.Add(name, subSession);
            }
        }

        return subSession;
    }
}