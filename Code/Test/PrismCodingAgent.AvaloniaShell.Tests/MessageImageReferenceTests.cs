using AgentLib.Model;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Microsoft.Extensions.AI;
using PrismCodingAgent.AvaloniaShell.Localizations;
using PrismCodingAgent.AvaloniaShell.ViewModels;
using PrismCodingAgent.AvaloniaShell.Views;

namespace PrismCodingAgent.AvaloniaShell.Tests;

[AvaloniaTestClass]
public sealed class MessageImageReferenceTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(false, false, false)]
    [DataRow(true, false, false)]
    [DataRow(false, true, false)]
    [DataRow(false, false, true)]
    public void ActualImageTemplateAndToolDetailsRenderWithoutExternalViewer(bool user, bool toolDetails, bool invalid)
    {
        var source = new Border { Width = 480, Height = 240, Background = Brushes.Orange };
        source.Measure(new Size(480, 240));
        source.Arrange(new Rect(0, 0, 480, 240));
        using var bitmap = new RenderTargetBitmap(new PixelSize(480, 240));
        bitmap.Render(source);
        using var stream = new MemoryStream();
        bitmap.Save(stream, PngBitmapEncoderOptions.Default);
        var image = new CopilotChatImageItem(invalid ? BinaryData.FromString("not an image") : BinaryData.FromBytes(stream.ToArray()), "image/png");
        var message = new CopilotChatMessage(user ? ChatRole.User : ChatRole.Assistant,
            invalid ? "Invalid image fallback 无效图片回退 — text remains 正文保留" : "Image projection fixture / 图片公开投影夹具 — not execution 非真实执行");
        if (toolDetails)
        {
            var details = new CopilotChatMessage(ChatRole.Tool, "Tool image details 工具图片详情");
            details.MessageItems.Add(image);
            message.MessageItems.Add(new CopilotChatToolItem("fixture", "Image details 图片详情", "No tool execution 不执行工具") { Details = details });
        }
        else
        {
            message.MessageItems.Add(image);
        }
        using var projection = new MessageItemViewModel(message);
        var view = new ChatView();
        var selector = view.FindControl<ItemsControl>("MessagesItemsControl")!.ItemTemplate;
        var content = new ContentControl
        {
            Content = projection, ContentTemplate = selector, Margin = new Thickness(24),
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Top
        };
        view.Content = content;
        var window = new Window { Width = 800, Height = 520, Content = view, Background = Brushes.White };
        try
        {
            window.Show();
            window.UpdateLayout();
            if (toolDetails)
                content.GetVisualDescendants().OfType<Expander>().Single().IsExpanded = true;
            window.UpdateLayout();
            Assert.AreEqual(1, content.GetVisualDescendants().OfType<BinaryImage>().Count(control => control.IsEffectivelyVisible));
            string directory = Path.Combine(AppContext.BaseDirectory, "ShellScreenshots", TestContext.TestName!, Lang.Current.IetfLanguageTag, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string kind = invalid ? "invalid" : toolDetails ? "tool" : user ? "user" : "assistant";
            string path = Path.Combine(directory, $"01-actual-avalonia-{kind}-image-no-execution.png");
            using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No reference frame.");
            frame.Save(path, PngBitmapEncoderOptions.Default);
            TestContext.WriteLine(path);
            TestContext.AddResultFile(path);
        }
        finally
        {
            window.Close();
        }
    }
}
