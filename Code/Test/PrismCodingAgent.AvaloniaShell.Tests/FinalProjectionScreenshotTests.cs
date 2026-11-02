using AgentLib;
using AgentLib.Model;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.AI;
using PrismCodingAgent.AvaloniaShell.Localizations;
using PrismCodingAgent.AvaloniaShell.ViewModels;
using PrismCodingAgent.AvaloniaShell.Views;

namespace PrismCodingAgent.AvaloniaShell.Tests;

[AvaloniaTestClass]
public sealed class FinalProjectionScreenshotTests
{
    public TestContext TestContext { get; set; } = null!;

    /// <summary>Renders production message templates and menus without input or clipboard access.</summary>
    [TestMethod]
    public void MultipleMessagesAndMenusRenderWithoutInput()
    {
        var messages = new[]
        {
            new CopilotChatMessage(ChatRole.User, "请检查这段代码的命名，并给出简洁建议。"),
            new CopilotChatMessage(ChatRole.Assistant, "命名整体清晰。建议将结果变量改为明确的业务名称。\n保留现有公共接口，不改变执行行为。"),
            new CopilotChatMessage(ChatRole.User, "好的，请说明验证范围。"),
            new CopilotChatMessage(ChatRole.Assistant, "本次仅检查正常静态界面。\n不执行工具，不调用在线模型，也不操作剪贴板。"),
        };
        var projections = messages.Select(message => new MessageItemViewModel(message)).ToArray();
        var view = new ChatView();
        var selector = view.FindControl<ItemsControl>("MessagesItemsControl")!.ItemTemplate;
        var items = new ItemsControl { ItemsSource = projections, ItemTemplate = selector, Margin = new Thickness(24) };
        view.Content = items;
        var window = new Window { Width = 800, Height = 600, Content = view, Background = Brushes.White };
        try
        {
            window.Show();
            Capture(window, "01-normal-multiple-messages-menu-closed-projection-not-interaction");
            var bubbles = items.GetVisualDescendants().OfType<Border>().Where(border => border.ContextMenu is not null).ToArray();
            Assert.HasCount(4, bubbles);
            foreach (int index in new[] { 0, 1 })
            {
                var menu = bubbles[index].ContextMenu!;
                menu.Open(bubbles[index]);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Assert.AreEqual(2, menu.Items.Count);
                var root = menu;
                CaptureControl(root, $"0{index + 2}-production-{(index == 0 ? "user" : "assistant")}-complete-copy-menu-rendering-not-click");
                menu.Close();
            }
        }
        finally
        {
            window.Close();
            foreach (var projection in projections) projection.Dispose();
        }
    }

    /// <summary>Projects public approval decisions, not clicks or tool execution.</summary>
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void ApprovalDecisionRendersWithoutInput(bool approve)
    {
        var approval = new CopilotChatApprovalToolItem("fixture", "inspect_preview", "{\"path\":\"预览/示例.txt\",\"execute\":false}", "仅审阅拟议预览，不执行工具。", "代码预览审批");
        var manager = new CopilotChatManager();
        if (approve) manager.ApproveToolExecution(approval);
        else manager.RejectToolExecution(approval, "本次仅检查界面，暂不执行。 ");
        var view = new ChatView();
        var items = new ItemsControl { ItemsSource = new[] { approval }, Margin = new Thickness(24) };
        view.Content = items;
        var window = new Window { Width = 800, Height = 520, Content = view, Background = Brushes.White };
        try
        {
            window.Show();
            Capture(window, $"01-approval-{(approve ? "approved" : "rejected")}-public-decision-projection-not-click");
            Assert.IsFalse(approval.CanRespondToApproval);
            Assert.IsFalse(items.GetVisualDescendants().OfType<Button>().Any(button => button.IsEffectivelyVisible));
            Assert.AreEqual(752d, items.GetVisualDescendants().OfType<Border>().Single(border => border.Classes.Contains("ApprovalPanel")).Bounds.Width, 0.1);
        }
        finally { window.Close(); }
    }

    private void Capture(Window window, string name)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        string path = ImagePath(name);
        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No reference frame.");
        frame.Save(path, PngBitmapEncoderOptions.Default);
        TestContext.AddResultFile(path);
    }

    private void CaptureControl(Control control, string name)
    {
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(control.Bounds.Width), (int)Math.Ceiling(control.Bounds.Height)));
        bitmap.Render(control);
        string path = ImagePath(name);
        bitmap.Save(path, PngBitmapEncoderOptions.Default);
        TestContext.AddResultFile(path);
    }

    private string ImagePath(string name)
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "ShellScreenshots", TestContext.TestName!, Lang.Current.IetfLanguageTag, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, name + ".png");
        TestContext.WriteLine(path);
        return path;
    }
}
