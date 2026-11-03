using Microsoft.Extensions.AI;

namespace AgentLib.Coding;

/// <summary>
/// 定义编程任务的运行选项，不承载执行引擎选择或协议历史。
/// </summary>
public readonly record struct CodingChatRunOptions
(
    bool EnableAutomaticCompression,
    bool EnableDotNetRun,
    ReasoningEffort? ReasoningEffort
)
{
    /// <summary>获取本次运行采用的应用语言标签。</summary>
    public required string LanguageTag { get; init; }

    /// <summary>获取默认运行选项。</summary>
    public static CodingChatRunOptions Default { get; } = new
    (
        EnableAutomaticCompression: true,
        EnableDotNetRun: false,
        ReasoningEffort: null
    )
    {
        LanguageTag = System.Globalization.CultureInfo.CurrentUICulture.Name,
    };
}