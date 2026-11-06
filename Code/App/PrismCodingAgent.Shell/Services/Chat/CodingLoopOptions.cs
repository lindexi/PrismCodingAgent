using System;
using AgentLib.Model;

namespace PrismCodingAgent.CodingShell.Services.Chat;

/// <summary>定义相邻轮次间的模型上下文处理方式。</summary>
public enum LoopConversationMode
{
    /// <summary>压缩已有模型上下文后开始下一轮。</summary>
    Compress,

    /// <summary>清空模型上下文后开始下一轮，保留可见聊天记录。</summary>
    Restart,

    /// <summary>保留已有模型上下文，追加下一轮对话。</summary>
    Append,
}

/// <summary>聊天持有的循环设置，不承载执行状态。</summary>
public sealed class CodingLoopOptions : NotifyBase
{
    private bool _isEnabled;
    private int _maximumIterations;
    private int _maximumDurationMinutes;
    private LoopConversationMode _conversationMode;

    /// <summary>下一次发送是否启动循环。</summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetField(ref _isEnabled, value);
    }

    /// <summary>最大尝试轮数，包含首次及失败尝试；零表示不限。</summary>
    public int MaximumIterations
    {
        get => _maximumIterations;
        set
        {
            if (value < 0) return;
            SetField(ref _maximumIterations, value);
        }
    }

    /// <summary>发送时采用的整次循环时限，单位分钟；零表示不限。</summary>
    public int MaximumDurationMinutes
    {
        get => _maximumDurationMinutes;
        set
        {
            if (value is < 0 or > 71582) return;
            SetField(ref _maximumDurationMinutes, value);
        }
    }

    /// <summary>下一轮开始前采用的上下文处理方式。</summary>
    public LoopConversationMode ConversationMode
    {
        get => _conversationMode;
        set
        {
            if (!Enum.IsDefined(value))
            {
                return;
            }
            SetField(ref _conversationMode, value);
        }
    }
}
