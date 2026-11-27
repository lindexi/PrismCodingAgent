using System.ComponentModel;
using AgentLib.Coding.SubAgents;
using AgentLib.Model;
using AgentLib.Tools;
using Microsoft.Extensions.AI;

namespace AgentLib.Coding.Tools;

internal sealed class CodingSubAgentTools
(
    CodingAgent agent,
    CodingRunContext parentContext,
    CodingWorkspaceCache? workspaceCache,
    IReadOnlyList<string> prompts
)
{
    internal ToolRegistration CreateRegistration() => new(
        AIFunctionFactory.Create(InvokeCodingSubAgentAsync, "InvokeCodingSubAgent"), CreatePresentation);

    private static ToolCallPresentation CreatePresentation(IDictionary<string, object?> arguments) =>
        new(ToolCallPresentationFactory.GetString(arguments, "name"), null);

    [Description("执行编程任务并返回提交的结果。")]
    private async Task<string> InvokeCodingSubAgentAsync
    (
        [Description("子智能体名称。使用已有名称继续其上下文，使用新名称开始独立对话。")]
        string? name,
        [Description("本次编程任务提示词。")] string prompt,
        [Description("提供给编程子智能体的图片文件路径列表。可以传绝对路径；相对路径以代码工作区为基准。")]
        string[]? imageFilePaths = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        FunctionCallContent callContent = (FunctionInvokingChatClient.CurrentContext
            ?? throw new InvalidOperationException("编程委托必须在模型工具调用中执行。")).CallContent;
        List<AIContent> contents = [new TextContent(prompt)];
        foreach (string filePath in imageFilePaths ?? [])
        {
            contents.Add(await CodingWorkspaceContentTools.LoadImageContentAsync(
                filePath, parentContext.WorkspacePath, cancellationToken).ConfigureAwait(false));
        }

        ReasoningEffort? effort = null;
        var model = parentContext.ModelResolver?.ResolveModel(CodingModelPurposes.CodeWriting, out effort);
        IChatClient childClient = model is not null
            ? await model.GetChatClientAsync().ConfigureAwait(false)
            : parentContext.ChatClient;
        cancellationToken.ThrowIfCancellationRequested();
        string? modelName = model?.ModelDefinition.ModelName;
        if (string.IsNullOrWhiteSpace(modelName))
        {
            modelName = childClient.GetService<ChatClientMetadata>()?.DefaultModelId ?? model?.ModelDefinition.ModelId;
        }

        var childMessage = CopilotChatMessage.CreateAssistant(string.Empty, isPresetInfo: false);
        await parentContext.InvokeOutputAsync(() =>
        {
            parentContext.Output.ClearPlaceholder();
            parentContext.Output.CreateToolItem(callContent,
                new ToolCallPresentation(name, string.IsNullOrWhiteSpace(modelName) ? null : modelName)).Details = childMessage;
        }).ConfigureAwait(false);
        var submission = new GeneratedTextSubmissionTool();
        var subSession = await CodingSubAgentSessions.GetOrCreateSessionAsync(
            parentContext.Session, childClient, name, cancellationToken).ConfigureAwait(false);

        var option = parentContext.Options with { };
        if (model is not null)
        {
            option.ReasoningEffort = effort;
        }

        CodingToolContext childToolContext = parentContext.ToolContext with
        {
            Session = subSession,
            Options = option,
        };
        var context = new CodingRunContext(childToolContext, childClient, childMessage)
        {
            ModelResolver = parentContext.ModelResolver,
        };
        CodingRunWorkspaceContext childTools = agent.CreateRunWorkspaceContext(workspaceCache, context)
            .WithTool(new ToolRegistration(submission.CreateTool("ReturnOutputToParent", "提交最终结果文本。")));
        IReadOnlyList<string> childPrompts = [.. prompts, "最终结果必须通过 `ReturnOutputToParent` 提交"];
        ICodingAgentRunResult run = await agent.RunAsync(context, contents, childTools,
            childPrompts, cancellationToken).ConfigureAwait(false);
        await run.CompletionTask.ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!submission.HasSubmittedText)
        {
            run = await agent.RunAsync(context, [new TextContent("要求调用 ReturnOutputToParent 方法")],
                childTools, childPrompts, cancellationToken).ConfigureAwait(false);
            await run.CompletionTask.ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }

        if (!submission.HasSubmittedText)
        {
            submission.SubmitText("子智能体没有返回任何内容");
        }

        return submission.SubmittedText ?? string.Empty;
    }

}