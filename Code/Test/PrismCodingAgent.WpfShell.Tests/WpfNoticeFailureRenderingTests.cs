using System.Windows;
using System.Windows.Controls;
using AgentLib.Model;
using PrismCodingAgent.CodingShell;
using PrismCodingAgent.CodingShell.ViewModels;

namespace PrismCodingAgent.WpfShell.Tests;

/// <summary>Renders public notice projections and the production failure window without external execution.</summary>
[WpfTestClass]
public sealed class WpfNoticeFailureRenderingTests
{
    public TestContext TestContext { get; set; } = null!;

    /// <summary>Captures notices without continuation; no retry interaction is claimed.</summary>
    [TestMethod]
    public async Task NoticesRenderWithoutContinuationAsync() => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        var resources = new MainWindow();
        resources.Closing += (_, args) => args.Cancel = false;
        var panel = new StackPanel { Margin = new Thickness(24) };
        foreach (var level in Enum.GetValues<CopilotChatNoticeLevel>())
        {
            var notice = new CopilotChatNotice("通知示例：请求暂时无法完成，请检查配置后继续。", level,
                new DateTimeOffset(2026, 11, 1, 9, 30, 0, TimeSpan.Zero));
            var projection = new NoticeItemViewModel(notice);
            Assert.IsFalse(projection.HasContinuation);
            panel.Children.Add(new ContentControl { Content = projection, ContentTemplate = (DataTemplate)resources.Resources["NoticeMessage"] });
        }
        var window = new Window { Width = 800, Height = 440, Content = panel, Background = System.Windows.Media.Brushes.White,
            WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false };
        using var screenshots = new WpfScreenshotSession(window, TestContext);
        try
        {
            window.Show();
            await screenshots.CaptureSurfaceAsync(window, "notices-no-continuation");
            foreach (ContentControl item in panel.Children)
            {
                var presenter = (ContentPresenter)System.Windows.Media.VisualTreeHelper.GetChild(item, 0);
                var button = (Button)item.ContentTemplate.FindName("RetryChatButton", presenter);
                Assert.IsNull(button.Command);
                Assert.IsFalse(button.IsVisible);
                Assert.IsTrue(button.IsEnabled);
                Assert.AreSame(((NoticeItemViewModel)item.Content).Notice, button.CommandParameter);
            }
            Assert.AreEqual(752d, panel.ActualWidth, 0.1);
        }
        finally { window.Close(); resources.Close(); }
    });

    /// <summary>Shows the existing failure window in-process with real exception details.</summary>
    [TestMethod]
    public async Task StartupFailureRendersCompleteWindowAsync() => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        var failure = new System.Text.Json.JsonException("配置文件格式无效：请检查模型配置。");
        var model = new StartupFailureViewModel(@"C:\示例\配置\AgentConfiguration.json", failure, () => { });
        var window = new StartupFailureWindow { DataContext = model, Width = 760, Height = 520, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize };
        using var screenshots = new WpfScreenshotSession(window, TestContext);
        window.Show();
        await screenshots.CaptureSurfaceAsync(window, "startup-failure-complete-window");
        Assert.IsTrue(model.ErrorDetails.Contains("JsonException") && model.ErrorDetails.Contains("配置文件格式无效"));
    });
}
