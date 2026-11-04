using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Input;
using AgentLib;
using AgentLib.Model;
using Microsoft.Extensions.AI;
using PrismCodingAgent.CodingShell;
using PrismCodingAgent.CodingShell.ViewModels;

namespace PrismCodingAgent.WpfShell.Tests;

[WpfTestClass]
public sealed class WpfFinalProjectionTests
{
    public TestContext TestContext { get; set; } = null!;

    /// <summary>Renders normal public messages and their production menus without input or clipboard access.</summary>
    [TestMethod]
    public async Task MultipleMessagesAndMenusRenderWithoutSendInputAsync() => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        var resources = new MainWindow();
        resources.Closing += (_, args) => args.Cancel = false;
        var messages = new[]
        {
            new CopilotChatMessage(ChatRole.User, "请检查这段代码的命名，并给出简洁建议。"),
            new CopilotChatMessage(ChatRole.Assistant, "命名整体清晰。建议将结果变量改为明确的业务名称。\n保留现有公共接口，不改变执行行为。"),
            new CopilotChatMessage(ChatRole.User, "好的，请说明验证范围。"),
            new CopilotChatMessage(ChatRole.Assistant, "本次仅检查正常静态界面。\n不执行工具，不调用在线模型，也不操作剪贴板。"),
        };
        var projections = messages.Select(message => new MessageItemViewModel(message)).ToArray();
        var stack = new StackPanel { Margin = new Thickness(24) };
        for (int i = 0; i < messages.Length; i++)
            stack.Children.Add(new ContentControl { Content = projections[i], ContentTemplate = (DataTemplate)resources.Resources[i % 2 == 0 ? "UserMessage" : "AssistantMessage"], HorizontalContentAlignment = HorizontalAlignment.Stretch });
        var surface = new Grid { Width = 800, Height = 600, Background = Brushes.White };
        surface.Children.Add(stack);
        var window = new Window { Content = surface, SizeToContent = SizeToContent.WidthAndHeight, WindowStyle = WindowStyle.None, ShowInTaskbar = false };
        // Retain production command availability; no command is executed.
        foreach (CommandBinding binding in resources.CommandBindings) window.CommandBindings.Add(binding);
        using var screenshots = new WpfScreenshotSession(window, TestContext);
        try
        {
            window.Show();
            await screenshots.CaptureAsync("normal-multiple-messages-menu-closed-projection-not-interaction");
            Assert.AreEqual(4, stack.Children.Count);
            var bubbles = Descendants(stack).OfType<Border>().Where(border => border.ContextMenu is not null).ToArray();
            Assert.HasCount(4, bubbles);
            foreach (int index in new[] { 0, 1 })
            {
                var menu = bubbles[index].ContextMenu;
                menu.PlacementTarget = bubbles[index];
                menu.Placement = PlacementMode.Bottom;
                menu.IsOpen = true;
                CommandManager.InvalidateRequerySuggested();
                await window.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                Assert.IsTrue(((MenuItem)menu.Items[0]).IsEnabled);
                await screenshots.CaptureSurfaceAsync(menu, $"production-{(index == 0 ? "user" : "assistant")}-complete-copy-menu-rendering-not-click");
                Assert.AreEqual(2, menu.Items.Count);
                Assert.AreEqual(projections[index].FullContent, ((MenuItem)menu.Items[1]).CommandParameter);
                menu.IsOpen = false;
            }
        }
        finally
        {
            foreach (var projection in projections) projection.Dispose();
            resources.Close();
        }
    });

    /// <summary>Uses the public shared decision API for static projection, not a simulated approval click.</summary>
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task ApprovalDecisionRendersWithoutSendInputAsync(bool approve) => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        var resources = new MainWindow();
        resources.Closing += (_, args) => args.Cancel = false;
        var approval = new CopilotChatApprovalToolItem("fixture", "inspect_preview", "{\"path\":\"预览/示例.txt\",\"execute\":false}", "仅审阅拟议预览，不执行工具。", "代码预览审批");
        var manager = new CopilotChatManager();
        if (approve) manager.ApproveToolExecution(approval);
        else manager.RejectToolExecution(approval, "本次仅检查界面，暂不执行。 ");
        var content = new ContentControl { Content = approval, ContentTemplate = (DataTemplate)resources.Resources["MessageApproval"], Margin = new Thickness(24), HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Top };
        var surface = new Grid { Width = 800, Height = 520, Background = Brushes.White };
        surface.Children.Add(content);
        var window = new Window { Content = surface, SizeToContent = SizeToContent.WidthAndHeight, WindowStyle = WindowStyle.None, ShowInTaskbar = false };
        using var screenshots = new WpfScreenshotSession(window, TestContext);
        try
        {
            window.Show();
            await screenshots.CaptureAsync($"approval-{(approve ? "approved" : "rejected")}-public-decision-projection-not-click");
            Assert.IsFalse(approval.CanRespondToApproval);
            Assert.IsFalse(Descendants(content).OfType<Button>().Any(button => button.IsVisible));
            Assert.AreEqual(752d, content.ActualWidth, 0.1);
            Assert.IsTrue(Descendants(content).OfType<TextBlock>().Any(text => text.IsVisible && text.Text == approval.ApprovalStateText));
        }
        finally { resources.Close(); }
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
