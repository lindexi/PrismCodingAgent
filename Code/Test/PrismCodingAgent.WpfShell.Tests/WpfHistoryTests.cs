using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AgentLib.Logging;
using AgentLib.Model;
using Microsoft.Extensions.AI;
using PrismCodingAgent.AvaloniaShell.Localizations;
using PrismCodingAgent.AvaloniaShell.ViewModels;

namespace PrismCodingAgent.WpfShell.Tests;

[WpfTestClass]
public sealed class WpfHistoryTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Ss05InitialHistoryNavigationThroughRealInputAsync() => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        string root = Path.Combine(AppContext.BaseDirectory, "Phase9HistoryData", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "ShellSettings.json"), JsonSerializer.Serialize(new
        {
            LanguageTag = Lang.Current.IetfLanguageTag, IsWindowsSandboxEnabled = false
        }));
        await File.WriteAllTextAsync(Path.Combine(root, "AgentConfiguration.json"),
            """{"PrimaryModel":"SS05 offline model","OpenAIConfigurationList":[{"EndPoint":"http://127.0.0.1:1/v1","Key":"offline-test","ModelDefinitions":[{"ModelName":"SS05 offline model","ModelId":"ss05-offline","Provider":"openai"}]}]}""");
        var window = (MainWindow)await ((App)Application.Current).OpenAsync(["--data-directory", root]);
        var model = (MainViewModel)window.DataContext;
        using var screenshots = new WpfScreenshotSession(window, TestContext);
        try
        {
            window.UpdateLayout();
            var content = (FrameworkElement)window.Content;
            window.Width += 1440 - content.ActualWidth;
            window.Height += 900 - content.ActualHeight;
            window.UpdateLayout();
            Assert.AreEqual(1440d, content.ActualWidth, 1d);
            Assert.AreEqual(900d, content.ActualHeight, 1d);
            Assert.AreEqual(96d, VisualTreeHelper.GetDpi(content).PixelsPerInchX);
            Assert.AreEqual(96d, VisualTreeHelper.GetDpi(content).PixelsPerInchY);
            await WpfInput.ClickAsync((Button)window.FindName("HistoryButton"));
            await WpfInput.WaitAsync(() => model.IsHistoryOpen && !model.SessionListViewModel.IsLoading, "History did not load.");
            Assert.AreEqual(1, model.SessionListViewModel.Sessions.Count);
            var initial = model.SessionListViewModel.Sessions.Single();
            await screenshots.CaptureAsync("all-history-initial-session");
            var historyPage = (UserControl)window.FindName("HistoryPage");
            var search = (TextBox)historyPage.FindName("HistorySearch");
            await WpfInput.ClickAsync(search);
            WpfInput.Text("no-matching-history-7c839f6d");
            await WpfInput.WaitAsync(() => model.SessionListViewModel.IsEmpty, "History search did not become empty.");
            Assert.IsTrue(((TextBlock)historyPage.FindName("HistoryEmpty")).IsVisible);
            await screenshots.CaptureAsync("all-history-search-no-match");
            await ClearAsync(search);
            await WpfInput.WaitAsync(() => model.SessionListViewModel.Sessions.Count == 1, "History search did not clear.");
            Assert.AreEqual(initial.SessionId, model.SessionListViewModel.Sessions.Single().SessionId);
            await screenshots.CaptureAsync("all-history-search-cleared");
            await WpfInput.ClickAsync((Button)historyPage.FindName("HistoryBack"));
            await WpfInput.WaitAsync(() => model.IsChatOpen, "History back did not return.");
            await screenshots.CaptureAsync("chat-after-all-history");
            await WpfInput.ClickAsync((Button)window.FindName("TaskHistoryButton"));
            await WpfInput.WaitAsync(() => model.IsHistoryOpen && !model.SessionListViewModel.IsLoading, "Task history did not load.");
            Assert.AreEqual(model.ActiveWorkTask.Id.ToString(), search.Text);
            Assert.AreEqual(initial.SessionId, model.SessionListViewModel.Sessions.Single().SessionId);
            await screenshots.CaptureAsync("current-task-history-initial-session");
            await WpfInput.ClickAsync((Button)historyPage.FindName("HistoryBack"));
            await WpfInput.WaitAsync(() => model.IsChatOpen, "Task history back did not return.");
            await screenshots.CaptureAsync("chat-after-task-history");
        }
        finally
        {
            window.Close();
            await WpfInput.ClickAsync((Button)window.FindName("ConfirmExitButton"));
            await WpfInput.WaitAsync(() => !window.IsVisible, "History window did not close.");
            await model.DisposeAsync();
        }
    });

    [TestMethod]
    public async Task GlobalHistorySearchOpenOwnershipAndReturnThroughRealInputAsync() => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        string root = Path.Combine(AppContext.BaseDirectory, "Phase6HistoryData", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "ShellSettings.json"), JsonSerializer.Serialize(new
        {
            LanguageTag = Lang.Current.IetfLanguageTag, IsWindowsSandboxEnabled = false
        }));
        await File.WriteAllTextAsync(Path.Combine(root, "AgentConfiguration.json"),
            """{"PrimaryModel":"demo","OpenAIConfigurationList":[{"EndPoint":"http://127.0.0.1:1/v1","Key":"offline-test","ModelDefinitions":[{"ModelName":"demo","ModelId":"model-id","Provider":"test"}]}]}""");
        Guid activeId = Guid.NewGuid();
        Guid otherId = Guid.NewGuid();
        await File.WriteAllTextAsync(Path.Combine(root, "WorkTasks.json"), JsonSerializer.Serialize(new[]
        {
            new { Id = activeId, DisplayName = "Active task", IsArchived = false },
            new { Id = otherId, DisplayName = "Other task", IsArchived = false }
        }));
        var store = new FileCopilotChatSessionStore(Path.Combine(root, "Sessions"));
        string title = Lang.Current.IetfLanguageTag == "en" ? "Stored conversation" : "已存储的历史会话";
        string body = Lang.Current.IetfLanguageTag == "en" ? "Actual stored answer for the selected conversation." : "所选会话真实存储的回答正文。";
        var stored = new CopilotChatSession { WorkTaskId = otherId, WorkTaskName = "Other task" };
        stored.SetTitle(title);
        await stored.AddEntryAsync(new CopilotChatMessage(ChatRole.User, title));
        await stored.AddEntryAsync(new CopilotChatMessage(ChatRole.Assistant, body));
        await store.SaveSessionAsync(stored, null);
        var window = (MainWindow)await ((App)Application.Current).OpenAsync(["--data-directory", root]);
        var model = (MainViewModel)window.DataContext;
        using var screenshots = new WpfScreenshotSession(window, TestContext);
        try
        {
            window.UpdateLayout();
            var content = (FrameworkElement)window.Content;
            window.Width += 1440 - content.ActualWidth;
            window.Height += 900 - content.ActualHeight;
            await WpfInput.ClickAsync((Button)window.FindName("HistoryButton"));
            await WpfInput.WaitAsync(() => model.IsHistoryOpen && !model.SessionListViewModel.IsLoading
                && model.SessionListViewModel.Sessions.Any(item => item.SessionId == stored.SessionId), "Stored global history did not load.");
            Assert.AreEqual(string.Empty, model.SessionListViewModel.SearchText);
            Assert.AreEqual(otherId, model.SessionListViewModel.Sessions.Single(item => item.SessionId == stored.SessionId).WorkTaskId);
            await screenshots.CaptureAsync("global-history");
            var historyPage = (UserControl)window.FindName("HistoryPage");
            var search = (TextBox)historyPage.FindName("HistorySearch");
            await WpfInput.ClickAsync(search);
            WpfInput.Text("no-matching-history-7c839f6d");
            await WpfInput.WaitAsync(() => model.SessionListViewModel.IsEmpty, "No-match history did not show empty state.");
            Assert.IsTrue(((TextBlock)historyPage.FindName("HistoryEmpty")).IsVisible);
            await screenshots.CaptureAsync("search-empty");
            await ClearAsync(search);
            await WpfInput.WaitAsync(() => model.SessionListViewModel.Sessions.Any(item => item.SessionId == stored.SessionId), "Clearing history search did not restore sessions.");
            await screenshots.CaptureAsync("search-cleared");
            await WpfInput.ClickAsync((Button)historyPage.FindName("HistoryBack"));
            await WpfInput.WaitAsync(() => model.IsChatOpen, "History back did not return to chat.");
            await WpfInput.ClickAsync((Button)window.FindName("TaskHistoryButton"));
            await WpfInput.WaitAsync(() => model.IsHistoryOpen && model.SessionListViewModel.SearchText == activeId.ToString(), "Task history did not search active task ID.");
            Assert.IsFalse(model.SessionListViewModel.Sessions.Any(item => item.SessionId == stored.SessionId));
            await screenshots.CaptureAsync("task-history");
            await WpfInput.ClickAsync((Button)window.FindName("HistoryButton"));
            await WpfInput.WaitAsync(() => model.SessionListViewModel.SearchText.Length == 0, "Global history did not reset task filter.");
            await WpfInput.ClickAsync(search);
            WpfInput.Text(title);
            await WpfInput.WaitAsync(() => model.SessionListViewModel.Sessions.Count == 1, "Title search did not select stored conversation.");
            await window.Dispatcher.InvokeAsync(window.UpdateLayout, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            Button open = Descendants(window).OfType<Button>().Single(button => button.Name == "OpenHistorySession"
                && button.DataContext is SessionItemViewModel item && item.SessionId == stored.SessionId);
            SessionItemViewModel edited = model.SessionListViewModel.Sessions.Single();
            T Row<T>(string name) where T : FrameworkElement => Descendants(window).OfType<T>().Single(element => element.Name == name && ReferenceEquals(element.DataContext, edited));
            await WpfInput.ClickAsync(Row<Button>("EditHistoryTitle"));
            await WpfInput.WaitAsync(() => edited.IsEditing, "Title edit did not start.");
            await ClearAsync(Row<TextBox>("HistoryTitleEditor"));
            WpfInput.Text("Cancelled title");
            await screenshots.CaptureAsync("title-editing");
            await WpfInput.ClickAsync(Row<Button>("CancelHistoryTitle"));
            Assert.AreEqual(title, edited.Title);
            await WpfInput.ClickAsync(Row<Button>("EditHistoryTitle"));
            await ClearAsync(Row<TextBox>("HistoryTitleEditor"));
            string renamed = Lang.Current.IetfLanguageTag == "en" ? "Saved conversation title" : "已保存的会话标题";
            WpfInput.Text(renamed);
            await WpfInput.ClickAsync(Row<Button>("SaveHistoryTitle"));
            await WpfInput.WaitAsync(() => !edited.IsEditing && edited.Title == renamed, "Title did not save.");
            Assert.AreEqual(renamed, (await store.ListSessionsAsync()).Single(item => item.SessionId == stored.SessionId).Title);
            await ClearAsync(search);
            await WpfInput.WaitAsync(() => Row<TextBlock>("HistoryTitle").IsVisible, "Renamed row did not render.");
            await WpfInput.ClickAsync(Row<TextBlock>("HistoryTitle"));
            await WpfInput.ClickAsync(Row<TextBlock>("HistoryTitle"));
            await WpfInput.WaitAsync(() => model.IsChatOpen && model.ChatViewModel.CurrentSessionId == stored.SessionId
                && model.ChatViewModel.Messages.Any(message => message.Content == body), "Selected session body did not open.");
            Assert.AreEqual(activeId, model.ActiveWorkTask.Id);
            Assert.AreEqual(activeId, model.SessionListViewModel.SelectedSession?.WorkTaskId);
            Assert.IsTrue(window.Title.Contains(renamed, StringComparison.Ordinal), "Opened session title must remain reachable in the window title.");
            await WpfInput.WaitAsync(() => Descendants((ItemsControl)window.FindName("HistoryMessages")).OfType<TextBox>()
                .Any(block => block.IsVisible && block.Text == body), "Stored body was not visible.");
            Assert.IsTrue(window.Title.Contains(model.ActiveWorkTask.DisplayName, StringComparison.Ordinal), "Active task must remain reachable in the window title.");
            Assert.IsNull(((FrameworkElement)window.FindName("HistoryButton")).Parent is FrameworkElement navigation ? navigation.ToolTip : null);
            await screenshots.CaptureAsync("opened-stored-session");
            await WpfInput.ClickAsync((Button)window.FindName("TaskHistoryButton"));
            await WpfInput.WaitAsync(() => model.IsHistoryOpen && model.SessionListViewModel.Sessions.Any(item => item.SessionId == stored.SessionId && item.IsCurrent), "Opened session did not appear under active task.");
            await screenshots.CaptureAsync("opened-session-task-ownership");
            await WpfInput.ClickAsync((Button)historyPage.FindName("HistoryBack"));
            await WpfInput.WaitAsync(() => model.IsChatOpen, "Final history back did not close history.");
            Assert.AreEqual(stored.SessionId, model.ChatViewModel.CurrentSessionId);
            await WpfInput.ClickAsync((Button)((PrismCodingAgent.AvaloniaShell.Views.ChatView)window.FindName("ChatComposer")).FindName("NewSessionButton"));
            await WpfInput.WaitAsync(() => model.ChatViewModel.CurrentSessionId != stored.SessionId, "New session did not replace the nonempty conversation.");
            Guid emptyId = model.ChatViewModel.CurrentSessionId;
            await WpfInput.ClickAsync((Button)((PrismCodingAgent.AvaloniaShell.Views.ChatView)window.FindName("ChatComposer")).FindName("NewSessionButton"));
            Assert.AreEqual(emptyId, model.ChatViewModel.CurrentSessionId);
            await WpfInput.ClickAsync((Button)window.FindName("HistoryButton"));
            await WpfInput.WaitAsync(() => model.IsHistoryOpen && !model.SessionListViewModel.IsLoading, "History did not reopen.");
            edited = model.SessionListViewModel.Sessions.Single(item => item.SessionId == stored.SessionId);
            Assert.AreEqual(renamed, edited.Title);
            await WpfInput.ClickAsync(Row<Button>("OpenHistorySession"));
            await WpfInput.WaitAsync(() => model.IsChatOpen && model.ChatViewModel.CurrentSessionId == stored.SessionId, "Saved conversation did not reopen.");
            await WpfInput.ClickAsync((Button)window.FindName("HistoryButton"));
            await WpfInput.WaitAsync(() => model.IsHistoryOpen && !model.SessionListViewModel.IsLoading, "Delete history did not load.");
            edited = model.SessionListViewModel.Sessions.Single(item => item.SessionId == stored.SessionId);
            await WpfInput.ClickAsync(Row<Button>("DeleteHistorySession"));
            await WpfInput.WaitAsync(() => !model.SessionListViewModel.Sessions.Any(item => item.SessionId == stored.SessionId)
                && model.ChatViewModel.CurrentSessionId != stored.SessionId, "Deleting current conversation did not select a replacement.");
            Assert.IsFalse((await store.ListSessionsAsync()).Any(item => item.SessionId == stored.SessionId));
            await screenshots.CaptureAsync("session-deleted-replacement");
            await WpfInput.ClickAsync((Button)historyPage.FindName("HistoryBack"));
            await WpfInput.WaitAsync(() => model.IsChatOpen, "History did not return to chat.");
            var previousSessionId = model.ChatViewModel.CurrentSessionId;
            await WpfInput.ClickAsync(Descendants(window).OfType<Button>().Single(button => button.Name == "NewSessionButton" && button.IsVisible));
            Assert.AreEqual(previousSessionId, model.ChatViewModel.CurrentSessionId);
            Assert.IsTrue(model.ChatViewModel.ShowWelcome);
            Assert.IsFalse(model.HasErrorMessage);
        }
        finally
        {
            window.Close();
            await WpfInput.ClickAsync((Button)window.FindName("ConfirmExitButton"));
            await WpfInput.WaitAsync(() => !window.IsVisible, "History window did not close.");
            await model.DisposeAsync();
        }
    });

    private static async Task ClearAsync(TextBox search)
    {
        await WpfInput.ClickAsync(search);
        WpfInput.Key(0x24);
        for (int i = 0, length = search.Text.Length; i < length; i++) WpfInput.Key(0x2E);
        await WpfInput.WaitAsync(() => search.Text.Length == 0, "Search did not clear through keyboard.");
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (DependencyObject descendant in Descendants(child)) yield return descendant;
        }
    }
}
