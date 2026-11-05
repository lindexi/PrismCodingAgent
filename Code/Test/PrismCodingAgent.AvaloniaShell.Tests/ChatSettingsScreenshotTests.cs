using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PrismCodingAgent.CodingShell.Views;

namespace PrismCodingAgent.AvaloniaShell.Tests;

[AvaloniaTestClass]
public sealed class ChatSettingsScreenshotTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ChatSettingsOverlayOpensAndClosesWithoutChangingDraftAsync()
    {
        const string configuration = """{"PrimaryModel":"Preview model","OpenAIConfigurationList":[{"EndPoint":"http://127.0.0.1:1/v1","Key":"offline-test","ModelDefinitions":[{"ModelName":"Preview model","ModelId":"preview","Provider":"openai"}]}]}""";
        await using var session = await ShellScreenshotSession.OpenAsync(TestContext, configuration);
        var chat = session.Window.GetVisualDescendants().OfType<ChatView>().Single();
        var overlay = chat.FindControl<Grid>("ChatOverlayLayer")!;
        var settings = chat.GetVisualDescendants().OfType<ChatSettingsView>().Single();
        session.Model.ChatViewModel.InputText = "保留当前草稿 / Keep this draft";

        session.Click(chat.FindControl<Button>("OpenChatSettingsButton")!);
        Assert.IsTrue(overlay.IsVisible);
        session.Capture("chat-settings-open");

        session.Click(settings.FindControl<ToggleSwitch>("ChatAutomaticCompressionToggle")!);
        Assert.IsTrue(settings.FindControl<ToggleSwitch>("ChatAutomaticCompressionToggle")!.IsChecked);
        Assert.IsTrue(session.Model.ChatViewModel.RunOptions.EnableAutomaticCompression);
        session.Capture("chat-settings-option-selected");

        session.Window.Width = 1280;
        session.Window.Height = 720;
        Dispatcher.UIThread.RunJobs();
        session.Window.UpdateLayout();
        using (var frame = session.Window.CaptureRenderedFrame()!)
        {
            string path = Path.Combine(session.DirectoryPath, "03-chat-settings-compact.png");
            frame.Save(path, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
            TestContext.AddResultFile(path);
            TestContext.WriteLine(path);
        }
        Assert.AreEqual(1280d, session.Window.ClientSize.Width, 1d);
        Assert.AreEqual(720d, session.Window.ClientSize.Height, 1d);
        Assert.AreEqual(overlay.Bounds.Width, settings.Bounds.Width);
        var close = settings.FindControl<Button>("CloseChatSettingsButton")!;
        Assert.IsTrue(close.IsEffectivelyVisible);
        var panelOrigin = settings.TranslatePoint(default, overlay)!.Value;
        Assert.IsGreaterThanOrEqualTo(0d, panelOrigin.Y);
        Assert.IsLessThanOrEqualTo(overlay.Bounds.Height, panelOrigin.Y + settings.Bounds.Height);
        Assert.HasCount(3, settings.GetVisualDescendants().OfType<ToggleSwitch>());
        session.Click(settings.FindControl<ToggleSwitch>("ChatDotNetRunToggle")!);
        Assert.IsTrue(settings.FindControl<ToggleSwitch>("ChatDotNetRunToggle")!.IsChecked);
        session.Click(settings.FindControl<ToggleSwitch>("ChatResponsesApiToggle")!);
        Assert.IsTrue(settings.FindControl<ToggleSwitch>("ChatResponsesApiToggle")!.IsChecked);
        Assert.IsTrue(session.Model.ChatViewModel.RunOptions.EnableDotNetRun);
        Assert.IsTrue(session.Model.ChatViewModel.IsResponsesApiEnabled);
        session.Click(close);
        Assert.IsTrue(overlay.IsVisible);
        Assert.IsFalse(settings.IsVisible);
        Assert.AreEqual("保留当前草稿 / Keep this draft", session.Model.ChatViewModel.InputText);
        session.Window.Width = 1440;
        session.Window.Height = 900;
        session.Capture("chat-settings-closed");
        session.Click(chat.FindControl<Button>("OpenChatSettingsButton")!);
        Assert.IsTrue(settings.FindControl<ToggleSwitch>("ChatAutomaticCompressionToggle")!.IsChecked);
        Assert.IsTrue(settings.FindControl<ToggleSwitch>("ChatDotNetRunToggle")!.IsChecked);
        Assert.IsTrue(settings.FindControl<ToggleSwitch>("ChatResponsesApiToggle")!.IsChecked);
        session.Capture("chat-settings-reopened");
    }
}
