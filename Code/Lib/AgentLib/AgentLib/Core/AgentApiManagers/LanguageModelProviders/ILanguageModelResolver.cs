using Microsoft.Extensions.AI;

namespace AgentLib.Core.AgentApiManagers.LanguageModelProviders;

/// <summary>
/// 根据用途或模型标识查找语言模型。
/// </summary>
public interface ILanguageModelResolver
{
    /// <summary>获取当前主模型。</summary>
    ILanguageModel PrimaryModel { get; }

    /// <summary>解析用途或模型标识，未找到时返回 <see langword="null"/>。</summary>
    ILanguageModel? ResolveModel(string modelSpecifier);

    /// <summary>解析模型及其用途思考强度。直接指定模型或未找到模型时，强度为空。</summary>
    ILanguageModel? ResolveModel(string modelSpecifier, out ReasoningEffort? effort)
    {
        effort = null;
        return ResolveModel(modelSpecifier);
    }

    /// <summary>获取指定用途或模型的聊天客户端，应用用途的思考强度；未解析到模型时使用主模型。</summary>
    async Task<IChatClient> GetChatClientAsync(string modelSpecifier)
    {
        var model = ResolveModel(modelSpecifier, out var effort) ?? PrimaryModel;
        var client = await model.GetChatClientAsync().ConfigureAwait(false);
        return effort is { } configuredEffort
            ? new ConfigureOptionsChatClient(client, options =>
            {
                if (options.Reasoning?.Effort is null)
                    options.Reasoning = new ReasoningOptions { Effort = configuredEffort, Output = options.Reasoning?.Output };
            })
            : client;
    }
}
