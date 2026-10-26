using AgentLib;
using AgentLib.Coding;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Microsoft.Extensions.AI;
using PrismCodingAgent.AvaloniaShell.Localizations;
using PrismCodingAgent.AvaloniaShell.Services.Chat;
using PrismCodingAgent.AvaloniaShell.Services.Sessions;
using PrismCodingAgent.AvaloniaShell.Services.WorkTasks;
using PrismCodingAgent.AvaloniaShell.ViewModels;

namespace PrismCodingAgent.AvaloniaShell.Tests;

[AvaloniaTestClass]
public sealed class MainWindowClosingTests
{
    [TestMethod]
    public async Task WhenIdleWindowClosesThenConfirmationIsShown()
    {
        await using var model = new MainViewModel();
        var window = new MainWindow { DataContext = model };
        window.Show();
        window.Close();
        Assert.IsTrue(window.FindControl<Border>("ExitOverlay")!.IsVisible);
        Confirm(window);
    }

    [TestMethod]
    public async Task WhenCloseRepeatsThenSameOverlayRemainsAndWindowStaysVisible()
    {
        await using var model = new MainViewModel();
        var window = new MainWindow { DataContext = model };
        window.Show();
        window.Close();
        window.Close();
        Assert.IsTrue(window.IsVisible);
        Confirm(window);
    }

