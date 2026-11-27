using Microsoft.Extensions.AI;

namespace AgentLib.Core.AgentApiManagers.LanguageModelProviders;

/// <summary>单个用途的模型配置。可以预设思考强度，指定并解析到模型后才会使用。</summary>
/// <param name="ModelSpecifier">模型名称、ID 或 Provider/ModelName；空值表示未指定。</param>
/// <param name="ReasoningEffort">思考强度；空值表示使用模型默认值。</param>
public sealed record PurposeModelConfiguration(string? ModelSpecifier, ReasoningEffort? ReasoningEffort = null);
