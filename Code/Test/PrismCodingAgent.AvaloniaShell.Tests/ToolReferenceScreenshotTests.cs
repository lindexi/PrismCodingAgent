using AgentLib.Model;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PrismCodingAgent.AvaloniaShell.Localizations;
using PrismCodingAgent.AvaloniaShell.ViewModels;
using PrismCodingAgent.AvaloniaShell.Views;

namespace PrismCodingAgent.AvaloniaShell.Tests;

[AvaloniaTestClass]
public sealed class ToolReferenceScreenshotTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ProjectedToolsAndAgentsRenderWithoutExecution()
    {
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
        var view = new ChatView();
        var content = new ContentControl
        {
            Content = projection, ContentTemplate = view.FindControl<ItemsControl>("MessagesItemsControl")!.ItemTemplate,
            Margin = new Thickness(24), HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Top
        };
        view.Content = content;
        var window = new Window { Width = 800, Height = 1000, Content = view, Background = Brushes.White };
        string directory = Path.Combine(AppContext.BaseDirectory, "ShellScreenshots", TestContext.TestName!, Lang.Current.IetfLanguageTag, Guid.NewGuid().ToString("N"));
        try
        {
            window.Show();
            await CaptureAsync("01-tools-agents-default-public-projection-not-execution.png");
            Assert.AreEqual(752d, content.Bounds.Width, 0.1);
            Expander Find(object model) => content.GetVisualDescendants().OfType<Expander>().Single(e => ReferenceEquals(e.DataContext, model));
            Assert.IsFalse(Find(tool).IsExpanded);
            Assert.IsTrue(Find(parent).IsExpanded);
            Assert.IsTrue(Find(child).IsExpanded);
            Find(tool).SetCurrentValue(Expander.IsExpandedProperty, true);
            Find(parent.MessageItems[1]).SetCurrentValue(Expander.IsExpandedProperty, true);
            await CaptureAsync("02-tools-agents-expanded-public-projection-not-execution.png");
            Assert.IsTrue(content.GetVisualDescendants().OfType<TextBox>().Any(t => t.IsEffectivelyVisible && t.Text == "示例输出"));
            Find(child).SetCurrentValue(Expander.IsExpandedProperty, false);
            await CaptureAsync("03-tools-agents-child-collapsed-public-projection-not-execution.png");
            Assert.IsFalse(Find(child).GetVisualDescendants().OfType<TextBox>().Any(t => t.IsEffectivelyVisible && t.Text!.Contains("嵌套正文")));
            Find(tool).SetCurrentValue(Expander.IsExpandedProperty, false);
            Find(parent).SetCurrentValue(Expander.IsExpandedProperty, false);
            await CaptureAsync("04-tools-agents-collapsed-public-projection-not-execution.png");
            Assert.IsFalse(Find(parent).IsExpanded);
        }
        finally { window.Close(); }

        async Task CaptureAsync(string name)
        {
            window.MouseMove(new Point(790, 990));
            await Task.Delay(300);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, name);
            using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No tool reference frame.");
            frame.Save(path, PngBitmapEncoderOptions.Default);
            TestContext.WriteLine(path);
            TestContext.AddResultFile(path);
        }
    }
}
