using AgentLib.Model;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using ChatView = PrismCodingAgent.CodingShell.Views.ChatView;
using Lang = PrismCodingAgent.CodingShell.Localizations.Lang;

namespace PrismCodingAgent.AvaloniaShell.Tests;

[AvaloniaTestClass]
public sealed class ApprovalReferenceScreenshotTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void PendingApprovalUsesActualReferenceTemplateWithoutExecution()
    {
        var approval = new CopilotChatApprovalToolItem("fixture", "inspect_preview", "{\"path\":\"preview-only/示例.txt\",\"execute\":false}",
            "Review the proposed preview only. 仅审阅拟议预览，不执行工具。", "inspect_preview 审批预览");
        var view = new ChatView();
        var items = new ItemsControl { ItemsSource = new[] { approval }, Margin = new Thickness(24) };
        view.Content = items;
        var window = new Window { Width = 800, Height = 520, Content = view, Background = Brushes.White };
        try
        {
            window.Show();
            window.UpdateLayout();
            Assert.AreEqual(2, items.GetVisualDescendants().OfType<Button>().Count(button => button.IsEffectivelyVisible));
            var reject = items.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, Lang.Current.RejectText));
            TestContext.WriteLine($"Reject effective background={reject.Background}; foreground={reject.Foreground}; border={reject.BorderBrush}; size={reject.Bounds.Size}");
            foreach (var presenter in reject.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.ContentPresenter>())
                TestContext.WriteLine($"Reject presenter background={presenter.Background}; border={presenter.BorderBrush}");
            var card = items.GetVisualDescendants().OfType<Border>().Single(border => border.Classes.Contains("ApprovalPanel"));
            Assert.AreEqual(752d, card.Bounds.Width, 1d);
            string directory = Path.Combine(AppContext.BaseDirectory, "ShellScreenshots", TestContext.TestName!, Lang.Current.IetfLanguageTag, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "01-actual-avalonia-approval-pending-no-execution.png");
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
