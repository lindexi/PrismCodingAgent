using System;
using System.Windows.Input;
using AgentLib.Model;

namespace PrismCodingAgent.AvaloniaShell.ViewModels;

/// <summary>时间线显示项的公共基类。</summary>
public abstract class ChatEntryViewModel : ViewModelBase, IDisposable
{
    /// <summary>条目的显示正文。</summary>
    public abstract string Content { get; }
    /// <inheritdoc />
    public abstract void Dispose();
}

/// <summary>没有作者与消息角色的通知显示项。</summary>
public sealed class NoticeItemViewModel : ChatEntryViewModel
{
    /// <summary>创建通知投影。</summary>
    public NoticeItemViewModel(CopilotChatNotice notice) => Notice = notice;

    internal NoticeItemViewModel(CopilotChatNotice notice, ICommand retryCommand, bool hasContinuation)
        : this(notice)
    {
        RetryCommand = retryCommand;
        _hasContinuation = hasContinuation;
    }

    private bool _hasContinuation;

    /// <summary>所属聊天视图共享的重试命令，以底层通知作为参数。</summary>
    public ICommand? RetryCommand { get; }

    /// <summary>此通知是否关联当前可继续的运行。</summary>
    public bool HasContinuation => _hasContinuation;

    internal void UpdateContinuation(bool hasContinuation)
    {
        if (_hasContinuation == hasContinuation) return;
        _hasContinuation = hasContinuation;
        OnPropertyChanged(nameof(HasContinuation));
    }
    /// <summary>底层通知。</summary>
    public CopilotChatNotice Notice { get; }
    /// <summary>通知正文。</summary>
    public override string Content => Notice.Content;
    /// <summary>创建时间。</summary>
    public string TimeText => Notice.TimeText;
    /// <summary>警告样式。</summary>
    public bool IsWarning => Notice.Level == CopilotChatNoticeLevel.Warning;
    /// <summary>错误样式。</summary>
    public bool IsError => Notice.Level == CopilotChatNoticeLevel.Error;
    /// <inheritdoc />
    public override void Dispose() { }
}
