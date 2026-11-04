using AgentLib.Logging.Sessions;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using PrismCodingAgent.CodingShell;
using PrismCodingAgent.CodingShell.Infrastructure;
using PrismCodingAgent.CodingShell.Localizations;
using PrismCodingAgent.CodingShell.ViewModels;

namespace PrismCodingAgent.WpfShell.Tests;

[WpfTestClass]
public sealed class WpfStartupTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task DispatcherAwaitsCompleteAsyncBody() => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        var dispatcher = new WpfMainThreadDispatcher();
        int result = await Task.Run(() => dispatcher.InvokeAsync(async () =>
        {
            Assert.IsTrue(dispatcher.CheckAccess());
            await Task.Delay(20);
            Assert.IsTrue(dispatcher.CheckAccess());
            return 42;
        }));
        Assert.AreEqual(42, result);
    });

    [TestMethod]
    public async Task RealStartupRestoresTasksAndExitRequiresExplicitConfirmation() => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        if (!WpfTestSession.SupportsReferenceWindow()) return;
        string language = Lang.Current.IetfLanguageTag;
        string root = Path.Combine(AppContext.BaseDirectory, "StartupData", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "ShellSettings.json"), JsonSerializer.Serialize(new { LanguageTag = language }));
        var app = (App)Application.Current;
        var window = (MainWindow)await app.OpenAsync(["--data-directory", root]);
        var model = (MainViewModel)window.DataContext;
        using var screenshots = new WpfScreenshotSession(window, TestContext);
        try
        {
            window.UpdateLayout();
            var content = (FrameworkElement)window.Content;
            window.Width += 1440 - content.ActualWidth;
            window.Height += 900 - content.ActualHeight;
            window.UpdateLayout();
            Assert.AreEqual(1440d, content.ActualWidth, 1d);
            Assert.AreEqual(900d, content.ActualHeight, 1d);
            Assert.AreEqual(96d, System.Windows.Media.VisualTreeHelper.GetDpi(content).PixelsPerInchX);
            Assert.AreEqual(96d, System.Windows.Media.VisualTreeHelper.GetDpi(content).PixelsPerInchY);
            Assert.AreEqual(language, Lang.Current.IetfLanguageTag);
            Assert.AreEqual(1, model.WorkTasks.Count);
            Assert.IsTrue(File.Exists(Path.Combine(root, "WorkTasks.json")));
            Assert.IsTrue(Directory.EnumerateFileSystemEntries(Path.Combine(root, "Abilities")).Any());
            Guid id = model.ActiveWorkTask!.Id;
            await screenshots.CaptureAsync("real-startup");
            window.Close();
            await screenshots.CaptureAsync("exit-confirmation");
            Assert.IsTrue(window.IsVisible);
            Assert.IsFalse(((FrameworkElement)window.FindName("MainContent")).IsEnabled);
            await WpfInput.ClickAsync((Button)window.FindName("CancelExitButton"));
            await WpfInput.WaitAsync(() => !((Border)window.FindName("ExitOverlay")).IsVisible, "Cancel did not dismiss exit.");
            Assert.IsTrue(((FrameworkElement)window.FindName("MainContent")).IsEnabled);
            await screenshots.CaptureAsync("main-after-mouse-cancel");
            window.Close();
            WpfInput.Key(0x0D);
            await Task.Delay(50);
            Assert.IsTrue(window.IsVisible);
            WpfInput.Key(0x1B);
            await WpfInput.WaitAsync(() => !((Border)window.FindName("ExitOverlay")).IsVisible, "Escape did not cancel exit.");
            Assert.IsTrue(((FrameworkElement)window.FindName("MainContent")).IsEnabled);
            await screenshots.CaptureAsync("main-after-escape-cancel");
            window.Close();
            await WpfInput.ClickAsync((Button)window.FindName("ConfirmExitButton"));
            await WpfInput.WaitAsync(() => !window.IsVisible, "Confirmed exit did not finish.");
            await model.DisposeAsync();
            var restored = (MainWindow)await app.OpenAsync(["--data-directory", root]);
            var restoredModel = (MainViewModel)restored.DataContext;
            try
            {
                Assert.AreEqual(id, restoredModel.ActiveWorkTask!.Id);
                restored.Close();
                await WpfInput.ClickAsync((Button)restored.FindName("ConfirmExitButton"));
                await WpfInput.WaitAsync(() => !restored.IsVisible, "Restored window did not close.");
            }
            finally { await restoredModel.DisposeAsync(); }
            File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "phase3-evidence.txt"),
                $"language={language}; root={root}; task={id}; restored=True; confirmedClosed={!window.IsVisible}; repeatDisposeCompleted=True{Environment.NewLine}");
        }
        finally
        {
            await model.DisposeAsync();
        }
    });

    [TestMethod]
    public async Task RealStartupRestoresHistoryMessageContents() => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        string root = Path.Combine(AppContext.BaseDirectory, "StartupData", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "ShellSettings.json"), JsonSerializer.Serialize(new { LanguageTag = Lang.Current.IetfLanguageTag }));
        Guid taskId = Guid.NewGuid();
        await File.WriteAllTextAsync(Path.Combine(root, "WorkTasks.json"), JsonSerializer.Serialize(new[]
        {
            new { Id = taskId, DisplayName = "History task", WorkspacePath = (string?)null, ModelReference = (string?)null, ReasoningEffort = (string?)null, IsArchived = false },
        }));
        var session = new AgentLib.Model.CopilotChatSession { WorkTaskId = taskId, WorkTaskName = "History task" };
        session.SetTitle("Persisted conversation");
        await session.AddEntryAsync(new AgentLib.Model.CopilotChatMessage(Microsoft.Extensions.AI.ChatRole.User, "Persisted question"));
        await session.AddEntryAsync(new AgentLib.Model.CopilotChatMessage(Microsoft.Extensions.AI.ChatRole.Assistant, "Persisted answer"));
        var store = new AgentLib.Logging.Sessions.FileCopilotChatSessionStore(Path.Combine(root, "Sessions"));
        await store.SaveSessionAsync(session, null);
        var window = (MainWindow)await ((App)Application.Current).OpenAsync(["--data-directory", root]);
        var model = (MainViewModel)window.DataContext;
        try
        {
            Assert.AreEqual("History task", model.ActiveWorkTask!.DisplayName);
            await model.ActiveWorkTask.Sessions.LoadAsync();
            var item = model.ActiveWorkTask.Sessions.Sessions.Single(value => value.SessionId == session.SessionId);
            model.OpenSessionCommand.Execute(item);
            await WpfInput.WaitAsync(() => model.ActiveWorkTask.Chat.Messages.Count == 2, "History messages did not restore.");
            CollectionAssert.AreEqual(new[] { "Persisted question", "Persisted answer" },
                model.ActiveWorkTask.Chat.Messages.Select(message => message.Content).ToArray());
            using var screenshots = new WpfScreenshotSession(window, TestContext);
            await screenshots.CaptureAsync("history-restored");
            File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "phase3-history-evidence.txt"),
                $"language={Lang.Current.IetfLanguageTag}; root={root}; task={taskId}; session={session.SessionId}; messages=2; contentsVerified=True{Environment.NewLine}");
        }
        finally
        {
            window.Close();
            await WpfInput.ClickAsync((Button)window.FindName("ConfirmExitButton"));
            await WpfInput.WaitAsync(() => !window.IsVisible, "History window did not close.");
            await model.DisposeAsync();
        }
    });

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RunningProductionTaskStopsAndDisposes(bool confirmWhileRunning) => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var requested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task ServeAsync()
        {
            using var client = await listener.AcceptTcpClientAsync(timeout.Token);
            await using var stream = client.GetStream();
            byte[] buffer = new byte[8192];
            int count = await stream.ReadAsync(buffer, timeout.Token);
            if (count == 0) throw new IOException("Model request was empty.");
            requested.SetResult();
            try { await Task.Delay(Timeout.Infinite, timeout.Token); }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested) { }
        }
        Task server = ServeAsync();
        string root = Path.Combine(AppContext.BaseDirectory, "StartupData", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "ShellSettings.json"), JsonSerializer.Serialize(new { LanguageTag = Lang.Current.IetfLanguageTag }));
        await File.WriteAllTextAsync(Path.Combine(root, "AgentConfiguration.json"),
            $$"""{"PrimaryModel":"demo","OpenAIConfigurationList":[{"EndPoint":"http://127.0.0.1:{{port}}/v1","Key":"test-key","ModelDefinitions":[{"ModelName":"demo","ModelId":"model-id","Provider":"test"}]}]}""");
        var window = (MainWindow)await ((App)Application.Current).OpenAsync(["--data-directory", root]);
        var model = (MainViewModel)window.DataContext;
        try
        {
            var chat = model.ActiveWorkTask!.Chat;
            chat.InputText = "Cancellation acceptance";
            chat.SendCommand.Execute(null);
            await requested.Task.WaitAsync(timeout.Token);
            Assert.IsTrue(chat.IsRunning);
            window.Close();
            await WpfInput.ClickAsync((Button)window.FindName("CancelExitButton"));
            Assert.IsTrue(chat.IsRunning);
            Assert.IsTrue(window.IsVisible);
            using var screenshots = new WpfScreenshotSession(window, TestContext);
            await screenshots.CaptureAsync(confirmWhileRunning ? "running-before-confirm" : "running-before-stop");
            if (!confirmWhileRunning)
            {
                chat.StopCommand.Execute(null);
                await WpfInput.WaitAsync(() => !model.ActiveWorkTask.IsWorking, "Stop did not finish production work.");
            }
            window.Close();
            await WpfInput.ClickAsync((Button)window.FindName("ConfirmExitButton"));
            await WpfInput.WaitAsync(() => !window.IsVisible, "Running shutdown did not finish.");
            await model.DisposeAsync();
            await model.DisposeAsync();
            Assert.IsFalse(model.ActiveWorkTask?.IsWorking == true);
            File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "phase3-running-evidence.txt"),
                $"language={Lang.Current.IetfLanguageTag}; confirmWhileRunning={confirmWhileRunning}; requestReceived=True; cancelExitKeptRunning=True; confirmedClosed=True; repeatedDispose=True; root={root}{Environment.NewLine}");
        }
        finally
        {
            await timeout.CancelAsync();
            listener.Stop();
            await server;
            await model.DisposeAsync();
        }
    });

    [TestMethod]
    public async Task MalformedConfigurationShowsRealFailureWindow() => await WpfTestClassAttribute.RunUiAsync(async () =>
    {
        string root = Path.Combine(AppContext.BaseDirectory, "StartupData", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "ShellSettings.json"), JsonSerializer.Serialize(new { LanguageTag = Lang.Current.IetfLanguageTag }));
        await File.WriteAllTextAsync(Path.Combine(root, "AgentConfiguration.json"), "{");
        Window window = await ((App)Application.Current).OpenAsync(["--data-directory", root]);
        using var screenshots = new WpfScreenshotSession(window, TestContext);
        Assert.IsInstanceOfType<StartupFailureWindow>(window);
        var model = (StartupFailureViewModel)window.DataContext;
        Assert.AreEqual(Path.Combine(root, "AgentConfiguration.json"), model.ConfigurationFilePath);
        Assert.IsFalse(string.IsNullOrWhiteSpace(model.ErrorDetails));
        await screenshots.CaptureAsync("startup-failure");
    });
}
