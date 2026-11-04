using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AgentLib.Model;
using Microsoft.Extensions.AI;
using PrismCodingAgent.CodingShell;
using PrismCodingAgent.CodingShell.ViewModels;
using PrismCodingAgent.CodingShell.Views;

namespace PrismCodingAgent.WpfShell.Tests;

[WpfTestClass]
public sealed class WpfMessageImageTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task AssistantImageOpensBuiltInViewerThroughRealMouseAsync() => await ShowAsync(ChatRole.Assistant, false);

    [TestMethod]
    public async Task UserImageOpensBuiltInViewerThroughRealMouseAsync() => await ShowAsync(ChatRole.User, false);

    [TestMethod]
    public async Task ToolDetailImageOpensBuiltInViewerThroughRealMouseAsync() => await ShowAsync(ChatRole.Assistant, true);

    [TestMethod]
    public async Task InvalidImageKeepsTextWithoutFakeImageAsync() => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        var resources = new MainWindow();
        resources.Show();
        resources.Close();
        await WpfInput.ClickAsync((Button)resources.FindName("ConfirmExitButton"));
        await WpfInput.WaitAsync(() => !resources.IsVisible, "Resource window did not close.");
        var message = new CopilotChatMessage(ChatRole.Assistant, "Invalid image fallback 无效图片回退 — text remains 正文保留");
        message.MessageItems.Add(new CopilotChatImageItem(BinaryData.FromString("not an image"), "image/png"));
        using var projection = new MessageItemViewModel(message);
        var content = new ContentControl { Content = projection, ContentTemplate = (DataTemplate)resources.Resources["AssistantMessage"], Margin = new Thickness(24) };
        var window = new Window { Width = 820, Height = 400, Content = content, Background = Brushes.White };
        try
        {
            window.Show();
            using var screenshots = new WpfScreenshotSession(window, TestContext);
            await screenshots.CaptureAsync("invalid-message-image-blank-text-preserved");
            Assert.IsNull(Descendants(content).OfType<BinaryImage>().Single().Source);
            Assert.IsTrue(Descendants(content).OfType<TextBox>().Any(text => text.IsVisible && text.Text.StartsWith("Invalid image fallback")));
            Assert.IsFalse(Descendants(content).OfType<TextBlock>().Any(text => text.Text.Contains(nameof(CopilotChatImageItem))));
        }
        finally
        {
            window.Close();
        }
    });

    /// <summary>Captures production image projections without input injection or viewer interaction.</summary>
    [TestMethod]
    [DataRow(false, false, false)]
    [DataRow(true, false, false)]
    [DataRow(false, true, false)]
    [DataRow(false, false, true)]
    public Task ImageProjectionRendersWithoutSendInputAsync(bool user, bool tool, bool invalid) => WpfTestClassAttribute.RunUiAsync(async () =>
    {
        var resources = new MainWindow();
        resources.Closing += (_, args) => args.Cancel = false;
        var message = new CopilotChatMessage(user ? ChatRole.User : ChatRole.Assistant, invalid
            ? "Invalid image fallback 无效图片回退 — text remains 正文保留"
            : "Image projection fixture / 图片公开投影夹具 — not execution 非真实执行");
        var image = new CopilotChatImageItem(invalid ? BinaryData.FromString("not an image") : CreateImage(), "image/png");
        if (tool)
        {
            var details = new CopilotChatMessage(ChatRole.Tool, "Tool image details 工具图片详情");
            details.MessageItems.Add(image);
            message.MessageItems.Add(new CopilotChatToolItem("fixture", "Image details 图片详情", "No tool execution 不执行工具") { Details = details });
        }
        else message.MessageItems.Add(image);
        using var projection = new MessageItemViewModel(message);
        var content = new ContentControl { Content = projection, ContentTemplate = (DataTemplate)resources.Resources[user ? "UserMessage" : "AssistantMessage"], Margin = new Thickness(24), VerticalAlignment = VerticalAlignment.Top, VerticalContentAlignment = VerticalAlignment.Top };
        var window = new Window { Width = 800, Height = 520, Content = content, Background = Brushes.White, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize };
        using var screenshots = new WpfScreenshotSession(window, TestContext);
        try
        {
            window.Show();
            window.UpdateLayout();
            if (tool) Descendants(content).OfType<Expander>().Single().SetCurrentValue(Expander.IsExpandedProperty, true);
            await screenshots.CaptureSurfaceAsync(window, $"image-{(invalid ? "invalid" : tool ? "tool" : user ? "user" : "assistant")}-public-projection-not-interaction");
            Assert.AreEqual(!invalid, Descendants(content).OfType<BinaryImage>().Single().Source is not null);
            Assert.IsTrue(Descendants(content).OfType<TextBox>().Any(text => text.IsVisible && text.Text.StartsWith(invalid ? "Invalid image fallback" : "Image projection fixture")));
        }
        finally { window.Close(); resources.Close(); }
    });

    private Task ShowAsync(ChatRole role, bool tool) => WpfTestClassAttribute.RunUiAsync(async () =>
    {
        var resources = new MainWindow();
        resources.Show();
        resources.Close();
        await WpfInput.ClickAsync((Button)resources.FindName("ConfirmExitButton"));
        await WpfInput.WaitAsync(() => !resources.IsVisible, "Resource window did not close.");
        var message = new CopilotChatMessage(role, "Image projection fixture / 图片公开投影夹具 — not execution 非真实执行");
        var image = new CopilotChatImageItem(CreateImage(), "image/png");
        if (tool)
        {
            var details = new CopilotChatMessage(ChatRole.Tool, "Tool image details 工具图片详情");
            details.MessageItems.Add(image);
            message.MessageItems.Add(new CopilotChatToolItem("fixture", "Image details 图片详情", "No tool execution 不执行工具") { Details = details });
        }
        else message.MessageItems.Add(image);
        using var projection = new MessageItemViewModel(message);
        var content = new ContentControl { Content = projection, ContentTemplate = (DataTemplate)resources.Resources[role == ChatRole.User ? "UserMessage" : "AssistantMessage"], Margin = new Thickness(24) };
        var surface = new Grid { Background = Brushes.White };
        surface.Children.Add(content);
        var window = new Window { Width = 800, Height = 520, Content = surface, Background = Brushes.White };
        try
        {
            window.Show();
            window.UpdateLayout();
            window.Width += 800 - surface.ActualWidth;
            window.Height += 520 - surface.ActualHeight;
            window.UpdateLayout();
            using var screenshots = new WpfScreenshotSession(window, TestContext);
            if (tool)
            {
                var expander = Descendants(content).OfType<Expander>().Single();
                await WpfInput.ClickAsync(Descendants(expander).OfType<ToggleButton>().First());
                await WpfInput.WaitAsync(() => expander.IsExpanded, "Tool details did not expand.");
            }
            await screenshots.CaptureAsync("message-image-public-projection-not-execution");
            var preview = Descendants(content).OfType<BinaryImage>().Single();
            Assert.IsNotNull(preview.Source);
            Assert.AreEqual(Stretch.Uniform, preview.Stretch);
            Rect bounds = preview.TransformToAncestor(window).TransformBounds(new Rect(preview.RenderSize));
            Assert.IsTrue(bounds.Left >= 24 && bounds.Right <= window.ActualWidth - 24 && bounds.Bottom < window.ActualHeight - 24);
            Assert.IsFalse(Descendants(content).OfType<TextBlock>().Any(text => text.Text.Contains(nameof(CopilotChatImageItem))));
            var button = Descendants(content).OfType<Button>().Single(item => ReferenceEquals(item.CommandParameter, image));
            await WpfInput.ClickAsync(button);
            await WpfInput.WaitAsync(() => window.OwnedWindows.Count == 1, "Built-in viewer did not open.");
            Window viewer = window.OwnedWindows[0];
            using var viewerScreenshots = new WpfScreenshotSession(viewer, TestContext);
            await viewerScreenshots.CaptureAsync("clicked-built-in-message-image-viewer");
            var fullImage = Assert.IsInstanceOfType<BinaryImage>(viewer.Content);
            await viewerScreenshots.CaptureSurfaceAsync(viewer, "whole-window-image-boundary");
            Rect imageBounds = fullImage.TransformToAncestor(viewer).TransformBounds(new Rect(fullImage.RenderSize));
            Rect drawingBounds = VisualTreeHelper.GetDrawing(fullImage).Bounds;
            TestContext.WriteLine($"Viewer={viewer.ActualWidth}x{viewer.ActualHeight}; image={fullImage.ActualWidth}x{fullImage.ActualHeight}; margin={fullImage.Margin}; image bounds={imageBounds}; drawing={drawingBounds}; source={fullImage.Source.Width}x{fullImage.Source.Height}");
            Assert.IsTrue(drawingBounds.Left >= 0 && drawingBounds.Top >= 0 && drawingBounds.Right <= fullImage.ActualWidth + 0.01 && drawingBounds.Bottom <= fullImage.ActualHeight + 0.01);
            Assert.IsTrue(imageBounds.Left >= 0 && imageBounds.Top >= 0 && imageBounds.Right <= viewer.ActualWidth && imageBounds.Bottom <= viewer.ActualHeight);
            Assert.AreEqual(Stretch.Uniform, fullImage.Stretch);
            Assert.IsNotNull(fullImage.Source);
            Assert.IsTrue(fullImage.ActualWidth > preview.ActualWidth && fullImage.ActualHeight > preview.ActualHeight);
            WpfInput.Key(0x1B);
            await WpfInput.WaitAsync(() => !viewer.IsVisible && window.OwnedWindows.Count == 0, "Escape did not close viewer.");
        }
        finally
        {
            window.Close();
        }
    });

    internal static BinaryData CreateImage()
    {
        var visual = new DrawingVisual();
        using (DrawingContext drawing = visual.RenderOpen())
        {
            drawing.DrawRectangle(Brushes.Orange, null, new Rect(0, 0, 480, 240));
        }
        var bitmap = new RenderTargetBitmap(480, 240, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return BinaryData.FromBytes(stream.ToArray());
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            yield return child;
            foreach (DependencyObject descendant in Descendants(child)) yield return descendant;
        }
    }
}
