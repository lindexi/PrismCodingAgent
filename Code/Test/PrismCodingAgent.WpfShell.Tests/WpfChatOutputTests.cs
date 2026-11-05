using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using AgentLib.Model;
using Microsoft.Extensions.AI;
using PrismCodingAgent.CodingShell;
using PrismCodingAgent.CodingShell.ViewModels;

namespace PrismCodingAgent.WpfShell.Tests;

[WpfTestClass]
public sealed class WpfChatOutputTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ProductionReasoningTemplateStartsCollapsedAndExpandsThroughRealInputAsync() => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        var resources = await LoadResourcesAsync();
        var message = new CopilotChatMessage(ChatRole.Assistant, "Answer 第一行\nSecond line 第二行 " + new string('x', 240));
        message.MessageItems.Insert(0, new CopilotChatReasoningItem("Reasoning 推理过程\nCheck each condition 检查每个条件。"));
        using var projection = new MessageItemViewModel(message);
        var content = new ContentControl { Content = projection, ContentTemplate = (DataTemplate)resources.Resources["AssistantMessage"], Margin = new Thickness(24) };
        var surface = new Grid();
        surface.Children.Add(content);
        var window = new Window { Width = 800, Height = 440, Content = surface, Background = Brushes.White };
        window.Show();
        using var screenshots = new WpfScreenshotSession(window, TestContext);
        await screenshots.CaptureAsync("reasoning-collapsed-bilingual");
        var expander = Descendants(content).OfType<Expander>().Single();
        Assert.IsFalse(expander.IsExpanded);
        await WpfInput.ClickAsync(Descendants(expander).OfType<ToggleButton>().First());
        await WpfInput.WaitAsync(() => expander.IsExpanded, "Reasoning did not expand through input.");
        await window.Dispatcher.InvokeAsync(window.UpdateLayout, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        var answer = Descendants(content).OfType<TextBox>().Single(text => text.Text.StartsWith("Answer 第一行"));
        Rect bounds = answer.TransformToAncestor(window).TransformBounds(new Rect(answer.RenderSize));
        Assert.IsTrue(bounds.Left >= 24 && bounds.Right <= window.ActualWidth - 24 && bounds.Top >= 0 && bounds.Bottom <= window.ActualHeight,
            $"Long text layout was clipped: {bounds}; window={window.ActualWidth}x{window.ActualHeight}.");
        Assert.IsGreaterThanOrEqualTo(answer.FontSize * 3, answer.ActualHeight);
        await screenshots.CaptureAsync("reasoning-expanded-bilingual");
        Assert.IsTrue(Descendants(expander).OfType<TextBlock>().Any(text => text.IsVisible && text.Text.Contains("Check each condition")));
        resources.Close();
    });

    [TestMethod]
    public async Task EmptyAssistantKeepsOuterBubbleWithoutVisibleTextAsync() => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        var resources = await LoadResourcesAsync();
        using var projection = new MessageItemViewModel(new CopilotChatMessage(ChatRole.Assistant, string.Empty));
        var content = new ContentControl { Content = projection, ContentTemplate = (DataTemplate)resources.Resources["AssistantMessage"], Margin = new Thickness(24) };
        var window = new Window { Width = 800, Height = 240, Content = content, Background = Brushes.White };
        window.Show();
        using var screenshots = new WpfScreenshotSession(window, TestContext);
        await screenshots.CaptureAsync("empty-assistant-reference-semantics");
        var parts = Descendants(content).OfType<ItemsControl>().Single();
        var bubble = (Border)VisualTreeHelper.GetParent(parts);
        Assert.IsTrue(bubble.IsVisible && bubble.ActualHeight >= bubble.Padding.Top + bubble.Padding.Bottom);
        Assert.IsFalse(Descendants(parts).OfType<TextBlock>().Any(text => text.IsVisible && !string.IsNullOrEmpty(text.Text)));
        resources.Close();
    });

    [TestMethod]
    public async Task ToolProjectionExpandsAndKeepsCollapseAfterResultThroughRealInputAsync() => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        var resources = await LoadResourcesAsync();
        var message = CopilotChatMessage.CreateAssistant("Projection fixture — 非生产执行", false);
        var tool = message.CreateToolItem(new FunctionCallContent("fixture", "InspectFixture", new Dictionary<string, object?> { ["path"] = "sample/示例.cs" }),
            new ToolCallPresentation("sample/示例.cs", "lines 1–20", "sample/示例.cs:1–20"));
        tool.Details = CopilotChatMessage.CreateAssistant("Detail 详情", false);
        tool.Details.AppendReasoning("Reasoning 推理");
        using var projection = new MessageItemViewModel(message);
        var content = new ContentControl { Content = projection, ContentTemplate = (DataTemplate)resources.Resources["AssistantMessage"], Margin = new Thickness(24) };
        var surface = new Grid();
        surface.Children.Add(content);
        var window = new Window { Width = 900, Height = 680, Content = surface, Background = Brushes.White };
        window.Show();
        using var screenshots = new WpfScreenshotSession(window, TestContext);
        var expander = Descendants(content).OfType<Expander>().First();
        Assert.IsFalse(expander.IsExpanded);
        Assert.IsTrue(Descendants(expander).OfType<TextBlock>().Any(text => text.IsVisible && text.Text == "InspectFixture"));
        Assert.IsTrue(Descendants(expander).OfType<TextBlock>().Any(text => text.IsVisible && text.Text.Contains("sample/示例.cs")));
        await screenshots.CaptureAsync("projection-fixture-tool-collapsed-not-execution");
        await WpfInput.ClickAsync(Descendants(expander).OfType<ToggleButton>().First());
        await WpfInput.WaitAsync(() => expander.IsExpanded, "Tool did not expand through mouse input.");
        Assert.IsTrue(Descendants(expander).OfType<TextBox>().Any(text => text.IsVisible && text.Text == "Detail 详情"));
        message.AppendFunctionResult(new FunctionResultContent("fixture", "Fixture returned 返回内容"));
        await WpfInput.WaitAsync(() => Descendants(expander).OfType<TextBlock>().Any(text => text.IsVisible && text.Text.Contains("Fixture returned")), "Public result projection did not render.");
        await screenshots.CaptureAsync("projection-fixture-tool-expanded-not-execution");
        await WpfInput.ClickAsync(Descendants(expander).OfType<ToggleButton>().First());
        await WpfInput.WaitAsync(() => !expander.IsExpanded, "Tool did not collapse through mouse input.");
        tool.Details.AppendText(" continued");
        await window.Dispatcher.InvokeAsync(window.UpdateLayout, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        Assert.IsFalse(expander.IsExpanded);
        resources.Close();
    });

    [TestMethod]
    public async Task SubAgentProjectionShowsNestedRelationshipsThroughRealInputAsync() => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        var resources = await LoadResourcesAsync();
        var message = CopilotChatMessage.CreateAssistant("Projection fixture — 非生产执行", false);
        var parent = new CopilotChatSubAgentItem("parent", "Parent fixture 父代理", "Task 任务", "Parent result 父返回");
        var child = new CopilotChatSubAgentItem("child", "Child fixture 子代理", "Child task 子任务", "Child result 子返回");
        child.MessageItems.Add(new CopilotChatTextItem("Nested text 嵌套正文"));
        parent.MessageItems.Add(child);
        parent.MessageItems.Add(new CopilotChatToolItem("tool", "NestedTool", "Nested input 嵌套参数", "Nested output 嵌套返回"));
        message.MessageItems.Add(parent);
        using var projection = new MessageItemViewModel(message);
        var content = new ContentControl { Content = projection, ContentTemplate = (DataTemplate)resources.Resources["AssistantMessage"], Margin = new Thickness(24) };
        var surface = new Grid();
        surface.Children.Add(content);
        var window = new Window { Width = 900, Height = 760, Content = surface, Background = Brushes.White };
        window.Show();
        using var screenshots = new WpfScreenshotSession(window, TestContext);
        var expanders = Descendants(content).OfType<Expander>().ToArray();
        Assert.HasCount(3, expanders);
        Assert.IsTrue(expanders[0].IsExpanded && expanders[1].IsExpanded && !expanders[2].IsExpanded);
        Assert.IsTrue(Descendants(expanders[1]).OfType<TextBox>().Any(text => text.IsVisible && text.Text == "Nested text 嵌套正文"));
        await WpfInput.ClickAsync(Descendants(expanders[2]).OfType<ToggleButton>().First());
        await WpfInput.WaitAsync(() => expanders[2].IsExpanded, "Nested tool did not expand through mouse input.");
        Assert.IsTrue(Descendants(expanders[2]).OfType<TextBlock>().Any(text => text.IsVisible && text.Text == "Nested output 嵌套返回"));
        await screenshots.CaptureAsync("projection-fixture-subagents-nested-tool-not-execution");
        await WpfInput.ClickAsync(Descendants(expanders[0]).OfType<ToggleButton>().First());
        await WpfInput.WaitAsync(() => !expanders[0].IsExpanded, "Parent did not collapse through mouse input.");
        Assert.IsFalse(Descendants(expanders[1]).OfType<TextBox>().Any(text => text.IsVisible && text.Text == "Nested text 嵌套正文"));
        await screenshots.CaptureAsync("projection-fixture-parent-collapsed-not-execution");
        resources.Close();
    });

    private static async Task<MainWindow> LoadResourcesAsync()
    {
        var window = new MainWindow();
        window.Show();
        window.Close();
        await WpfInput.ClickAsync((Button)window.FindName("ConfirmExitButton"));
        await WpfInput.WaitAsync(() => !window.IsVisible, "Resource window did not close.");
        return window;
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
