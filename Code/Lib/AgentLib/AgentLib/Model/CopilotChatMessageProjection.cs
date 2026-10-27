using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace AgentLib.Model;

/// <summary>增量维护时间线消息引用的只读可观察投影；所有修改均通过源 Entries 进行。</summary>
public sealed class CopilotChatMessageProjection : ReadOnlyObservableCollection<CopilotChatMessage>
{
    private readonly ObservableCollection<CopilotChatEntry> _entries;
    private readonly ObservableCollection<CopilotChatMessage> _messages;

    internal CopilotChatMessageProjection(ObservableCollection<CopilotChatEntry> entries)
        : this(entries, new ObservableCollection<CopilotChatMessage>(entries.OfType<CopilotChatMessage>()))
    {
    }

    private CopilotChatMessageProjection(ObservableCollection<CopilotChatEntry> entries,
        ObservableCollection<CopilotChatMessage> messages) : base(messages)
    {
        _entries = entries;
        _messages = messages;
        _entries.CollectionChanged += OnEntriesChanged;
    }

    private int CountBefore(int sourceIndex)
    {
        int count = 0;
        for (int i = 0; i < sourceIndex; i++)
            if (_entries[i] is CopilotChatMessage) count++;
        return count;
    }

    private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var oldMessage = e.OldItems?[0] as CopilotChatMessage;
        var newMessage = e.NewItems?[0] as CopilotChatMessage;
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Reset:
                if (_messages.Count > 0) _messages.Clear();
                break;
            case NotifyCollectionChangedAction.Move:
                if (newMessage is null) return;
                int newIndex = CountBefore(e.NewStartingIndex);
                // 源已经完成移动，向前移动时需从旧位置前缀中扣除移动项本身。
                bool movedBackward = e.OldStartingIndex > e.NewStartingIndex;
                int oldIndex = CountBefore(e.OldStartingIndex + (movedBackward ? 1 : 0)) - (movedBackward ? 1 : 0);
                if (oldIndex != newIndex) _messages.Move(oldIndex, newIndex);
                break;
            default:
                if (oldMessage is null && newMessage is null) return;
                // 末尾追加只读取缓存消息数，避免流式会话随时间线增长反复扫描。
                int index = e.Action == NotifyCollectionChangedAction.Add && e.NewStartingIndex == _entries.Count - 1
                    ? _messages.Count
                    : CountBefore(e.NewStartingIndex >= 0 ? e.NewStartingIndex : e.OldStartingIndex);
                if (newMessage is null) _messages.RemoveAt(index);
                else if (oldMessage is null) _messages.Insert(index, newMessage);
                else _messages[index] = newMessage;
                break;
        }
    }
}
