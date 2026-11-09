using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using PrismCodingAgent.CodingShell;
using PrismCodingAgent.CodingShell.Localizations;
using PrismCodingAgent.CodingShell.ViewModels;
using PrismCodingAgent.CodingShell.Views;

namespace PrismCodingAgent.WpfShell.Tests;

[WpfTestClass]
public sealed class WpfChatSettingsAcceptanceTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("zh-Hans")]
    [DataRow("en")]
    [DataRow("de")]
    public async Task SingleColumnSettingsUseProductionPathsAsync(string language) => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        Lang.SetCurrent(language);
        string root = Path.Combine(AppContext.BaseDirectory, "SettingsAcceptanceData", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "ShellSettings.json"),
            System.Text.Json.JsonSerializer.Serialize(new { LanguageTag = language }));
        await File.WriteAllTextAsync(Path.Combine(root, "AgentConfiguration.json"),
            """{"PrimaryModel":"Preview model","OpenAIConfigurationList":[{"EndPoint":"http://127.0.0.1:1/v1","Key":"offline-test","ModelDefinitions":[{"ModelName":"Preview model","ModelId":"preview","Provider":"openai"}]}]}""");
        var window = (MainWindow)await ((App)Application.Current).OpenAsync(["--data-directory", root]);
        var model = (MainViewModel)window.DataContext;
        window.Closing += (_, args) => args.Cancel = false;
        using var screenshots = new WpfScreenshotSession(window, TestContext);
        try
        {
            window.MinWidth = 0;
            window.Width = 1200;
            window.Height = 900;
            model.ChatViewModel.InputText = "Draft";
            model.ChatViewModel.WorkspaceInput = root;
            string logDirectory = Path.Combine(root, "Logs", "20260101");
            Directory.CreateDirectory(logDirectory);
            string expectedLog = Path.Combine(logDirectory, $"preview_{model.ChatViewModel.CurrentSessionId:N}.log");
            await File.WriteAllTextAsync(expectedLog, "Offline screenshot fixture");
            var settings = (ChatSettingsView)window.FindName("ChatSettingsPanel");
            model.ChatViewModel.IsChatSettingsOpen = true;
            await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle);
            var overlay = (Grid)window.FindName("ChatOverlayLayer");
            var surface = (Border)window.FindName("ContentSurface");
            var host = (Grid)surface.Child;
            Assert.AreSame(host, overlay.Parent);
            Assert.AreEqual(default(Thickness), overlay.Margin);
            Assert.AreEqual(new Point(), overlay.TranslatePoint(new Point(), host));
            Assert.AreEqual(host.RenderSize, overlay.RenderSize);
            Assert.IsGreaterThan(Panel.GetZIndex((UIElement)window.FindName("HistoryConversation")), Panel.GetZIndex(overlay));
            Assert.IsNotNull(overlay.Background);
            var workspace = (TextBox)settings.FindName("SettingsWorkspaceInput");
            Assert.AreEqual(root, workspace.Text);
            var selector = (ComboBox)settings.FindName("SettingsModelSelector");
            Assert.AreSame(model.ChatViewModel.SelectedModel, selector.SelectedItem);
            var reasoning = (ComboBox)settings.FindName("SettingsReasoningSelector");
            Assert.AreEqual(selector.TranslatePoint(default, settings).Y, reasoning.TranslatePoint(default, settings).Y);
            string topScreenshot = await screenshots.CaptureAsync("1200-settings-top");
            string topDirectory = Path.Combine(AppContext.BaseDirectory, "ShellScreenshots", "SingleColumnAcceptance", language);
            Directory.CreateDirectory(topDirectory);
            File.Copy(topScreenshot, Path.Combine(topDirectory, "top.png"), true);
            var scroll = (ScrollViewer)settings.FindName("SettingsScrollViewer");
            scroll.ScrollToEnd();
            await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle);
            var archive = (TextBox)settings.FindName("SessionsDirectoryInput");
            Assert.IsTrue(archive.IsReadOnly);
            Assert.IsTrue(archive.Text.StartsWith(Path.Combine(root, "Sessions") + Path.DirectorySeparatorChar));
            Assert.AreNotEqual(Path.Combine(root, "Sessions"), archive.Text);
            var log = (TextBox)settings.FindName("LogFileInput");
            Assert.IsTrue(log.IsReadOnly);
            Assert.AreEqual(expectedLog, log.Text);
            Assert.AreEqual(Environment.ProcessId.ToString(), ((TextBlock)settings.FindName("ProcessIdText")).Text);
            Assert.IsTrue(((Button)settings.FindName("LaunchDebuggerButton")).IsEnabled);
            string artifactDirectory = Path.Combine(AppContext.BaseDirectory, "ShellScreenshots", "SingleColumnAcceptance", language);
            Directory.CreateDirectory(artifactDirectory);
            File.Copy(await screenshots.CaptureAsync("1200-settings-diagnostics"), Path.Combine(artifactDirectory, "diagnostics.png"), true);
            window.Width = 1440;
            scroll.ScrollToTop();
            await screenshots.CaptureAsync("1440-settings-top");
            scroll.ScrollToEnd();
            await screenshots.CaptureAsync("1440-settings-diagnostics");
            model.ChatViewModel.ToggleChatSettingsCommand.Execute(null);
            Assert.IsFalse(model.ChatViewModel.IsChatSettingsOpen);
            Assert.AreEqual("Draft", model.ChatViewModel.InputText);
        }
        finally
        {
            window.Close();
            await model.DisposeAsync();
        }
    });
}
