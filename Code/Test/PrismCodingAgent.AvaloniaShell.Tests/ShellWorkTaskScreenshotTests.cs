using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using PrismCodingAgent.AvaloniaShell.Localizations;

namespace PrismCodingAgent.AvaloniaShell.Tests;

/// <summary>通过真实鼠标和键盘输入检查工作任务导航与搜索，并保留截图。</summary>
[AvaloniaTestClass]
public sealed class ShellWorkTaskScreenshotTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task WorkTasksShouldCreateSwitchSearchAndRestoreThroughUserInput()
    {
        string originalLanguage = Lang.Current.IetfLanguageTag;
        Lang.SetCurrent("zh-Hans");
        try
        {
            await using var session = await ShellScreenshotSession.OpenAsync(TestContext);
            string mainScreenshot = session.Capture("main-window");
            Assert.IsGreaterThan(0L, new FileInfo(mainScreenshot).Length);
            var original = session.Model.ActiveWorkTask;

            Button create = session.Window.GetVisualDescendants().OfType<Button>()
                .Single(button => ReferenceEquals(button.Command, session.Model.CreateWorkTaskCommand));
            await session.ClickAndWaitAsync(create);
            session.Capture("task-created");
            Assert.HasCount(2, session.Model.WorkTasks);
            var created = session.Model.ActiveWorkTask;
            Assert.AreNotSame(original, created);

            Button TaskButton(object task) => session.Window.GetVisualDescendants().OfType<Button>()
                .Single(button => button.Classes.Contains("TaskSelect") && ReferenceEquals(button.CommandParameter, task));
            int VisibleTaskCount() => session.Window.GetVisualDescendants().OfType<Button>()
                .Count(button => button.Classes.Contains("TaskSelect") && button.IsVisible);

            session.Click(TaskButton(original));
            session.Capture("switched-to-original");
            Assert.AreSame(original, session.Model.ActiveWorkTask);
            session.Click(TaskButton(created));
            session.Capture("switched-to-created");
            Assert.AreSame(created, session.Model.ActiveWorkTask);

            TextBox search = session.Window.GetVisualDescendants().OfType<TextBox>()
                .Single(box => box.PlaceholderText == Lang.Current.WorkTaskSearchText);
            session.Click(search);
            session.Window.KeyTextInput(original.DisplayName);
            session.Capture("search-match");
            Assert.AreEqual(1, VisibleTaskCount());
            Assert.AreSame(original, TaskButton(original).CommandParameter);

            session.Window.KeyTextInput("-no-such-task");
            session.Capture("search-empty");
            Assert.AreEqual(0, VisibleTaskCount());
            Assert.AreSame(created, session.Model.ActiveWorkTask);

            session.Window.KeyPressQwerty(PhysicalKey.A, RawInputModifiers.Control);
            session.Window.KeyReleaseQwerty(PhysicalKey.A, RawInputModifiers.Control);
            session.Window.KeyPressQwerty(PhysicalKey.Backspace, RawInputModifiers.None);
            session.Window.KeyReleaseQwerty(PhysicalKey.Backspace, RawInputModifiers.None);
            session.Capture("search-cleared");
            Assert.AreEqual(string.Empty, search.Text);
            Assert.AreEqual(2, VisibleTaskCount());
            Assert.AreSame(created, session.Model.ActiveWorkTask);
        }
        finally
        {
            Lang.SetCurrent(originalLanguage);
        }
    }
}
