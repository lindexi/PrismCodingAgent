using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AgentLib.Model;
using Microsoft.Extensions.AI;
using PrismCodingAgent.CodingShell;
using PrismCodingAgent.CodingShell.ViewModels;

namespace PrismCodingAgent.WpfShell.Tests;

/// <summary>Renders public tool and nested agent projections without input injection or execution.</summary>
[WpfTestClass]
public sealed class WpfToolRenderingTests
{
    public TestContext TestContext { get; set; } = null!;

    /// <summary>Captures initialized model states, not user gestures or actual tool execution.</summary>
    [TestMethod]
    public async Task ProjectedToolsAndAgentsRenderLocallyWithoutSendInputAsync() => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        var resources = new MainWindow();
        resources.Closing += (_, args) => args.Cancel = false;
        var message = CopilotChatMessage.CreateAssistant("公开模型渲染示例；非实际工具执行。", false);
        var tool = new CopilotChatToolItem("tool", "读取示例", "路径：示例/说明.cs\n范围：第1至20行", "示例输出：仅用于检查参数与结果布局。", new ToolCallPresentation("示例/说明.cs", "第1至20行"));
        tool.Details = CopilotChatMessage.CreateAssistant("工具详情：公开模型初始化，不运行工具。", false);
        message.MessageItems.Add(tool);
        var parent = new CopilotChatSubAgentItem("parent", "父代理示例", "核对示例布局", "父代理示例返回");
        var child = new CopilotChatSubAgentItem("child", "嵌套子代理示例", "检查嵌套内容", "子代理示例返回");
        child.MessageItems.Add(new CopilotChatTextItem("嵌套正文：不调用外部模型。"));
        parent.MessageItems.Add(child);
        parent.MessageItems.Add(new CopilotChatToolItem("nested", "嵌套工具示例", "示例参数", "示例输出"));
        message.MessageItems.Add(parent);
        using var projection = new MessageItemViewModel(message);
        var content = new ContentControl { Content = projection, ContentTemplate = (DataTemplate)resources.Resources["AssistantMessage"], Margin = new Thickness(24), VerticalContentAlignment = VerticalAlignment.Top, VerticalAlignment = VerticalAlignment.Top };
        var surface = new Grid { Width = 800, Height = 1000, Background = Brushes.White };
        surface.Children.Add(content);
        var viewport = new Viewbox { Child = surface, Stretch = Stretch.Uniform };
        var window = new Window { Content = viewport, Width = 560, Height = 700, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, Background = Brushes.White };
        using var screenshots = new WpfScreenshotSession(window, TestContext);
        int step = 0;
        async Task CaptureAsync(string name)
        {
            await window.Dispatcher.InvokeAsync(window.UpdateLayout, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            // Render the measured surface offscreen so the desktop work area cannot clip a tall fixture.
            viewport.Child = null;
            surface.InvalidateMeasure();
            surface.InvalidateArrange();
            surface.Measure(new Size(800, 1000));
            surface.Arrange(new Rect(0, 0, 800, 1000));
            surface.UpdateLayout();
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(800, 1000, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(surface);
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            string path = System.IO.Path.Combine(screenshots.DirectoryPath, $"{++step:00}-{name}.png");
            using (var stream = System.IO.File.Create(path)) encoder.Save(stream);
            TestContext.AddResultFile(path);
            viewport.Child = surface;
            window.UpdateLayout();
        }
        try
        {
            window.Show();
            await CaptureAsync("tools-agents-default-public-projection-not-execution");
            Assert.AreEqual(752d, content.ActualWidth, 0.1);
            Expander Find(object model) => Descendants(content).OfType<Expander>().Single(e => ReferenceEquals(e.DataContext, model));
            Assert.IsFalse(Find(tool).IsExpanded);
            Assert.IsTrue(Find(parent).IsExpanded);
            Assert.IsTrue(Find(child).IsExpanded);
            Find(tool).SetCurrentValue(Expander.IsExpandedProperty, true);
            Find(parent.MessageItems[1]).SetCurrentValue(Expander.IsExpandedProperty, true);
            await CaptureAsync("tools-agents-expanded-public-projection-not-execution");
            Assert.IsTrue(Descendants(content).OfType<TextBox>().Any(t => t.IsVisible && t.Text.Contains("工具详情：")));
            Assert.IsTrue(Descendants(content).OfType<TextBlock>().Any(t => t.IsVisible && t.Text == "示例输出") || Descendants(content).OfType<TextBox>().Any(t => t.IsVisible && t.Text == "示例输出"));
            Find(child).SetCurrentValue(Expander.IsExpandedProperty, false);
            await CaptureAsync("tools-agents-child-collapsed-public-projection-not-execution");
            Assert.IsFalse(Descendants(Find(child)).OfType<TextBox>().Any(t => t.IsVisible && t.Text.Contains("嵌套正文")));
            Find(tool).SetCurrentValue(Expander.IsExpandedProperty, false);
            Find(parent).SetCurrentValue(Expander.IsExpandedProperty, false);
            await CaptureAsync("tools-agents-collapsed-public-projection-not-execution");
            Assert.IsFalse(Find(parent).IsExpanded);
        }
        finally
        {
            window.Close();
            resources.Close();
        }
    });

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
