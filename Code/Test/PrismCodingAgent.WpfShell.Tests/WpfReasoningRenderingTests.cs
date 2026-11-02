using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AgentLib.Model;
using Microsoft.Extensions.AI;
using PrismCodingAgent.AvaloniaShell.ViewModels;

namespace PrismCodingAgent.WpfShell.Tests;

/// <summary>Checks production message rendering without SendInput or model/tool execution.</summary>
[WpfTestClass]
public sealed class WpfReasoningRenderingTests
{
    public TestContext TestContext { get; set; } = null!;

    /// <summary>Captures projected reasoning states, not user interaction, on the existing STA host.</summary>
    [TestMethod]
    public async Task ProjectedReasoningRendersLocallyWithoutSendInputAsync() => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        // Constructing the resource owner does not show the business window or run startup.
        var resources = new MainWindow();
        // No business VM/session is attached; the unshown resource owner needs no exit confirmation.
        resources.Closing += (_, args) => args.Cancel = false;
        var message = new CopilotChatMessage(ChatRole.Assistant, "Answer 第一行\nSecond line 第二行 " + new string('x', 240));
        message.MessageItems.Insert(0, new CopilotChatReasoningItem("Reasoning 推理过程\nCheck each condition 检查每个条件。"));
        using var projection = new MessageItemViewModel(message);
        var content = new ContentControl
        {
            Content = projection,
            ContentTemplate = (DataTemplate)resources.Resources["AssistantMessage"],
            Margin = new Thickness(24),
        };
        var surface = new Grid { Width = 800, Height = 440, Background = Brushes.White };
        surface.Children.Add(content);
        var window = new Window
        {
            Width = 800, Height = 440, WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize, SizeToContent = SizeToContent.WidthAndHeight,
            Content = surface, Background = Brushes.White, ShowInTaskbar = false,
        };
        using var screenshots = new WpfScreenshotSession(window, TestContext);
        try
        {
            window.Show();
            string collapsed = await screenshots.CaptureAsync("reasoning-collapsed-bilingual");
            Assert.AreEqual(752d, content.ActualWidth, 0.1);
            var expander = Descendants(content).OfType<Expander>().Single();
            Assert.IsFalse(expander.IsExpanded);
            Assert.IsFalse(Descendants(expander).OfType<TextBlock>().Any(text => text.IsVisible && text.Text.Contains("Check each condition")));
            // State projection for rendering only; the separate real-input test still verifies the gesture.
            expander.SetCurrentValue(Expander.IsExpandedProperty, true);
            string expanded = await screenshots.CaptureAsync("reasoning-expanded-bilingual");
            Assert.IsTrue(Descendants(expander).OfType<TextBlock>().Any(text => text.IsVisible && text.Text.Contains("Check each condition")));
            var answer = Descendants(content).OfType<TextBox>().Single(text => text.Text.StartsWith("Answer 第一行"));
            Rect bounds = answer.TransformToAncestor(surface).TransformBounds(new Rect(answer.RenderSize));
            Assert.IsTrue(bounds.Left >= 24 && bounds.Right <= 776 && bounds.Top >= 0 && bounds.Bottom <= 440,
                $"Long text layout was clipped: {bounds}.");
            Assert.IsGreaterThanOrEqualTo(answer.FontSize * 3, answer.ActualHeight);
            VerifyPng(collapsed, surface);
            VerifyPng(expanded, surface);
            message.AppendUsageDetails(new UsageDetails { InputTokenCount = 12, OutputTokenCount = 8, TotalTokenCount = 20 });
            string usage = await screenshots.CaptureAsync("reasoning-usage-public-projection-not-execution");
            var summary = Descendants(content).OfType<TextBlock>().Single(text => text.Text == projection.UsageSummaryText);
            Assert.IsTrue(summary.IsVisible);
            VerifyPng(usage, surface);
        }
        finally
        {
            window.Close();
            resources.Close();
        }
    });

    private static void VerifyPng(string path, FrameworkElement surface)
    {
        using FileStream stream = File.OpenRead(path);
        BitmapFrame frame = BitmapFrame.Create(stream);
        DpiScale dpi = VisualTreeHelper.GetDpi(surface);
        Assert.AreEqual((int)Math.Ceiling(800 * dpi.DpiScaleX), frame.PixelWidth);
        Assert.AreEqual((int)Math.Ceiling(440 * dpi.DpiScaleY), frame.PixelHeight);
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
