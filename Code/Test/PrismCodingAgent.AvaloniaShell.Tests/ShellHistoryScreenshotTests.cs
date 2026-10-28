using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PrismCodingAgent.AvaloniaShell.Localizations;
using PrismCodingAgent.AvaloniaShell.Views;

namespace PrismCodingAgent.AvaloniaShell.Tests;

/// <summary>通过真实主界面输入验证启动历史、搜索空状态和存档导航，并保留各状态截图。</summary>
[AvaloniaTestClass]
public sealed class ShellHistoryScreenshotTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task EmptyHistoryAndArchiveShouldNavigateThroughMouseInput()
    {
        await using var session = await ShellScreenshotSession.OpenAsync(TestContext);
        session.Capture("main-model-setup");
        var main = session.Window.GetVisualDescendants().OfType<MainView>().Single();
        await session.ConfigureOfflineModelAsync();
        session.Capture("chat-ready");
        Assert.IsTrue(session.Model.IsChatOpen);
        var chat = main.GetVisualDescendants().OfType<ChatView>().Single();
        Button taskHistory = chat.GetVisualDescendants().OfType<Button>()
            .Single(button => button.Name == "TaskHistoryButton");
        Assert.IsTrue(taskHistory.IsEffectivelyEnabled);

        Button BackButton() => main.GetVisualDescendants().OfType<Button>()
            .Single(button => button.IsEffectivelyVisible && Equals(button.Content, Lang.Current.BackToChatText));
        async Task WaitForHistoryAsync()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            while (session.Model.SessionListViewModel.IsLoading)
            {
                await Task.Delay(20, timeout.Token);
                Dispatcher.UIThread.RunJobs();
            }
        }

        void AssertEmptyHistory(string expectedSearch)
        {
            Assert.IsTrue(session.Model.IsHistoryOpen);
            var history = main.GetVisualDescendants().OfType<SessionListView>().Single();
            Assert.IsTrue(history.IsEffectivelyVisible);
            Assert.IsTrue(history.GetVisualDescendants().OfType<TextBlock>()
                .Single(block => block.Text == Lang.Current.EmptySessionListText).IsEffectivelyVisible);
            Assert.IsEmpty(session.Model.SessionListViewModel.Sessions);
            Assert.AreEqual(expectedSearch, history.GetVisualDescendants().OfType<TextBox>()
                .Single(box => box.PlaceholderText == Lang.Current.HistorySearchText).Text);
        }

        await session.ClickAndWaitAsync(main.GetVisualDescendants().OfType<Button>()
            .Single(button => button.Name == "HistoryButton"));
        await WaitForHistoryAsync();
        session.Capture("all-history-initial-session");
        var historyView = main.GetVisualDescendants().OfType<SessionListView>().Single();
        var historyList = historyView.GetVisualDescendants().OfType<ListBox>().Single();
        var search = historyView.GetVisualDescendants().OfType<TextBox>()
            .Single(box => box.PlaceholderText == Lang.Current.HistorySearchText);
        Assert.IsTrue(historyView.IsEffectivelyVisible);
        Assert.AreEqual(1, historyList.ItemCount);
        var initialSession = historyList.Items[0];

        session.Click(search);
        session.Window.KeyTextInput("no-matching-history-7c839f6d");
        Dispatcher.UIThread.RunJobs();
        session.Capture("all-history-search-no-match");
        AssertEmptyHistory("no-matching-history-7c839f6d");

        session.Click(search);
        session.Window.KeyPressQwerty(PhysicalKey.A, RawInputModifiers.Control);
        session.Window.KeyReleaseQwerty(PhysicalKey.A, RawInputModifiers.Control);
        session.Window.KeyPressQwerty(PhysicalKey.Backspace, RawInputModifiers.None);
        session.Window.KeyReleaseQwerty(PhysicalKey.Backspace, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        session.Capture("all-history-search-cleared");
        Assert.AreEqual(string.Empty, search.Text);
        Assert.AreEqual(1, historyList.ItemCount);
        Assert.AreSame(initialSession, historyList.Items[0]);
        session.Click(BackButton());
        session.Capture("chat-after-all-history");
        Assert.IsTrue(session.Model.IsChatOpen);
        Assert.IsTrue(chat.IsEffectivelyVisible);

        await session.ClickAndWaitAsync(taskHistory);
        await WaitForHistoryAsync();
        session.Capture("current-task-history-initial-session");
        Assert.IsTrue(historyView.IsEffectivelyVisible);
        Assert.AreEqual(session.Model.ActiveWorkTask.Id.ToString(), search.Text);
        Assert.AreEqual(1, historyList.ItemCount);
        Assert.AreSame(initialSession, historyList.Items[0]);
        session.Click(BackButton());
        session.Capture("chat-after-task-history");
        Assert.IsTrue(session.Model.IsChatOpen);
        Assert.IsTrue(chat.IsEffectivelyVisible);

        session.Click(main.GetVisualDescendants().OfType<Button>()
            .Single(button => button.Name == "ArchiveButton"));
        session.Capture("archive-empty");
        Assert.IsTrue(session.Model.IsArchiveOpen);
        var archive = main.GetVisualDescendants().OfType<ArchivedWorkTasksView>().Single();
        Assert.IsTrue(archive.IsEffectivelyVisible);
        Assert.IsTrue(archive.GetVisualDescendants().OfType<TextBlock>()
            .Single(block => block.Text == Lang.Current.EmptyArchivedWorkTasksText).IsEffectivelyVisible);
        Assert.IsEmpty(session.Model.ArchivedWorkTasks);
        session.Click(BackButton());
        session.Capture("chat-after-archive");
        Assert.IsTrue(session.Model.IsChatOpen);
        Assert.IsTrue(chat.IsEffectivelyVisible);
    }
}
