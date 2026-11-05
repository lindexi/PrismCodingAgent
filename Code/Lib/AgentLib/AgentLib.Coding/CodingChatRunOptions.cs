using AgentLib.Model;
using Microsoft.Extensions.AI;

namespace AgentLib.Coding;

/// <summary>
/// 定义可绑定的编程任务运行设置，不承载执行引擎选择或协议历史。
/// </summary>
public sealed class CodingChatRunOptions : NotifyBase
{
    private bool _enableAutomaticCompression = true;
    private bool _enableDotNetRun;
    private ReasoningEffort? _reasoningEffort;
    private string _languageTag = System.Globalization.CultureInfo.CurrentUICulture.Name;

    /// <summary>获取或设置是否自动压缩对话历史。</summary>
    public bool EnableAutomaticCompression
    {
        get => _enableAutomaticCompression;
        set => SetField(ref _enableAutomaticCompression, value);
    }

    /// <summary>获取或设置是否提供 <c>dotnet run</c> 工具。</summary>
    public bool EnableDotNetRun
    {
        get => _enableDotNetRun;
        set => SetField(ref _enableDotNetRun, value);
    }

    /// <summary>获取或设置模型思考强度。</summary>
    public ReasoningEffort? ReasoningEffort
    {
        get => _reasoningEffort;
        set => SetField(ref _reasoningEffort, value);
    }

    /// <summary>获取或设置运行采用的应用语言标签。</summary>
    public string LanguageTag
    {
        get => _languageTag;
        set => SetField(ref _languageTag, value);
    }

    /// <summary>获取一份独立的默认运行设置。</summary>
    public static CodingChatRunOptions Default => new();

}
