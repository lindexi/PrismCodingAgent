using AgentLib.Coding.Prompts;
using AgentLib.Coding.Tools;
using AgentLib.Coding.Tools.ExecuteCommands;
using AgentLib.Core.AgentApiManagers.LanguageModelProviders;
using AgentLib.Model;
using Microsoft.Extensions.AI;
using OpenAI.Responses;

#pragma warning disable OPENAI001

namespace AgentLib.Coding.Responses;

/// <summary>
/// 使用原生 Responses 流式协议和共享工作区工具执行编程任务。
/// </summary>
public sealed class ResponsesCodingAgent
{
    private readonly CodingAgent _codingAgent;
    private readonly string? _copilotInstructionsPath;

    /// <summary>
    /// 创建执行引擎；共享工作区资源的生命周期由宿主负责。
    /// </summary>
    public ResponsesCodingAgent(CodingAgent codingAgent, string? copilotInstructionsPath = null)
    {
        ArgumentNullException.ThrowIfNull(codingAgent);
        _codingAgent = codingAgent;
        _copilotInstructionsPath = copilotInstructionsPath;
    }

    /// <summary>使用同一会话的工具上下文和 Responses 历史启动运行。</summary>
    public async Task<ICodingAgentRunResult> RunAsync(IManualSendMessageContext context, CodingToolContext toolContext,
        IReadOnlyList<AIContent> contents, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(toolContext);
        if (!ReferenceEquals(context.Session, toolContext.Session))
        {
            throw new ArgumentException("编程运行与工具上下文必须使用同一会话。", nameof(toolContext));
        }

        ArgumentNullException.ThrowIfNull(contents);
        if (contents.Count == 0)
        {
            throw new ArgumentException("编程任务内容不能为空。", nameof(contents));
        }

        var runContext = new CodingRunContext(toolContext, context.ChatClient, context.AssistantChatMessage)
            .NormalizeWorkspacePath();
        CodingChatRunOptions runOptions = runContext.Options;
        var input = ResponseItem.CreateUserMessageItem(contents.Select(CreateInputPart));
        var prompts = await CodingPromptProvider.BuildAsync(_copilotInstructionsPath, runOptions, cancellationToken).ConfigureAwait(false);
        var clientContext = await ((IResponsesClientProvider)context.LanguageModel)
            .GetResponsesClientAsync().ConfigureAwait(false);
        var workspaceCache = await _codingAgent.GetWorkspaceCacheAsync(runContext, cancellationToken).ConfigureAwait(false);
        var workspaceContext = _codingAgent.CreateRunWorkspaceContext(workspaceCache, runContext);
        var runner = new ResponsesCodingAgentRunner
        {
            MessageContext = context,
            Contents = contents,
            Conversation = runContext.Session.ResponsesSession,
            Input = input,
            Instructions = string.Join(Environment.NewLine + Environment.NewLine, prompts),
            ClientContext = clientContext,
            Workspace = workspaceContext.WithTool(
                ExecuteCommandTool.CreateRegistration(runContext)),
            CancellationToken = cancellationToken,
            ReasoningEffort = runOptions.ReasoningEffort,
            EnableAutomaticCompression = runOptions.EnableAutomaticCompression,
        };
        return new ResponsesCodingAgentRunResult(runner);
    }

    /// <summary>
    /// 修复未完成的函数调用历史后通过服务端原生 Compact 压缩；无历史返回 false，失败或取消不应用压缩且不回退。
    /// </summary>
    public async Task<bool> TryCompactConversationAsync(ILanguageModel model, CopilotResponsesSession conversation,
        string? additionalInstructions = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(conversation);
        cancellationToken.ThrowIfCancellationRequested();
        conversation.RepairToolCallHistory();
        if (conversation.Items.Count == 0) return false;
        var prompts = await CodingPromptProvider.BuildAsync(_copilotInstructionsPath, CodingChatRunOptions.Default, cancellationToken).ConfigureAwait(false);
        string instructions = string.Join(Environment.NewLine + Environment.NewLine, prompts);
        if (!string.IsNullOrWhiteSpace(additionalInstructions)) instructions += Environment.NewLine + additionalInstructions;
        var clientContext = await ((IResponsesClientProvider)model).GetResponsesClientAsync().ConfigureAwait(false);
        return await TryCompactConversationAsync(clientContext, conversation,
            instructions, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<bool> TryCompactConversationAsync(ResponsesClientContext clientContext,
        CopilotResponsesSession conversation, string instructions, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (conversation.Items.Count == 0) return false;
        using var content = System.ClientModel.BinaryContent.Create(BinaryData.FromObjectAsJson(new
        {
            model = clientContext.ModelId,
            input = conversation.Items.Select(item => System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
                System.ClientModel.Primitives.ModelReaderWriter.Write(item).ToString())).ToArray(),
            instructions,
        }));
        var result = await clientContext.Client.CompactResponseAsync(content, "application/json",
            new System.ClientModel.Primitives.RequestOptions { CancellationToken = cancellationToken }).ConfigureAwait(false);
        using var document = System.Text.Json.JsonDocument.Parse(result.GetRawResponse().Content);
        var output = document.RootElement.GetProperty("output").EnumerateArray()
            .Select(item => System.ClientModel.Primitives.ModelReaderWriter.Read<ResponseItem>(BinaryData.FromString(item.GetRawText()))
                ?? throw new System.IO.InvalidDataException("Compact 返回了空的原生 Item。"))
            .ToArray();

        cancellationToken.ThrowIfCancellationRequested();
        conversation.ApplyCompaction(output);
        return true;
    }

    internal static ResponseContentPart CreateInputPart(AIContent content) => content switch
    {
        TextContent text => ResponseContentPart.CreateInputTextPart(text.Text),
        DataContent image when image.MediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) =>
            ResponseContentPart.CreateInputImagePart(new BinaryData(image.Data, image.MediaType)),
        UriContent image when image.MediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) =>
            ResponseContentPart.CreateInputImagePart(image.Uri),
        _ => throw new NotSupportedException($"Responses 编程输入不支持 {content.GetType().Name}。"),
    };
}
