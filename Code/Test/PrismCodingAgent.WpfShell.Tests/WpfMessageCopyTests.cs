using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AgentLib.Model;
using PrismCodingAgent.CodingShell;
using PrismCodingAgent.CodingShell.ViewModels;

namespace PrismCodingAgent.WpfShell.Tests;

[WpfTestClass]
public sealed class WpfMessageCopyTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task InternalMenuBindsMessageAndStandardEditingCommandsAsync() => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        var resources = new MainWindow();
        resources.Closing += (_, args) => args.Cancel = false;
        using var message = new MessageItemViewModel(new CopilotChatMessage(Microsoft.Extensions.AI.ChatRole.Assistant, "body text"));
        var content = new ContentControl { Content = message, ContentTemplate = (DataTemplate)resources.Resources["AssistantMessage"] };
        var window = new Window { Width = 800, Height = 400, Content = content };
        try
        {
            window.Show();
            await window.Dispatcher.InvokeAsync(window.UpdateLayout, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            var text = Descendants(content).OfType<TextBox>().Single();
            text.Select(0, 4);
            var menu = text.ContextMenu!;
            menu.PlacementTarget = text;
            menu.IsOpen = true;
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            await window.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            Assert.AreSame(message, menu.DataContext);
            Assert.IsTrue(text.IsReadOnly);
            Assert.AreEqual("body", text.SelectedText);
            Assert.IsFalse(((MenuItem)menu.Items[0]).IsEnabled);
            Assert.IsTrue(((MenuItem)menu.Items[1]).IsEnabled);
            Assert.IsFalse(((MenuItem)menu.Items[2]).IsEnabled);
            Assert.AreEqual(message.Content, ((MenuItem)menu.Items[4]).CommandParameter);
            Assert.AreEqual(message.FullContent, ((MenuItem)menu.Items[5]).CommandParameter);
            Assert.AreSame(message.CreateSessionFromSnapshotCommand, ((MenuItem)menu.Items[6]).Command);
            Assert.AreSame(message, ((MenuItem)menu.Items[6]).CommandParameter);
            using var screenshots = new WpfScreenshotSession(window, TestContext);
            await screenshots.CaptureSurfaceAsync(menu, "internal-message-menu-binding");
            menu.IsOpen = false;
        }
        finally
        {
            window.Close();
            resources.Close();
        }
    });

    [TestMethod]
    public async Task ReadOnlyBodySelectsAndCopiesThroughRealInputAsync() => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        var resources = new MainWindow();
        resources.Show();
        resources.Close();
        await WpfInput.ClickAsync((Button)resources.FindName("ConfirmExitButton"));
        await WpfInput.WaitAsync(() => !resources.IsVisible, "Resource window did not close.");
        const string body = "Selectable message / 可选择消息\r\nSecond line / 第二行\r\nA long wrapped line: " +
            "This message must remain readable and immutable while selecting its content. 选择文字不会编辑消息。";
        var content = new ContentControl
        {
            Content = new CopilotChatTextItem(body),
            ContentTemplate = (DataTemplate)resources.Resources["MessageText"],
            Margin = new Thickness(24),
            VerticalAlignment = VerticalAlignment.Top,
        };
        var surface = new Grid { Background = Brushes.White };
        surface.Children.Add(content);
        var window = new Window { Width = 520, Height = 320, Content = surface, Background = Brushes.White };
        IDataObject? original = Clipboard.GetDataObject();
        try
        {
            window.Show();
            using var screenshots = new WpfScreenshotSession(window, TestContext);
            await screenshots.CaptureAsync("selectable-body-public-projection");
            var text = Descendants(content).OfType<TextBox>().Single();
            Assert.IsTrue(text.IsReadOnly);
            Assert.IsTrue(text.AcceptsReturn);
            Assert.AreEqual(TextWrapping.Wrap, text.TextWrapping);
            Assert.AreEqual(body, text.Text);
            Assert.IsGreaterThan(3, text.LineCount, $"Expected wrapping, actual lines: {text.LineCount}.");
            await WpfInput.ClickAsync(text);
            WpfInput.ControlKey(0x41);
            await WpfInput.WaitAsync(() => text.SelectedText == body, "Real Ctrl+A did not select the body.");
            WpfInput.ControlKey(0x43);
            await window.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            Assert.AreEqual(body, WpfInput.ReadClipboard(TestContext), "Real Ctrl+C did not write the clipboard; no copy retry performed.");
            WpfInput.Text("must not edit");
            await window.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            Assert.AreEqual(body, text.Text);
            Clipboard.SetText("context-copy-sentinel");
            await WpfInput.RightClickAsync(text);
            await WpfInput.WaitAsync(() => text.ContextMenu is { IsOpen: true }, "Framework context menu did not open.");
            var menu = text.ContextMenu!;
            await screenshots.CaptureSurfaceAsync(menu, "framework-copy-menu");
            var copy = menu.Items.OfType<MenuItem>().Single(item => item.Command == System.Windows.Input.ApplicationCommands.Copy);
            Assert.IsTrue(copy.Focus(), "Could not focus the framework Copy menu item.");
            WpfInput.Key(0x0D);
            await WpfInput.WaitAsync(() => !menu.IsOpen, "Copy menu did not close.");
            Assert.AreEqual(body, WpfInput.ReadClipboard(TestContext), "Context menu did not copy; no retry performed.");
        }
        finally
        {
            window.Close();
            if (original is null) Clipboard.Clear();
            else Clipboard.SetDataObject(original, true);
        }
    });

    [TestMethod]
    [Ignore("手动剪贴板验收：依赖系统剪贴板及桌面输入，不作为自动单元测试。")]
    public Task BubbleMenuCopiesExactBodyAndFullMessageThroughRealInputAsync() => VerifyMessageCopyAsync(false);

    [TestMethod]
    [Ignore("手动剪贴板验收：依赖系统剪贴板及桌面输入，不作为自动单元测试。")]
    public Task InternalMenuCopiesExactBodyAndFullMessageThroughRealInputAsync() => VerifyMessageCopyAsync(true);

    private async Task VerifyMessageCopyAsync(bool insideTextBox) => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        var resources = new MainWindow();
        resources.Closing += (_, args) => args.Cancel = false;
        var message = new CopilotChatMessage(Microsoft.Extensions.AI.ChatRole.Assistant, "Exact body / 精确正文\r\nSecond line");
        message.AppendReasoning("Reasoning included only in full message / 完整消息思考");
        using var projection = new MessageItemViewModel(message);
        var snapshotCommand = projection.CreateSessionFromSnapshotCommand;
        var content = new ContentControl
        {
            Content = projection,
            ContentTemplate = (DataTemplate)resources.Resources["AssistantMessage"],
            Margin = new Thickness(24),
            VerticalAlignment = VerticalAlignment.Top,
        };
        var window = new Window { Width = 800, Height = 400, Content = content };
        foreach (System.Windows.Input.CommandBinding binding in resources.CommandBindings)
            window.CommandBindings.Add(binding);
        IDataObject? original = Clipboard.GetDataObject();
        try
        {
            window.Show();
            using var screenshots = new WpfScreenshotSession(window, TestContext);
            await screenshots.CaptureAsync("bubble-copy-public-projection");
            var bubble = Descendants(content).OfType<Border>().Single(border => border.ContextMenu is not null);
            var text = Descendants(content).OfType<TextBox>().Single();
            await WpfInput.ClickAsync(text);
            WpfInput.ControlKey(0x41);
            await WpfInput.WaitAsync(() => text.SelectedText == message.Content, "Ctrl+A did not select body.");
            WpfInput.ControlKey(0x43);
            await window.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            Assert.AreEqual(message.Content, WpfInput.ReadClipboard(TestContext));
            FrameworkElement target = insideTextBox ? text : bubble;
            var menu = target.ContextMenu;
            Clipboard.SetText("body-sentinel");
            await WpfInput.RightClickAsync(target, new Point(target.ActualWidth - 4, 4));
            await WpfInput.WaitAsync(() => menu.IsOpen, "Message menu did not open.");
            Assert.AreSame(projection, menu.DataContext);
            var snapshot = menu.Items.OfType<MenuItem>().Single(item => item.Command == snapshotCommand);
            Assert.AreSame(projection, snapshot.CommandParameter);
            Assert.IsTrue(snapshot.IsEnabled);
            if (insideTextBox)
            {
                Assert.IsFalse(menu.Items.OfType<MenuItem>().Single(item => item.Command == System.Windows.Input.ApplicationCommands.Cut).IsEnabled);
                Assert.IsFalse(menu.Items.OfType<MenuItem>().Single(item => item.Command == System.Windows.Input.ApplicationCommands.Paste).IsEnabled);
            }
            int copyOffset = insideTextBox ? 4 : 0;
            await screenshots.CaptureSurfaceAsync(menu, "bubble-copy-menu");
            await WpfInput.ClickAsync((MenuItem)menu.Items[copyOffset]);
            await WpfInput.WaitAsync(() => !menu.IsOpen, "Body copy menu did not close.");
            Assert.AreEqual("Exact body / 精确正文\r\nSecond line", WpfInput.ReadClipboard(TestContext), "Exact body copy failed; no retry.");
            Clipboard.SetText("full-sentinel");
            await WpfInput.RightClickAsync(target, new Point(target.ActualWidth - 4, 4));
            await WpfInput.WaitAsync(() => menu.IsOpen, "Full message menu did not open.");
            await WpfInput.ClickAsync((MenuItem)menu.Items[copyOffset + 1]);
            await WpfInput.WaitAsync(() => !menu.IsOpen, "Full copy menu did not close.");
            Assert.AreEqual(message.FullContent, WpfInput.ReadClipboard(TestContext), "Exact full message copy failed; no retry.");
        }
        finally
        {
            if (original is null) Clipboard.Clear();
            else Clipboard.SetDataObject(original, true);
            window.Close();
            resources.Close();
        }
    });

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
