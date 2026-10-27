using AgentLib.Model;
using Microsoft.Extensions.AI;
using System.Collections.Specialized;
using System.ComponentModel;

namespace AgentLib.Tests;

[TestClass]
public class CopilotChatMessageProjectionTests
{
    [TestMethod]
    public void ProjectionInstanceIsStableAndSharedWithManager()
    {
        var manager = new CopilotChatManager();
        var projection = manager.ChatMessages;
        int originalCount = projection.Count;
        manager.SelectedSession.Entries.Add(Message("message"));
        Assert.AreEqual((true, true, originalCount + 1), (ReferenceEquals(projection, manager.ChatMessages),
            ReferenceEquals(projection, manager.SelectedSession.ChatMessages), projection.Count));
    }

    [TestMethod]
    public void NoticeChangesDoNotNotifyMessages()
    {
        var session = new CopilotChatSession();
        var changes = new List<NotifyCollectionChangedEventArgs>();
        session.ChatMessages.CollectionChanged += (_, e) => changes.Add(e);
        session.Entries.Add(new CopilotChatNotice("first"));
        session.Entries.Add(new CopilotChatNotice("second"));
        session.Entries[0] = new CopilotChatNotice("replacement");
        session.Entries.Move(0, 1);
        session.Entries.RemoveAt(0);
        session.Entries.Clear();
        Assert.IsEmpty(changes);
    }

    [DataTestMethod]
    [DataRow(0, 0)]
    [DataRow(2, 1)]
    [DataRow(4, 2)]
    public void AddUsesFilteredIndex(int sourceIndex, int expectedIndex)
    {
        var session = CreateSession();
        var changes = Observe(session);
        var message = Message("new");
        session.Entries.Insert(sourceIndex, message);
        var change = changes.Single();
        Assert.AreEqual((NotifyCollectionChangedAction.Add, expectedIndex, message),
            (change.Action, change.NewStartingIndex, change.NewItems![0]));
    }

    [TestMethod]
    public void RemoveUsesFilteredIndex()
    {
        var session = CreateSession();
        var message = session.ChatMessages[1];
        var changes = Observe(session);
        session.Entries.RemoveAt(3);
        var change = changes.Single();
        Assert.AreEqual((NotifyCollectionChangedAction.Remove, 1, message),
            (change.Action, change.OldStartingIndex, change.OldItems![0]));
    }

    [DataTestMethod]
    [DataRow(false, true, NotifyCollectionChangedAction.Add)]
    [DataRow(true, false, NotifyCollectionChangedAction.Remove)]
    [DataRow(true, true, NotifyCollectionChangedAction.Replace)]
    public void ReplaceTranslatesEntryTypes(bool oldIsMessage, bool newIsMessage, NotifyCollectionChangedAction action)
    {
        var session = CreateSession();
        session.Entries[2] = oldIsMessage ? Message("old") : new CopilotChatNotice("old");
        var changes = Observe(session);
        session.Entries[2] = newIsMessage ? Message("new") : new CopilotChatNotice("new");
        var change = changes.Single();
        Assert.AreEqual((action, 1), (change.Action, Math.Max(change.OldStartingIndex, change.NewStartingIndex)));
    }

    [DataTestMethod]
    [DataRow(1, 4, 0, 1)]
    [DataRow(3, 0, 1, 0)]
    public void MoveUsesFilteredIndices(int from, int to, int oldIndex, int newIndex)
    {
        var session = CreateSession();
        var message = session.Entries[from];
        var changes = Observe(session);
        session.Entries.Move(from, to);
        var change = changes.Single();
        Assert.AreEqual((NotifyCollectionChangedAction.Move, oldIndex, newIndex, message),
            (change.Action, change.OldStartingIndex, change.NewStartingIndex, change.NewItems![0]));
    }

    [TestMethod]
    public void MovingAcrossOnlyNoticesDoesNotChangeMessages()
    {
        var session = CreateSession();
        var changes = Observe(session);
        session.Entries.Move(1, 2);
        session.Entries.Move(0, 4);
        Assert.IsEmpty(changes);
    }

    [TestMethod]
    public void ClearNotifiesResetAndReadsEmptySource()
    {
        var session = CreateSession();
        var changes = Observe(session);
        session.Entries.Clear();
        Assert.AreEqual((NotifyCollectionChangedAction.Reset, 0), (changes.Single().Action, session.ChatMessages.Count));
    }

