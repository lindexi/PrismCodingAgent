namespace AgentLib.Core;

/// <summary>
/// 通用模型用途标识。调用方也可以使用自定义的用途字符串。
/// </summary>
public static class ModelPurposes
{
    // 这些值是稳定的持久化标识；重命名成员不应改变已有配置中的值，因此不使用 nameof。
    /// <summary>文档生成。</summary>
    public const string DocumentGeneration = "DocumentGeneration";

    /// <summary>图片分析。</summary>
    public const string ImageAnalysis = "ImageAnalysis";

    /// <summary>面向用户的自然语言表达。</summary>
    public const string UserCommunication = "UserCommunication";

    /// <summary>独立推理任务。</summary>
    public const string Reasoning = "Reasoning";

    /// <summary>会话标题生成。</summary>
    public const string SessionTitle = "SessionTitle";

    /// <summary>历史压缩。</summary>
    public const string HistoryCompression = "HistoryCompression";
}
