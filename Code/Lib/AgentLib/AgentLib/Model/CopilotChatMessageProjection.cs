using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace AgentLib.Model;

/// <summary>直接读取时间线的只读可观察消息投影；所有修改均通过源 Entries 进行。</summary>
public sealed class CopilotChatMessageProjection : IReadOnlyList<CopilotChatMessage>, INotifyCollectionChanged, INotifyPropertyChanged
{
    private readonly ObservableCollection<CopilotChatEntry> _entries;
    private NotifyCollectionChangedEventHandler? _collectionChanged;
    private PropertyChangedEventHandler? _propertyChanged;
    private bool _subscribed;
    private int _observedCount;

    internal CopilotChatMessageProjection(ObservableCollection<CopilotChatEntry> entries) => _entries = entries;

    /// <summary>获取当前消息数。</summary>
    public int Count => CountBefore(_entries.Count);

    /// <summary>按过滤后的索引读取消息。</summary>
    public CopilotChatMessage this[int index]
    {
        get
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
            foreach (var entry in _entries)
                if (entry is CopilotChatMessage message && index-- == 0) return message;
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    /// <summary>转发消息的集合变更，索引为消息投影中的索引。</summary>
    public event NotifyCollectionChangedEventHandler? CollectionChanged
    {
        add { _collectionChanged += value; UpdateSubscription(); }
        remove { _collectionChanged -= value; UpdateSubscription(); }
    }

    /// <summary>通知消息数或索引器变更。</summary>
    public event PropertyChangedEventHandler? PropertyChanged
    {
        add { _propertyChanged += value; UpdateSubscription(); }
        remove { _propertyChanged -= value; UpdateSubscription(); }
    }

    /// <summary>获取指定消息在投影中的索引。</summary>
    public int IndexOf(CopilotChatMessage message)
    {
        int index = 0;
        foreach (var item in this)
        {
            if (ReferenceEquals(item, message)) return index;
            index++;
        }
        return -1;
    }

    /// <summary>判断消息是否在源时间线中。</summary>
    public bool Contains(CopilotChatMessage message) => IndexOf(message) >= 0;

    /// <summary>按时间线顺序枚举消息。</summary>
    public IEnumerator<CopilotChatMessage> GetEnumerator() => _entries.OfType<CopilotChatMessage>().GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private int CountBefore(int sourceIndex)
    {
        int count = 0;
        for (int i = 0; i < sourceIndex; i++)
            if (_entries[i] is CopilotChatMessage) count++;
        return count;
    }

    private void UpdateSubscription()
    {
        bool needed = _collectionChanged is not null || _propertyChanged is not null;
        if (needed == _subscribed) return;
        if (needed)
        {
            _observedCount = Count;
            _entries.CollectionChanged += OnEntriesChanged;
        }
        else _entries.CollectionChanged -= OnEntriesChanged;
        _subscribed = needed;
    }

    private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var oldMessage = e.OldItems?[0] as CopilotChatMessage;
        var newMessage = e.NewItems?[0] as CopilotChatMessage;
        NotifyCollectionChangedEventArgs change;
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Reset:
                if (_observedCount == 0 && Count == 0) return;
                _observedCount = Count;
                change = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);
                break;
            case NotifyCollectionChangedAction.Move:
                if (newMessage is null) return;
                int newIndex = CountBefore(e.NewStartingIndex);
                bool movedBackward = e.OldStartingIndex > e.NewStartingIndex;
                int oldIndex = CountBefore(e.OldStartingIndex + (movedBackward ? 1 : 0)) - (movedBackward ? 1 : 0);
                if (oldIndex == newIndex) return;
                change = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Move, newMessage, newIndex, oldIndex);
                break;
            default:
                if (oldMessage is null && newMessage is null) return;
                // 流式会话通常在末尾追加，避免每条消息都重新扫描整个时间线。
                int index = e.Action == NotifyCollectionChangedAction.Add && e.NewStartingIndex == _entries.Count - 1
                    ? _observedCount
                    : CountBefore(e.NewStartingIndex >= 0 ? e.NewStartingIndex : e.OldStartingIndex);
                _observedCount += (newMessage is null ? 0 : 1) - (oldMessage is null ? 0 : 1);
                change = oldMessage is null
                    ? new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, newMessage, index)
                    : newMessage is null
                        ? new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, oldMessage, index)
                        : new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, newMessage, oldMessage, index);
                break;
        }
        if (change.Action is NotifyCollectionChangedAction.Add or NotifyCollectionChangedAction.Remove or NotifyCollectionChangedAction.Reset)
            _propertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
        _propertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        _collectionChanged?.Invoke(this, change);
    }
}
