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
            Assert.IsTrue(text.LineCount > 3, $"Expected wrapping, actual lines: {text.LineCount}.");
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
            await WpfInput.ClickAsync(copy);
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
    public async Task BubbleMenuCopiesExactBodyAndFullMessageThroughRealInputAsync() => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        var window = new MainWindow();
        object originalContent = window.Content;
        var message = new CopilotChatMessage(Microsoft.Extensions.AI.ChatRole.Assistant, "Exact body / 精确正文\r\nSecond line");
        message.AppendReasoning("Reasoning included only in full message / 完整消息思考");
        using var projection = new MessageItemViewModel(message);
        var content = new ContentControl
        {
            Content = projection,
            ContentTemplate = (DataTemplate)window.Resources["AssistantMessage"],
            Margin = new Thickness(24),
            VerticalAlignment = VerticalAlignment.Top,
        };
        window.Content = content;
        IDataObject? original = Clipboard.GetDataObject();
        try
        {
            window.Show();
            using var screenshots = new WpfScreenshotSession(window, TestContext);
            await screenshots.CaptureAsync("bubble-copy-public-projection");
            var bubble = Descendants(content).OfType<Border>().Single(border => border.ContextMenu is not null);
            Clipboard.SetText("body-sentinel");
            await WpfInput.RightClickAsync(bubble, new Point(bubble.ActualWidth - 4, 4));
            await WpfInput.WaitAsync(() => bubble.ContextMenu.IsOpen, "Bubble menu did not open.");
            var menu = bubble.ContextMenu;
            await screenshots.CaptureSurfaceAsync(menu, "bubble-copy-menu");
            await WpfInput.ClickAsync((MenuItem)menu.Items[0]);
            await WpfInput.WaitAsync(() => !menu.IsOpen, "Body copy menu did not close.");
            Assert.AreEqual("Exact body / 精确正文\r\nSecond line", WpfInput.ReadClipboard(TestContext), "Exact body copy failed; no retry.");
            Clipboard.SetText("full-sentinel");
            await WpfInput.RightClickAsync(bubble, new Point(bubble.ActualWidth - 4, 4));
            await WpfInput.WaitAsync(() => menu.IsOpen, "Full message menu did not open.");
            await WpfInput.ClickAsync((MenuItem)menu.Items[1]);
            await WpfInput.WaitAsync(() => !menu.IsOpen, "Full copy menu did not close.");
            Assert.AreEqual("Exact body / 精确正文\r\nSecond line\r\n--------\r\n思考：\r\nReasoning included only in full message / 完整消息思考", WpfInput.ReadClipboard(TestContext), "Exact full message copy failed; no retry.");
        }
        finally
        {
            if (original is null) Clipboard.Clear();
            else Clipboard.SetDataObject(original, true);
            window.Content = originalContent;
            window.Close();
            await WpfInput.ClickAsync((Button)window.FindName("ConfirmExitButton"));
            await WpfInput.WaitAsync(() => !window.IsVisible, "Copy window did not close.");
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
