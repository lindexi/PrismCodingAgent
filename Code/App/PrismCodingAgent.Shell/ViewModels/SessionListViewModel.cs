using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AgentLib.Logging;
using PrismCodingAgent.CodingShell.Localizations;
using PrismCodingAgent.CodingShell.Services.WorkTasks;

namespace PrismCodingAgent.CodingShell.ViewModels;

/// <summary>
/// 提供持续加载、搜索和管理历史会话的页面。
/// </summary>
public sealed class SessionListViewModel : ViewModelBase, IDisposable
{
    private readonly CodingWorkTaskController? _workTaskController;
    private readonly NotifyCollectionChangedEventHandler? _sessionsChangedHandler;
    private readonly EventHandler? _stateChangedHandler;
    private string _searchText = string.Empty;
    private string? _errorMessage;
    private bool _isLoading;
    private bool _hasLoaded;
    private readonly CancellationTokenSource _loadCancellation = new();
    private readonly System.Collections.Generic.Dictionary<Guid, SessionItemViewModel> _items = [];
    private bool _isOperating;
    private readonly SimpleAsyncCommand _createNewSessionCommand;
    private readonly SimpleAsyncCommand<SessionItemViewModel> _deleteSessionCommand;
    private readonly SimpleAsyncCommand<SessionItemViewModel> _saveTitleCommand;

    /// <summary>
    /// 初始化空页面。
    /// </summary>
    public SessionListViewModel()
    {
        _createNewSessionCommand = new SimpleAsyncCommand(() => RunOperationAsync(async () =>
        {
            if (_workTaskController is null) return;
            await _workTaskController.CreateNewSessionAsync();
        }), () => CanChangeSession);
        _deleteSessionCommand = new SimpleAsyncCommand<SessionItemViewModel>(item => RunOperationAsync(async () =>
        {
            if (_workTaskController is not null && item is not null)
                await _workTaskController.DeleteSessionAsync(item.SessionId);
        }), CanExecute);
        _saveTitleCommand = new SimpleAsyncCommand<SessionItemViewModel>(item => RunOperationAsync(async () =>
        {
            if (_workTaskController is not null && item is not null && !string.IsNullOrWhiteSpace(item.EditedTitle))
            {
                await _workTaskController.RenameSessionAsync(item.SessionId, item.EditedTitle);
                item.IsEditing = false;
            }
        }), CanExecute);
        EditTitleCommand = new SimpleCommand<SessionItemViewModel>(item =>
        {
            if (item is null) return;
            item.EditedTitle = item.Title;
            item.IsEditing = true;
        }, CanExecute);
        CancelEditCommand = new SimpleCommand<SessionItemViewModel>(item =>
        {
            if (item is not null) item.IsEditing = false;
        });
        ReloadCommand = new SimpleAsyncCommand(() => LoadCoreAsync(), () => !IsLoading);
    }

    internal SessionListViewModel(CodingWorkTaskController workTaskController) : this()
    {
        ArgumentNullException.ThrowIfNull(workTaskController);
        _workTaskController = workTaskController;
        _sessionsChangedHandler = (_, args) =>
        {
            if (args.Action == NotifyCollectionChangedAction.Add && args.NewItems is { } added
                && args.NewStartingIndex == workTaskController.Sessions.Count - added.Count)
            {
                foreach (CopilotChatSessionSummary summary in added)
                {
                    var item = new SessionItemViewModel(summary)
                    {
                        IsCurrent = summary.SessionId == workTaskController.SelectedSessionId,
                    };
                    _items.Add(summary.SessionId, item);
                    if (MatchesSearch(summary, SearchText.Trim())) Sessions.Add(item);
                }
                OnPropertyChanged(nameof(IsEmpty));
            }
            else if (args.Action == NotifyCollectionChangedAction.Remove && args.OldItems is { } removed)
            {
                foreach (CopilotChatSessionSummary summary in removed)
                {
                    if (_items.Remove(summary.SessionId, out var item)) Sessions.Remove(item);
                }
                UpdateState();
            }
            else if (args.Action == NotifyCollectionChangedAction.Replace
                && args.NewItems is { Count: 1 } && args.OldItems is { Count: 1 }
                && args.NewItems[0] is CopilotChatSessionSummary updated
                && args.OldItems[0] is CopilotChatSessionSummary previous
                && updated.SessionId == previous.SessionId)
            {
                _items[updated.SessionId].Update(updated);
                if (MatchesSearch(updated, SearchText.Trim()) != MatchesSearch(previous, SearchText.Trim())) Refresh();
            }
            else Refresh();
        };
        _stateChangedHandler = (_, _) => UpdateState();
        workTaskController.Sessions.CollectionChanged += _sessionsChangedHandler;
        workTaskController.StateChanged += _stateChangedHandler;
        Refresh();
    }

