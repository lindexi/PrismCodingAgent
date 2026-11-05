using System;
using System.Globalization;
using AgentLib.Logging.Sessions;
using PrismCodingAgent.CodingShell.Localizations;

namespace PrismCodingAgent.CodingShell.ViewModels;

/// <summary>
/// 表示历史会话摘要及标题编辑状态。
/// </summary>
public sealed class SessionItemViewModel : ViewModelBase
{
    private string _editedTitle;
    private bool _isEditing;
    private bool _isCurrent;

    /// <summary>
    /// 获取此条目是否为当前聊天会话，与列表焦点或选中状态无关。
    /// </summary>
    public bool IsCurrent
    {
        get => _isCurrent;
        internal set => SetField(ref _isCurrent, value);
    }

    internal SessionItemViewModel(CopilotChatSessionSummary summary)
    {
        SessionId = summary.SessionId;
        Title = summary.Title;
        _editedTitle = Title;
        WorkspacePath = summary.WorkspacePath;
        WorkTaskId = summary.WorkTaskId;
        WorkTaskName = summary.WorkTaskName;
        StartedTime = summary.StartedTime;
        MessageCount = summary.MessageCount;
    }

    public Guid SessionId { get; }
    public string Title { get; private set; }
    public string? WorkspacePath { get; private set; }
    public Guid? WorkTaskId { get; private set; }
    public string? WorkTaskName { get; private set; }

    public string WorkTaskText => WorkTaskId is Guid workTaskId
        ? $"{WorkTaskName ?? Lang.Current.UnnamedTask} ({workTaskId})"
        : WorkTaskName ?? string.Empty;

    public DateTimeOffset StartedTime { get; }
    public int? MessageCount { get; private set; }

    internal void Update(CopilotChatSessionSummary summary)
    {
        if (Title != summary.Title)
        {
            Title = summary.Title;
            OnPropertyChanged(nameof(Title));
            if (!IsEditing) EditedTitle = Title;
        }

        if (WorkspacePath != summary.WorkspacePath)
        {
            WorkspacePath = summary.WorkspacePath;
            OnPropertyChanged(nameof(WorkspacePath));
        }

        if (WorkTaskId != summary.WorkTaskId || WorkTaskName != summary.WorkTaskName)
        {
            WorkTaskId = summary.WorkTaskId;
            WorkTaskName = summary.WorkTaskName;
            OnPropertyChanged(nameof(WorkTaskId));
            OnPropertyChanged(nameof(WorkTaskName));
            OnPropertyChanged(nameof(WorkTaskText));
        }

        if (MessageCount != summary.MessageCount)
        {
            MessageCount = summary.MessageCount;
            OnPropertyChanged(nameof(MessageCount));
            OnPropertyChanged(nameof(Subtitle));
        }
    }

    public string EditedTitle
    {
        get => _editedTitle;
        set => SetField(ref _editedTitle, value);
    }

    public bool IsEditing
    {
        get => _isEditing;
        set => SetField(ref _isEditing, value);
    }

    public string Subtitle => Localizations.Lang.Format
        (Lang.Current.SessionSubtitle, StartedTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture));
}