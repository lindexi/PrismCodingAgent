using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PrismCodingAgent.AvaloniaShell.Localizations;
using PrismCodingAgent.AvaloniaShell.ViewModels;
using PrismCodingAgent.AvaloniaShell.Views;

namespace PrismCodingAgent.WpfShell.Tests;

[WpfTestClass]
public sealed class WpfNoticeWelcomeTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task FailedChatContinuesThroughRealRetryButtonAsync() => await RunFailedChatAsync(false);

    /// <summary>Renders a real HTTP failure through public initialization, without input injection or retry interaction.</summary>
    [TestMethod]
    public async Task HttpFailureNoticeRendersWithoutSendInputAsync() => await RunFailedChatAsync(true);

    private Task RunFailedChatAsync(bool renderOnly) => WpfTestClassAttribute.RunUiAsync(async () =>
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        string root = Path.Combine(AppContext.BaseDirectory, "Phase8NoticeData", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "ShellSettings.json"), JsonSerializer.Serialize(new
        {
            LanguageTag = Lang.Current.IetfLanguageTag, IsWindowsSandboxEnabled = false
        }));
        await File.WriteAllTextAsync(Path.Combine(root, "AgentConfiguration.json"), JsonSerializer.Serialize(new
        {
            PrimaryModel = "demo", OpenAIConfigurationList = new[] { new
            {
                EndPoint = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/v1", Key = "offline-test",
                ModelDefinitions = new[] { new { ModelName = "demo", ModelId = "model-id", Provider = "test" } }
            } }
        }));
        var window = (MainWindow)await ((App)Application.Current).OpenAsync(["--data-directory", root]);
        var model = (MainViewModel)window.DataContext;
        using var screenshots = new WpfScreenshotSession(window, TestContext);
        try
        {
            var welcome = (StackPanel)window.FindName("WelcomePanel");
            var messages = (ScrollViewer)window.FindName("MessagesScrollViewer");
            await WpfInput.WaitAsync(() => model.ChatViewModel.ShowWelcome && welcome.IsVisible, "Welcome did not follow the initial shared state.");
            Assert.AreSame(model.ChatViewModel, welcome.DataContext);
            Assert.IsFalse(messages.IsVisible);
            CollectionAssert.AreEqual(new[]
            {
                Lang.Current.WelcomeTitleText.ToString(),
                Lang.Current.WelcomeDescriptionText.ToString(),
                Lang.Current.WelcomeExamplesText.ToString()
            }, welcome.Children.OfType<TextBlock>().Select(text => text.Text).ToArray());
            var logs = (Button)window.FindName("OpenLogDirectoryButton");
            Assert.AreSame(model.OpenLogDirectoryCommand, logs.Command);
            Assert.IsTrue(logs.IsVisible && logs.IsEnabled);
            Assert.AreEqual(Lang.Current.OpenLogDirectoryText.ToString(), logs.ToolTip?.ToString());
            await screenshots.CaptureAsync("welcome-before-chat-log-entry-not-clicked");
            var composer = (ChatView)window.FindName("ChatComposer");
            if (renderOnly)
            {
                model.ChatViewModel.InputText = "Controlled continuation question";
                model.ChatViewModel.SendCommand.Execute(null);
            }
            else
            {
                await WpfInput.ClickAsync((TextBox)composer.FindName("MessageInputTextBox"));
                WpfInput.Text("Controlled continuation question");
                await WpfInput.ClickAsync((Button)composer.FindName("SendButton"));
            }
            using (var client = await listener.AcceptTcpClientAsync(WpfTestSession.Cancellation))
            {
                string request = await ReadRequestAsync(client);
                StringAssert.Contains(request, "Controlled continuation question");
                await WpfInput.WaitAsync(() => !model.ChatViewModel.ShowWelcome && !welcome.IsVisible && messages.IsVisible, "Starting the chat did not hide the welcome.");
                string partial = "data: {\"id\":\"local\",\"object\":\"chat.completion.chunk\",\"choices\":[{\"index\":0,\"delta\":{\"role\":\"assistant\",\"content\":\"Partial answer before failure\"},\"finish_reason\":null}]}\n\n";
                await client.GetStream().WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/event-stream\r\nConnection: close\r\n\r\n"), WpfTestSession.Cancellation);
                await client.GetStream().WriteAsync(Encoding.UTF8.GetBytes(partial), WpfTestSession.Cancellation);
                await WpfInput.WaitAsync(() => model.ChatViewModel.Messages.OfType<MessageItemViewModel>().Any(item => item.Content.Contains("Partial answer before failure")), "Partial response did not render.");
                await client.GetStream().WriteAsync(Encoding.UTF8.GetBytes("data: invalid-json\n\n"), WpfTestSession.Cancellation);
            }
            var history = (ItemsControl)window.FindName("HistoryMessages");
            await WpfInput.WaitAsync(() => !model.ChatViewModel.IsRunning && model.ChatViewModel.Messages.OfType<NoticeItemViewModel>().Any(item => item.HasContinuation), "Failed chat did not expose continuation.");
            await window.Dispatcher.InvokeAsync(window.UpdateLayout, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            var notice = model.ChatViewModel.Messages.OfType<NoticeItemViewModel>().Single(item => item.HasContinuation);
            var retry = Descendants(history).OfType<Button>().Single(button => button.Name == "RetryChatButton" && button.IsVisible);
            Assert.AreSame(notice.RetryCommand, retry.Command);
            Assert.AreSame(notice.Notice, retry.CommandParameter);
            Assert.AreEqual((string)Lang.Current.RetryChatText, retry.Content?.ToString());
            Assert.IsTrue(notice.IsError);
            await screenshots.CaptureAsync("notice-before-retry");
            if (renderOnly)
            {
                var panel = new StackPanel { Margin = new Thickness(24) };
                panel.Children.Add(new ContentControl { Content = notice, ContentTemplate = (DataTemplate)window.Resources["NoticeMessage"] });
                var local = new Window { Width = 800, Height = 440, Content = panel, Background = Brushes.White, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize };
                try
                {
                    local.Show();
                    using var localScreenshots = new WpfScreenshotSession(local, TestContext);
                    await localScreenshots.CaptureSurfaceAsync(local, "http-continuation-public-projection-not-retry-interaction");
                    Assert.AreEqual("#33000000", retry.Background.ToString());
                }
                finally { local.Close(); }
                return;
            }
            await WpfInput.ClickAsync(retry);
            using (var client = await listener.AcceptTcpClientAsync(WpfTestSession.Cancellation))
            {
                string request = await ReadRequestAsync(client);
                StringAssert.Contains(request, "Controlled continuation question");
                await WpfInput.WaitAsync(() => model.ChatViewModel.IsRunning && !retry.IsVisible, "Consumed continuation remained visible.");
                await RespondAsync(client, "200 OK", "text/event-stream", "data: {\"id\":\"local\",\"object\":\"chat.completion.chunk\",\"choices\":[{\"index\":0,\"delta\":{\"role\":\"assistant\",\"content\":\"Continuation recovered 重试成功\"},\"finish_reason\":null}]}\n\ndata: {\"id\":\"local\",\"object\":\"chat.completion.chunk\",\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"stop\"}]}\n\ndata: [DONE]\n\n");
            }
            await WpfInput.WaitAsync(() => !model.ChatViewModel.IsRunning && !model.ChatViewModel.IsFinalizing && Descendants(history).OfType<TextBox>().Any(text => text.Text == "Partial answer before failureContinuation recovered 重试成功"), "Continuation response did not render.");
            Assert.IsFalse(notice.HasContinuation);
            Assert.AreEqual(1, model.ChatViewModel.Messages.OfType<MessageItemViewModel>().Count(item => item.Message.Role == Microsoft.Extensions.AI.ChatRole.User));
            await screenshots.CaptureAsync("notice-after-retry");
        }
        finally
        {
            if (renderOnly) window.Closing += (_, args) => args.Cancel = false;
            window.Close();
            if (!renderOnly) await WpfInput.ClickAsync((Button)window.FindName("ConfirmExitButton"));
            await WpfInput.WaitAsync(() => !window.IsVisible, "Notice window did not close.");
            await model.DisposeAsync();
        }
    });

    private static async Task<string> ReadRequestAsync(TcpClient client)
    {
        var bytes = new List<byte>();
        byte[] one = new byte[1];
        while (true)
        {
            await client.GetStream().ReadExactlyAsync(one, WpfTestSession.Cancellation);
            bytes.Add(one[0]);
            if (bytes.Count >= 4 && bytes.TakeLast(4).SequenceEqual(new byte[] { 13, 10, 13, 10 })) break;
        }
        string header = Encoding.ASCII.GetString(bytes.ToArray());
        int length = int.Parse(header.Split("\r\n").Single(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)).Split(':')[1]);
        byte[] body = new byte[length];
        await client.GetStream().ReadExactlyAsync(body, WpfTestSession.Cancellation);
        return header + Encoding.UTF8.GetString(body);
    }

    private static async Task RespondAsync(TcpClient client, string status, string contentType, string body)
    {
        byte[] payload = Encoding.UTF8.GetBytes(body);
        byte[] header = Encoding.ASCII.GetBytes($"HTTP/1.1 {status}\r\nContent-Type: {contentType}\r\nContent-Length: {payload.Length}\r\nConnection: close\r\n\r\n");
        await client.GetStream().WriteAsync(header, WpfTestSession.Cancellation);
        await client.GetStream().WriteAsync(payload, WpfTestSession.Cancellation);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