    public ObservableCollection<SessionItemViewModel> Sessions { get; } = [];
    public bool IsEmpty => !IsLoading && Sessions.Count == 0;
    public bool CanChangeSession => (_workTaskController?.CanChangeSession ?? false) && !_isOperating;
    public SessionItemViewModel? SelectedSession => Sessions.FirstOrDefault(item => item.SessionId == _workTaskController?.SelectedSessionId);
    public string SearchText
    {
        get => _searchText;
        set { if (SetField(ref _searchText, value)) Refresh(); }
    }
    public bool IsLoading
    {
        get => _isLoading;
        private set { if (SetField(ref _isLoading, value)) UpdateState(); }
    }
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetField(ref _errorMessage, value);
    }
    public ICommand CreateNewSessionCommand => _createNewSessionCommand;
    public ICommand DeleteSessionCommand => _deleteSessionCommand;
    public ICommand SaveTitleCommand => _saveTitleCommand;
    public ICommand EditTitleCommand { get; }
    public ICommand CancelEditCommand { get; }
    public ICommand ReloadCommand { get; }

    /// <summary>
    /// 进入页面时在后台读取历史摘要。
    /// </summary>
    public Task LoadAsync(CancellationToken cancellationToken = default) => _hasLoaded ? Task.CompletedTask : LoadCoreAsync(cancellationToken);

    private async Task LoadCoreAsync(CancellationToken cancellationToken = default)
    {
        if (_workTaskController is null || IsLoading || _loadCancellation.IsCancellationRequested) return;
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _loadCancellation.Token);
        IsLoading = true;
        _hasLoaded = false;
        ErrorMessage = null;
        try
        {
            await _workTaskController.InitializeAsync(linkedCancellation.Token);
            _hasLoaded = true;
        }
        catch (OperationCanceledException) when (linkedCancellation.IsCancellationRequested)
        {
            _hasLoaded = false;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or System.Text.Json.JsonException or System.Xml.XmlException)
        {
            Trace.TraceError("[SessionListViewModel][Load] Exception={0}", exception);
            ErrorMessage = Lang.Current.HistoryLoadFailed;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task RunOperationAsync(Func<Task> operation)
    {
        _isOperating = true;
        ErrorMessage = null;
        UpdateState();
        try
        {
            await operation();
            Refresh();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or System.Text.Json.JsonException or System.Xml.XmlException)
        {
            Trace.TraceError("[SessionListViewModel][Operation] Exception={0}", exception);
            ErrorMessage = Lang.Current.HistoryOperationFailed;
        }
        finally
        {
            _isOperating = false;
            UpdateState();
        }
    }

    internal async Task<bool> OpenSessionAsync(SessionItemViewModel? item)
    {
        if (_workTaskController is null || item is null || !CanChangeSession)
        {
            return false;
        }

        bool opened = false;
        await RunOperationAsync(async () =>
        {
            await _workTaskController.OpenSessionAsync(item.SessionId);
            opened = true;
        });
        return opened;
    }

    private bool CanExecute(SessionItemViewModel? item) => item is not null && CanChangeSession;

    private void Refresh()
    {
        if (_workTaskController is null) return;
        string query = SearchText.Trim();
        var activeIds = _workTaskController.Sessions.Select(summary => summary.SessionId).ToHashSet();
        foreach (Guid id in _items.Keys.Where(id => !activeIds.Contains(id)).ToArray()) _items.Remove(id);
        var visible = new System.Collections.Generic.List<SessionItemViewModel>();
        foreach (CopilotChatSessionSummary summary in _workTaskController.Sessions)
        {
            if (!_items.TryGetValue(summary.SessionId, out var item))
            {
                item = new SessionItemViewModel(summary);
                _items.Add(summary.SessionId, item);
            }
            else
            {
                item.Update(summary);
            }
            if (MatchesSearch(summary, query))
            {
                visible.Add(item);
            }
        }
        if (!Sessions.SequenceEqual(visible))
        {
            Sessions.Clear();
            foreach (var item in visible) Sessions.Add(item);
        }
        UpdateState();
    }

    private static bool MatchesSearch(CopilotChatSessionSummary summary, string query) => query.Length == 0
        || summary.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
        || (summary.WorkspacePath?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
        || (summary.WorkTaskName?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
        || (summary.WorkTaskId?.ToString().Contains(query, StringComparison.OrdinalIgnoreCase) ?? false);

    /// <inheritdoc />
    public void Dispose()
    {
        _loadCancellation.Cancel();
        if (_workTaskController is null) return;
        if (_sessionsChangedHandler is not null)
        {
            _workTaskController.Sessions.CollectionChanged -= _sessionsChangedHandler;
        }

        if (_stateChangedHandler is not null)
        {
            _workTaskController.StateChanged -= _stateChangedHandler;
        }
    }

    private void UpdateState()
    {
        foreach (SessionItemViewModel item in _items.Values)
        {
            item.IsCurrent = item.SessionId == _workTaskController?.SelectedSessionId;
        }
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(CanChangeSession));
        OnPropertyChanged(nameof(SelectedSession));
        _createNewSessionCommand.RaiseCanExecuteChanged();
        _deleteSessionCommand.RaiseCanExecuteChanged();
        _saveTitleCommand.RaiseCanExecuteChanged();
        (EditTitleCommand as SimpleCommand<SessionItemViewModel>)?.RaiseCanExecuteChanged();
        (ReloadCommand as SimpleAsyncCommand)?.RaiseCanExecuteChanged();
    }
}

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
    public string EditedTitle { get => _editedTitle; set => SetField(ref _editedTitle, value); }
    public bool IsEditing { get => _isEditing; set => SetField(ref _isEditing, value); }
    public string Subtitle => Localizations.Lang.Format(Lang.Current.SessionSubtitle, StartedTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture));
}
