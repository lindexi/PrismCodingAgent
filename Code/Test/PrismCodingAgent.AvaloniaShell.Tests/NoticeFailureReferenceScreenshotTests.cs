using AgentLib.Model;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PrismCodingAgent.CodingShell.ViewModels;
using ChatView = PrismCodingAgent.CodingShell.Views.ChatView;
using Lang = PrismCodingAgent.CodingShell.Localizations.Lang;

namespace PrismCodingAgent.AvaloniaShell.Tests;

[AvaloniaTestClass]
public sealed class NoticeFailureReferenceScreenshotTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task NoticesRenderWithoutContinuation(bool hasContinuation)
    {
        var view = new ChatView();
        var panel = new StackPanel { Margin = new Thickness(24) };
        var retryCommand = new SimpleAsyncCommand<CopilotChatNotice>(_ => throw new InvalidOperationException("Rendering must not execute retry."));
        foreach (var level in Enum.GetValues<CopilotChatNoticeLevel>())
        {
            var projection = new NoticeItemViewModel(new CopilotChatNotice("通知示例：请求暂时无法完成，请检查配置后继续。", level,
                new DateTimeOffset(2026, 11, 1, 9, 30, 0, TimeSpan.Zero)), retryCommand, hasContinuation);
            Assert.AreEqual(hasContinuation, projection.HasContinuation);
            panel.Children.Add(new ContentControl { Content = projection, DataContext = projection,
                ContentTemplate = view.FindControl<ItemsControl>("MessagesItemsControl")!.ItemTemplate,
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch });
        }
        view.Content = panel;
        var window = new Window { Width = 800, Height = 440, Content = view, Background = Brushes.White };
        try
        {
            window.Show();
            await CaptureAsync(window, hasContinuation ? "01-notices-continuation-available-fixture-not-interaction.png" : "01-notices-no-continuation.png");
            var surfaces = panel.GetVisualDescendants().OfType<Border>().Where(border => border.CornerRadius == new CornerRadius(6)).ToArray();
            Assert.HasCount(3, surfaces);
            foreach (var surface in surfaces) Assert.AreEqual("#ffeef2f7", surface.Background?.ToString());
            foreach (var button in panel.GetVisualDescendants().OfType<Button>())
            {
                Assert.AreSame(retryCommand, button.Command);
                Assert.AreEqual(hasContinuation, button.IsVisible);
                Assert.IsTrue(button.IsEnabled);
                Assert.IsInstanceOfType<CopilotChatNotice>(button.CommandParameter);
            }
            Assert.AreEqual(752d, panel.Bounds.Width, 0.1);
        }
        finally { window.Close(); }
    }

    [TestMethod]
    public async Task StartupFailureRendersCompleteWindow()
    {
        var failure = new System.Text.Json.JsonException("配置文件格式无效：请检查模型配置。");
        var model = new StartupFailureViewModel(@"C:\示例\配置\AgentConfiguration.json", failure, () => { });
        var window = new CodingShell.StartupFailureWindow { DataContext = model, Width = 760, Height = 520, WindowDecorations = WindowDecorations.None };
        try
        {
            window.Show();
            await CaptureAsync(window, "01-startup-failure-complete-window.png");
            StringAssert.Contains(model.ErrorDetails, "JsonException");
            var details = window.GetVisualDescendants().OfType<TextBox>().Single();
            TestContext.WriteLine($"Production TextBox padding: {details.Padding}; bounds: {details.Bounds}");
        }
        finally { window.Close(); }
    }

    private async Task CaptureAsync(Window window, string name)
    {
        window.MouseMove(new Point(790, 430));
        await Task.Delay(300);
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        foreach (var button in window.GetVisualDescendants().OfType<Button>())
        {
            TestContext.WriteLine($"Effective button: background={button.Background}; opacity={button.Opacity}; window={window.Background}");
            foreach (var border in button.GetVisualDescendants().OfType<Border>())
                TestContext.WriteLine($"Button border: background={border.Background}; opacity={border.Opacity}");
        }
        string directory = Path.Combine(AppContext.BaseDirectory, "ShellScreenshots", TestContext.TestName!, Lang.Current.IetfLanguageTag, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, name);
        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No reference frame.");
        frame.Save(path, PngBitmapEncoderOptions.Default);
        TestContext.WriteLine(path);
        TestContext.AddResultFile(path);
    }
}
