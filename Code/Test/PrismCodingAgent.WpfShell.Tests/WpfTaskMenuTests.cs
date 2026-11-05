using AgentLib.Logging.Sessions;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PrismCodingAgent.CodingShell;
using PrismCodingAgent.CodingShell.Localizations;
using PrismCodingAgent.CodingShell.ViewModels;

namespace PrismCodingAgent.WpfShell.Tests;

[WpfTestClass]
public sealed class WpfTaskMenuTests
{
    public TestContext TestContext { get; set; } = null!;

    /// <summary>Renders the existing archived search template menu without business execution or input injection.</summary>
    [TestMethod]
    public Task ArchivedSearchMenuRendersWithoutSendInputAsync() => WpfTestClassAttribute.RunUiAsync(async () =>
    {
        var window = new MainWindow();
        window.Closing += (_, args) => args.Cancel = false;
        var items = (ItemsControl)window.FindName("ArchivedSearchItems");
        var tile = (FrameworkElement)items.ItemTemplate.LoadContent();
        var button = (Button)tile.FindName("ArchivedSearchMenu");
        var menu = button.ContextMenu;
        window.Content = tile;
        using var screenshots = new WpfScreenshotSession(window, TestContext);
        try
        {
            window.Show();
            menu.PlacementTarget = button;
            menu.IsOpen = true;
            await window.Dispatcher.InvokeAsync(menu.UpdateLayout, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            var restore = (MenuItem)menu.Items[0];

            await screenshots.CaptureSurfaceAsync(menu, "production-archived-search-menu-rendering-not-interaction");
            TestContext.WriteLine($"Restore state: highlighted={restore.IsHighlighted}, focus={restore.IsKeyboardFocused}, hover={restore.IsMouseOver}; border={menu.BorderBrush}; background={menu.Background}");
            Assert.IsFalse(restore.IsHighlighted);
            CollectionAssert.AreEqual(new[] { Lang.Current.RestoreWorkTaskText.ToString(), Lang.Current.DeleteSessionText.ToString() }, menu.Items.OfType<MenuItem>().Select(item => item.Header.ToString()).ToArray());
            Assert.IsTrue(menu.Items.OfType<MenuItem>().All(item => item.Icon is System.Windows.Shapes.Path));
        }
        finally { menu.IsOpen = false; window.Close(); }
    });

    [TestMethod]
    public async Task ArchiveSearchRestoreThroughRealInputAsync() => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        if (!WpfTestSession.SupportsReferenceWindow()) return;
        string root = Path.Combine(AppContext.BaseDirectory, "ArchiveSearchRestoreData", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "AgentConfiguration.json"),
            """{"PrimaryModel":"Offline navigation model","OpenAIConfigurationList":[{"EndPoint":"http://127.0.0.1:1/v1","Key":"offline-test","ModelDefinitions":[{"ModelName":"Offline navigation model","ModelId":"offline-navigation","Provider":"openai"}]}]}""");
        await File.WriteAllTextAsync(Path.Combine(root, "ShellSettings.json"), JsonSerializer.Serialize(new
        {
            LanguageTag = Lang.Current.IetfLanguageTag, IsWindowsSandboxEnabled = false
        }));
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
            var original = model.ActiveWorkTask;
            await WpfInput.ClickAsync((Button)window.FindName("NewTaskButton"));
            await WpfInput.WaitAsync(() => model.WorkTasks.Count == 2, "Second task did not load.");
            await screenshots.CaptureAsync("second-task-created");
            ContextMenu menu = await OpenMenuAsync(window, original);
            await screenshots.CaptureSurfaceAsync(menu, "task-archive-menu");
            await WpfInput.ClickAsync((MenuItem)menu.Items[2]);
            await WpfInput.WaitAsync(() => model.WorkTasks.Count == 1 && model.ArchivedWorkTasks.Count == 1, "Task did not archive.");
            await screenshots.CaptureAsync("task-archived");
            await WpfInput.ClickAsync((Button)window.FindName("ArchiveButton"));
            await WpfInput.WaitAsync(() => model.IsArchiveOpen, "Archive did not open.");
            await screenshots.CaptureAsync("archive-list");
            await WpfInput.ClickAsync((Button)((UserControl)window.FindName("ArchivePage")).FindName("ArchiveBack"));
            await WpfInput.WaitAsync(() => model.IsChatOpen, "Archive back did not return to chat.");
            Assert.HasCount(1, model.ArchivedWorkTasks);
            await screenshots.CaptureAsync("chat-after-archive-back");
            await WpfInput.ClickAsync((Button)window.FindName("ArchiveButton"));
            await WpfInput.WaitAsync(() => model.IsArchiveOpen, "Archive did not reopen.");
            var search = (TextBox)window.FindName("TaskSearch");
            await WpfInput.ClickAsync(search);
            WpfInput.Text(original.DisplayName);
            await WpfInput.WaitAsync(() => model.HasFilteredArchivedWorkTasks, "Archived sidebar match missing.");
            await screenshots.CaptureAsync("archive-search-match");
            WpfInput.Text("-no-matching-archived-task");
            await WpfInput.WaitAsync(() => model.IsWorkTaskSearchEmpty, "Archived sidebar no-match missing.");
            Assert.AreEqual(1, ((ItemsControl)((UserControl)window.FindName("ArchivePage")).FindName("ArchiveItems")).Items.Count);
            await screenshots.CaptureAsync("archive-search-no-match");
            WpfInput.Key(0x24);
            for (int i = 0, length = search.Text.Length; i < length; i++) WpfInput.Key(0x2E);
            await WpfInput.WaitAsync(() => search.Text.Length == 0, "Sidebar search did not clear.");
            await screenshots.CaptureAsync("archive-search-cleared");
            await WpfInput.ClickAsync(Descendants(window).OfType<Button>().Single(button => button.Name == "RestoreTask" && button.IsVisible));
            await WpfInput.WaitAsync(() => model.ArchivedWorkTasks.Count == 0 && model.WorkTasks.Count == 2 && !model.IsArchiveOpen, "Restore did not return to task.");
            Assert.AreEqual(original.Id, model.ActiveWorkTask.Id);
            await screenshots.CaptureAsync("restored-task-in-main-list");
        }
        finally
        {
            window.Close();
            await WpfInput.ClickAsync((Button)window.FindName("ConfirmExitButton"));
            await WpfInput.WaitAsync(() => !window.IsVisible, "Archive window did not close.");
            await model.DisposeAsync();
        }
    });

    [TestMethod]
    public async Task RenamePinArchiveAndRestoreThroughRealInputAsync() => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        if (!WpfTestSession.SupportsReferenceWindow()) return;
        string root = Path.Combine(AppContext.BaseDirectory, "TaskMenuActionsData", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "ShellSettings.json"), JsonSerializer.Serialize(new
        {
            LanguageTag = Lang.Current.IetfLanguageTag, IsWindowsSandboxEnabled = false
        }));
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
            WorkTaskItemViewModel task = model.WorkTasks.Single();
            ContextMenu menu = await OpenMenuAsync(window, task);
            Assert.IsFalse(((MenuItem)menu.Items[2]).IsEnabled);
            Assert.IsFalse(((MenuItem)menu.Items[4]).IsEnabled);
            await screenshots.CaptureSurfaceAsync(menu, "single-task-menu");
            WpfInput.Key(0x1B);
            await WpfInput.WaitAsync(() => !menu.IsOpen, "Task menu did not close.");
            await WpfInput.ClickAsync((Button)window.FindName("NewTaskButton"));
            await WpfInput.WaitAsync(() => model.WorkTasks.Count == 2, "Task creation did not complete.");
            menu = await OpenMenuAsync(window, task);
            Assert.IsTrue(((MenuItem)menu.Items[2]).IsEnabled);
            await screenshots.CaptureSurfaceAsync(menu, "task-menu");
            await WpfInput.ClickAsync((MenuItem)menu.Items[1]);
            await WpfInput.WaitAsync(() => task.IsEditing, "Rename menu did not begin editing.");
            TextBox editor = Find<TextBox>(window, "TaskNameEditor", task);
            await WpfInput.ClickAsync(editor);
            WpfInput.Key(0x24);
            for (int i = 0; i < task.DisplayName.Length; i++) WpfInput.Key(0x2E);
            WpfInput.Text("   ");
            await WpfInput.WaitAsync(() => !Find<Button>(window, "SaveTaskName", task).IsEnabled, "Blank task name was accepted.");
            WpfInput.Key(0x24);
            WpfInput.Key(0x2E); WpfInput.Key(0x2E); WpfInput.Key(0x2E);
            string name = Lang.Current.IetfLanguageTag == "en" ? "Renamed task" : "重命名后的任务";
            WpfInput.Text(name);
            await WpfInput.WaitAsync(() => task.EditedDisplayName == name, "Rename input did not bind.");
            await WpfInput.ClickAsync(Find<Button>(window, "SaveTaskName", task));
            await WpfInput.WaitAsync(() => !task.IsEditing && task.DisplayName == name, "Task name did not save.");
            menu = await OpenMenuAsync(window, task);
            await WpfInput.ClickAsync((MenuItem)menu.Items[0]);
            await WpfInput.WaitAsync(() => model.WorkTasks[0].Id == task.Id, "Pin did not move task to first position.");
            await screenshots.CaptureAsync("renamed-pinned");
            menu = await OpenMenuAsync(window, task);
            await WpfInput.ClickAsync((MenuItem)menu.Items[2]);
            await WpfInput.WaitAsync(() => model.ArchivedWorkTasks.Count == 1 && model.WorkTasks.Count == 1, "Task did not archive.");
            await WpfInput.ClickAsync((Button)window.FindName("ArchiveButton"));
            await WpfInput.WaitAsync(() => model.IsArchiveOpen && ((UserControl)window.FindName("ArchivePage")).IsVisible, "Archive navigation did not open.");
            var search = (TextBox)window.FindName("TaskSearch");
            await WpfInput.ClickAsync(search);
            WpfInput.Text("no-match-phase-six");
            await WpfInput.WaitAsync(() => model.IsWorkTaskSearchEmpty, "Sidebar search did not become empty.");
            Assert.AreEqual(1, ((ItemsControl)((UserControl)window.FindName("ArchivePage")).FindName("ArchiveItems")).Items.Count);
            Assert.AreEqual(name, model.ArchivedWorkTasks.Single().DisplayName);
            await screenshots.CaptureAsync("archive-sidebar-search-empty");
            await WpfInput.ClickAsync(Descendants(window).OfType<Button>().Single(button => button.Name == "RestoreTask"));
            await WpfInput.WaitAsync(() => model.ArchivedWorkTasks.Count == 0 && model.WorkTasks.Count == 2, "Archive restore did not complete.");
            await WpfInput.WaitAsync(() => !model.IsArchiveOpen && model.ActiveWorkTask.Id == task.Id, "Restore did not activate the restored task.");
            await WpfInput.ClickAsync((Button)window.FindName("ArchiveButton"));
            await WpfInput.WaitAsync(() => ((TextBlock)((UserControl)window.FindName("ArchivePage")).FindName("ArchiveEmpty")).IsVisible, "Archive empty state did not appear.");
            Assert.IsTrue(model.IsArchiveEmpty);
            await screenshots.CaptureAsync("archive-restored-empty");
            Assert.IsFalse(model.HasErrorMessage);
        }
        finally
        {
            window.Close();
            await WpfInput.ClickAsync((Button)window.FindName("ConfirmExitButton"));
            await WpfInput.WaitAsync(() => !window.IsVisible, "Task window did not close.");
            await model.DisposeAsync();
        }

        var reopened = (MainWindow)await ((App)Application.Current).OpenAsync(["--data-directory", root]);
        var restored = (MainViewModel)reopened.DataContext;
        using var reopenedScreenshots = new WpfScreenshotSession(reopened, TestContext);
        try
        {
            string expected = Lang.Current.IetfLanguageTag == "en" ? "Renamed task" : "重命名后的任务";
            Assert.AreEqual(expected, restored.WorkTasks[0].DisplayName);
            Assert.HasCount(2, restored.WorkTasks);
            Assert.IsTrue(restored.IsArchiveEmpty);
            await reopenedScreenshots.CaptureAsync("saved-tasks-reopened");
            WorkTaskItemViewModel target = restored.WorkTasks[0];
            var store = new AgentLib.Logging.Sessions.FileCopilotChatSessionStore(Path.Combine(root, "Sessions"));
            var history = new AgentLib.Model.CopilotChatSession { WorkTaskId = target.Id, WorkTaskName = target.DisplayName };
            history.SetTitle("Retained task history");
            await history.AddEntryAsync(new AgentLib.Model.CopilotChatMessage(Microsoft.Extensions.AI.ChatRole.User, "Retained task history"));
            await store.SaveSessionAsync(history, null);
            ContextMenu menu = await OpenMenuAsync(reopened, target);
            await WpfInput.ClickAsync((MenuItem)menu.Items[2]);
            await WpfInput.WaitAsync(() => restored.ArchivedWorkTasks.Count == 1, "Reopened task did not archive.");
            var search = (TextBox)reopened.FindName("TaskSearch");
            await WpfInput.ClickAsync(search);
            WpfInput.Text(expected);
            await WpfInput.WaitAsync(() => restored.HasFilteredArchivedWorkTasks, "Archived search did not find task.");
            await reopened.Dispatcher.InvokeAsync(reopened.UpdateLayout, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            Button archivedMenu = Descendants(reopened).OfType<Button>().Single(button => button.Name == "ArchivedSearchMenu" && button.IsVisible);
            await WpfInput.ClickAsync(archivedMenu);
            await WpfInput.WaitAsync(() => archivedMenu.ContextMenu.IsOpen, "Archived result menu did not open.");
            await reopenedScreenshots.CaptureSurfaceAsync(archivedMenu.ContextMenu, "archived-search-menu");
            await WpfInput.ClickAsync((MenuItem)archivedMenu.ContextMenu.Items[0]);
            await WpfInput.WaitAsync(() => restored.ActiveWorkTask.Id == target.Id && restored.ArchivedWorkTasks.Count == 0, "Search menu restore did not activate task.");
            menu = await OpenMenuAsync(reopened, restored.WorkTasks.Single(item => item.Id == target.Id));
            await WpfInput.ClickAsync((MenuItem)menu.Items[2]);
            await WpfInput.WaitAsync(() => restored.HasFilteredArchivedWorkTasks, "Second archive did not reach search.");
            await reopenedScreenshots.CaptureAsync("archived-search-result");
            await WpfInput.ClickAsync(Descendants(reopened).OfType<Button>().Single(button => button.Name == "RestoreSearchTask"));
            await WpfInput.WaitAsync(() => restored.ActiveWorkTask.Id == target.Id && restored.ArchivedWorkTasks.Count == 0, "Search row restore did not activate task.");
            menu = await OpenMenuAsync(reopened, restored.WorkTasks.Single(item => item.Id == target.Id));
            await WpfInput.ClickAsync((MenuItem)menu.Items[2]);
            await WpfInput.WaitAsync(() => restored.HasFilteredArchivedWorkTasks, "Third archive did not reach search.");
            await reopened.Dispatcher.InvokeAsync(reopened.UpdateLayout, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            archivedMenu = Descendants(reopened).OfType<Button>().Single(button => button.Name == "ArchivedSearchMenu" && button.IsVisible);
            await WpfInput.ClickAsync(archivedMenu);
            await WpfInput.WaitAsync(() => archivedMenu.ContextMenu.IsOpen, "Delete menu did not open.");
            await WpfInput.ClickAsync((MenuItem)archivedMenu.ContextMenu.Items[2]);
            await WpfInput.WaitAsync(() => restored.ArchivedWorkTasks.Count == 0, "Archived search deletion did not complete.");
            Assert.IsTrue((await store.ListSessionsAsync()).Any(item => item.SessionId == history.SessionId));
            await WpfInput.ClickAsync(search);
            WpfInput.Key(0x24);
            for (int i = 0; i < expected.Length; i++) WpfInput.Key(0x2E);
            await WpfInput.WaitAsync(() => search.Text.Length == 0, "Task search did not clear.");
            await WpfInput.ClickAsync((Button)reopened.FindName("NewTaskButton"));
            await WpfInput.WaitAsync(() => restored.WorkTasks.Count == 2, "Delete target creation failed.");
            WorkTaskItemViewModel deleteTarget = restored.ActiveWorkTask;
            var deletedHistory = new AgentLib.Model.CopilotChatSession { WorkTaskId = deleteTarget.Id, WorkTaskName = deleteTarget.DisplayName };
            deletedHistory.SetTitle("Retained deleted task history");
            await deletedHistory.AddEntryAsync(new AgentLib.Model.CopilotChatMessage(Microsoft.Extensions.AI.ChatRole.User, "Retained deleted task history"));
            await store.SaveSessionAsync(deletedHistory, null);
            menu = await OpenMenuAsync(reopened, deleteTarget);
            await WpfInput.ClickAsync((MenuItem)menu.Items[4]);
            await WpfInput.WaitAsync(() => restored.WorkTasks.Count == 1 && restored.ActiveWorkTask.Id != deleteTarget.Id, "Active task deletion did not select replacement.");
            Assert.IsTrue((await store.ListSessionsAsync()).Any(item => item.SessionId == deletedHistory.SessionId));
            await WpfInput.ClickAsync((Button)reopened.FindName("HistoryButton"));
            await WpfInput.WaitAsync(() => !restored.SessionListViewModel.IsLoading
                && restored.SessionListViewModel.Sessions.Any(item => item.SessionId == history.SessionId)
                && restored.SessionListViewModel.Sessions.Any(item => item.SessionId == deletedHistory.SessionId), "Task deletion removed retained global history.");
            await reopenedScreenshots.CaptureAsync("deleted-tasks-history-retained");
            await WpfInput.ClickAsync((Button)reopened.FindName("NewTaskButton"));
            await WpfInput.WaitAsync(() => restored.WorkTasks.Count == 2, "Archive page delete target was not created.");
            Guid pageDeleteId = restored.ActiveWorkTask.Id;
            menu = await OpenMenuAsync(reopened, restored.ActiveWorkTask);
            await WpfInput.ClickAsync((MenuItem)menu.Items[2]);
            await WpfInput.WaitAsync(() => restored.ArchivedWorkTasks.Count == 1, "Archive page target did not archive.");
            await WpfInput.ClickAsync((Button)reopened.FindName("ArchiveButton"));
            await WpfInput.WaitAsync(() => restored.IsArchiveOpen, "Archive page did not open for delete.");
            await WpfInput.ClickAsync(Descendants(reopened).OfType<Button>().Single(button => button.Name == "DeleteArchivedTask" && button.IsVisible));
            await WpfInput.WaitAsync(() => restored.ArchivedWorkTasks.Count == 0, "Archive page delete did not complete.");
            Assert.IsFalse(restored.WorkTasks.Any(item => item.Id == pageDeleteId));
        }
        finally
        {
            reopened.Close();
            await WpfInput.ClickAsync((Button)reopened.FindName("ConfirmExitButton"));
            await WpfInput.WaitAsync(() => !reopened.IsVisible, "Reopened task window did not close.");
            await restored.DisposeAsync();
        }
    });

    private static async Task<ContextMenu> OpenMenuAsync(Window window, WorkTaskItemViewModel task)
    {
        await window.Dispatcher.InvokeAsync(window.UpdateLayout, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        await WpfInput.WaitAsync(() => Descendants(window).OfType<Button>().Any(button => button.Name == "TaskMore" && ReferenceEquals(button.DataContext, task) && button.IsVisible), "Task menu target did not render.");
        Button button = Find<Button>(window, "TaskMore", task);
        await WpfInput.ClickAsync(button);
        await WpfInput.WaitAsync(() => button.ContextMenu.IsOpen, "Task menu did not open.");
        await window.Dispatcher.InvokeAsync(button.ContextMenu.UpdateLayout, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        var deleteItem = (MenuItem)button.ContextMenu.Items[4];
        var deleteIcon = (System.Windows.Shapes.Path)deleteItem.Icon;
        Assert.IsTrue(deleteIcon.IsVisible, "Delete icon must remain visible, including when disabled.");
        Assert.IsTrue(deleteIcon.ActualWidth > 0 && deleteIcon.ActualHeight > 0, "Delete icon must occupy rendered space.");
        Assert.IsNotNull(deleteIcon.Fill, "Delete icon foreground binding must resolve.");
        Assert.AreEqual(deleteItem.Foreground.ToString(), deleteIcon.Fill.ToString(), "Delete icon must inherit the danger foreground.");
        return button.ContextMenu;
    }

    private static T Find<T>(DependencyObject window, string name, object task) where T : FrameworkElement
        => Descendants(window).OfType<T>().Single(element => element.Name == name && ReferenceEquals(element.DataContext, task));

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
