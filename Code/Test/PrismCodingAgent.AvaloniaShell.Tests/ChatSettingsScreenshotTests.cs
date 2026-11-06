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
    [DataRow(1200, 900)]
    [DataRow(1440, 900)]
    [DataRow(1280, 720)]
    [DataRow(800, 600)]
    public async Task ProductionChatSettingsShouldRenderAndRemainOperableAsync(int width, int height)
    {
        const string configuration = """{"PrimaryModel":"Preview model","OpenAIConfigurationList":[{"EndPoint":"http://127.0.0.1:1/v1","Key":"offline-test","ModelDefinitions":[{"ModelName":"Preview model","ModelId":"preview","Provider":"openai"}]}]}""";
        await using var session = await ShellScreenshotSession.OpenAsync(TestContext, configuration);
        session.Window.MinWidth = 0;
        session.Window.MinHeight = 0;
        session.Window.Width = width;
        session.Window.Height = height;
        Dispatcher.UIThread.RunJobs();
        session.Window.UpdateLayout();
        var chat = session.Window.GetVisualDescendants().OfType<ChatView>().Single();
        session.Click(chat.FindControl<Button>("OpenChatSettingsButton")!);
        var settings = chat.GetVisualDescendants().OfType<ChatSettingsView>().Single();
        Assert.IsTrue(settings.IsEffectivelyVisible);
        Assert.AreSame(session.Model.ChatViewModel, settings.DataContext);
        var modelSelector = chat.FindControl<ComboBox>("ModelSelector")!;
        var historyButton = chat.FindControl<Button>("TaskHistoryButton")!;
        bool showModelSettings = width >= 1200;
        Assert.AreEqual(width - 280d - 8d - 2d, chat.Bounds.Width);
        Assert.AreEqual(showModelSettings, modelSelector.IsEffectivelyVisible);
        var reasoningSelector = chat.FindControl<ComboBox>("ReasoningEffortSelector")!;
        Assert.AreEqual(showModelSettings, reasoningSelector.IsEffectivelyVisible);
        Assert.IsTrue(chat.GetVisualDescendants().OfType<Grid>()
            .Where(grid => grid.Classes.Contains("ModelSettings"))
            .All(grid => grid.IsVisible == showModelSettings));
        var workspace = chat.FindControl<TextBox>("WorkspaceInputBox")!;
        var toolbarOrigin = workspace.TranslatePoint(default, chat)!.Value;
        var toolbarControls = new List<Control>
        {
            workspace,
            chat.FindControl<Button>("ApplyWorkspaceButton")!,
        };
        if (showModelSettings)
        {
            toolbarControls.Add(modelSelector);
            toolbarControls.Add(reasoningSelector);
        }
        toolbarControls.Add(historyButton);
        toolbarControls.Add(chat.FindControl<Button>("OpenLogDirectoryButton")!);
        double previousRight = 0;
        foreach (var control in toolbarControls)
        {
            var origin = control.TranslatePoint(default, chat)!.Value;
            Assert.AreEqual(toolbarOrigin.Y, origin.Y, 0.5, control.Name);
            Assert.IsGreaterThanOrEqualTo(previousRight, origin.X, control.Name);
            Assert.IsLessThanOrEqualTo(chat.Bounds.Width, origin.X + control.Bounds.Width, control.Name);
            Assert.IsGreaterThan(0d, control.Bounds.Width, control.Name);
            previousRight = origin.X + control.Bounds.Width;
        }
        Assert.IsLessThan(110d, toolbarOrigin.Y + workspace.Bounds.Height);
        var model = session.Model.ChatViewModel;
        var iterations = settings.FindControl<TextBox>("LoopMaximumIterationsInput")!;
        var duration = settings.FindControl<TextBox>("LoopMaximumDurationInput")!;
        var mode = settings.FindControl<ComboBox>("LoopConversationSelector")!;
        var scroll = settings.GetVisualDescendants().OfType<ScrollViewer>().First();
        string directory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "ShellScreenshots", "ChatSettingsAcceptance",
            PrismCodingAgent.CodingShell.Localizations.Lang.Current.IetfLanguageTag, $"{width}x{height}"));
        Directory.CreateDirectory(directory);

        Capture("01-default-top");
        model.LoopOptions.MaximumIterations = 12345;
        model.LoopOptions.MaximumDurationMinutes = 120;
        model.LoopOptions.ConversationMode = PrismCodingAgent.CodingShell.Services.Chat.LoopConversationMode.Append;
        model.RunOptions.EnableAutomaticCompression = true;
        model.RunOptions.EnableDotNetRun = true;
        model.IsResponsesApiEnabled = true;
        Dispatcher.UIThread.RunJobs();
        Assert.AreEqual("12345", iterations.Text);
        Assert.AreEqual("120", duration.Text);
        Assert.AreEqual(2, mode.SelectedIndex);
        Assert.AreEqual(32d, iterations.Height);
        Capture("02-custom-top");

        iterations.Text = "42";
        duration.Text = "90";
        mode.SelectedIndex = 1;
        Dispatcher.UIThread.RunJobs();
        Assert.AreEqual(42, model.LoopOptions.MaximumIterations);
        Assert.AreEqual(90, model.LoopOptions.MaximumDurationMinutes);
        Assert.AreEqual(PrismCodingAgent.CodingShell.Services.Chat.LoopConversationMode.Restart, model.LoopOptions.ConversationMode);
        Assert.AreEqual(((ComboBoxItem)mode.SelectedItem!).DataContext?.ToString(),
            settings.FindControl<TextBlock>("LoopConversationHint")!.Text);
        Capture("02-restart-top");
        scroll.ScrollToEnd();
        Dispatcher.UIThread.RunJobs();
        Capture("03-custom-bottom");
        foreach (string name in new[] { "ChatAutomaticCompressionToggle", "ChatDotNetRunToggle", "ChatResponsesApiToggle" })
        {
            var toggle = settings.FindControl<ToggleSwitch>(name)!;
            toggle.BringIntoView();
            Dispatcher.UIThread.RunJobs();
            session.Window.UpdateLayout();
            Assert.IsTrue(toggle.IsChecked);
            var origin = toggle.TranslatePoint(default, settings)!.Value;
            Assert.IsTrue(origin.Y >= 0 && origin.Y + toggle.Bounds.Height <= settings.Bounds.Height);
            session.Click(toggle);
            Assert.IsFalse(toggle.IsChecked);
        }
        Assert.IsFalse(model.RunOptions.EnableAutomaticCompression);
        Assert.IsFalse(model.RunOptions.EnableDotNetRun);
        Assert.IsFalse(model.IsResponsesApiEnabled);
        Capture("04-toggles-off");
        session.Click(settings.FindControl<Button>("CloseChatSettingsButton")!);
        Assert.IsFalse(settings.IsVisible);
        Capture("05-chat-single-row-toolbar");

        void Capture(string name)
        {
            Dispatcher.UIThread.RunJobs();
            session.Window.UpdateLayout();
            Assert.AreEqual((double)width, session.Window.ClientSize.Width);
            Assert.AreEqual((double)height, session.Window.ClientSize.Height);
            using var frame = session.Window.CaptureRenderedFrame();
            Assert.IsNotNull(frame);
            Assert.AreEqual(new PixelSize(width, height), frame.PixelSize);
            string path = Path.Combine(directory, name + ".png");
            frame.Save(path, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
            TestContext.AddResultFile(path);
            TestContext.WriteLine(path);
        }
    }

    [TestMethod]
    public async Task ChatSettingsOverlayOpensAndClosesWithoutChangingDraftAsync()
    {
        PrismCodingAgent.CodingShell.Localizations.Lang.SetCurrent("zh-Hans");
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
