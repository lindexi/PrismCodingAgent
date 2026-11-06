using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PrismCodingAgent.CodingShell.Localizations;
using PrismCodingAgent.CodingShell.Views;

namespace PrismCodingAgent.AvaloniaShell.Tests;

[AvaloniaTestClass]
public sealed class ChatSettingsSingleColumnTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(1200, 900, "zh-Hans")]
    [DataRow(1440, 900, "zh-Hans")]
    [DataRow(1200, 900, "en")]
    [DataRow(1200, 900, "de")]
    public async Task SettingsReuseChatConfigurationAndRenderSingleColumnAsync(int width, int height, string language)
    {
        Lang.SetCurrent(language);
        const string configuration = """{"PrimaryModel":"Preview model","OpenAIConfigurationList":[{"EndPoint":"http://127.0.0.1:1/v1","Key":"offline-test","ModelDefinitions":[{"ModelName":"Preview model","ModelId":"preview","Provider":"openai"}]}]}""";
        await using var session = await ShellScreenshotSession.OpenAsync(TestContext, configuration);
        session.Window.MinWidth = 0;
        session.Window.MinHeight = 0;
        session.Window.Width = width;
        session.Window.Height = height;
        var model = session.Model.ChatViewModel;
        var runtime = session.Model.ActiveWorkTask.Runtime!;
        await runtime.ChatLogger.LogDiagnosticAsync(model.CurrentSessionId, "Preview", "设置截图验收");
        model.WorkspaceInput = @"C:\Code\Workspace\PrismCodingAgent";
        model.InputText = "关闭设置后保留的草稿";
        model.LoopOptions.MaximumIterations = 10;
        model.LoopOptions.MaximumDurationMinutes = 30;
        Layout();
        Capture("01-main");
        var chat = session.Window.GetVisualDescendants().OfType<ChatView>().Single();
        var originalWorkspace = chat.FindControl<TextBox>("WorkspaceInputBox")!;
        Rect originalBounds = originalWorkspace.Bounds;
        session.Click(chat.FindControl<Button>("OpenChatSettingsButton")!);
        Layout();
        var settings = chat.GetVisualDescendants().OfType<ChatSettingsView>().Single();
        Assert.IsTrue(settings.IsEffectivelyVisible);
        Assert.AreEqual(0d, settings.TranslatePoint(default, chat)!.Value.Y);
        Assert.AreEqual(chat.Bounds.Height, settings.Bounds.Height);
        Assert.AreEqual(originalBounds, originalWorkspace.Bounds);
        var workspace = settings.FindControl<TextBox>("SettingsWorkspaceInput")!;
        Assert.AreEqual(model.WorkspaceInput, workspace.Text);
        workspace.Text = @"C:\Code\Workspace\SharedConfiguration";
        Layout();
        Assert.AreEqual(workspace.Text, originalWorkspace.Text);
        var selector = settings.FindControl<ComboBox>("SettingsModelSelector")!;
        Assert.AreSame(model.SelectedModel, selector.SelectedItem);
        Capture("02-settings-top");
        var scroll = settings.FindControl<ScrollViewer>("SettingsScrollViewer")!;
        scroll.Offset = new Vector(0, 350);
        Layout();
        Capture("03-settings-middle");
        scroll.ScrollToEnd();
        Layout();
        Assert.AreEqual(Environment.ProcessId.ToString(), settings.FindControl<TextBlock>("ProcessIdText")!.Text);
        Assert.IsTrue(settings.FindControl<Button>("OpenLogFileButton")!.IsEnabled);
        string expectedLogFile = Directory.GetFiles(runtime.Paths.LogDirectory,
            $"*{model.CurrentSessionId:N}*.log", SearchOption.AllDirectories).Single();
        Assert.AreEqual(expectedLogFile, settings.FindControl<TextBox>("LogFileInput")!.Text);
        Assert.IsTrue(settings.FindControl<TextBox>("LogFileInput")!.IsReadOnly);
        var currentSession = runtime.ChatManager.SelectedSession;
        string expectedArchiveDirectory = Path.Join(runtime.Paths.SessionDirectory.FullName,
            currentSession.StartedTime.ToString("yyyyMMdd"), currentSession.SessionId.ToString("N"));
        Assert.AreEqual(expectedArchiveDirectory, settings.FindControl<TextBox>("SessionsDirectoryInput")!.Text);
        Assert.IsTrue(settings.FindControl<TextBox>("SessionsDirectoryInput")!.IsReadOnly);
        Assert.AreEqual(selector.TranslatePoint(default, settings)!.Value.Y,
            settings.FindControl<ComboBox>("SettingsReasoningSelector")!.TranslatePoint(default, settings)!.Value.Y);
        Assert.AreEqual(settings.FindControl<Button>("CopyPidButton")!.TranslatePoint(default, settings)!.Value.Y,
            settings.FindControl<Button>("LaunchDebuggerButton")!.TranslatePoint(default, settings)!.Value.Y);
        Capture("04-settings-diagnostics");
        session.Click(settings.FindControl<Button>("CloseChatSettingsButton")!);
        Layout();
        Assert.IsFalse(model.IsChatSettingsOpen);
        Assert.AreEqual("关闭设置后保留的草稿", model.InputText);
        Assert.AreEqual(originalBounds, originalWorkspace.Bounds);
        Capture("05-main-restored");

        void Layout()
        {
            Dispatcher.UIThread.RunJobs();
            session.Window.UpdateLayout();
        }

        void Capture(string name)
        {
            Layout();
            string directory = Path.Combine(AppContext.BaseDirectory, "ShellScreenshots", "SingleColumnSettings", language, $"{width}x{height}");
            Directory.CreateDirectory(directory);
            using var frame = session.Window.CaptureRenderedFrame();
            Assert.IsNotNull(frame);
            string path = Path.Combine(directory, name + ".png");
            frame.Save(path, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
            TestContext.AddResultFile(path);
        }
    }
}
