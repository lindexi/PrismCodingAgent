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

    /// <summary>获取指定用途或模型的聊天客户端；未解析到模型时使用主模型。</summary>
    Task<IChatClient> GetChatClientAsync(string modelSpecifier) =>
        (ResolveModel(modelSpecifier) ?? PrimaryModel).GetChatClientAsync();
}