    [TestMethod]
    public async Task WhenCancelledThenBackgroundIsEnabledAndDraftIsPreserved()
    {
        await using var model = new MainViewModel();
        model.ChatViewModel.InputText = "draft";
        var window = new MainWindow { DataContext = model };
        window.Show();
        window.Close();
        window.FindControl<Button>("CancelExitButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.IsTrue(window.FindControl<Control>("MainContent")!.IsEnabled && model.ChatViewModel.InputText == "draft");
        window.Close();
        Confirm(window);
    }

    [TestMethod]
    [DataRow(Key.Escape, false)]
    [DataRow(Key.Enter, true)]
    public async Task WhenConfirmationReceivesKeyboardThenItDoesNotExit(Key key, bool overlayVisible)
    {
        await using var model = new MainViewModel();
        var window = new MainWindow { DataContext = model };
        window.Show();
        window.Close();
        window.FindControl<Button>("CancelExitButton")!.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = key,
        });
        Assert.AreEqual(overlayVisible, window.FindControl<Border>("ExitOverlay")!.IsVisible);
        window.Close();
        Confirm(window);
    }

    [TestMethod]
    public async Task WhenConfirmationOpensThenCancelHasFocus()
    {
        await using var model = new MainViewModel();
        var window = new MainWindow { DataContext = model };
        window.Show();
        window.Close();
        Assert.IsTrue(window.FindControl<Button>("CancelExitButton")!.IsFocused);
        Confirm(window);
    }

    [TestMethod]
    public async Task WhenConfirmedThenWindowClosesAfterCleanup()
    {
        await using var model = new MainViewModel();
        var window = new MainWindow { DataContext = model };
        window.Show();
        window.Close();
        Confirm(window);
        Assert.IsFalse(window.IsVisible);
    }

    [TestMethod]
    public async Task WhenConfirmationIsShownThenCurrentLanguageIsUsed()
    {
        await using var model = new MainViewModel();
        var window = new MainWindow { DataContext = model };
        window.Show();
        window.Close();
        Assert.AreEqual(Lang.Current.ExitConfirmText, window.FindControl<Button>("ConfirmExitButton")!.Content);
        Confirm(window);
    }

    [TestMethod]
    [Timeout(10000)]
    public async Task WhenStoppedThenCleanupWaitsForRunnerToFinish()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runner = new WaitingRunner(entered, cancelled, release);
        var controller = CodingWorkTaskControllerTestFactory.CreateApplication(new CopilotChatManager(),
            new EmptySessionStore(), runner);
        Task run = controller.SendMessageAsync("offline");
        await entered.Task;
        Task cleanup = controller.StopAndWaitAsync();
        await cancelled.Task;
        bool completedBeforeRelease = cleanup.IsCompleted;
        release.TrySetResult();
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => run);
        await cleanup;
        Assert.IsFalse(completedBeforeRelease);
    }

    [TestMethod]
    public async Task WhenConfirmedDuringRunThenRepeatedCloseWaitsForRuntimeCleanup()
    {
        string root = Path.Join(Path.GetTempPath(), "PrismExitTests", Guid.NewGuid().ToString("N"));
        var runtime = await CodingWorkTaskRuntimeFactory.InitializeAsync(
            PrismCodingAgent.AvaloniaShell.Infrastructure.CodingChatRoomPaths.Create(root), new ImmediateDispatcher());
        await using var model = new MainViewModel();
        model.WorkTasks.Add(MainViewModel.CreateRuntimeTask(runtime,
            new WorkTaskRecord(Guid.NewGuid(), "offline", null, null, null), null));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task run = runtime.Controller.SendMessageAsync([new TextContent("offline")],
            runner: new WaitingRunner(entered, cancelled, release));
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var window = new MainWindow { DataContext = model };
            window.Show();
            window.Close();
            bool cancelledBeforeConfirmation = cancelled.Task.IsCompleted;
            Confirm(window);
            await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(3));
            window.Close();
            Confirm(window);
            bool visibleDuringCleanup = window.IsVisible;
            Task cleanup = model.DisposeAsync().AsTask();
            release.TrySetResult();
            await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => run);
            await cleanup.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.IsTrue(!cancelledBeforeConfirmation && visibleDuringCleanup);
        }
        finally
        {
            release.TrySetResult();
        }
    }

    [TestMethod]
    public async Task WhenConfirmationIsRenderedThenShellThemeAndCancelFocusArePreserved()
    {
        await using var model = new MainViewModel();
        var window = new MainWindow { DataContext = model };
        window.Show();
        window.Close();
        try
        {
            var cancel = window.FindControl<Button>("CancelExitButton")!;
            window.UpdateLayout();
            using var bitmap = Avalonia.Headless.HeadlessWindowExtensions.CaptureRenderedFrame(window)
                ?? throw new InvalidOperationException("Exit confirmation frame unavailable.");
            string directory = Path.Join(AppContext.BaseDirectory, "Screenshots");
            Directory.CreateDirectory(directory);
            bitmap.Save(Path.Join(directory, "exit-confirmation.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
            Assert.IsTrue(cancel.IsFocused, "退出确认应把键盘焦点放在取消按钮上。");
        }
        finally
        {
            Confirm(window);
        }
    }

    [TestMethod]
    public async Task WhenCleanupFailsThenRetrySkipsPreviouslyDisposedTask()
    {
        await using var model = new MainViewModel();
        WorkTaskItemViewModel first = model.ActiveWorkTask;
        var second = new WorkTaskItemViewModel("second", new ChatViewModel(), new SessionListViewModel());
        model.WorkTasks.Add(second);
        System.Collections.Specialized.NotifyCollectionChangedEventHandler fail = (_, _) =>
            throw new InvalidOperationException("Offline cleanup failure.");
        second.Chat.Messages.CollectionChanged += fail;
        try
        {
            await Assert.ThrowsExactlyAsync<AggregateException>(() => model.DisposeAsync().AsTask());
        }
        finally
        {
            second.Chat.Messages.CollectionChanged -= fail;
        }
        int repeatedDisposals = 0;
        first.Chat.Messages.CollectionChanged += (_, _) => repeatedDisposals++;
        await model.DisposeAsync();
        Assert.AreEqual(0, repeatedDisposals);
    }

    private sealed class ImmediateDispatcher : IMainThreadDispatcher
    {
        public bool CheckAccess() => true;
        public Task InvokeAsync(Func<Task> action) => action();
        public Task<T> InvokeAsync<T>(Func<Task<T>> action) => action();
    }

    private sealed class WaitingRunner(TaskCompletionSource entered, TaskCompletionSource cancelled,
        TaskCompletionSource release) : ICodingChatRunner
    {
        public async Task<ICodingAgentRunResult> RunAsync(IReadOnlyList<AIContent> contents, string? workspacePath,
            CodingChatRunOptions options, CancellationToken cancellationToken)
        {
            using var registration = cancellationToken.Register(() => cancelled.TrySetResult());
            entered.TrySetResult();
            await release.Task;
            throw new OperationCanceledException(cancellationToken);
        }
    }

    private sealed class EmptySessionStore : ICodingChatSessionStore
    {
        public Task<IReadOnlyList<AgentLib.Logging.CopilotChatSessionSummary>> ListSessionsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AgentLib.Logging.CopilotChatSessionSummary>>([]);
        public Task<AgentLib.Model.CopilotChatSession> LoadSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<bool> DeleteSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
        public Task SaveSessionAsync(AgentLib.Model.CopilotChatSession session, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private static void Confirm(MainWindow window)
        => window.FindControl<Button>("ConfirmExitButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
}
