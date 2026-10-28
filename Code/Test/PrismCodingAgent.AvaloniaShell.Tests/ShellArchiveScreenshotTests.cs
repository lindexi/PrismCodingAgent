using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PrismCodingAgent.AvaloniaShell.Localizations;
using PrismCodingAgent.AvaloniaShell.Views;

namespace PrismCodingAgent.AvaloniaShell.Tests;

/// <summary>通过真实运行时的鼠标和键盘输入验证任务归档、搜索和恢复，并保留截图。</summary>
[AvaloniaTestClass]
public sealed class ShellArchiveScreenshotTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ArchivedTaskShouldBeSearchableAndRestoredThroughUserInput()
    {
        await using var session = await ShellScreenshotSession.OpenAsync(TestContext);
        var original = session.Model.ActiveWorkTask;
        await session.ClickAndWaitAsync(session.Window.GetVisualDescendants().OfType<Button>()
            .Single(button => ReferenceEquals(button.Command, session.Model.CreateWorkTaskCommand)));
        session.Capture("second-task-created");
        Assert.HasCount(2, session.Model.WorkTasks);

        Button more = session.Window.GetVisualDescendants().OfType<Button>()
            .Single(button => button.Classes.Contains("TaskMore") && ReferenceEquals(button.DataContext, original));
        session.Click(more);
        var menu = (MenuFlyout)more.Flyout!;
        var archive = menu.Items.OfType<MenuItem>()
            .Single(item => Equals(item.Header, Lang.Current.ArchiveWorkTaskText));
        session.Capture("task-archive-menu");
        Assert.IsTrue(archive.IsEffectivelyEnabled);
        session.Window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
        session.Window.KeyReleaseQwerty(PhysicalKey.Home, RawInputModifiers.None);
        session.Window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        session.Window.KeyReleaseQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        session.Window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        session.Window.KeyReleaseQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        session.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        session.Window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
        {
            while (session.Model.WorkTasks.Contains(original))
            {
                await Task.Delay(20, timeout.Token);
                Dispatcher.UIThread.RunJobs();
            }
        }
        session.Capture("task-archived");
        Assert.HasCount(1, session.Model.WorkTasks);

        session.Click(session.Window.GetVisualDescendants().OfType<Button>()
            .Single(button => button.Name == "ArchiveButton"));
        var archiveView = session.Window.GetVisualDescendants().OfType<ArchivedWorkTasksView>().Single();
        var archiveList = archiveView.GetVisualDescendants().OfType<ListBox>().Single();
        session.Capture("archive-list");
        Assert.IsTrue(archiveView.IsEffectivelyVisible);
        Assert.AreEqual(1, archiveList.ItemCount);
        Assert.IsTrue(archiveView.GetVisualDescendants().OfType<TextBlock>()
            .Any(block => block.IsEffectivelyVisible && block.Text == original.DisplayName));

        TextBox search = session.Window.GetVisualDescendants().OfType<TextBox>()
            .Single(box => box.PlaceholderText == Lang.Current.WorkTaskSearchText);
        int VisibleSearchResults() => session.Window.GetVisualDescendants().OfType<Button>()
            .Count(button => button.Classes.Contains("TaskSelect") && button.IsEffectivelyVisible);
        session.Click(search);
        session.Window.KeyTextInput(original.DisplayName);
        session.Capture("archive-search-match");
        Assert.AreEqual(1, VisibleSearchResults());
        Assert.IsTrue(session.Window.GetVisualDescendants().OfType<Button>()
            .Any(button => button.Classes.Contains("TaskSelect") && button.IsEffectivelyVisible
                && ReferenceEquals(button.Command, session.Model.RestoreWorkTaskCommand)));

        session.Window.KeyTextInput("-no-matching-archived-task");
        session.Capture("archive-search-no-match");
        Assert.AreEqual(0, VisibleSearchResults());

        session.Window.KeyPressQwerty(PhysicalKey.A, RawInputModifiers.Control);
        session.Window.KeyReleaseQwerty(PhysicalKey.A, RawInputModifiers.Control);
        session.Window.KeyPressQwerty(PhysicalKey.Backspace, RawInputModifiers.None);
        session.Window.KeyReleaseQwerty(PhysicalKey.Backspace, RawInputModifiers.None);
        session.Capture("archive-search-cleared");
        Assert.AreEqual(string.Empty, search.Text);
        Assert.AreEqual(1, VisibleSearchResults());
        Assert.AreEqual(1, archiveList.ItemCount);

        session.Click(archiveView.GetVisualDescendants().OfType<Button>()
            .Single(button => Equals(button.Content, Lang.Current.RestoreWorkTaskText)));
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
        {
            while (archiveList.ItemCount != 0)
            {
                await Task.Delay(20, timeout.Token);
                Dispatcher.UIThread.RunJobs();
            }
        }
        session.Capture("archive-restored");
        Assert.AreEqual(0, archiveList.ItemCount);
        Assert.HasCount(2, session.Model.WorkTasks);

        Button restored = session.Window.GetVisualDescendants().OfType<Button>()
            .Single(button => button.Classes.Contains("TaskSelect") && button.IsEffectivelyVisible
                && button.GetVisualDescendants().OfType<TextBlock>()
                    .Any(block => block.Text == original.DisplayName));
        session.Click(restored);
        session.Capture("restored-task-in-main-list");
        Assert.AreEqual(original.DisplayName, session.Model.ActiveWorkTask.DisplayName);
        Assert.IsFalse(archiveView.IsEffectivelyVisible);
    }
}
