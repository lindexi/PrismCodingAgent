namespace AgentLib.Coding;

/// <summary>
/// 编程任务的模型用途标识。
/// </summary>
public static class CodingModelPurposes
{
    // 这些值是稳定的持久化标识；重命名成员不应改变已有配置中的值，因此不使用 nameof。
    /// <summary>代码设计。</summary>
    public const string CodeDesign = "CodeDesign";

    /// <summary>代码审查。</summary>
    public const string CodeReview = "CodeReview";

    /// <summary>代码探索。</summary>
    public const string CodeExploration = "CodeExploration";
}
