using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AgentLib.Model;
using Microsoft.Extensions.AI;
using PrismCodingAgent.CodingShell;
using PrismCodingAgent.CodingShell.Localizations;
using PrismCodingAgent.CodingShell.ViewModels;

namespace PrismCodingAgent.WpfShell.Tests;

[WpfTestClass]
public sealed class WpfApprovalTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ApproveThroughRealMouseWithoutToolExecutionAsync() => await VerifyDecisionAsync(true);

    [TestMethod]
    public async Task RejectThroughRealMouseWithoutToolExecutionAsync() => await VerifyDecisionAsync(false);

    private async Task VerifyDecisionAsync(bool approve) => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        string root = Path.Combine(AppContext.BaseDirectory, "ApprovalData", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "ShellSettings.json"), JsonSerializer.Serialize(new { LanguageTag = Lang.Current.IetfLanguageTag }));
        var shell = (MainWindow)await ((App)Application.Current).OpenAsync(["--data-directory", root]);
        Window? window = null;
        try
        {
            var chat = ((MainViewModel)shell.DataContext).ActiveWorkTask!.Chat;
            // Public projection fixture only: no human-approval tool, pending runner, or tool execution is attached.
            var approval = new CopilotChatApprovalToolItem("fixture", "inspect_preview", "{\"path\":\"preview-only/示例.txt\",\"execute\":false}",
                "Review the proposed preview only. 仅审阅拟议预览，不执行工具。", "inspect_preview 审批预览");
            var message = new CopilotChatMessage(ChatRole.Assistant, string.Empty);
            message.MessageItems.Add(approval);
            using var projection = new MessageItemViewModel(message);
            var content = new ContentControl { Content = approval, ContentTemplate = (DataTemplate)shell.Resources["MessageApproval"], HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Top, VerticalContentAlignment = VerticalAlignment.Top, Margin = new Thickness(24) };
            var surface = new Grid { Background = Brushes.White };
            surface.Children.Add(content);
            window = new Window { Width = 800, Height = 520, Content = surface, Background = Brushes.White, DataContext = chat };
            window.Show();
            window.UpdateLayout();
            window.Width += 800 - surface.ActualWidth;
            window.Height += 520 - surface.ActualHeight;
            window.UpdateLayout();
            using var screenshots = new WpfScreenshotSession(window, TestContext);
            await screenshots.CaptureAsync("approval-fixture-pending-no-execution");
            var buttons = Descendants(content).OfType<Button>().Where(button => ReferenceEquals(button.CommandParameter, approval)).ToArray();
            var approveButton = buttons.Single(button => Equals(button.Content, Lang.Current.ApproveText));
            var rejectButton = buttons.Single(button => Equals(button.Content, Lang.Current.RejectText));
            var title = Descendants(content).OfType<TextBlock>().Single(text => text.IsVisible && ReferenceEquals(text.DataContext, approval) && text.Text == approval.DisplayName);
            var card = (Border)VisualTreeHelper.GetParent(VisualTreeHelper.GetParent(VisualTreeHelper.GetParent(title)));
            Rect bounds = card.TransformToAncestor(surface).TransformBounds(new Rect(card.RenderSize));
            Assert.IsTrue(bounds.Left >= 24 && bounds.Right <= surface.ActualWidth - 24 && bounds.Bottom <= surface.ActualHeight - 24, $"Approval card clipped: {bounds}.");
            Assert.AreEqual(752d, card.ActualWidth, 1d);
            Assert.HasCount(2, buttons);
            Assert.IsTrue(buttons.All(button => button.IsVisible && button.IsEnabled));
            Assert.IsTrue(Descendants(content).OfType<TextBlock>().Any(text => text.IsVisible && text.Text == approval.InputText));
            await WpfInput.ClickAsync(approve ? approveButton : rejectButton);
            var expected = approve ? CopilotToolApprovalState.Approved : CopilotToolApprovalState.Rejected;
            await WpfInput.WaitAsync(() => approval.ApprovalState == expected, "Approval decision did not complete through mouse input.");
            await screenshots.CaptureAsync(approve ? "approval-fixture-approved-no-execution" : "approval-fixture-rejected-no-execution");
            Assert.IsTrue(Descendants(content).OfType<TextBlock>().Any(text => text.IsVisible && text.Text == approval.ApprovalStateText));
            Assert.IsTrue(buttons.All(button => !button.IsVisible && !button.IsEnabled));
            Assert.IsFalse(approval.CanRespondToApproval);
            Assert.IsFalse(approval.HasOutputText);
        }
        finally
        {
            window?.Close();
            if (shell.IsVisible)
            {
                shell.Close();
                await WpfInput.ClickAsync((Button)shell.FindName("ConfirmExitButton"));
                await WpfInput.WaitAsync(() => !shell.IsVisible, "Shell did not close.");
            }
        }
        Assert.IsFalse(Application.Current.Windows.Cast<Window>().Any(open => ReferenceEquals(open, window) || ReferenceEquals(open, shell)));
    });

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
