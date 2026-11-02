using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PrismCodingAgent.AvaloniaShell.Localizations;

namespace PrismCodingAgent.AvaloniaShell.Tests;

/// <summary>通过真实任务菜单和键盘输入验证重命名与置顶，并保留截图。</summary>
[AvaloniaTestClass]
public sealed class ShellTaskMenuScreenshotTests
{
    public TestContext TestContext { get; set; } = null!;

    /// <summary>Captures the production archived search flyout after public command initialization.</summary>
    [TestMethod]
    public async Task ArchivedSearchMenuRendersWithoutInput()
    {
        await using var session = await ShellScreenshotSession.OpenAsync(TestContext);
        var original = session.Model.ActiveWorkTask;
        session.Model.CreateWorkTaskCommand.Execute(null);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (session.Model.WorkTasks.Count != 2)
        {
            await Task.Delay(20, timeout.Token);
            Dispatcher.UIThread.RunJobs();
        }
        session.Model.ArchiveWorkTaskCommand.Execute(original);
        while (session.Model.ArchivedWorkTasks.Count != 1)
        {
            await Task.Delay(20, timeout.Token);
            Dispatcher.UIThread.RunJobs();
        }
        session.Model.WorkTaskSearchText = original.DisplayName;
        Dispatcher.UIThread.RunJobs();
        session.Window.UpdateLayout();
        var more = session.Window.GetVisualDescendants().OfType<Button>().Single(button => button.Classes.Contains("TaskMore") && button.DataContext is PrismCodingAgent.AvaloniaShell.ViewModels.ArchivedWorkTaskItemViewModel);
        var menu = (MenuFlyout)more.Flyout!;
        menu.ShowAt(more);
        Dispatcher.UIThread.RunJobs();
        var restore = menu.Items.OfType<MenuItem>().First();
        session.Window.MouseMove(new Point(1200, 800));
        restore.IsSelected = false;
        Dispatcher.UIThread.RunJobs();
        session.Capture("production-archived-search-menu-rendering-not-interaction");
        TestContext.WriteLine($"Restore state: selected={restore.IsSelected}, focus={restore.IsFocused}, hover={restore.IsPointerOver}");
        Assert.IsFalse(restore.IsSelected);
        CollectionAssert.AreEqual(new[] { Lang.Current.RestoreWorkTaskText.ToString(), Lang.Current.DeleteSessionText.ToString() }, menu.Items.OfType<MenuItem>().Select(item => item.Header!.ToString()).ToArray());
        menu.Hide();
    }

    [TestMethod]
    public async Task TaskMenuShouldRenameAndPinThroughUserInput()
    {
        await using var session = await ShellScreenshotSession.OpenAsync(TestContext);
        var original = session.Model.ActiveWorkTask;
        await session.ClickAndWaitAsync(session.Window.GetVisualDescendants().OfType<Button>()
            .Single(button => ReferenceEquals(button.Command, session.Model.CreateWorkTaskCommand)));
        var created = session.Model.ActiveWorkTask;
        session.Capture("second-task-created");
        Assert.HasCount(2, session.Model.WorkTasks);
        Assert.AreNotSame(original, created);

        Button TaskButton(object task) => session.Window.GetVisualDescendants().OfType<Button>()
            .Single(button => button.Classes.Contains("TaskSelect") && ReferenceEquals(button.CommandParameter, task));
        Button MoreButton() => session.Window.GetVisualDescendants().OfType<Button>()
            .Single(button => button.Classes.Contains("TaskMore") && ReferenceEquals(button.DataContext, original));
        void Press(PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
        {
            session.Window.KeyPressQwerty(key, modifiers);
            session.Window.KeyReleaseQwerty(key, modifiers);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.IsLessThan(TaskButton(original).TranslatePoint(default, session.Window)!.Value.Y,
            TaskButton(created).TranslatePoint(default, session.Window)!.Value.Y);
        Button more = MoreButton();
        session.Click(more);
        var menu = (MenuFlyout)more.Flyout!;
        MenuItem rename = menu.Items.OfType<MenuItem>()
            .Single(item => Equals(item.Header, Lang.Current.RenameWorkTaskText));
        session.Capture("task-rename-menu");
        Assert.IsTrue(menu.IsOpen);
        Assert.IsTrue(rename.IsEffectivelyEnabled);
        Press(PhysicalKey.Home);
        Press(PhysicalKey.ArrowDown);
        Assert.IsTrue(rename.IsSelected);
        Press(PhysicalKey.Enter);

        TextBox editor = session.Window.GetVisualDescendants().OfType<TextBox>()
            .Single(box => box.IsEffectivelyVisible && ReferenceEquals(box.DataContext, original));
        session.Click(editor);
        Press(PhysicalKey.A, RawInputModifiers.Control);
        session.Window.KeyTextInput("菜单重命名任务");
        session.Capture("task-name-edited");
        Assert.AreEqual("菜单重命名任务", editor.Text);
        session.Click(session.Window.GetVisualDescendants().OfType<Button>()
            .Single(button => button.IsEffectivelyVisible && ReferenceEquals(button.DataContext, original)
                && Equals(button.Content, Lang.Current.ConfirmTaskNameText)));
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
        {
            while (original.IsEditing || !session.Model.SaveWorkTaskNameCommand.CanExecute(original))
            {
                await Task.Delay(20, timeout.Token);
                Dispatcher.UIThread.RunJobs();
            }
        }
        session.Capture("task-name-saved");
        Assert.IsFalse(original.IsEditing);
        Assert.AreEqual("菜单重命名任务", original.DisplayName);
        Assert.IsTrue(TaskButton(original).GetVisualDescendants().OfType<TextBlock>()
            .Any(block => block.IsEffectivelyVisible && block.Text == "菜单重命名任务"));

        more = MoreButton();
        session.Click(more);
        menu = (MenuFlyout)more.Flyout!;
        MenuItem pin = menu.Items.OfType<MenuItem>()
            .Single(item => Equals(item.Header, Lang.Current.PinWorkTaskText));
        session.Capture("task-pin-menu");
        Assert.IsTrue(menu.IsOpen);
        Assert.IsTrue(pin.IsEffectivelyEnabled);
        Press(PhysicalKey.Home);
        Assert.IsTrue(pin.IsSelected);
        Press(PhysicalKey.Enter);
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
        {
            while (!ReferenceEquals(session.Model.FilteredWorkTasks[0], original)
                || !session.Model.PinWorkTaskCommand.CanExecute(original))
            {
                await Task.Delay(20, timeout.Token);
                Dispatcher.UIThread.RunJobs();
            }
        }
        string screenshot = session.Capture("renamed-task-pinned");
        CollectionAssert.AreEqual(new[] { original, created }, session.Model.FilteredWorkTasks.ToArray());
        Assert.IsLessThan(TaskButton(created).TranslatePoint(default, session.Window)!.Value.Y,
            TaskButton(original).TranslatePoint(default, session.Window)!.Value.Y);
        Assert.AreSame(created, session.Model.ActiveWorkTask);
        Assert.IsGreaterThan(0L, new FileInfo(screenshot).Length);
    }
}