    [TestMethod]
    public void ResubscribeReadsChangesMadeWhileUnsubscribed()
    {
        var session = CreateSession();
        var changes = new List<NotifyCollectionChangedEventArgs>();
        NotifyCollectionChangedEventHandler handler = (_, e) => changes.Add(e);
        session.ChatMessages.CollectionChanged += handler;
        session.ChatMessages.CollectionChanged -= handler;
        session.Entries.Clear();
        session.Entries.Add(Message("new"));
        session.ChatMessages.CollectionChanged += handler;
        session.Entries.Clear();
        Assert.AreEqual(NotifyCollectionChangedAction.Reset, changes.Single().Action);
    }

    [TestMethod]
    public void PropertyOnlySubscriberReceivesCountAndIndexerChanges()
    {
        var session = new CopilotChatSession();
        var properties = new List<string?>();
        PropertyChangedEventHandler handler = (_, e) => properties.Add(e.PropertyName);
        session.ChatMessages.PropertyChanged += handler;
        session.Entries.Add(Message("new"));
        session.ChatMessages.PropertyChanged -= handler;
        session.Entries.Clear();
        CollectionAssert.AreEqual(new[] { "Count", "Item[]" }, properties);
    }

    [TestMethod]
    public void SwitchingSessionsNotifiesBindingAndAllowsRebinding()
    {
        var manager = new CopilotChatManager();
        var oldSession = manager.SelectedSession;
        var projection = manager.ChatMessages;
        var changes = new List<NotifyCollectionChangedEventArgs>();
        int bindingChanges = 0;
        NotifyCollectionChangedEventHandler handler = (_, e) => changes.Add(e);
        projection.CollectionChanged += handler;
        manager.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(manager.ChatMessages)) return;
            projection.CollectionChanged -= handler;
            projection = manager.ChatMessages;
            projection.CollectionChanged += handler;
            bindingChanges++;
        };
        var newSession = new CopilotChatSession();
        manager.SelectedSession = newSession;
        manager.SelectedSession = newSession;
        oldSession.Entries.Add(Message("old"));
        newSession.Entries.Add(Message("new"));
        Assert.AreEqual((1, 1, true), (bindingChanges, changes.Count, ReferenceEquals(projection, newSession.ChatMessages)));
    }

    [TestMethod]
    public void ReadingApisUseCurrentEntries()
    {
        var session = CreateSession();
        var projection = session.ChatMessages;
        var message = projection[1];
        session.Entries.RemoveAt(1);
        Assert.AreEqual((1, 0, true, message), (projection.Count, projection.IndexOf(message), projection.Contains(message), projection.Single()));
    }

    [DataTestMethod]
    [DataRow(1, 0, 0, 0)]
    [DataRow(1, 1, 0, 0)]
    [DataRow(1, 2, 0, 0)]
    [DataRow(1, 3, 0, 1)]
    [DataRow(1, 4, 0, 1)]
    [DataRow(3, 0, 1, 0)]
    [DataRow(3, 1, 1, 0)]
    [DataRow(3, 2, 1, 1)]
    [DataRow(3, 3, 1, 1)]
    [DataRow(3, 4, 1, 1)]
    public void MovePreservesFilteredOrderIncludingNoOp(int from, int to, int oldIndex, int newIndex)
    {
        var session = CreateSession();
        var expected = session.ChatMessages.ToList();
        var moved = expected[oldIndex];
        expected.RemoveAt(oldIndex);
        expected.Insert(newIndex, moved);
        Observe(session);
        session.Entries.Move(from, to);
        CollectionAssert.AreEqual(expected, session.ChatMessages.ToList());
    }

    [TestMethod]
    public void RemovingDuplicateReferenceUsesOccurrenceIndex()
    {
        var session = CreateSession();
        var duplicate = session.ChatMessages[0];
        session.Entries.Add(duplicate);
        var changes = Observe(session);
        session.Entries.RemoveAt(5);
        var change = changes.Single();
        Assert.AreEqual((NotifyCollectionChangedAction.Remove, 2, duplicate, 0),
            (change.Action, change.OldStartingIndex, change.OldItems![0], session.ChatMessages.IndexOf(duplicate)));
    }

    [TestMethod]
    public void MovingDuplicateReferenceUsesOccurrenceIndices()
    {
        var session = CreateSession();
        var duplicate = session.ChatMessages[0];
        session.Entries.Add(duplicate);
        var changes = Observe(session);
        session.Entries.Move(5, 0);
        var change = changes.Single();
        Assert.AreEqual((NotifyCollectionChangedAction.Move, 2, 0, duplicate),
            (change.Action, change.OldStartingIndex, change.NewStartingIndex, change.NewItems![0]));
    }

    [TestMethod]
    public void ReplacingDuplicateReferenceUsesOccurrenceIndex()
    {
        var session = CreateSession();
        session.Entries.Add(session.ChatMessages[0]);
        var replacement = Message("replacement");
        var changes = Observe(session);
        session.Entries[5] = replacement;
        var change = changes.Single();
        Assert.AreEqual((NotifyCollectionChangedAction.Replace, 2, replacement),
            (change.Action, change.NewStartingIndex, change.NewItems![0]));
    }

    [TestMethod]
    public void NoticeChangesDoNotNotifyMessageProperties()
    {
        var session = CreateSession();
        var properties = new List<string?>();
        session.ChatMessages.PropertyChanged += (_, e) => properties.Add(e.PropertyName);
        session.Entries.Insert(0, new CopilotChatNotice("added"));
        session.Entries[0] = new CopilotChatNotice("replaced");
        session.Entries.Move(0, 5);
        session.Entries.RemoveAt(5);
        Assert.IsEmpty(properties);
    }

    [TestMethod]
    public void RemovingCollectionSubscriberKeepsPropertySubscription()
    {
        var session = new CopilotChatSession();
        var properties = new List<string?>();
        NotifyCollectionChangedEventHandler collectionHandler = (_, _) => Assert.Fail("Unsubscribed handler invoked.");
        session.ChatMessages.CollectionChanged += collectionHandler;
        session.ChatMessages.PropertyChanged += (_, e) => properties.Add(e.PropertyName);
        session.ChatMessages.CollectionChanged -= collectionHandler;
        session.Entries.Add(Message("new"));
        CollectionAssert.AreEqual(new[] { "Count", "Item[]" }, properties);
    }

    [TestMethod]
    public void RemovingPropertySubscriberKeepsCollectionSubscription()
    {
        var session = new CopilotChatSession();
        var changes = Observe(session);
        PropertyChangedEventHandler propertyHandler = (_, _) => Assert.Fail("Unsubscribed handler invoked.");
        session.ChatMessages.PropertyChanged += propertyHandler;
        session.ChatMessages.PropertyChanged -= propertyHandler;
        session.Entries.Add(Message("new"));
        Assert.AreEqual(NotifyCollectionChangedAction.Add, changes.Single().Action);
    }

    [TestMethod]
    public void AppendAfterUnobservedChangesUsesCurrentFilteredCount()
    {
        var session = CreateSession();
        session.Entries.Add(Message("unobserved"));
        var changes = Observe(session);
        session.Entries.Add(new CopilotChatNotice("notice"));
        session.Entries.Add(Message("observed"));
        Assert.AreEqual(3, changes.Single().NewStartingIndex);
    }

    [TestMethod]
    public void IndexerRejectsOutOfRangeIndices()
    {
        var projection = CreateSession().ChatMessages;
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => projection[projection.Count]);
    }

    [TestMethod]
    public void IndexerRejectsNegativeIndices()
    {
        var projection = CreateSession().ChatMessages;
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => projection[-1]);
    }

    private static CopilotChatMessage Message(string text) => new(ChatRole.User, text);

    private static CopilotChatSession CreateSession()
    {
        var session = new CopilotChatSession();
        session.Entries.Add(new CopilotChatNotice("first"));
        session.Entries.Add(Message("one"));
        session.Entries.Add(new CopilotChatNotice("middle"));
        session.Entries.Add(Message("two"));
        session.Entries.Add(new CopilotChatNotice("last"));
        return session;
    }

    private static List<NotifyCollectionChangedEventArgs> Observe(CopilotChatSession session)
    {
        var changes = new List<NotifyCollectionChangedEventArgs>();
        session.ChatMessages.CollectionChanged += (_, e) => changes.Add(e);
        return changes;
    }
}
